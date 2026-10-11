// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Rendering;
using PdfViewerLite.Core.Tests.Fakes;
using ReactiveUI.Primitives;

namespace PdfViewerLite.Core.Tests.Rendering;

/// <summary>Tests for <see cref="RenderScheduler"/>.</summary>
public sealed class RenderSchedulerTests
{
    /// <summary>The edge of test tiles.</summary>
    private const int Edge = 8;

    /// <summary>The document identifier of the document used to occupy the render thread.</summary>
    private const int BlockerDocument = 99;

    /// <summary>The page still wanted after the view moves.</summary>
    private const int WantedPage = 2;

    /// <summary>The file name of the document used to occupy the render thread.</summary>
    private const string BlockerName = "blocker.pdf";

    /// <summary>The test document file name.</summary>
    private const string DocumentName = "a.pdf";

    /// <summary>How long to wait for asynchronous work.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <summary>The longest Dispose may occupy a caller while a render is still active.</summary>
    private static readonly TimeSpan DisposeLimit = TimeSpan.FromSeconds(2);

    /// <summary>How often the test checks whether its blocked render completed.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);

    /// <summary>Verifies a request is rendered and handed back once.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RendersAndCompletes()
    {
        using var completed = new SemaphoreSlim(0);
        using var scheduler = new RenderScheduler(new FakeSurfaceFactory());
        using var completions = scheduler.Completed.SubscribeSafe(_ => completed.Release(), static _ => { });
        var document = new FakeDocument(DocumentName, FakeEngine.A4);
        var client = new RenderClient();
        var key = Key(0);

        await Assert.That(scheduler.Request(Request(key, document, client, RenderPriority.Visible))).IsTrue();
        await Assert.That(await completed.WaitAsync(Timeout)).IsTrue();
        await Assert.That(scheduler.TryTakeCompleted(out var tile)).IsTrue();

        await Assert.That(tile.Key).IsEqualTo(key);
        await Assert.That(document.RenderCount).IsEqualTo(1);
        await Assert.That(scheduler.IsPending(key)).IsFalse();
        tile.Surface.Dispose();
    }

    /// <summary>Slow page preparation releases the render thread and resumes the waiting page afterwards.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreparationDoesNotBlockOtherPages()
    {
        using var completed = new SemaphoreSlim(0);
        using var timeout = new CancellationTokenSource(Timeout);
        using var scheduler = new RenderScheduler(new FakeSurfaceFactory());
        using var completions = scheduler.Completed.SubscribeSafe(_ => completed.Release(), static _ => { });
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiting = new FakeDocument(DocumentName, FakeEngine.A4);
        waiting.Preparation = (pageIndex, token) =>
        {
            _ = pageIndex;
            _ = entered.TrySetResult();
            return new(release.Task.WaitAsync(token));
        };
        var ready = new FakeDocument(DocumentName, FakeEngine.A4, FakeEngine.A4);
        var client = new RenderClient();

        _ = scheduler.Request(Request(Key(0), waiting, client, RenderPriority.Visible));
        await entered.Task.WaitAsync(timeout.Token);
        _ = scheduler.Request(Request(Key(1), ready, client, RenderPriority.Visible));
        await Assert.That(await completed.WaitAsync(Timeout)).IsTrue();
        await Assert.That(scheduler.TryTakeCompleted(out var first)).IsTrue();
        await Assert.That(first.Key).IsEqualTo(Key(1));
        await Assert.That(waiting.RenderCount).IsEqualTo(0);
        first.Surface.Dispose();

        _ = release.TrySetResult();
        await Assert.That(await completed.WaitAsync(Timeout)).IsTrue();
        await Assert.That(scheduler.TryTakeCompleted(out var second)).IsTrue();
        await Assert.That(second.Key).IsEqualTo(Key(0));
        await Assert.That(waiting.RenderCount).IsEqualTo(1);
        second.Surface.Dispose();
    }

