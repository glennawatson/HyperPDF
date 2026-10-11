// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using PdfViewerLite.Core.Documents;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Advanced;
using ReactiveUI.Primitives.Signals;

namespace PdfViewerLite.Core.Rendering;

/// <summary>
/// Runs every page rasterisation on a single dedicated thread, highest priority first. Requests that became stale
/// (their client advanced its generation without re-requesting them) are dropped before any work is done. Results are
/// handed back through <see cref="TryTakeCompleted"/>, and <see cref="Completed"/> emits once per batch so the UI can
/// drain them on its own thread.
/// </summary>
[DebuggerDisplay("RenderScheduler: Queue {QueueLength}")]
public sealed class RenderScheduler : IDisposable
{
    /// <summary>The bit position of the priority within the queue ordering key.</summary>
    private const int PriorityShift = 48;

    /// <summary>Guards the queue and pending map.</summary>
    private readonly Lock _gate = new();

    /// <summary>The work queue, ordered by priority then arrival.</summary>
    private readonly PriorityQueue<RenderRequest, long> _queue = new();

    /// <summary>Keys that are queued or rendered but not yet taken, mapped to the latest request for them.</summary>
    private readonly Dictionary<TileKey, PendingTile> _pending = [];

    /// <summary>The epoch at which each document's content last changed; tiles rendered before it are discarded.</summary>
    private readonly Dictionary<int, long> _invalidated = [];

    /// <summary>Completed renders waiting for the UI.</summary>
    private readonly ConcurrentQueue<FinishedTile> _finished = new();

    /// <summary>Emits on the render thread when finished tiles are waiting.</summary>
    private readonly Signal<RxVoid> _completed = new();

    /// <summary>Wakes the render thread.</summary>
    private readonly SemaphoreSlim _signal = new(0);

    /// <summary>Creates surfaces.</summary>
    private readonly IRenderSurfaceFactory _surfaceFactory;

    /// <summary>The render thread.</summary>
    private readonly Thread _thread;

    /// <summary>Cancels the render thread.</summary>
    private readonly CancellationTokenSource _shutdown = new();

    /// <summary>Monotonic counter preserving FIFO order within a priority.</summary>
    private long _sequence;

    /// <summary>Counts content invalidations; each render records the value it started under.</summary>
    private long _epoch;

    /// <summary>1 while a completion notification is outstanding.</summary>
    private int _notificationPending;

    /// <summary>1 once disposed.</summary>
    private int _disposed;

    /// <summary>1 once the worker's synchronization resources have been released.</summary>
    private int _resourcesDisposed;

    /// <summary>Initializes a new instance of the <see cref="RenderScheduler"/> class.</summary>
    /// <param name="surfaceFactory">Creates output surfaces on the render thread.</param>
    public RenderScheduler(IRenderSurfaceFactory surfaceFactory)
    {
        ArgumentNullException.ThrowIfNull(surfaceFactory);
        _surfaceFactory = surfaceFactory;
        _thread = new(Run) { IsBackground = true, Name = "PdfViewerLite render" };
        _thread.Start();
        Completed = new(_completed);
    }

    /// <summary>
    /// Gets notifications, raised on the render thread, that finished tiles are waiting to be taken with
    /// <see cref="TryTakeCompleted"/>. Bursts are coalesced: one notification covers every tile finished before the next
    /// drain starts.
    /// </summary>
    public AsObservableSignal<RxVoid> Completed { get; }

    /// <summary>Gets the token that cancels when this scheduler stops.</summary>
    public CancellationToken ShutdownToken => _shutdown.Token;

    /// <summary>Gets the number of queued requests, including stale ones not yet discarded.</summary>
    public int QueueLength
    {
        get
        {
            lock (_gate)
            {
                return _queue.Count;
            }
        }
    }

