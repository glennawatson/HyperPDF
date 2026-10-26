// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Rendering;
using HyperPdf.Core.Tests.Fakes;
using ReactiveUI.Primitives;

namespace HyperPdf.Core.Tests.Rendering;

/// <summary>Checks queue order, visible-work counting and signalling of the scheduler.</summary>
public sealed class RenderSchedulerQueueOrderTests
{
    /// <summary>The edge of test tiles.</summary>
    private const int Edge = 8;

    /// <summary>The document identifier of the document used to occupy the render thread.</summary>
    private const int BlockerDocument = 99;

    /// <summary>The last of three pages.</summary>
    private const int LastPage = 2;

    /// <summary>A second document identifier for the invalidation test.</summary>
    private const int OtherDocument = 2;

    /// <summary>The number of signals two requests raise.</summary>
    private const int TwoSignals = 2;

    /// <summary>The test document file name.</summary>
    private const string DocumentName = "a.pdf";

    /// <summary>How many tiles the batch tests request.</summary>
    private const int BatchSize = 5;

    /// <summary>A tile requested at a lower priority renders after tiles requested later at a higher one.</summary>
    /// <param name="cancellationToken">Ends the waits when the test is cancelled.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task DemotedTileRendersAfterNewVisibleTiles(CancellationToken cancellationToken)
    {
        using var gate = new ManualResetEventSlim(false);
        using var completed = new SemaphoreSlim(0);
        using var scheduler = new RenderScheduler(new FakeSurfaceFactory());
        using var completions = scheduler.Completed.SubscribeSafe(_ => completed.Release(), static _ => { });
        var blocker = new FakeDocument("blocker.pdf", FakeEngine.A4) { Gate = gate };
        var document = new FakeDocument(DocumentName, FakeEngine.A4, FakeEngine.A4, FakeEngine.A4);
        var client = new RenderClient();
        var blockerKey = new TileKey(BlockerDocument, 0, 1, PageRotation.None, 0, 0, 0);

        _ = scheduler.Request(Request(blockerKey, blocker, client, RenderPriority.Visible));
        blocker.Started.Wait(cancellationToken);
        _ = scheduler.Request(Request(Key(0), document, client, RenderPriority.Visible));
        _ = scheduler.Request(Request(Key(0), document, client, RenderPriority.Prefetch));
        _ = scheduler.Request(Request(Key(1), document, client, RenderPriority.Visible));
        _ = scheduler.Request(Request(Key(LastPage), document, client, RenderPriority.Visible));
        gate.Set();

        var order = await CompletedTiles.CollectAsync(scheduler, completed, BatchSize - 1, cancellationToken);

        await Assert.That(order).IsEquivalentTo([blockerKey, Key(1), Key(LastPage), Key(0)]);
        await Assert.That(order[3]).IsEqualTo(Key(0));
    }

    /// <summary>Visible work follows requests through demotion, removal and completion.</summary>
    /// <param name="cancellationToken">Ends the waits when the test is cancelled.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task VisibleWorkFollowsPendingRequests(CancellationToken cancellationToken)
    {
        using var gate = new ManualResetEventSlim(false);
        using var completed = new SemaphoreSlim(0);
        using var scheduler = new RenderScheduler(new FakeSurfaceFactory());
        using var completions = scheduler.Completed.SubscribeSafe(_ => completed.Release(), static _ => { });
        var document = new FakeDocument(DocumentName, FakeEngine.A4) { Gate = gate };
        var client = new RenderClient();

        _ = scheduler.Request(Request(Key(0), document, client, RenderPriority.Visible));
        await Assert.That(scheduler.HasVisibleWork).IsTrue();
        _ = scheduler.Request(Request(Key(0), document, client, RenderPriority.Prefetch));
        await Assert.That(scheduler.HasVisibleWork).IsFalse();
        _ = scheduler.Request(Request(Key(0), document, client, RenderPriority.Visible));
        await Assert.That(scheduler.HasVisibleWork).IsTrue();
        gate.Set();

        _ = await CompletedTiles.CollectAsync(scheduler, completed, 1, cancellationToken);

        await Assert.That(scheduler.HasVisibleWork).IsFalse();
        await Assert.That(scheduler.IsIdle).IsTrue();
    }