    /// <summary>Switching away cancels in-progress page preparation before a stale tile can render.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TabCancellationStopsInProgressPreparation()
    {
        using var scheduler = new RenderScheduler(new FakeSurfaceFactory());
        using var tab = CancellationTokenSource.CreateLinkedTokenSource(scheduler.ShutdownToken);
        using var timeout = new CancellationTokenSource(Timeout);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var document = new FakeDocument(DocumentName, FakeEngine.A4);
        document.Preparation = async (_, token) =>
        {
            Signal(entered);

            try
            {
                await release.Task.WaitAsync(token);
            }
            finally
            {
                Signal(stopped);
            }
        };
        var request = Request(Key(0), document, new(), RenderPriority.Visible) with { CancellationToken = tab.Token };
        await Assert.That(scheduler.Request(request)).IsTrue();
        await entered.Task.WaitAsync(timeout.Token);

        var started = Stopwatch.GetTimestamp();
        await tab.CancelAsync();
        scheduler.Invalidate(request.Key.DocumentId);
        await stopped.Task.WaitAsync(timeout.Token);

        await Assert.That(Stopwatch.GetElapsedTime(started)).IsLessThan(DisposeLimit);
        await Assert.That(document.RenderCount).IsEqualTo(0);
        await Assert.That(scheduler.TryTakeCompleted(out _)).IsFalse();
    }

    /// <summary>A synchronous preparation failure leaves the render thread available for the next page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreparationFailureDoesNotStopRendering()
    {
        using var completed = new SemaphoreSlim(0);
        using var scheduler = new RenderScheduler(new FakeSurfaceFactory());
        using var completions = scheduler.Completed.SubscribeSafe(_ => completed.Release(), static _ => { });
        var failed = new FakeDocument(DocumentName, FakeEngine.A4);
        failed.Preparation = static (_, _) => throw new IOException("The resource is unavailable.");
        var ready = new FakeDocument(DocumentName, FakeEngine.A4, FakeEngine.A4);
        var client = new RenderClient();
        _ = scheduler.Request(Request(Key(0), failed, client, RenderPriority.Visible));
        _ = scheduler.Request(Request(Key(1), ready, client, RenderPriority.Visible));

        await Assert.That(await completed.WaitAsync(Timeout)).IsTrue();
        await Assert.That(scheduler.TryTakeCompleted(out var tile)).IsTrue();
        await Assert.That(tile.Key).IsEqualTo(Key(1));
        await Assert.That(scheduler.IsPending(Key(0))).IsFalse();
        tile.Surface.Dispose();
    }

    /// <summary>Verifies duplicate requests are coalesced.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CoalescesDuplicates()
    {
        using var gate = new ManualResetEventSlim(false);
        using var scheduler = new RenderScheduler(new FakeSurfaceFactory());
        var document = new FakeDocument(DocumentName, FakeEngine.A4) { Gate = gate };
        var client = new RenderClient();

        var first = scheduler.Request(Request(Key(0), document, client, RenderPriority.Visible));
        var second = scheduler.Request(Request(Key(0), document, client, RenderPriority.Visible));
        gate.Set();

        await Assert.That(first).IsTrue();
        await Assert.That(second).IsFalse();
    }

    /// <summary>Verifies requests made stale by advancing the client generation are never rendered.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DropsStaleRequests()
    {
        using var gate = new ManualResetEventSlim(false);
        using var completed = new SemaphoreSlim(0);
        using var scheduler = new RenderScheduler(new FakeSurfaceFactory());
        using var completions = scheduler.Completed.SubscribeSafe(_ => completed.Release(), static _ => { });
        var blocker = new FakeDocument(BlockerName, FakeEngine.A4) { Gate = gate };
        var document = new FakeDocument(DocumentName, FakeEngine.A4, FakeEngine.A4, FakeEngine.A4);
        var client = new RenderClient();
        var blockerClient = new RenderClient();
        const int stalePages = 3;

        // Occupy the render thread so the following requests queue up.
        _ = scheduler.Request(Request(new(BlockerDocument, 0, 1, PageRotation.None, 0, 0, 0), blocker, blockerClient, RenderPriority.Visible));
        _ = blocker.Started.Wait(Timeout);
        for (var page = 0; page < stalePages; page++)
        {
            _ = scheduler.Request(Request(Key(page), document, client, RenderPriority.Visible));
        }

        // The view moved: only page 2 is still wanted.
        var generation = client.Advance();
        _ = scheduler.Request(Request(Key(WantedPage), document, client, RenderPriority.Visible) with { Generation = generation });
        gate.Set();

        var taken = new List<TileKey>();
        while (taken.Count < WantedPage && await completed.WaitAsync(Timeout))
        {
            while (scheduler.TryTakeCompleted(out var tile))
            {
                taken.Add(tile.Key);
                tile.Surface.Dispose();
            }
        }

        await Assert.That(document.RenderCount).IsEqualTo(1);
        await Assert.That(taken).Contains(Key(WantedPage));
        await Assert.That(scheduler.IsPending(Key(0))).IsFalse();
    }

