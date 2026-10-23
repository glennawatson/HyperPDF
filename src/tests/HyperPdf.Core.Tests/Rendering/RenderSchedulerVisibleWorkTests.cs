// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Rendering;
using HyperPdf.Core.Tests.Fakes;
using ReactiveUI.Primitives;

namespace HyperPdf.Core.Tests.Rendering;

/// <summary>Checks how the scheduler tells background preparation that something on screen needs drawing.</summary>
public sealed class RenderSchedulerVisibleWorkTests
{
    /// <summary>The side of a test tile.</summary>
    private const int Edge = 8;

    /// <summary>How many surfaces the roomy budget holds.</summary>
    private const int Roomy = 4;

    /// <summary>A visible request raises the signal and shows as visible work until it is taken.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task VisibleRequestRaisesTheSignal()
    {
        using var gate = new ManualResetEventSlim(false);
        using var scheduler = new RenderScheduler(new FakeSurfaceFactory());
        var raised = 0;
        using var subscription = scheduler.VisibleWorkRequested.SubscribeSafe(_ => Interlocked.Increment(ref raised), static _ => { });
        var document = new FakeDocument("visible.pdf", FakeEngine.A4) { Gate = gate };
        var client = new RenderClient();

        _ = scheduler.Request(Request(0, document, client, RenderPriority.Visible));

        await Assert.That(Volatile.Read(ref raised)).IsEqualTo(1);
        await Assert.That(scheduler.HasVisibleWork).IsTrue();
        gate.Set();
    }

    /// <summary>Prefetch requests are not visible work and raise nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PrefetchIsNotVisibleWork()
    {
        using var gate = new ManualResetEventSlim(false);
        using var scheduler = new RenderScheduler(new FakeSurfaceFactory());
        var raised = 0;
        using var subscription = scheduler.VisibleWorkRequested.SubscribeSafe(_ => Interlocked.Increment(ref raised), static _ => { });
        var document = new FakeDocument("prefetch.pdf", FakeEngine.A4) { Gate = gate };
        var client = new RenderClient();

        _ = scheduler.Request(Request(0, document, client, RenderPriority.Prefetch));

        await Assert.That(Volatile.Read(ref raised)).IsEqualTo(0);
        await Assert.That(scheduler.HasVisibleWork).IsFalse();
        gate.Set();
    }

    /// <summary>A scheduler with nothing queued has no visible work.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IdleSchedulerHasNoVisibleWork()
    {
        using var scheduler = new RenderScheduler(new FakeSurfaceFactory());

        await Assert.That(scheduler.HasVisibleWork).IsFalse();
        await Assert.That(scheduler.IsIdle).IsTrue();
    }

    /// <summary>The cache counts as full at nine tenths of its budget.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CacheFullnessFollowsTheBudget()
    {
        var surface = new FakeSurfaceFactory().Create(Edge, Edge);
        using var cache = new TileCache(surface.ByteSize * Roomy);

        await Assert.That(MemoryPressure.IsCacheFull(cache)).IsFalse();
        cache.Add(new(1, 0, 1, PageRotation.None, 0, 0, 0), surface);
        await Assert.That(MemoryPressure.IsCacheFull(cache)).IsFalse();
        cache.BudgetBytes = surface.ByteSize;
        await Assert.That(MemoryPressure.IsCacheFull(cache)).IsTrue();
    }

    /// <summary>Creates a request for a page's tile.</summary>
    /// <param name="page">The page.</param>
    /// <param name="document">The document.</param>
    /// <param name="client">The client.</param>
    /// <param name="priority">The priority.</param>
    /// <returns>The request.</returns>
    private static RenderRequest Request(int page, IDocument document, RenderClient client, RenderPriority priority)
    {
        var key = new TileKey(1, page, 1, PageRotation.None, 0, 0, 0);
        return new(key, document, new(page, 1, PageRotation.None, 0, 0, RenderFlags.None), Edge, Edge, priority, client, client.Generation, PageTone.None);
    }
}
