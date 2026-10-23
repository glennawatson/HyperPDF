// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdf.Core.Diagnostics;
using HyperPdf.Core.Documents;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Advanced;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Core.Rendering;

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

    /// <summary>The number of pending tiles the map holds before it grows.</summary>
    private const int InitialPendingCapacity = 8;

    /// <summary>Guards the queue and pending map.</summary>
    private readonly Lock _gate = new();

    /// <summary>The work queue, ordered by priority then arrival.</summary>
    private readonly RequestQueue _queue = new();

    /// <summary>Keys that are queued or rendered but not yet taken, mapped to the latest request for them; sized for a typical burst so it makes no table while rendering.</summary>
    private readonly Dictionary<TileKey, PendingTile> _pending = [with(InitialPendingCapacity)];

    /// <summary>The epoch at which each document's content last changed; tiles rendered before it are discarded.</summary>
    private readonly Dictionary<int, long> _invalidated = [];

    /// <summary>Completed renders waiting for the UI.</summary>
    private readonly ConcurrentQueue<FinishedTile> _finished = new();

    /// <summary>Emits on the render thread when finished tiles are waiting.</summary>
    private readonly Signal<RxVoid> _completed = new();

    /// <summary>Emits when something on screen is requested, so background preparation can stand aside.</summary>
    private readonly Signal<RxVoid> _visibleRequested = new();

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

    /// <summary>The number of pending tiles whose latest request is visible work; written under the gate, read without it.</summary>
    private int _visibleCount;

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
        _thread = new(Run) { IsBackground = true, Name = "HyperPDF render" };
        _thread.Start();
        Completed = new(_completed);
        VisibleWorkRequested = new(_visibleRequested);
    }

    /// <summary>
    /// Gets notifications, raised on the render thread, that finished tiles are waiting to be taken with
    /// <see cref="TryTakeCompleted"/>. Bursts are coalesced: one notification covers every tile finished before the next
    /// drain starts.
    /// </summary>
    public AsObservableSignal<RxVoid> Completed { get; }

    /// <summary>
    /// Gets notifications, raised on the requesting thread, each time a visible tile or visible preview is requested, even when
    /// it merges with work already queued. Background preparation cancels what it is doing when one arrives.
    /// </summary>
    public AsObservableSignal<RxVoid> VisibleWorkRequested { get; }

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
    /// Gets whether a request for something on screen, a visible tile or a visible preview, is queued or rendering. Background
    /// preparation uses it to stand aside.
    /// </summary>
    public bool HasVisibleWork => Volatile.Read(ref _visibleCount) > 0;

    /// <summary>Gets whether no requests are queued or currently rendering.</summary>
    public bool IsIdle
    {
        get
        {
            lock (_gate)
            {
                return _queue.Count == 0 && _pending.Count == 0;
            }
        }
    }

    /// <summary>
    /// Queues a render. When the tile is already pending, the newer request replaces it, so the render uses the latest
    /// document, generation and tone; a changed priority queues it again so its rank follows the latest request.
    /// A render already running for the same client and document carries on and is delivered to the newer generation.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns><see langword="true"/> when a new job was queued.</returns>
    public bool Request(in RenderRequest request)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (request.CancellationToken.IsCancellationRequested)
        {
            return false;
        }

        if (request.Priority <= RenderPriority.Visible)
        {
            SignalVisibleWork();
        }

        return EnqueueRequest(request);
    }

    /// <summary>
    /// Queues the renders of one frame under a single lock, with the same merging as <see cref="Request"/>.
    /// Visible work is announced once for the whole batch rather than once per request.
    /// </summary>
    /// <param name="requests">The requests.</param>
    /// <returns>The number of new jobs queued.</returns>
    public int RequestBatch(ReadOnlySpan<RenderRequest> requests)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var visible = false;
        var queued = 0;
        lock (_gate)
        {
            foreach (ref readonly var request in requests)
            {
                if (request.CancellationToken.IsCancellationRequested)
                {
                    continue;
                }

                visible |= request.Priority <= RenderPriority.Visible;
                if (TryEnqueueLocked(request))
                {
                    queued++;
                }
            }
        }

        if (visible)
        {
            SignalVisibleWork();
        }

        return queued > 0 && WakeRenderThread(queued) ? queued : 0;
    }

    /// <summary>
    /// Marks a document's content as changed: tiles of it that are rendering or waiting to be taken are discarded, and
    /// requests for them are no longer merged with the old work.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Invalidate(int documentId) => Invalidate(documentId, documentId);

    /// <summary>Marks two documents' content as changed in one pass; see <see cref="Invalidate(int)"/>.</summary>
    /// <param name="firstDocumentId">The first document identifier.</param>
    /// <param name="secondDocumentId">The second document identifier.</param>
    public void Invalidate(int firstDocumentId, int secondDocumentId)
    {
        lock (_gate)
        {
            _epoch++;
            _invalidated[firstDocumentId] = _epoch;
            _invalidated[secondDocumentId] = _epoch;
            foreach (var (key, pending) in _pending)
            {
                // Work that has not started yet will render the new content, so only started work is cut loose.
                if ((key.DocumentId == firstDocumentId || key.DocumentId == secondDocumentId) && pending.Started)
                {
                    RemovePending(key);
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
        _visibleRequested.OnCompleted();
        _visibleRequested.Dispose();
        _signal.Dispose();
        _shutdown.Dispose();
    }

    /// <summary>Renders a request into a surface.</summary>
    /// <param name="target">The locked surface.</param>
    /// <param name="job">The request and the scheduler that tells whether its result is still wanted.</param>
    /// <returns><see langword="true"/> when rendered.</returns>
    private static bool RenderInto(RenderTarget target, in RenderJob job)
    {
        var request = job.Request;
        request.CancellationToken.ThrowIfCancellationRequested();
        if (!request.Document.Render(request.Info, target))
        {
            return false;
        }

        request.CancellationToken.ThrowIfCancellationRequested();

        // A tile the client no longer wants is dropped when it is taken, so toning its pixels would be wasted work.
        if (!job.Scheduler.IsResultUnwanted(request.Key))
        {
            request.Tone.Apply(target);
        }

        return true;
    }

    /// <summary>Checks whether a request asks for exactly what the pending tile already holds.</summary>
    /// <param name="latest">The pending tile's latest request.</param>
    /// <param name="request">The new request.</param>
    /// <returns>Whether storing the new request would change nothing.</returns>
    private static bool IsUnchanged(in RenderRequest latest, in RenderRequest request) =>
        latest.Generation == request.Generation
        && latest.Priority == request.Priority
        && latest.CancellationToken == request.CancellationToken
        && ReferenceEquals(latest.Client, request.Client)
        && ReferenceEquals(latest.Document, request.Document)
        && ReferenceEquals(latest.Tone, request.Tone)
        && latest.Width == request.Width
        && latest.Height == request.Height
        && latest.Info == request.Info;

    /// <summary>Checks whether a request for a pending tile needs a new queue entry rather than a merge into the pending one.</summary>
    /// <param name="pending">The pending tile.</param>
    /// <param name="request">The new request.</param>
    /// <returns>Whether a new entry is needed.</returns>
    private static bool NeedsNewEntry(in PendingTile pending, in RenderRequest request)
    {
        // A queued tile asked for again at a different priority gets a fresh queue entry, so its rank follows the
        // latest request: a promotion does not wait behind prefetch work, and a demotion does not keep its old rank.
        // For a started tile, the key fixes the page, scale, rotation, tone and tile, so a running render for the same
        // client and document already makes the pixels a newer generation wants. Restarting it would discard every
        // tile that takes longer than a frame while the view keeps scrolling.
        return pending.Started
            ? !ReferenceEquals(request.Client, pending.Latest.Client) || !ReferenceEquals(request.Document, pending.Latest.Document)
            : request.Priority != pending.Latest.Priority;
    }

    /// <summary>Determines whether an exception from rendering one tile may be dropped with that tile.</summary>
    /// <param name="exception">The exception.</param>
    /// <returns><see langword="false"/> for exceptions that mean the process is failing.</returns>
    private static bool IsRecoverable(Exception exception) =>
        exception is not (OutOfMemoryException or AccessViolationException);

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
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return ValueTask.FromException(exception);
        }
    }

    /// <summary>Applies the change of a pending tile's latest priority to the count of visible tiles; call with the gate held.</summary>
    /// <param name="before">The priority before the change, or <see langword="null"/> when the tile was not pending.</param>
    /// <param name="after">The priority after the change, or <see langword="null"/> when the tile is no longer pending.</param>
    private void CountVisible(RenderPriority? before, RenderPriority? after)
    {
        var delta = (after <= RenderPriority.Visible ? 1 : 0) - (before <= RenderPriority.Visible ? 1 : 0);
        if (delta != 0)
        {
            Volatile.Write(ref _visibleCount, _visibleCount + delta);
        }
    }

    /// <summary>Removes a key from the pending map, keeping the visible count in step; call with the gate held.</summary>
    /// <param name="key">The key.</param>
    private void RemovePending(in TileKey key)
    {
        if (_pending.Remove(key, out var removed))
        {
            CountVisible(removed.Latest.Priority, null);
        }
    }

    /// <summary>Tells background preparation that something on screen needs drawing.</summary>
    private void SignalVisibleWork()
    {
        try
        {
            _visibleRequested.OnNext(RxVoid.Default);
        }
        catch (ObjectDisposedException)
        {
            // The dispose came between the caller's check and the signal; the request is refused like any other after the dispose.
            ObjectDisposedException.ThrowIf(true, this);
        }
    }

    /// <summary>Checks whether the render of a key would be discarded when it finishes, so its pixels need no finishing work.</summary>
    /// <param name="key">The tile key.</param>
    /// <returns>Whether the latest request for the key is cancelled or from an older generation, or the key is no longer pending.</returns>
    private bool IsResultUnwanted(in TileKey key)
    {
        lock (_gate)
        {
            if (!_pending.TryGetValue(key, out var pending))
            {
                return true;
            }

            var latest = pending.Latest;
            return IsWorkCancelled(latest) || latest.Generation != latest.Client.Generation;
        }
    }

    /// <summary>Queues an active request, preserving latest-client coalescing.</summary>
    /// <param name="request">The active request.</param>
    /// <returns>Whether a queue entry was added.</returns>
    private bool EnqueueRequest(in RenderRequest request)
    {
        bool queued;
        lock (_gate)
        {
            queued = TryEnqueueLocked(request);
        }

        return queued && WakeRenderThread(1);
    }

    /// <summary>Merges a request into the pending map or queues a new entry for it; call with the gate held.</summary>
    /// <param name="request">The active request.</param>
    /// <returns>Whether a queue entry was added.</returns>
    private bool TryEnqueueLocked(in RenderRequest request)
    {
        ref var pending = ref CollectionsMarshal.GetValueRefOrAddDefault(_pending, request.Key, out var exists);
        if (!exists)
        {
            QueueNewEntry(ref pending, request, null);
            return true;
        }

        if (IsUnchanged(pending.Latest, request))
        {
            // The common repaint asks again for what is already pending; there is nothing to record.
            return false;
        }

        var oldPriority = pending.Latest.Priority;
        if (NeedsNewEntry(pending, request))
        {
            QueueNewEntry(ref pending, request, oldPriority);
            return true;
        }

        CountVisible(oldPriority, request.Priority);
        pending = pending with { Latest = request };
        return false;
    }

    /// <summary>Replaces a key's pending tile with a not yet started one and queues an entry carrying its stamp; call with the gate held.</summary>
    /// <param name="pending">The pending slot of the key.</param>
    /// <param name="request">The request.</param>
    /// <param name="oldPriority">The priority of the tile being replaced, or <see langword="null"/> when there was none.</param>
    private void QueueNewEntry(ref PendingTile pending, in RenderRequest request, RenderPriority? oldPriority)
    {
        var sequence = _sequence;
        _sequence = sequence + 1;
        var stamp = ((long)request.Priority << PriorityShift) | sequence;
        CountVisible(oldPriority, request.Priority);
        pending = new(request, Started: false, Epoch: 0, Stamp: stamp);
        _queue.Enqueue(new(request.Key, stamp));
    }

    /// <summary>
    /// Wakes the render thread for queued requests. The thread releases the semaphore when it stops, so a request that
    /// raced with <see cref="Dispose"/> finds it gone and is dropped, as a request after the dispose would be.
    /// </summary>
    /// <param name="count">The number of queue entries added.</param>
    /// <returns><see langword="true"/> when the render thread was woken.</returns>
    private bool WakeRenderThread(int count)
    {
        try
        {
            _ = _signal.Release(count);
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    /// <summary>Uses the tab token when present and the scheduler token otherwise.</summary>
    /// <param name="request">The render request.</param>
    /// <returns>The token that cancels this request.</returns>
    private CancellationToken WorkToken(in RenderRequest request) =>
        request.CancellationToken.CanBeCanceled ? request.CancellationToken : _shutdown.Token;

    /// <summary>
    /// Checks whether a request became unusable while its render was running. A newer generation is checked when the
    /// tile is taken, because the client may request the running tile again in the meantime.
    /// </summary>
    /// <param name="request">The request being rendered.</param>
    /// <returns>Whether the work must be discarded.</returns>
    private bool IsWorkCancelled(in RenderRequest request) =>
        Volatile.Read(ref _disposed) != 0 || request.CancellationToken.IsCancellationRequested || request.Document.IsDisposed;

    /// <summary>Checks whether a finished tile is no longer wanted by its client or shows content changed since.</summary>
    /// <param name="latest">The latest request for the tile.</param>
    /// <param name="key">The tile key.</param>
    /// <param name="epoch">The epoch the render started under.</param>
    /// <returns>Whether the result must be discarded.</returns>
    private bool IsFinishedStale(in RenderRequest latest, in TileKey key, long epoch) =>
        IsWorkCancelled(latest) || latest.Generation != latest.Client.Generation || IsInvalidated(key, epoch);

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
                _visibleRequested.OnCompleted();
                _visibleRequested.Dispose();
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

                var sequence = _sequence;
                _sequence = sequence + 1;
                var stamp = ((long)pending.Latest.Priority << PriorityShift) | sequence;
                _pending[request.Key] = pending with { Started = false, Stamp = stamp };
                _queue.Enqueue(new(request.Key, stamp));
                _ = _signal.Release();
            }
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            AppLog.Warning(exception, $"Page preparation failed for {request.Key}.");
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
            while (_queue.TryDequeue(out var entry))
            {
                ref var pending = ref CollectionsMarshal.GetValueRefOrNullRef(_pending, entry.Key);

                // A key queued again at another priority leaves its earlier entry behind; only the entry holding the
                // pending tile's current stamp is live.
                if (Unsafe.IsNullRef(ref pending) || pending.Started || pending.Stamp != entry.Stamp)
                {
                    continue;
                }

                request = pending.Latest;
                if (request.Generation != request.Client.Generation || request.Document.IsDisposed || request.CancellationToken.IsCancellationRequested)
                {
                    RemovePending(entry.Key);
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

            // Generations may differ: a client that asked for the tile again while it rendered still wants it.
            var sameRender = pending.Started
                && pending.Epoch == finished.Epoch
                && ReferenceEquals(pending.Latest.Client, finished.Request.Client)
                && ReferenceEquals(pending.Latest.Document, finished.Request.Document);
            if (!sameRender)
            {
                return false;
            }

            RemovePending(key);
            return !IsFinishedStale(pending.Latest, key, finished.Epoch);
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
                : surface.Write(new RenderJob(request, this), RenderInto);
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
        catch (Exception ex) when (IsRecoverable(ex))
        {
            // A damaged file or a failed read must lose its tile, not the render thread and the process.
            AppLog.Warning(ex, $"Render failed for {request.Key}.");
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
                RemovePending(key);
            }
        }
    }

    /// <summary>The latest request for a pending tile.</summary>
    /// <param name="Latest">The most recent request.</param>
    /// <param name="Started">Whether the render thread has taken it.</param>
    /// <param name="Epoch">The invalidation epoch when the render started.</param>
    /// <param name="Stamp">The queue stamp of the live entry for this tile; entries with another stamp are stale.</param>
    private readonly record struct PendingTile(RenderRequest Latest, bool Started, long Epoch, long Stamp);

    /// <summary>A request and the scheduler that judges whether its result is still wanted, passed to the surface writer.</summary>
    /// <param name="Request">The request.</param>
    /// <param name="Scheduler">The scheduler.</param>
    private readonly record struct RenderJob(RenderRequest Request, RenderScheduler Scheduler);

    /// <summary>A finished render with the invalidation epoch it started under.</summary>
    /// <param name="Tile">The tile.</param>
    /// <param name="Epoch">The epoch.</param>
    /// <param name="Request">The request that produced the surface.</param>
    private readonly record struct FinishedTile(RenderedTile Tile, long Epoch, RenderRequest Request);
}