    /// <summary>Verifies higher priority work runs first.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RunsHigherPriorityFirst()
    {
        using var gate = new ManualResetEventSlim(false);
        using var completed = new SemaphoreSlim(0);
        using var scheduler = new RenderScheduler(new FakeSurfaceFactory());
        using var completions = scheduler.Completed.SubscribeSafe(_ => completed.Release(), static _ => { });
        var blocker = new FakeDocument(BlockerName, FakeEngine.A4) { Gate = gate };
        var document = new FakeDocument(DocumentName, FakeEngine.A4, FakeEngine.A4);
        var client = new RenderClient();
        var blockerKey = new TileKey(BlockerDocument, 0, 1, PageRotation.None, 0, 0, 0);
        const int expectedTiles = 3;

        _ = scheduler.Request(Request(blockerKey, blocker, client, RenderPriority.Visible));
        _ = blocker.Started.Wait(Timeout);
        _ = scheduler.Request(Request(Key(0), document, client, RenderPriority.Thumbnail));
        _ = scheduler.Request(Request(Key(1), document, client, RenderPriority.VisiblePreview));
        gate.Set();

        var order = new List<TileKey>();
        while (order.Count < expectedTiles && await completed.WaitAsync(Timeout))
        {
            while (scheduler.TryTakeCompleted(out var tile))
            {
                order.Add(tile.Key);
                tile.Surface.Dispose();
            }
        }

        await Assert.That(order).IsEquivalentTo([blockerKey, Key(1), Key(0)]);
        await Assert.That(order[0]).IsEqualTo(blockerKey);
        await Assert.That(order[1]).IsEqualTo(Key(1));
    }

    /// <summary>Verifies a tile rendered before its page changed is dropped, and the page is rendered again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DropsTilesRenderedBeforeAnEdit()
    {
        using var gate = new ManualResetEventSlim(false);
        using var completed = new SemaphoreSlim(0);
        using var scheduler = new RenderScheduler(new FakeSurfaceFactory());
        using var completions = scheduler.Completed.SubscribeSafe(_ => completed.Release(), static _ => { });
        var document = new FakeDocument(DocumentName, FakeEngine.A4) { Gate = gate };
        var client = new RenderClient();
        const int renders = 2;

        _ = scheduler.Request(Request(Key(0), document, client, RenderPriority.Visible));
        _ = document.Started.Wait(Timeout);
        scheduler.Invalidate(Key(0).DocumentId);
        var requeued = scheduler.Request(Request(Key(0), document, client, RenderPriority.Visible));
        gate.Set();

        var taken = 0;
        while (document.RenderCount < renders || scheduler.IsPending(Key(0)))
        {
            _ = await completed.WaitAsync(Timeout);
            while (scheduler.TryTakeCompleted(out var tile))
            {
                taken++;
                tile.Surface.Dispose();
            }
        }

        await Assert.That(requeued).IsTrue();
        await Assert.That(document.RenderCount).IsEqualTo(renders);
        await Assert.That(taken).IsEqualTo(1);
    }

