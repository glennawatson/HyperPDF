// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Graphics.Images.Jpx;

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>Checks the loop the decoder shares between the calling thread and the pool: every item once, failures rethrown, and reuse.</summary>
[NotInParallel]
public sealed class JpxParallelJobTests
{
    /// <summary>The items of the loops.</summary>
    private const int Items = 64;

    /// <summary>The loops run one after another on one job.</summary>
    private const int Loops = 200;

    /// <summary>The item that throws.</summary>
    private const int FailingItem = 17;

    /// <summary>The processors the loop needs to queue two helpers.</summary>
    private const int MinimumProcessors = 3;

    /// <summary>The loops of the repeated-loop check.</summary>
    private const int StressLoops = 2000;

    /// <summary>How long a test waits for a helper before giving up.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    /// <summary>How long the held helper stays inside the loop.</summary>
    private static readonly TimeSpan HoldTime = TimeSpan.FromMilliseconds(100);

    /// <summary>Every item of every loop runs exactly once, however the pool shares them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EveryItemRunsOnceInEveryLoop()
    {
        var job = new CountingJob(Items, -1);
        for (var loop = 0; loop < Loops; loop++)
        {
            job.Reset();
            job.Dispatch(Items, true);

            await Assert.That(job.AllRanOnce()).IsTrue();
        }
    }

    /// <summary>A loop that is not parallel runs on the calling thread alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ASerialLoopRunsOnTheCaller()
    {
        var job = new CountingJob(Items, -1);
        job.Dispatch(Items, false);

        await Assert.That(job.AllRanOnce()).IsTrue();
        await Assert.That(job.Threads).IsEqualTo(1);
    }

    /// <summary>An exception in an item is thrown to the caller, and the job runs the next loop.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AFailureReachesTheCallerAndTheJobIsReusable()
    {
        var job = new CountingJob(Items, FailingItem);

        await Assert.That(() => job.Dispatch(Items, true)).Throws<InvalidOperationException>();

        job.Reset();
        job.FailAt = -1;
        job.Dispatch(Items, true);

        await Assert.That(job.AllRanOnce()).IsTrue();
    }

    /// <summary>A failure to queue a helper leaves no helper inside the loop when the caller gets the exception.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AFailedQueueWaitsForHelpersAlreadyInsideTheLoop()
    {
        if (Environment.ProcessorCount < MinimumProcessors)
        {
            Skip.Test("The loop queues two helpers only with three or more processors.");
        }

        var job = new FaultyQueueJob();
        var dispatch = Task.Run(() =>
        {
            try
            {
                job.Dispatch(Items, true);
            }
            finally
            {
                job.NoteInside();
            }
        });
        await job.Entered.WaitAsync(Patience);

        // The helper holds the loop open; releasing it later shows whether the caller waited for it.
        await Task.Delay(HoldTime);
        job.Release();

        await Assert.That(async () => await dispatch).Throws<InvalidOperationException>();
        await Assert.That(job.InsideAtReturn).IsEqualTo(0);
    }

    /// <summary>Many loops on one job still run every item once and leave no worker behind.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RepeatedLoopsLeaveNoWorkerInsideAfterDispatch()
    {
        var job = new CountingJob(Items, -1);
        for (var loop = 0; loop < StressLoops; loop++)
        {
            job.Reset();
            job.Dispatch(Items, true);

            await Assert.That(job.AllRanOnce()).IsTrue();
            await Assert.That(job.Inside).IsEqualTo(0);
        }
    }

    /// <summary>A job whose second helper cannot be queued, as when the pool is out of memory.</summary>
    private sealed class FaultyQueueJob : JpxParallelJob
    {
        /// <summary>Set when a helper is inside the loop.</summary>
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Held by the first helper until the test releases it.</summary>
        private int _hold;

        /// <summary>Set when a helper is inside the loop; the failing queue call waits on it.</summary>
        private int _enteredSignal;

        /// <summary>The helpers queued so far.</summary>
        private int _queued;

        /// <summary>The workers inside the loop.</summary>
        private int _inside;

        /// <summary>The workers inside the loop when the second helper failed to queue and the caller got the exception.</summary>
        private int _insideAtReturn = -1;