    /// <summary>A batch of visible requests raises the visible-work signal once and queues every tile.</summary>
    /// <param name="cancellationToken">Ends the waits when the test is cancelled.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task BatchRequestSignalsOnceAndQueuesEveryTile(CancellationToken cancellationToken)
    {
        using var gate = new ManualResetEventSlim(false);
        using var completed = new SemaphoreSlim(0);
        using var scheduler = new RenderScheduler(new FakeSurfaceFactory());
        using var completions = scheduler.Completed.SubscribeSafe(_ => completed.Release(), static _ => { });
        var raised = 0;
        using var signals = scheduler.VisibleWorkRequested.SubscribeSafe(_ => Interlocked.Increment(ref raised), static _ => { });
        var document = new FakeDocument(DocumentName, FakeEngine.A4, FakeEngine.A4, FakeEngine.A4, FakeEngine.A4, FakeEngine.A4) { Gate = gate };
        var client = new RenderClient();
        var batch = new RenderRequest[BatchSize];
        for (var page = 0; page < BatchSize; page++)
        {
            batch[page] = Request(Key(page), document, client, RenderPriority.Visible);
        }

        var queued = scheduler.RequestBatch(batch);
        var merged = scheduler.RequestBatch(batch);
        gate.Set();
        var order = await CompletedTiles.CollectAsync(scheduler, completed, BatchSize, cancellationToken);

        await Assert.That(queued).IsEqualTo(BatchSize);
        await Assert.That(merged).IsEqualTo(0);
        await Assert.That(Volatile.Read(ref raised)).IsEqualTo(TwoSignals);
        await Assert.That(order.Count).IsEqualTo(BatchSize);
    }

    /// <summary>A request whose token is already cancelled signals nothing and queues nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledRequestRaisesNoSignal()
    {
        using var scheduler = new RenderScheduler(new FakeSurfaceFactory());
        var raised = 0;
        using var signals = scheduler.VisibleWorkRequested.SubscribeSafe(_ => Interlocked.Increment(ref raised), static _ => { });
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var document = new FakeDocument(DocumentName, FakeEngine.A4);
        var request = Request(Key(0), document, new(), RenderPriority.Visible) with { CancellationToken = cancelled.Token };

        var queued = scheduler.Request(request);
        var batched = scheduler.RequestBatch([request]);

        await Assert.That(queued).IsFalse();
        await Assert.That(batched).IsEqualTo(0);
        await Assert.That(Volatile.Read(ref raised)).IsEqualTo(0);
        await Assert.That(scheduler.IsIdle).IsTrue();
    }

    /// <summary>A repeated identical request merges and still raises the visible-work signal.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RepeatedRequestMergesAndStillSignals()
    {
        using var gate = new ManualResetEventSlim(false);
        using var scheduler = new RenderScheduler(new FakeSurfaceFactory());
        var raised = 0;
        using var signals = scheduler.VisibleWorkRequested.SubscribeSafe(_ => Interlocked.Increment(ref raised), static _ => { });
        var document = new FakeDocument(DocumentName, FakeEngine.A4) { Gate = gate };
        var client = new RenderClient();
        var request = Request(Key(0), document, client, RenderPriority.Visible);

        var first = scheduler.Request(request);
        var second = scheduler.Request(request);
        gate.Set();

        await Assert.That(first).IsTrue();
        await Assert.That(second).IsFalse();
        await Assert.That(Volatile.Read(ref raised)).IsEqualTo(TwoSignals);
    }

    /// <summary>Invalidating two documents at once discards started work of both.</summary>
    /// <param name="cancellationToken">Ends the waits when the test is cancelled.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task InvalidatingTwoDocumentsDropsBothStartedTiles(CancellationToken cancellationToken)
    {
        using var gate = new ManualResetEventSlim(false);
        using var scheduler = new RenderScheduler(new FakeSurfaceFactory());
        var first = new FakeDocument(DocumentName, FakeEngine.A4) { Gate = gate };
        var client = new RenderClient();
        var firstKey = new TileKey(1, 0, 1, PageRotation.None, 0, 0, 0);

        _ = scheduler.Request(Request(firstKey, first, client, RenderPriority.Visible));
        first.Started.Wait(cancellationToken);
        scheduler.Invalidate(1, OtherDocument);
        var pending = scheduler.IsPending(firstKey);
        gate.Set();

        await Assert.That(pending).IsFalse();
        await Assert.That(scheduler.HasVisibleWork).IsFalse();
    }

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
}