    /// <summary>Verifies a pending tile renders from the latest request, such as a document that was reopened.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RendersTheLatestRequest()
    {
        using var gate = new ManualResetEventSlim(false);
        using var completed = new SemaphoreSlim(0);
        using var scheduler = new RenderScheduler(new FakeSurfaceFactory());
        using var completions = scheduler.Completed.SubscribeSafe(_ => completed.Release(), static _ => { });
        var blocker = new FakeDocument(BlockerName, FakeEngine.A4) { Gate = gate };
        var closed = new FakeDocument(DocumentName, FakeEngine.A4);
        var reopened = new FakeDocument(DocumentName, FakeEngine.A4);
        var client = new RenderClient();

        _ = scheduler.Request(Request(new(BlockerDocument, 0, 1, PageRotation.None, 0, 0, 0), blocker, client, RenderPriority.Visible));
        _ = blocker.Started.Wait(Timeout);
        _ = scheduler.Request(Request(Key(0), closed, client, RenderPriority.Visible));
        closed.Dispose();
        var merged = scheduler.Request(Request(Key(0), reopened, client, RenderPriority.Visible));
        gate.Set();

        var found = false;
        while (!found && await completed.WaitAsync(Timeout))
        {
            while (scheduler.TryTakeCompleted(out var tile))
            {
                found |= tile.Key == Key(0);
                tile.Surface.Dispose();
            }
        }

        await Assert.That(merged).IsFalse();
        await Assert.That(found).IsTrue();
        await Assert.That(reopened.RenderCount).IsEqualTo(1);
    }

    /// <summary>Verifies a pending tile requested again at a more urgent priority no longer waits behind less urgent work.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RaisesThePriorityOfAPendingTile()
    {
        using var gate = new ManualResetEventSlim(false);
        using var completed = new SemaphoreSlim(0);
        using var scheduler = new RenderScheduler(new FakeSurfaceFactory());
        using var completions = scheduler.Completed.SubscribeSafe(_ => completed.Release(), static _ => { });
        var blocker = new FakeDocument(BlockerName, FakeEngine.A4) { Gate = gate };
        var document = new FakeDocument(DocumentName, FakeEngine.A4, FakeEngine.A4);
        var client = new RenderClient();
        var blockerKey = new TileKey(BlockerDocument, 0, 1, PageRotation.None, 0, 0, 0);
        const int expectedTiles = 3;

        _ = scheduler.Request(Request(blockerKey, blocker, client, RenderPriority.Visible));
        _ = blocker.Started.Wait(Timeout);
        _ = scheduler.Request(Request(Key(0), document, client, RenderPriority.Prefetch));
        _ = scheduler.Request(Request(Key(1), document, client, RenderPriority.Thumbnail));
        _ = scheduler.Request(Request(Key(1), document, client, RenderPriority.Visible));
        gate.Set();

        var order = new List<TileKey>();
        while (order.Count < expectedTiles && await completed.WaitAsync(Timeout))
        {
            while (scheduler.TryTakeCompleted(out var tile))
            {
                order.Add(tile.Key);
                tile.Surface.Dispose();
            }
        }

        await Assert.That(order).IsEquivalentTo([blockerKey, Key(1), Key(0)]);
        await Assert.That(order[1]).IsEqualTo(Key(1));
        await Assert.That(document.RenderCount).IsEqualTo(expectedTiles - 1);
    }

    /// <summary>Verifies the request's page tone recolours rendered pixels.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AppliesPageTone()
    {
        const uint paper = 0x2A2826U;
        const uint ink = 0xD2CDC5U;
        const byte inkBlue = 0xC5;
        using var completed = new SemaphoreSlim(0);
        using var scheduler = new RenderScheduler(new FakeSurfaceFactory());
        using var completions = scheduler.Completed.SubscribeSafe(_ => completed.Release(), static _ => { });
        var document = new FakeDocument(DocumentName, FakeEngine.A4);
        var request = Request(Key(0), document, new(), RenderPriority.Visible) with { Tone = new(paper, ink) };

        _ = scheduler.Request(request);
        _ = await completed.WaitAsync(Timeout);
        _ = scheduler.TryTakeCompleted(out var tile);

        // The fake renders page 0 as all-zero bytes (black), which the tone maps to the ink colour.
        await Assert.That(((FakeSurface)tile.Surface).Pixels[0]).IsEqualTo(inkBlue);
        tile.Surface.Dispose();
    }

