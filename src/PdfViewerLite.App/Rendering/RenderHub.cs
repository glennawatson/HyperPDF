// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using PdfViewerLite.Core.Rendering;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Advanced;
using ReactiveUI.Primitives.Signals;

namespace PdfViewerLite.App.Rendering;

/// <summary>
/// Owns the process wide tile cache and render scheduler. Completed tiles are moved into the cache on the UI thread in
/// batches, then <see cref="TilesArrived"/> lets visible controls repaint.
/// </summary>
[DebuggerDisplay("RenderHub: {Cache}")]
public sealed class RenderHub : IDisposable
{
    /// <summary>Emits on the UI thread after tiles were added to the cache.</summary>
    private readonly Signal<RxVoid> _tilesArrived = new();

    /// <summary>The subscription draining completed tiles.</summary>
    private readonly IDisposable _drainSubscription;

    /// <summary>The tile factory and its graphics availability.</summary>
    private readonly AvaloniaSurfaceFactory _surfaceFactory;

    /// <summary>1 once disposed.</summary>
    private int _disposed;

    /// <summary>One while a software recovery callback is queued.</summary>
    private int _recoveryQueued;

    /// <summary>Initializes a new instance of the <see cref="RenderHub"/> class.</summary>
    /// <param name="cacheBytes">The tile cache budget in bytes.</param>
    public RenderHub(long cacheBytes)
    {
        Cache = new(cacheBytes);
        _surfaceFactory = new(RequestSoftwareRecovery);
        Scheduler = new(_surfaceFactory);
        TilesArrived = new(_tilesArrived);
        _drainSubscription = Scheduler.Completed
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SubscribeSafe(_ => Drain(), static ex => Trace.TraceError(ex.ToString()));
    }

    /// <summary>Gets notifications, on the UI thread, that new tiles were added to the cache.</summary>
    public AsObservableSignal<RxVoid> TilesArrived { get; }

    /// <summary>Gets the tile cache. Use only on the UI thread.</summary>
    public TileCache Cache { get; }

    /// <summary>Gets the render scheduler.</summary>
    public RenderScheduler Scheduler { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _drainSubscription.Dispose();
        Scheduler.Dispose();
        Cache.Dispose();
        _tilesArrived.OnCompleted();
        _tilesArrived.Dispose();
    }

    /// <summary>Schedules a cache refresh so software tiles are rendered by the worker after device loss.</summary>
    internal void RequestSoftwareRecovery()
    {
        if (Volatile.Read(ref _disposed) != 0 || Interlocked.Exchange(ref _recoveryQueued, 1) != 0)
        {
            return;
        }

        Dispatcher.UIThread.Post(RecoverSoftwareOnUi, DispatcherPriority.Background);
    }

    /// <summary>Releases a document's tiles and starts a frame so its GPU images retire on the owning context.</summary>
    /// <param name="documentId">The document being closed or changed.</param>
    internal void RemoveDocumentTiles(int documentId)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        Cache.RemoveDocument(documentId);
        _tilesArrived.OnNext(RxVoid.Default);
        InvalidateWindows();
    }

    /// <summary>Starts a compositor frame even when no page canvas remains attached.</summary>
    private static void InvalidateWindows()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            foreach (var window in desktop.Windows)
            {
                window.InvalidateVisual();
            }
        }
    }

    /// <summary>Moves completed tiles into the cache.</summary>
    private void Drain()
    {
        var any = false;
        while (Scheduler.TryTakeCompleted(out var tile))
        {
            if (_surfaceFactory.IsGpuUnavailable && tile.Surface is GpuPreparedRenderSurface { SoftwareBitmap: null })
            {
                tile.Surface.Dispose();
                continue;
            }

            Cache.Add(tile.Key, tile.Surface);
            any = true;
        }

        if (any)
        {
            _tilesArrived.OnNext(RxVoid.Default);
        }
    }

    /// <summary>Releases stale GPU tiles and asks visible controls to request software output.</summary>
    private void RecoverSoftwareOnUi()
    {
        Volatile.Write(ref _recoveryQueued, 0);
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        Cache.Clear();
        _tilesArrived.OnNext(RxVoid.Default);
        InvalidateWindows();
    }
}
