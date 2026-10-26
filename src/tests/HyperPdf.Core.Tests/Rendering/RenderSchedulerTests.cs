// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Rendering;
using HyperPdf.Core.Tests.Fakes;
using ReactiveUI.Primitives;

namespace HyperPdf.Core.Tests.Rendering;

/// <summary>Tests for <see cref="RenderScheduler"/>. Every wait is on a signal the scheduler or the fake document raises; the test run's own cancellation ends a wait that never comes.</summary>
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

    /// <summary>Verifies a request is rendered and handed back once.</summary>
    /// <param name="cancellationToken">Ends the waits when the test is cancelled.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task RendersAndCompletes(CancellationToken cancellationToken)
    {
        using var completed = new SemaphoreSlim(0);
        using var scheduler = new RenderScheduler(new FakeSurfaceFactory());
        using var completions = scheduler.Completed.SubscribeSafe(_ => completed.Release(), static _ => { });
        var document = new FakeDocument(DocumentName, FakeEngine.A4);
        var client = new RenderClient();
        var key = Key(0);

        await Assert.That(scheduler.Request(Request(key, document, client, RenderPriority.Visible))).IsTrue();
        await completed.WaitAsync(cancellationToken);
        await Assert.That(scheduler.TryTakeCompleted(out var tile)).IsTrue();

        await Assert.That(tile.Key).IsEqualTo(key);
        await Assert.That(document.RenderCount).IsEqualTo(1);
        await Assert.That(scheduler.IsPending(key)).IsFalse();
        tile.Surface.Dispose();
    }

    /// <summary>Slow page preparation releases the render thread and resumes the waiting page afterwards.</summary>
    /// <param name="cancellationToken">Ends the waits when the test is cancelled.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreparationDoesNotBlockOtherPages(CancellationToken cancellationToken)
    {
        using var completed = new SemaphoreSlim(0);
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
        await entered.Task.WaitAsync(cancellationToken);
        _ = scheduler.Request(Request(Key(1), ready, client, RenderPriority.Visible));
        await completed.WaitAsync(cancellationToken);
        await Assert.That(scheduler.TryTakeCompleted(out var first)).IsTrue();
        await Assert.That(first.Key).IsEqualTo(Key(1));
        await Assert.That(waiting.RenderCount).IsEqualTo(0);
        first.Surface.Dispose();

        _ = release.TrySetResult();
        await completed.WaitAsync(cancellationToken);
        await Assert.That(scheduler.TryTakeCompleted(out var second)).IsTrue();
        await Assert.That(second.Key).IsEqualTo(Key(0));
        await Assert.That(waiting.RenderCount).IsEqualTo(1);
        second.Surface.Dispose();
    }

    /// <summary>Switching away cancels in-progress page preparation before a stale tile can render.</summary>
    /// <param name="cancellationToken">Ends the waits when the test is cancelled.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task TabCancellationStopsInProgressPreparation(CancellationToken cancellationToken)
    {
        using var scheduler = new RenderScheduler(new FakeSurfaceFactory());
        using var tab = CancellationTokenSource.CreateLinkedTokenSource(scheduler.ShutdownToken);
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
        await entered.Task.WaitAsync(cancellationToken);

        await tab.CancelAsync();
        scheduler.Invalidate(request.Key.DocumentId);

        // The preparation only ends because the tab's token reached it: nothing else releases it.
        await stopped.Task.WaitAsync(cancellationToken);
        await Assert.That(release.Task.IsCompleted).IsFalse();
        await Assert.That(document.RenderCount).IsEqualTo(0);
        await Assert.That(scheduler.TryTakeCompleted(out _)).IsFalse();
    }

    /// <summary>A synchronous preparation failure leaves the render thread available for the next page.</summary>
    /// <param name="cancellationToken">Ends the waits when the test is cancelled.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreparationFailureDoesNotStopRendering(CancellationToken cancellationToken)
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

        await completed.WaitAsync(cancellationToken);
        await Assert.That(scheduler.TryTakeCompleted(out var tile)).IsTrue();
        await Assert.That(tile.Key).IsEqualTo(Key(1));
        await Assert.That(scheduler.IsPending(Key(0))).IsFalse();
        tile.Surface.Dispose();
    }

    /// <summary>A render that throws a damaged-file or read exception loses only its tile.</summary>
    /// <param name="failureKind">Which exception the render throws.</param>
    /// <param name="cancellationToken">Ends the waits when the test is cancelled.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(nameof(InvalidDataException))]
    [Arguments(nameof(IOException))]
    [Arguments(nameof(FormatException))]
    [Arguments(nameof(OverflowException))]
    public async Task RenderFailureDoesNotStopRendering(string failureKind, CancellationToken cancellationToken)
    {
        using var completed = new SemaphoreSlim(0);
        using var scheduler = new RenderScheduler(new FakeSurfaceFactory());
        using var completions = scheduler.Completed.SubscribeSafe(_ => completed.Release(), static _ => { });
        var failed = new FakeDocument(DocumentName, FakeEngine.A4)
        {
            RenderFailure = failureKind switch
            {
                nameof(InvalidDataException) => new InvalidDataException("The decoded data is larger than the supported limit."),
                nameof(IOException) => new IOException("The read failed."),
                nameof(FormatException) => new FormatException("The number is malformed."),
                _ => new OverflowException("The value is too large."),
            },
        };
        var ready = new FakeDocument(DocumentName, FakeEngine.A4, FakeEngine.A4);
        var client = new RenderClient();
        _ = scheduler.Request(Request(Key(0), failed, client, RenderPriority.Visible));
        _ = scheduler.Request(Request(Key(1), ready, client, RenderPriority.Visible));

        await completed.WaitAsync(cancellationToken);
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
    /// <param name="cancellationToken">Ends the waits when the test is cancelled.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task DropsStaleRequests(CancellationToken cancellationToken)
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
        blocker.Started.Wait(cancellationToken);
        for (var page = 0; page < stalePages; page++)
        {
            _ = scheduler.Request(Request(Key(page), document, client, RenderPriority.Visible));
        }

        // The view moved: only page 2 is still wanted.
        var generation = client.Advance();
        _ = scheduler.Request(Request(Key(WantedPage), document, client, RenderPriority.Visible) with { Generation = generation });
        gate.Set();

        var taken = await CompletedTiles.CollectAsync(scheduler, completed, WantedPage, cancellationToken);

        await Assert.That(document.RenderCount).IsEqualTo(1);
        await Assert.That(taken).Contains(Key(WantedPage));
        await Assert.That(scheduler.IsPending(Key(0))).IsFalse();
    }

    /// <summary>Verifies higher priority work runs first.</summary>
    /// <param name="cancellationToken">Ends the waits when the test is cancelled.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task RunsHigherPriorityFirst(CancellationToken cancellationToken)
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
        blocker.Started.Wait(cancellationToken);
        _ = scheduler.Request(Request(Key(0), document, client, RenderPriority.Thumbnail));
        _ = scheduler.Request(Request(Key(1), document, client, RenderPriority.VisiblePreview));
        gate.Set();

        var order = await CompletedTiles.CollectAsync(scheduler, completed, expectedTiles, cancellationToken);

        await Assert.That(order).IsEquivalentTo([blockerKey, Key(1), Key(0)]);
        await Assert.That(order[0]).IsEqualTo(blockerKey);
        await Assert.That(order[1]).IsEqualTo(Key(1));
    }

    /// <summary>Verifies a tile rendered before its page changed is dropped, and the page is rendered again.</summary>
    /// <param name="cancellationToken">Ends the waits when the test is cancelled.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task DropsTilesRenderedBeforeAnEdit(CancellationToken cancellationToken)
    {
        using var gate = new ManualResetEventSlim(false);
        using var completed = new SemaphoreSlim(0);
        using var scheduler = new RenderScheduler(new FakeSurfaceFactory());
        using var completions = scheduler.Completed.SubscribeSafe(_ => completed.Release(), static _ => { });
        var document = new FakeDocument(DocumentName, FakeEngine.A4) { Gate = gate };
        var client = new RenderClient();
        const int renders = 2;

        _ = scheduler.Request(Request(Key(0), document, client, RenderPriority.Visible));
        document.Started.Wait(cancellationToken);
        scheduler.Invalidate(Key(0).DocumentId);
        var requeued = scheduler.Request(Request(Key(0), document, client, RenderPriority.Visible));
        gate.Set();

        var taken = 0;
        while (document.RenderCount < renders || scheduler.IsPending(Key(0)))
        {
            await completed.WaitAsync(cancellationToken);
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
    /// <param name="cancellationToken">Ends the waits when the test is cancelled.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task RendersTheLatestRequest(CancellationToken cancellationToken)
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
        blocker.Started.Wait(cancellationToken);
        _ = scheduler.Request(Request(Key(0), closed, client, RenderPriority.Visible));
        closed.Dispose();
        var merged = scheduler.Request(Request(Key(0), reopened, client, RenderPriority.Visible));
        gate.Set();

        var found = false;
        var order = new List<TileKey>();
        while (!found)
        {
            await completed.WaitAsync(cancellationToken);
            CompletedTiles.Drain(scheduler, order);
            found = order.Contains(Key(0));
        }

        await Assert.That(merged).IsFalse();
        await Assert.That(found).IsTrue();
        await Assert.That(reopened.RenderCount).IsEqualTo(1);
    }

    /// <summary>Verifies a pending tile requested again at a more urgent priority no longer waits behind less urgent work.</summary>
    /// <param name="cancellationToken">Ends the waits when the test is cancelled.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task RaisesThePriorityOfAPendingTile(CancellationToken cancellationToken)
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
        blocker.Started.Wait(cancellationToken);
        _ = scheduler.Request(Request(Key(0), document, client, RenderPriority.Prefetch));
        _ = scheduler.Request(Request(Key(1), document, client, RenderPriority.Thumbnail));
        _ = scheduler.Request(Request(Key(1), document, client, RenderPriority.Visible));
        gate.Set();

        var order = await CompletedTiles.CollectAsync(scheduler, completed, expectedTiles, cancellationToken);

        await Assert.That(order).IsEquivalentTo([blockerKey, Key(1), Key(0)]);
        await Assert.That(order[1]).IsEqualTo(Key(1));
        await Assert.That(document.RenderCount).IsEqualTo(expectedTiles - 1);
    }

    /// <summary>Verifies the request's page tone recolours rendered pixels.</summary>
    /// <param name="cancellationToken">Ends the waits when the test is cancelled.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task AppliesPageTone(CancellationToken cancellationToken)
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
        await completed.WaitAsync(cancellationToken);
        _ = scheduler.TryTakeCompleted(out var tile);

        // The fake renders page 0 as all-zero bytes (black), which the tone maps to the ink colour.
        await Assert.That(((FakeSurface)tile.Surface).Pixels[0]).IsEqualTo(inkBlue);
        tile.Surface.Dispose();
    }

    /// <summary>A preparation surface records content without asking the document for CPU pixels.</summary>
    /// <param name="cancellationToken">Ends the waits when the test is cancelled.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PreparationSurfaceSkipsCpuRaster(CancellationToken cancellationToken)
    {
        using var completed = new SemaphoreSlim(0);
        var factory = new PreparationSurfaceFactory();
        using var scheduler = new RenderScheduler(factory);
        using var completions = scheduler.Completed.SubscribeSafe(_ => completed.Release(), static _ => { });
        using var document = new FakeDocument(DocumentName, FakeEngine.A4);
        var key = Key(0);

        _ = scheduler.Request(Request(key, document, new(), RenderPriority.Visible));
        await completed.WaitAsync(cancellationToken);
        await Assert.That(scheduler.TryTakeCompleted(out var tile)).IsTrue();
        await Assert.That(tile.Surface).IsTypeOf<PreparationSurface>();
        await Assert.That(((PreparationSurface)tile.Surface).PrepareCount).IsEqualTo(1);
        await Assert.That(document.RenderCount).IsEqualTo(0);
        tile.Surface.Dispose();
    }

    /// <summary>A completed prepared tile is released when its client advances before collection.</summary>
    /// <param name="cancellationToken">Ends the waits when the test is cancelled.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PreparedTileDropsAfterGenerationAdvance(CancellationToken cancellationToken)
    {
        using var completed = new SemaphoreSlim(0);
        var factory = new PreparationSurfaceFactory();
        using var scheduler = new RenderScheduler(factory);
        using var completions = scheduler.Completed.SubscribeSafe(_ => completed.Release(), static _ => { });
        using var document = new FakeDocument(DocumentName, FakeEngine.A4);
        var client = new RenderClient();
        var key = Key(0);

        _ = scheduler.Request(Request(key, document, client, RenderPriority.Visible));
        await completed.WaitAsync(cancellationToken);
        _ = client.Advance();

        await Assert.That(scheduler.TryTakeCompleted(out _)).IsFalse();
        await Assert.That(factory.Last!.IsDisposed).IsTrue();
        await Assert.That(scheduler.IsPending(key)).IsFalse();
    }

    /// <summary>
    /// A tile requested again by a newer generation while it renders is delivered once, as when a page keeps scrolling
    /// past while its tile takes longer than a frame.
    /// </summary>
    /// <param name="cancellationToken">Ends the waits when the test is cancelled.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ActiveRenderIsKeptAfterGenerationAdvance(CancellationToken cancellationToken)
    {
        const int frames = 3;
        using var gate = new ManualResetEventSlim(false);
        using var completed = new SemaphoreSlim(0);
        using var scheduler = new RenderScheduler(new FakeSurfaceFactory());
        using var completions = scheduler.Completed.SubscribeSafe(_ => completed.Release(), static _ => { });
        using var document = new FakeDocument(DocumentName, FakeEngine.A4) { Gate = gate };
        var client = new RenderClient();
        var key = Key(0);

        _ = scheduler.Request(Request(key, document, client, RenderPriority.Visible));
        await document.FirstRenderStarted.WaitAsync(cancellationToken);
        var requeued = false;
        for (var frame = 0; frame < frames; frame++)
        {
            var generation = client.Advance();
            requeued |= scheduler.Request(Request(key, document, client, RenderPriority.Visible) with { Generation = generation });
        }

        gate.Set();
        var taken = 0;
        while (scheduler.IsPending(key))
        {
            await completed.WaitAsync(cancellationToken);
            while (scheduler.TryTakeCompleted(out var tile))
            {
                taken++;
                tile.Surface.Dispose();
            }
        }

        await Assert.That(requeued).IsFalse();
        await Assert.That(document.RenderCount).IsEqualTo(1);
        await Assert.That(taken).IsEqualTo(1);
    }

    /// <summary>A tile whose client moved on while it rendered, without asking for it again, is discarded.</summary>
    /// <param name="cancellationToken">Ends the waits when the test is cancelled.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ActiveRenderDropsWhenNoLongerRequested(CancellationToken cancellationToken)
    {
        using var gate = new ManualResetEventSlim(false);
        using var completed = new SemaphoreSlim(0);
        using var scheduler = new RenderScheduler(new FakeSurfaceFactory());
        using var completions = scheduler.Completed.SubscribeSafe(_ => completed.Release(), static _ => { });
        using var document = new FakeDocument(DocumentName, FakeEngine.A4) { Gate = gate };
        var client = new RenderClient();
        var key = Key(0);

        _ = scheduler.Request(Request(key, document, client, RenderPriority.Visible));
        await document.FirstRenderStarted.WaitAsync(cancellationToken);
        _ = client.Advance();
        gate.Set();

        // The finished tile is signalled like any other and discarded when the client collects it.
        var taken = 0;
        while (scheduler.IsPending(key))
        {
            await completed.WaitAsync(cancellationToken);
            while (scheduler.TryTakeCompleted(out var tile))
            {
                taken++;
                tile.Surface.Dispose();
            }
        }

        await Assert.That(taken).IsEqualTo(0);
        await Assert.That(document.RenderCount).IsEqualTo(1);
    }

    /// <summary>A tile requested for a reopened document while the old one renders is rendered again from the new one.</summary>
    /// <param name="cancellationToken">Ends the waits when the test is cancelled.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ActiveRenderIsReplacedForANewDocument(CancellationToken cancellationToken)
    {
        using var gate = new ManualResetEventSlim(false);
        using var completed = new SemaphoreSlim(0);
        using var scheduler = new RenderScheduler(new FakeSurfaceFactory());
        using var completions = scheduler.Completed.SubscribeSafe(_ => completed.Release(), static _ => { });
        using var closed = new FakeDocument(DocumentName, FakeEngine.A4) { Gate = gate };
        using var reopened = new FakeDocument(DocumentName, FakeEngine.A4);
        var client = new RenderClient();
        var key = Key(0);

        _ = scheduler.Request(Request(key, closed, client, RenderPriority.Visible));
        await closed.FirstRenderStarted.WaitAsync(cancellationToken);
        var requeued = scheduler.Request(Request(key, reopened, client, RenderPriority.Visible));
        gate.Set();

        var taken = 0;
        while (scheduler.IsPending(key))
        {
            await completed.WaitAsync(cancellationToken);
            while (scheduler.TryTakeCompleted(out var tile))
            {
                taken++;
                tile.Surface.Dispose();
            }
        }

        await Assert.That(requeued).IsTrue();
        await Assert.That(reopened.RenderCount).IsEqualTo(1);
        await Assert.That(taken).IsEqualTo(1);
    }

    /// <summary>Closing a viewer does not wait for an active render to release the UI thread.</summary>
    /// <param name="cancellationToken">Ends the waits when the test is cancelled.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DisposeReturnsWhileRenderIsActive(CancellationToken cancellationToken)
    {
        using var gate = new ManualResetEventSlim(false);
        var scheduler = new RenderScheduler(new FakeSurfaceFactory());
        using var document = new FakeDocument(DocumentName, FakeEngine.A4) { Gate = gate };
        var key = Key(0);
        int rendersWhenDisposeReturned;
        try
        {
            _ = scheduler.Request(Request(key, document, new(), RenderPriority.Visible));
            await document.FirstRenderStarted.WaitAsync(cancellationToken);
        }
        finally
        {
            // The render is held at the gate, so a Dispose that returns did not wait for it to finish.
            scheduler.Dispose();
            rendersWhenDisposeReturned = document.RenderCount;
            gate.Set();
        }

        await document.FirstRenderFinished.WaitAsync(cancellationToken);

        await Assert.That(rendersWhenDisposeReturned).IsEqualTo(0);
        await Assert.That(document.RenderCount).IsEqualTo(1);
    }

    /// <summary>Disposal cancels queued page preparation before any tile surface is created.</summary>
    /// <param name="cancellationToken">Ends the waits when the test is cancelled.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DisposeCancelsAwaitingPreparation(CancellationToken cancellationToken)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var factory = new PreparationSurfaceFactory();
        var scheduler = new RenderScheduler(factory);
        using var document = new FakeDocument(DocumentName, FakeEngine.A4);
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
            await entered.Task.WaitAsync(cancellationToken);
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