    /// <summary>A preparation surface records content without asking the document for CPU pixels.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PreparationSurfaceSkipsCpuRaster()
    {
        using var completed = new SemaphoreSlim(0);
        var factory = new PreparationSurfaceFactory();
        using var scheduler = new RenderScheduler(factory);
        using var completions = scheduler.Completed.SubscribeSafe(_ => completed.Release(), static _ => { });
        using var document = new FakeDocument(DocumentName, FakeEngine.A4);
        var key = Key(0);

        _ = scheduler.Request(Request(key, document, new(), RenderPriority.Visible));
        await Assert.That(await completed.WaitAsync(Timeout)).IsTrue();
        await Assert.That(scheduler.TryTakeCompleted(out var tile)).IsTrue();
        await Assert.That(tile.Surface).IsTypeOf<PreparationSurface>();
        await Assert.That(((PreparationSurface)tile.Surface).PrepareCount).IsEqualTo(1);
        await Assert.That(document.RenderCount).IsEqualTo(0);
        tile.Surface.Dispose();
    }

    /// <summary>A completed prepared tile is released when its client advances before collection.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PreparedTileDropsAfterGenerationAdvance()
    {
        using var completed = new SemaphoreSlim(0);
        var factory = new PreparationSurfaceFactory();
        using var scheduler = new RenderScheduler(factory);
        using var completions = scheduler.Completed.SubscribeSafe(_ => completed.Release(), static _ => { });
        using var document = new FakeDocument(DocumentName, FakeEngine.A4);
        var client = new RenderClient();
        var key = Key(0);

        _ = scheduler.Request(Request(key, document, client, RenderPriority.Visible));
        await Assert.That(await completed.WaitAsync(Timeout)).IsTrue();
        _ = client.Advance();

        await Assert.That(scheduler.TryTakeCompleted(out _)).IsFalse();
        await Assert.That(factory.Last!.IsDisposed).IsTrue();
        await Assert.That(scheduler.IsPending(key)).IsFalse();
    }

    /// <summary>A new generation requested during an active render replaces its stale result.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ActiveRenderIsReplacedAfterGenerationAdvance()
    {
        const int expectedRenders = 2;
        using var gate = new ManualResetEventSlim(false);
        using var completed = new SemaphoreSlim(0);
        using var scheduler = new RenderScheduler(new FakeSurfaceFactory());
        using var completions = scheduler.Completed.SubscribeSafe(_ => completed.Release(), static _ => { });
        using var document = new FakeDocument(DocumentName, FakeEngine.A4) { Gate = gate };
        var client = new RenderClient();
        var key = Key(0);

        _ = scheduler.Request(Request(key, document, client, RenderPriority.Visible));
        await document.FirstRenderStarted.WaitAsync(Timeout);
        _ = client.Advance();
        var requeued = scheduler.Request(Request(key, document, client, RenderPriority.Visible));
        gate.Set();

        var taken = 0;
        while (scheduler.IsPending(key))
        {
            await Assert.That(await completed.WaitAsync(Timeout)).IsTrue();
            while (scheduler.TryTakeCompleted(out var tile))
            {
                taken++;
                tile.Surface.Dispose();
            }
        }

        await Assert.That(requeued).IsTrue();
        await Assert.That(document.RenderCount).IsEqualTo(expectedRenders);
        await Assert.That(taken).IsEqualTo(1);
    }

    /// <summary>Closing a viewer does not wait for an active render to release the UI thread.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DisposeReturnsWhileRenderIsActive()
    {
        using var gate = new ManualResetEventSlim(false);
        var scheduler = new RenderScheduler(new FakeSurfaceFactory());
        using var document = new FakeDocument(DocumentName, FakeEngine.A4) { Gate = gate };
        using var timeout = new CancellationTokenSource(Timeout);
        using var timer = new PeriodicTimer(PollInterval);
        var key = Key(0);
        TimeSpan elapsed;
        try
        {
            _ = scheduler.Request(Request(key, document, new(), RenderPriority.Visible));
            await document.FirstRenderStarted.WaitAsync(timeout.Token);
        }
        finally
        {
            var started = Stopwatch.GetTimestamp();
            scheduler.Dispose();
            elapsed = Stopwatch.GetElapsedTime(started);
            gate.Set();
        }

        while (document.RenderCount == 0)
        {
            if (!await timer.WaitForNextTickAsync(timeout.Token))
            {
                break;
            }
        }

        await Assert.That(elapsed).IsLessThan(DisposeLimit);
        await Assert.That(document.RenderCount).IsEqualTo(1);
    }

