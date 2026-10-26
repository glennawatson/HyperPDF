// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Rendering;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Signals;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Rendering;

/// <summary>
/// Owns the process wide tile cache and render scheduler. Completed tiles are moved into the cache on the UI thread in
/// batches, then <see cref="TilesArrived"/> lets visible controls repaint.
/// </summary>
[DebuggerDisplay("RenderHub: {Cache}")]
public sealed partial class RenderHub : ReactiveObject, IDisposable
{
    /// <summary>
    /// Passes a software recovery request from a render thread, which asks for one when the GPU is lost, to the signal
    /// the constructor's chain moves onto the UI thread. It is a signal because the requests come from any thread; the
    /// hub owns the signal through <see cref="_subscriptions"/>.
    /// </summary>
    private readonly Action<RxVoid> _requestRecovery;

    /// <summary>Owns the signal, the drain and the cache, and releases them in the order they were added.</summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>The tile factory and its graphics availability.</summary>
    private readonly IOwnedSurfaceFactory _surfaceFactory;

    /// <summary>The UI toolkit's side of rendering.</summary>
    private readonly IRenderHead _head;

    /// <summary>1 once disposed.</summary>
    private int _disposed;

    /// <summary>One while a software recovery callback is queued.</summary>
    private int _recoveryQueued;

    /// <summary>Initializes a new instance of the <see cref="RenderHub"/> class.</summary>
    /// <param name="cacheBytes">The tile cache budget in bytes.</param>
    /// <param name="head">The UI toolkit's side of rendering.</param>
    public RenderHub(long cacheBytes, IRenderHead head)
    {
        ArgumentNullException.ThrowIfNull(head);
        _head = head;
        Cache = new(cacheBytes);
        _surfaceFactory = head.CreateSurfaceFactory(RequestSoftwareRecovery);
        Scheduler = new(_surfaceFactory);
        Preparer = new(HasVisibleWork, UnderPressure, NeighbourPagePreparer.DefaultDebounce, TimeProvider.System);
        TilesArrived = this.WhenChanged(static hub => hub.TileVersion).Skip(1).Select(static _ => RxVoid.Default);
        PreviewsArrived = this.WhenChanged(static hub => hub.PreviewVersion).Skip(1).Select(static _ => RxVoid.Default);

        // Closing is first, so work that finishes while the rest is released finds the hub closed.
        var recoveryRequests = new Signal<RxVoid>();
        _requestRecovery = recoveryRequests.OnNext;
        _subscriptions.Add(Scope.Create(MarkClosed));
        _subscriptions.Add(
            Scheduler.Completed
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SubscribeSafe(_ => Drain(), ErrorTrace.Write));
        _subscriptions.Add(
            recoveryRequests
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SubscribeSafe(_ => RecoverSoftware(), ErrorTrace.Write));
        _subscriptions.Add(recoveryRequests);

        // Something on screen needs drawing, so background preparation of neighbouring pages stands aside.
        _subscriptions.Add(Scheduler.VisibleWorkRequested.SubscribeSafe(_ => Preparer.Preempt(), ErrorTrace.Write));
        _subscriptions.Add(Preparer);
        _subscriptions.Add(Scheduler);
        _subscriptions.Add(Cache);
    }

    /// <summary>Gets notifications, on the UI thread, that new tiles were added to the cache.</summary>
    public IObservable<RxVoid> TilesArrived { get; }

    /// <summary>Gets a number that changes each time tiles were added to the cache, or dropped from it.</summary>
    [Reactive]
    public partial int TileVersion { get; private set; }

    /// <summary>
    /// Gets notifications, on the UI thread, that a page preview was added to the cache, or that cached tiles were dropped
    /// or must be presented again. Tiles of the page canvas alone do not raise it.
    /// </summary>
    public IObservable<RxVoid> PreviewsArrived { get; }

    /// <summary>Gets a number that changes each time <see cref="PreviewsArrived"/> raises.</summary>
    [Reactive]
    public partial int PreviewVersion { get; private set; }

    /// <summary>Gets the tile cache. Use only on the UI thread.</summary>
    public TileCache Cache { get; }

    /// <summary>Gets the render scheduler.</summary>
    public RenderScheduler Scheduler { get; }

    /// <summary>Gets the worker that prepares the pages next to the one being read.</summary>
    public NeighbourPagePreparer Preparer { get; }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        _subscriptions.Dispose();
        _surfaceFactory.Dispose();
    }

    /// <summary>Schedules a repaint so cached GPU tiles can present software pixels after device loss.</summary>
    internal void RequestSoftwareRecovery()
    {
        if (Volatile.Read(ref _disposed) != 0 || Interlocked.Exchange(ref _recoveryQueued, 1) != 0)
        {
            return;
        }

        _requestRecovery(RxVoid.Default);
    }

    /// <summary>Releases a document's tiles and starts a frame so its GPU images retire on the owning context.</summary>
    /// <param name="documentId">The document being closed or changed.</param>
    internal void RemoveDocumentTiles(int documentId)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        Preparer.Reset();
        Cache.RemoveDocument(documentId);
        TileVersion++;
        PreviewVersion++;
        InvalidateWindows();
    }

    /// <summary>Starts a compositor frame even when no page canvas remains attached.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InvalidateWindows() => _head.InvalidateDisplay();

    /// <summary>Tells whether something on screen is queued or rendering.</summary>
    /// <returns><see langword="true"/> when preparation of neighbouring pages should wait.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool HasVisibleWork() => Scheduler.HasVisibleWork;

    /// <summary>
    /// Tells whether memory is short: the system is under load, or the tile cache is nearly full. The cache's byte count is a
    /// single 64-bit read, which is safe to make from the preparation thread; a stale value only moves the decision by a tile.
    /// </summary>
    /// <returns><see langword="true"/> when neighbouring pages should not be prepared.</returns>
    private bool UnderPressure() => MemoryPressure.IsSystemHigh() || MemoryPressure.IsCacheFull(Cache);

    /// <summary>Marks the hub closed, so work that finishes while the rest is released finds it closed.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void MarkClosed() => Volatile.Write(ref _disposed, 1);

    /// <summary>Moves completed tiles into the cache.</summary>
    private void Drain()
    {
        var any = false;
        var preview = false;
        while (Scheduler.TryTakeCompleted(out var tile))
        {
            Cache.Add(tile.Key, tile.Surface);
            any = true;
            preview |= tile.Key.IsPreview;
        }

        if (any)
        {
            TileVersion++;
        }

        if (preview)
        {
            PreviewVersion++;
        }
    }

    /// <summary>Repaints visible tiles after GPU loss while retaining cached tiles for software presentation.</summary>
    private void RecoverSoftware()
    {
        Volatile.Write(ref _recoveryQueued, 0);
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        TileVersion++;
        PreviewVersion++;
        InvalidateWindows();
    }
}
