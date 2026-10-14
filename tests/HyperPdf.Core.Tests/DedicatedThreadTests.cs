// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Tests;

/// <summary>Tests the helper that keeps spinning test work off the thread pool.</summary>
public sealed class DedicatedThreadTests
{
    /// <summary>The jobs that wait for each other, more than any thread pool starts at once.</summary>
    private const int Jobs = 32;

    /// <summary>Spinning work on pool threads starves every other test, so the helper must not use them.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task RunsOnAThreadOutsideThePool(CancellationToken cancellationToken)
    {
        var onPool = await DedicatedThread.Run(0, static _ => Thread.CurrentThread.IsThreadPoolThread, cancellationToken);

        await Assert.That(onPool).IsFalse();
    }

    /// <summary>Jobs that wait for each other all get a thread at once, however few threads the pool starts with.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task JobsThatWaitForEachOtherAllStart(CancellationToken cancellationToken)
    {
        var meeting = new Meeting();
        var tasks = new Task<bool>[Jobs];
        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = DedicatedThread.Run(new Arrival(meeting, cancellationToken), static arrival => arrival.Meeting.ArriveAndWait(arrival.CancellationToken), CancellationToken.None);
        }

        var met = await Task.WhenAll(tasks);

        await Assert.That(Array.TrueForAll(met, static met => met)).IsTrue();
    }

    /// <summary>A point where every job must arrive before any leaves.</summary>
    private sealed class Meeting
    {
        /// <summary>The jobs that have arrived.</summary>
        private int _arrived;

        /// <summary>
        /// Arrives and waits until every job has arrived. There is no wall-clock budget, because a loaded runner can take
        /// seconds to start a thread; the test's own timeout ends a job that never gets a thread.
        /// </summary>
        /// <param name="cancellationToken">Cancelled when the test times out.</param>
        /// <returns><see langword="true"/> when all of them arrived before the token was cancelled.</returns>
        internal bool ArriveAndWait(CancellationToken cancellationToken)
        {
            _ = Interlocked.Increment(ref _arrived);
            var wait = default(SpinWait);
            while (Volatile.Read(ref _arrived) < Jobs)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return false;
                }

                wait.SpinOnce();
            }

            return true;
        }
    }

    /// <summary>A job's meeting and the token that stops it waiting.</summary>
    /// <param name="Meeting">The meeting.</param>
    /// <param name="CancellationToken">Cancelled when the test times out.</param>
    private sealed record Arrival(Meeting Meeting, CancellationToken CancellationToken);
}
