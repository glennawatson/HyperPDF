// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Tests;

/// <summary>Tests the helper that keeps spinning test work off the thread pool.</summary>
public sealed class DedicatedThreadTests
{
    /// <summary>The jobs that wait for each other, more than any thread pool starts at once.</summary>
    private const int Jobs = 64;

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
            tasks[i] = DedicatedThread.Run(meeting, static meeting => meeting.ArriveAndWait(), cancellationToken);
        }

        var met = await Task.WhenAll(tasks);

        await Assert.That(Array.TrueForAll(met, static met => met)).IsTrue();
    }

    /// <summary>A point where every job must arrive before any leaves.</summary>
    private sealed class Meeting
    {
        /// <summary>The longest each job waits for the others.</summary>
        private static readonly TimeSpan MeetingWait = TimeSpan.FromSeconds(10);

        /// <summary>The jobs that have arrived.</summary>
        private int _arrived;

        /// <summary>Arrives and spins until every job has arrived.</summary>
        /// <returns><see langword="true"/> when all of them arrived in time.</returns>
        internal bool ArriveAndWait()
        {
            _ = Interlocked.Increment(ref _arrived);
            var started = Stopwatch.GetTimestamp();
            var wait = default(SpinWait);
            while (Volatile.Read(ref _arrived) < Jobs)
            {
                if (Stopwatch.GetElapsedTime(started) > MeetingWait)
                {
                    return false;
                }

                wait.SpinOnce();
            }

            return true;
        }
    }
}
