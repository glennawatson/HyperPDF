// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Rendering;
using HyperPdf.Core.Tests.Fakes;

namespace HyperPdf.Core.Tests.Rendering;

/// <summary>Tests requests made while a <see cref="RenderScheduler"/> is disposed.</summary>
public sealed class RenderSchedulerDisposeRaceTests
{
    /// <summary>The edge of test tiles.</summary>
    private const int Edge = 8;

    /// <summary>The schedulers made and disposed while requests arrive.</summary>
    private const int Rounds = 100;

    /// <summary>The threads that request tiles.</summary>
    private const int Requesters = 3;

    /// <summary>The page requested.</summary>
    private const int Page = 0;

    /// <summary>The file name of the test document.</summary>
    private const string DocumentName = "race.pdf";

    /// <summary>A request that races with the dispose is dropped, and never fails with an error from the scheduler's own wake-up signal.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task RequestRacingWithDisposeIsDroppedCleanly(CancellationToken cancellationToken)
    {
        var failures = new ConcurrentQueue<Exception>();
        for (var round = 0; round < Rounds; round++)
        {
            var scheduler = new RenderScheduler(new FakeSurfaceFactory());
            using var document = new FakeDocument(DocumentName, FakeEngine.A4);
            using var stop = new CancellationTokenSource();
            var job = new RequestJob(scheduler, document, failures, stop.Token);
            var tasks = new List<Task>();

            // The requesters spin until stopped, so they run on their own threads and never starve the thread pool.
            for (var requester = 0; requester < Requesters; requester++)
            {
                tasks.Add(DedicatedThread.Run(job, RequestUntilStopped, cancellationToken));
            }

            await Task.Delay(TimeSpan.FromMilliseconds(1), cancellationToken);
            scheduler.Dispose();
            await stop.CancelAsync();
            await Task.WhenAll(tasks);
        }

        await Assert.That(failures.Count).IsEqualTo(0);
    }

    /// <summary>Requests tiles until stopped. A request after the dispose is refused with the scheduler's own error; anything else is recorded.</summary>
    /// <param name="job">The scheduler, the document, and what stops and reports the loop.</param>
    private static void RequestUntilStopped(RequestJob job)
    {
        var client = new RenderClient();
        try
        {
            for (var tile = 0; !job.Stop.IsCancellationRequested; tile++)
            {
                var key = new TileKey(1, Page, 1, PageRotation.None, tile, 0, 0);
                var info = new PageRenderInfo(Page, 1, PageRotation.None, 0, 0, RenderFlags.None);
                _ = job.Scheduler.Request(new(key, job.Document, info, Edge, Edge, RenderPriority.Visible, client, client.Generation, PageTone.None));
            }
        }
        catch (ObjectDisposedException ex) when (ex.ObjectName == typeof(RenderScheduler).FullName)
        {
            // The dispose came before the request, which the scheduler documents.
        }
        catch (Exception ex)
        {
            job.Failures.Enqueue(ex);
        }
    }

    /// <summary>What one requesting thread reads and reports to.</summary>
    /// <param name="Scheduler">The scheduler.</param>
    /// <param name="Document">The document.</param>
    /// <param name="Failures">Receives the unexpected exceptions.</param>
    /// <param name="Stop">Stops the loop.</param>
    private sealed record RequestJob(RenderScheduler Scheduler, IDocument Document, ConcurrentQueue<Exception> Failures, CancellationToken Stop);
}
