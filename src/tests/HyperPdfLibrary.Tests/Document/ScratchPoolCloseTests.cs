// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>A scratch pool made as the open context closes is closed, whichever thread gets there first.</summary>
public sealed class ScratchPoolCloseTests
{
    /// <summary>The contexts raced.</summary>
    private const int Rounds = 500;

    /// <summary>The two threads that race: one closes, one uses the pool.</summary>
    private const int Racers = 2;

    /// <summary>A pool made after the close is closed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PoolMadeAfterTheCloseIsClosed()
    {
        var context = new PdfOpenContext(null, null, CancellationToken.None);

        context.CloseScratchPools();

        await Assert.That(context.ScratchPools.IsClosed).IsTrue();
    }

    /// <summary>When the close and the first use of the pool race, the pool ends closed.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task PoolIsClosedWhenTheCloseRacesItsFirstUse(CancellationToken cancellationToken)
    {
        var open = 0;
        for (var round = 0; round < Rounds; round++)
        {
            using var start = new Barrier(Racers);
            var race = new RaceJob(new PdfOpenContext(null, null, CancellationToken.None), start, cancellationToken);
            var closer = DedicatedThread.Run(race, static job => job.CloseAfterStart(), cancellationToken);
            var user = DedicatedThread.Run(race, static job => job.UseAfterStart(), cancellationToken);

            var pool = await user;
            await closer;
            open += pool.IsClosed ? 0 : 1;
        }

        await Assert.That(open).IsEqualTo(0);
    }

    /// <summary>The two sides of a race on an open context.</summary>
    /// <param name="Context">The context.</param>
    /// <param name="Start">The barrier both sides meet at.</param>
    /// <param name="CancellationToken">Cancels the waiting.</param>
    private sealed record RaceJob(PdfOpenContext Context, Barrier Start, CancellationToken CancellationToken)
    {
        /// <summary>Closes the context's pools once both sides are ready.</summary>
        internal void CloseAfterStart()
        {
            Start.SignalAndWait(CancellationToken);
            Context.CloseScratchPools();
        }

        /// <summary>Gets the context's pools once both sides are ready.</summary>
        /// <returns>The pools.</returns>
        internal ScratchPools UseAfterStart()
        {
            Start.SignalAndWait(CancellationToken);
            return Context.ScratchPools;
        }
    }
}
