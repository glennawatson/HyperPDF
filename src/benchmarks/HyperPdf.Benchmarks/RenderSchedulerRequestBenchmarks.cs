// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Rendering;
using ReactiveUI.Primitives;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures the scheduler's request path while its render thread is held on a blocking render, so every request
/// stays queued: re-requesting pending tiles, demoting then requesting new visible tiles, a subscribed visible-work
/// signal, and the visible-work check under request traffic. Uses only the public scheduler API so the baseline
/// and the change run the same definition.
/// </summary>
public class RenderSchedulerRequestBenchmarks
{
    /// <summary>The number of tiles requested in the re-request and signal cases.</summary>
    private const int FrameTiles = 60;

    /// <summary>The number of visible tiles requested before some are demoted.</summary>
    private const int InitialVisible = 40;

    /// <summary>The number of tiles demoted to prefetch.</summary>
    private const int Demoted = 30;

    /// <summary>The number of new visible tiles requested after the demotion.</summary>
    private const int NewVisible = 10;

    /// <summary>The document identifier of the blocking render.</summary>
    private const int BlockerDocument = 99;

    /// <summary>The document identifier of the measured tiles.</summary>
    private const int DocumentId = 1;

    /// <summary>The edge of a tile in pixels.</summary>
    private const int Edge = 8;

    /// <summary>Holds the render thread inside surface creation and signals when it has arrived.</summary>
    private readonly BlockingSurfaceFactory _factory = new();

    /// <summary>The scheduler under test.</summary>
    private RenderScheduler? _scheduler;

    /// <summary>The document every request names; the render thread never reaches it while blocked.</summary>
    private BenchmarkPdfDocument? _document;

    /// <summary>The client that owns the requests.</summary>
    private RenderClient? _client;

    /// <summary>A frame of visible requests.</summary>
    private RenderRequest[] _frame = [];

    /// <summary>Counts visible-work signals received.</summary>
    private int _signals;

    /// <summary>Opens the document the requests name.</summary>
    [GlobalSetup]
    public void OpenDocument() => _document = new(RenderSamplePages.CreateTextPage());

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void CloseDocument()
    {
        _document?.Dispose();
        _factory.Dispose();
    }

    /// <summary>Holds the render thread on a blocking render so requests stay queued.</summary>
    [IterationSetup]
    public void BlockRenderThread()
    {
        _factory.Reset();
        _scheduler = new(_factory);
        _client = new();
        _ = _scheduler.Request(Create(new(BlockerDocument, 0, 1, PageRotation.None, 0, 0, 0), RenderPriority.Visible));
        _factory.Entered.Wait();
        _frame = new RenderRequest[FrameTiles];
        for (var page = 0; page < FrameTiles; page++)
        {
            _frame[page] = Create(Key(page), RenderPriority.Visible);
        }
    }

    /// <summary>Lets the render thread finish and stops it.</summary>
    [IterationCleanup]
    public void ReleaseRenderThread()
    {
        _factory.Release();
        _scheduler?.Dispose();
    }

    /// <summary>Requests 60 visible tiles, then requests the same 60 again, as a repaint of unchanged tiles does.</summary>
    /// <returns>The number of new jobs queued by the second pass.</returns>
    [Benchmark]
    public int RequestPendingTilesAgain()
    {
        var scheduler = _scheduler!;
        foreach (ref readonly var request in _frame.AsSpan())
        {
            _ = scheduler.Request(request);
        }

        var queued = 0;
        foreach (ref readonly var request in _frame.AsSpan())
        {
            queued += scheduler.Request(request) ? 1 : 0;
        }

        return queued;
    }

    /// <summary>Requests 40 visible tiles, demotes 30 to prefetch, then requests 10 new visible tiles.</summary>
    /// <returns>The number of new jobs queued.</returns>
    [Benchmark]
    public int RequestDemoteThenRequestNewVisibleTiles()
    {
        var scheduler = _scheduler!;
        var queued = 0;
        for (var page = 0; page < InitialVisible; page++)
        {
            queued += scheduler.Request(_frame[page]) ? 1 : 0;
        }

        for (var page = 0; page < Demoted; page++)
        {
            queued += scheduler.Request(_frame[page] with { Priority = RenderPriority.Prefetch }) ? 1 : 0;
        }

        for (var page = InitialVisible; page < InitialVisible + NewVisible; page++)
        {
            queued += scheduler.Request(_frame[page]) ? 1 : 0;
        }

        return queued;
    }

    /// <summary>Requests 60 visible tiles one at a time with a subscribed visible-work listener.</summary>
    /// <returns>The number of signals received.</returns>
    [Benchmark]
    public int RequestFrameWithSubscribedListener()
    {
        var scheduler = _scheduler!;
        _signals = 0;
        using var subscription = scheduler.VisibleWorkRequested.SubscribeSafe(_ => _signals++, static _ => { });
        foreach (ref readonly var request in _frame.AsSpan())
        {
            _ = scheduler.Request(request);
        }

        return _signals;
    }

    /// <summary>Requests 60 visible tiles then reads whether visible work is pending, as the preparer's quiet check does.</summary>
    /// <returns>Whether visible work was pending.</returns>
    [Benchmark]
    public bool HasVisibleWorkWithSixtyPending()
    {
        var scheduler = _scheduler!;
        foreach (ref readonly var request in _frame.AsSpan())
        {
            _ = scheduler.Request(request);
        }

        return scheduler.HasVisibleWork;
    }

    /// <summary>Creates a tile key.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The key.</returns>
    private static TileKey Key(int page) => new(DocumentId, page, 1, PageRotation.None, 0, 0, 0);

    /// <summary>Creates a request.</summary>
    /// <param name="key">The key.</param>
    /// <param name="priority">The priority.</param>
    /// <returns>The request.</returns>
    private RenderRequest Create(in TileKey key, RenderPriority priority) =>
        new(key, _document!.Document, new(0, 1, PageRotation.None, 0, 0, RenderFlags.None), Edge, Edge, priority, _client!, _client!.Generation, PageTone.None);

    /// <summary>Creates surfaces, the first of which blocks until released.</summary>
    private sealed class BlockingSurfaceFactory : IRenderSurfaceFactory, IDisposable
    {
        /// <summary>Holds the render thread until released.</summary>
        private readonly ManualResetEventSlim _gate = new(false);

        /// <summary>Gets the signal set once the render thread is inside the first surface creation.</summary>
        internal ManualResetEventSlim Entered { get; } = new(false);

        /// <inheritdoc/>
        public IRenderSurface Create(int width, int height)
        {
            Entered.Set();
            _gate.Wait();
            return new EmptySurface(width, height);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Entered.Dispose();
            _gate.Dispose();
        }

        /// <summary>Re-arms the factory for a new iteration.</summary>
        internal void Reset()
        {
            Entered.Reset();
            _gate.Reset();
        }

        /// <summary>Lets the blocked creation continue.</summary>
        internal void Release() => _gate.Set();
    }

    /// <summary>A surface that renders nothing.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    private sealed class EmptySurface(int width, int height) : IRenderSurface
    {
        /// <inheritdoc/>
        public int Width => width;

        /// <inheritdoc/>
        public int Height => height;

        /// <inheritdoc/>
        public long ByteSize => 0;

        /// <inheritdoc/>
        public bool Write<TState>(in TState state, SurfaceWriter<TState> writer) => false;

        /// <inheritdoc/>
        public void Dispose()
        {
        }
    }
}