        /// <summary>Gets a task completed when a helper is inside the loop.</summary>
        internal Task Entered => _entered.Task;

        /// <summary>Gets how many workers were inside the loop when the caller left it.</summary>
        internal int InsideAtReturn => Volatile.Read(ref _insideAtReturn);

        /// <summary>Notes how many workers are inside the loop now; the test calls it when <c>Dispatch</c> has returned or thrown.</summary>
        internal void NoteInside() => Volatile.Write(ref _insideAtReturn, Volatile.Read(ref _inside));

        /// <summary>Lets the held helper leave.</summary>
        internal void Release() => Volatile.Write(ref _hold, 1);

        /// <inheritdoc/>
        protected override void QueueHelper()
        {
            if (Interlocked.Increment(ref _queued) == 1)
            {
                base.QueueHelper();
                return;
            }

            WaitUntilSet(ref _enteredSignal);
            throw new InvalidOperationException("The helper could not be queued.");
        }

        /// <inheritdoc/>
        protected override void RunWorker()
        {
            _ = Interlocked.Increment(ref _inside);
            try
            {
                Volatile.Write(ref _enteredSignal, 1);
                _ = _entered.TrySetResult();
                WaitUntilSet(ref _hold);
            }
            finally
            {
                _ = Interlocked.Decrement(ref _inside);
            }
        }

        /// <summary>Waits for a worker handshake with a bounded timeout.</summary>
        /// <param name="signal">The signal another thread sets.</param>
        /// <exception cref="TimeoutException">The other thread did not signal in time.</exception>
        private static void WaitUntilSet(ref int signal)
        {
            var start = Stopwatch.GetTimestamp();
            SpinWait spinner = default;
            while (Volatile.Read(ref signal) == 0)
            {
                if (Stopwatch.GetElapsedTime(start) > Patience)
                {
                    throw new TimeoutException("The JPX test worker did not signal in time.");
                }

                spinner.SpinOnce();
            }
        }
    }

    /// <summary>A job that counts how often each item runs.</summary>
    private sealed class CountingJob : JpxParallelJob
    {
        /// <summary>How often each item ran.</summary>
        private readonly int[] _runs;

        /// <summary>The threads that ran items.</summary>
        private readonly HashSet<int> _threads = [];

        /// <summary>Guards the set of worker thread identifiers.</summary>
        private readonly Lock _threadGate = new();

        /// <summary>The workers inside the loop.</summary>
        private int _inside;

        /// <summary>Initializes a new instance of the <see cref="CountingJob"/> class.</summary>
        /// <param name="items">The items.</param>
        /// <param name="failAt">The item that throws, or -1.</param>
        internal CountingJob(int items, int failAt)
        {
            _runs = new int[items];
            FailAt = failAt;
        }

        /// <summary>Gets or sets the item that throws, or -1.</summary>
        internal int FailAt { get; set; }

        /// <summary>Gets the number of workers inside the loop now.</summary>
        internal int Inside => Volatile.Read(ref _inside);

        /// <summary>Gets the number of threads that ran items.</summary>
        internal int Threads
        {
            get
            {
                lock (_threadGate)
                {
                    return _threads.Count;
                }
            }
        }

        /// <summary>Forgets earlier runs.</summary>
        internal void Reset()
        {
            Array.Clear(_runs);
            lock (_threadGate)
            {
                _threads.Clear();
            }
        }

        /// <summary>Determines whether every item ran once.</summary>
        /// <returns><see langword="true"/> when it did.</returns>
        internal bool AllRanOnce()
        {
            foreach (var count in _runs)
            {
                if (count != 1)
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc/>
        protected override void RunWorker()
        {
            _ = Interlocked.Increment(ref _inside);
            try
            {
                while (TryClaim(out var index))
                {
                    if (index == FailAt)
                    {
                        throw new InvalidOperationException("An item failed.");
                    }

                    _ = Interlocked.Increment(ref _runs[index]);
                    lock (_threadGate)
                    {
                        _ = _threads.Add(Environment.CurrentManagedThreadId);
                    }
                }
            }
            finally
            {
                _ = Interlocked.Decrement(ref _inside);
            }
        }
    }
}