    /// <summary>Disposal cancels queued page preparation before any tile surface is created.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DisposeCancelsAwaitingPreparation()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var factory = new PreparationSurfaceFactory();
        var scheduler = new RenderScheduler(factory);
        using var document = new FakeDocument(DocumentName, FakeEngine.A4);
        using var timeout = new CancellationTokenSource(Timeout);
        Task? pending = null;
        document.Preparation = (pageIndex, token) =>
        {
            _ = pageIndex;
            pending = release.Task.WaitAsync(token);
            _ = entered.TrySetResult();
            return new(pending);
        };

        try
        {
            _ = scheduler.Request(Request(Key(0), document, new(), RenderPriority.Visible));
            await entered.Task.WaitAsync(timeout.Token);
        }
        finally
        {
            scheduler.Dispose();
        }

        var cancelled = false;
        try
        {
            await pending!;
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        await Assert.That(cancelled).IsTrue();
        await Assert.That(factory.Last).IsNull();
        await Assert.That(document.RenderCount).IsEqualTo(0);
    }

    /// <summary>Signals a test phase even when another callback has already signaled it.</summary>
    /// <param name="source">The phase signal.</param>
    private static void Signal(TaskCompletionSource source) => _ = source.TrySetResult();

    /// <summary>Creates a key for a page.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The key.</returns>
    private static TileKey Key(int page) => new(1, page, 1, PageRotation.None, 0, 0, 0);

    /// <summary>Creates a request.</summary>
    /// <param name="key">The key.</param>
    /// <param name="document">The document.</param>
    /// <param name="client">The client.</param>
    /// <param name="priority">The priority.</param>
    /// <returns>The request.</returns>
    private static RenderRequest Request(in TileKey key, IDocument document, RenderClient client, RenderPriority priority) =>
        new(key, document, new(key.PageIndex, 1, PageRotation.None, 0, 0, RenderFlags.None), Edge, Edge, priority, client, client.Generation, PageTone.None);

    /// <summary>Creates surfaces that prepare page recordings.</summary>
    private sealed class PreparationSurfaceFactory : IRenderSurfaceFactory
    {
        /// <summary>Gets the most recently created surface.</summary>
        internal PreparationSurface? Last { get; private set; }

        /// <inheritdoc/>
        public IRenderSurface Create(int width, int height) => Last = new(width, height);
    }

    /// <summary>A test surface that fails if asked to write CPU pixels.</summary>
    /// <param name="width">The surface width.</param>
    /// <param name="height">The surface height.</param>
    private sealed class PreparationSurface(int width, int height) : IRenderPreparationSurface
    {
        /// <summary>The bytes in one colour pixel.</summary>
        private const int BytesPerPixel = 4;

        /// <inheritdoc/>
        public int Width { get; } = width;

        /// <inheritdoc/>
        public int Height { get; } = height;

        /// <inheritdoc/>
        public long ByteSize => (long)Width * Height * BytesPerPixel;

        /// <summary>Gets how often a page was prepared.</summary>
        internal int PrepareCount { get; private set; }

        /// <summary>Gets whether the surface was disposed.</summary>
        internal bool IsDisposed { get; private set; }

        /// <inheritdoc/>
        public bool Prepare(in RenderRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PrepareCount++;
            return true;
        }

        /// <inheritdoc/>
        public bool Write<TState>(in TState state, SurfaceWriter<TState> writer) => throw new InvalidOperationException("CPU pixels were requested.");

        /// <inheritdoc/>
        public void Dispose() => IsDisposed = true;
    }
}