    /// <summary>
    /// Queues a render. When the tile is already pending, the newer request replaces it, so the render uses the latest
    /// document, generation and tone; a more urgent priority queues it again so it does not wait behind prefetch work.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns><see langword="true"/> when a new job was queued.</returns>
    public bool Request(in RenderRequest request)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return !request.CancellationToken.IsCancellationRequested && EnqueueRequest(request);
    }

    /// <summary>
    /// Marks a document's content as changed: tiles of it that are rendering or waiting to be taken are discarded, and
    /// requests for them are no longer merged with the old work.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    public void Invalidate(int documentId)
    {
        lock (_gate)
        {
            _epoch++;
            _invalidated[documentId] = _epoch;
            foreach (var (key, pending) in _pending)
            {
                // Work that has not started yet will render the new content, so only started work is cut loose.
                if (key.DocumentId == documentId && pending.Started)
                {
                    _ = _pending.Remove(key);
                }
            }
        }
    }

    /// <summary>Determines whether a tile is queued or awaiting collection.</summary>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when pending.</returns>
    public bool IsPending(in TileKey key)
    {
        lock (_gate)
        {
            return _pending.ContainsKey(key);
        }
    }

    /// <summary>Takes one completed tile. Call repeatedly from the UI thread after the completion callback fires.</summary>
    /// <param name="tile">The completed tile; the caller owns its surface.</param>
    /// <returns><see langword="true"/> when a tile was taken.</returns>
    public bool TryTakeCompleted(out RenderedTile tile)
    {
        // Clear the flag first so a render finishing during the drain raises a fresh notification.
        _ = Interlocked.Exchange(ref _notificationPending, 0);
        while (_finished.TryDequeue(out var finished))
        {
            if (TryRelease(finished))
            {
                tile = finished.Tile;
                return true;
            }

            finished.Tile.Surface.Dispose();
        }

        tile = default;
        return false;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _shutdown.Cancel();
        while (_finished.TryDequeue(out var finished))
        {
            finished.Tile.Surface.Dispose();
        }

        if (_thread.IsAlive || Interlocked.Exchange(ref _resourcesDisposed, 1) != 0)
        {
            return;
        }

        _completed.OnCompleted();
        _completed.Dispose();
        _signal.Dispose();
        _shutdown.Dispose();
    }

    /// <summary>Renders a request into a surface.</summary>
    /// <param name="target">The locked surface.</param>
    /// <param name="request">The request.</param>
    /// <returns><see langword="true"/> when rendered.</returns>
    private static bool RenderInto(RenderTarget target, in RenderRequest request)
    {
        request.CancellationToken.ThrowIfCancellationRequested();
        if (!request.Document.Render(request.Info, target))
        {
            return false;
        }

        request.CancellationToken.ThrowIfCancellationRequested();
        request.Tone.Apply(target);
        return true;
    }

    /// <summary>Reports synchronous preparation failures through the returned task.</summary>
    /// <param name="request">The page request.</param>
    /// <param name="cancellationToken">Cancels preparation.</param>
    /// <returns>The preparation task.</returns>
    private static ValueTask Prepare(in RenderRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return request.Document.PreparePageAsync(request.Info.PageIndex, cancellationToken);
        }
        catch (Exception exception) when (exception is OperationCanceledException or HttpRequestException or IOException or InvalidOperationException)
        {
            return ValueTask.FromException(exception);
        }
    }

    /// <summary>Queues an active request, preserving latest-client coalescing.</summary>
    /// <param name="request">The active request.</param>
    /// <returns>Whether a queue entry was added.</returns>
    private bool EnqueueRequest(in RenderRequest request)
    {
        lock (_gate)
        {
            ref var pending = ref CollectionsMarshal.GetValueRefOrAddDefault(_pending, request.Key, out var exists);
            var urgent = exists && !pending.Started && request.Priority < pending.Latest.Priority;
            var superseded = exists && pending.Started
                && (request.Generation != pending.Latest.Generation
                    || !ReferenceEquals(request.Client, pending.Latest.Client)
                    || !ReferenceEquals(request.Document, pending.Latest.Document));
            if (exists && !urgent && !superseded)
            {
                pending = pending with { Latest = request };
                return false;
            }

            pending = new(request, Started: false, Epoch: 0);
            var sequence = _sequence;
            _sequence = sequence + 1;
            _queue.Enqueue(request, ((long)request.Priority << PriorityShift) | sequence);
        }

        _ = _signal.Release();
        return true;
    }

    /// <summary>Uses the tab token when present and the scheduler token otherwise.</summary>
    /// <param name="request">The render request.</param>
    /// <returns>The token that cancels this request.</returns>
    private CancellationToken WorkToken(in RenderRequest request) =>
        request.CancellationToken.CanBeCanceled ? request.CancellationToken : _shutdown.Token;

    /// <summary>Checks whether a request became unusable while its render was running.</summary>
    /// <param name="request">The request being rendered.</param>
    /// <returns>Whether the work must be discarded.</returns>
    private bool IsWorkCancelled(in RenderRequest request) =>
        Volatile.Read(ref _disposed) != 0 || request.CancellationToken.IsCancellationRequested
        || request.Document.IsDisposed || request.Generation != request.Client.Generation;

    /// <summary>Checks whether a finished tile still belongs to the current document and client.</summary>
    /// <param name="finished">The completed tile.</param>
    /// <param name="key">The tile key.</param>
    /// <returns>Whether the result must be discarded.</returns>
    private bool IsFinishedStale(in FinishedTile finished, in TileKey key) =>
        IsWorkCancelled(finished.Request) || IsInvalidated(key, finished.Epoch);

    /// <summary>The render thread loop.</summary>
    private void Run()
    {
        try
        {
            var token = _shutdown.Token;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    _signal.Wait(token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (!TryDequeue(out var request, out var epoch))
                {
                    continue;
                }

                var prepare = Prepare(request, WorkToken(request));
                if (!prepare.IsCompletedSuccessfully)
                {
                    _ = PrepareAndRequeueAsync(prepare, request, epoch);
                    continue;
                }

                if (!Execute(request, epoch))
                {
                    Forget(request.Key, epoch);
                }
            }
        }
        finally
        {
            while (_finished.TryDequeue(out var finished))
            {
                finished.Tile.Surface.Dispose();
            }

            if (Interlocked.Exchange(ref _resourcesDisposed, 1) == 0)
            {
                _completed.OnCompleted();
                _completed.Dispose();
                _signal.Dispose();
                _shutdown.Dispose();
            }
        }
    }

    /// <summary>Checks whether an earlier request was invalidated, with the queue gate held.</summary>
    /// <param name="key">The tile key.</param>
    /// <param name="epoch">The request's epoch.</param>
    /// <returns>Whether the document changed during the request.</returns>
    private bool IsInvalidated(in TileKey key, long epoch) =>
        _invalidated.TryGetValue(key.DocumentId, out var changed) && epoch < changed;

    /// <summary>Waits for external resources without occupying the render thread, then requeues the latest tile.</summary>
    /// <param name="prepare">The resource preparation.</param>
    /// <param name="request">The request waiting for resources.</param>
    /// <param name="epoch">The invalidation epoch the preparation started under.</param>
    /// <returns>A task completing when the request is requeued or dropped.</returns>
    private async Task PrepareAndRequeueAsync(ValueTask prepare, RenderRequest request, long epoch)
    {
        try
        {
            await prepare.ConfigureAwait(false);
            lock (_gate)
            {
                if (Volatile.Read(ref _disposed) != 0 || request.CancellationToken.IsCancellationRequested
                    || !_pending.TryGetValue(request.Key, out var pending) || IsInvalidated(request.Key, epoch))
                {
                    return;
                }

                _pending[request.Key] = pending with { Started = false };
                var sequence = _sequence;
                _sequence = sequence + 1;
                _queue.Enqueue(pending.Latest, ((long)pending.Latest.Priority << PriorityShift) | sequence);
                _ = _signal.Release();
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or HttpRequestException or IOException or InvalidOperationException or ObjectDisposedException)
        {
            Debug.WriteLine($"Page preparation failed for {request.Key}: {exception.Message}");
            Forget(request.Key, epoch);
        }
    }

    /// <summary>Dequeues the latest request for the next live key, discarding stale and superseded entries.</summary>
    /// <param name="request">The request to render.</param>
    /// <param name="epoch">The invalidation epoch the render starts under.</param>
    /// <returns><see langword="true"/> when a request should be rendered.</returns>
    private bool TryDequeue(out RenderRequest request, out long epoch)
    {
        lock (_gate)
        {
            epoch = _epoch;
            while (_queue.TryDequeue(out var queued, out _))
            {
                ref var pending = ref CollectionsMarshal.GetValueRefOrNullRef(_pending, queued.Key);

                // A key queued again at a more urgent priority leaves its first entry behind; skip it once handled.
                if (Unsafe.IsNullRef(ref pending) || pending.Started)
                {
                    continue;
                }

                request = pending.Latest;
                if (request.Generation != request.Client.Generation || request.Document.IsDisposed || request.CancellationToken.IsCancellationRequested)
                {
                    _ = _pending.Remove(queued.Key);
                    continue;
                }

                pending = pending with { Started = true, Epoch = epoch };
                return true;
            }

            request = default;
            return false;
        }
    }

    /// <summary>Clears a taken tile's pending entry, unless its document changed after the render started.</summary>
    /// <param name="finished">The finished render.</param>
    /// <returns><see langword="true"/> when the tile shows current content.</returns>
    private bool TryRelease(in FinishedTile finished)
    {
        var key = finished.Tile.Key;
        lock (_gate)
        {
            if (!_pending.TryGetValue(key, out var pending))
            {
                return false;
            }

            var sameRequest = pending.Started
                && pending.Epoch == finished.Epoch
                && ReferenceEquals(pending.Latest.Client, finished.Request.Client)
                && ReferenceEquals(pending.Latest.Document, finished.Request.Document)
                && pending.Latest.Generation == finished.Request.Generation;
            if (!sameRequest)
            {
                return false;
            }

            if (IsFinishedStale(finished, key))
            {
                _ = _pending.Remove(key);
                return false;
            }

            _ = _pending.Remove(key);
            return true;
        }
    }

    /// <summary>Renders one request and publishes the result.</summary>
    /// <param name="request">The request.</param>
    /// <param name="epoch">The invalidation epoch the render started under.</param>
    /// <returns><see langword="true"/> when a tile was published.</returns>
    private bool Execute(in RenderRequest request, long epoch)
    {
        IRenderSurface? surface = null;
        try
        {
            request.CancellationToken.ThrowIfCancellationRequested();
            surface = _surfaceFactory.Create(request.Width, request.Height);
            var completed = surface is IRenderPreparationSurface preparation
                ? preparation.Prepare(request, WorkToken(request))
                : surface.Write(request, RenderInto);
            if (!completed)
            {
                surface.Dispose();
                return false;
            }
        }
        catch (ObjectDisposedException)
        {
            surface?.Dispose();
            return false;
        }
        catch (OperationCanceledException) when (IsWorkCancelled(request))
        {
            surface?.Dispose();
            return false;
        }
        catch (InvalidOperationException ex)
        {
            Debug.WriteLine($"Render failed for {request.Key}: {ex.Message}");
            surface?.Dispose();
            return false;
        }

        if (IsWorkCancelled(request))
        {
            surface.Dispose();
            return false;
        }

        _finished.Enqueue(new(new(request.Key, surface), epoch, request));
        if (Interlocked.Exchange(ref _notificationPending, 1) == 0)
        {
            _completed.OnNext(RxVoid.Default);
        }

        return true;
    }

    /// <summary>Removes a key from the pending map after a failed or skipped render.</summary>
    /// <param name="key">The key.</param>
    /// <param name="epoch">The epoch the request started under.</param>
    private void Forget(in TileKey key, long epoch)
    {
        lock (_gate)
        {
            // Only the entry this render started is removed; a newer request queued after an invalidation stays.
            if (!IsInvalidated(key, epoch) && _pending.TryGetValue(key, out var pending) && pending.Started && pending.Epoch == epoch)
            {
                _ = _pending.Remove(key);
            }
        }
    }

    /// <summary>The latest request for a pending tile.</summary>
    /// <param name="Latest">The most recent request.</param>
    /// <param name="Started">Whether the render thread has taken it.</param>
    /// <param name="Epoch">The invalidation epoch when the render started.</param>
    private readonly record struct PendingTile(RenderRequest Latest, bool Started, long Epoch);

    /// <summary>A finished render with the invalidation epoch it started under.</summary>
    /// <param name="Tile">The tile.</param>
    /// <param name="Epoch">The epoch.</param>
    /// <param name="Request">The request that produced the surface.</param>
    private readonly record struct FinishedTile(RenderedTile Tile, long Epoch, RenderRequest Request);
}
