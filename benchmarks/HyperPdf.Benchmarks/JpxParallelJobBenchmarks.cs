// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Graphics.Images.Jpx;

namespace HyperPdf.Benchmarks;

/// <summary>Measures serial JPX dispatch and a dispatch that must join an active helper.</summary>
public class JpxParallelJobBenchmarks
{
    /// <summary>The work shared by the caller and one helper.</summary>
    private readonly JoinJob _job = new();

    /// <summary>Gets or sets the duration of work performed by the helper.</summary>
    [Params(100, 10_000)]
    public int WorkMicroseconds { get; set; }

    /// <summary>Runs the loop on the caller without a helper.</summary>
    [Benchmark]
    public void Serial() => _job.Run(false, WorkMicroseconds);

    /// <summary>Runs work on a helper that remains active after the caller finishes its share.</summary>
    [Benchmark]
    public void Contended() => _job.Run(true, WorkMicroseconds);

    /// <summary>Holds a helper in CPU work so dispatch must join it.</summary>
    private sealed class JoinJob : JpxParallelJob
    {
        /// <summary>The two items let dispatch queue one helper.</summary>
        private const int Items = 2;

        /// <summary>The maximum wait for a queued helper to start.</summary>
        private static readonly TimeSpan QueueTimeout = TimeSpan.FromSeconds(10);

        /// <summary>The helper's work duration.</summary>
        private int _workMicroseconds;

        /// <summary>The caller's thread identifier.</summary>
        private int _owner;

        /// <summary>Set once the helper enters the loop.</summary>
        private int _entered;

        /// <summary>Set when the caller has finished claiming work.</summary>
        private int _callerFinished;

        /// <summary>Dispatches one loop.</summary>
        /// <param name="parallel">Whether a helper joins.</param>
        /// <param name="workMicroseconds">The helper's work duration.</param>
        internal void Run(bool parallel, int workMicroseconds)
        {
            _owner = Environment.CurrentManagedThreadId;
            _workMicroseconds = workMicroseconds;
            Volatile.Write(ref _entered, 0);
            Volatile.Write(ref _callerFinished, 0);
            Dispatch(Items, parallel);
        }

        /// <inheritdoc/>
        protected override void QueueHelper()
        {
            base.QueueHelper();
            var start = Stopwatch.GetTimestamp();
            SpinWait spinner = default;
            while (Volatile.Read(ref _entered) == 0)
            {
                if (Stopwatch.GetElapsedTime(start) > QueueTimeout)
                {
                    Volatile.Write(ref _callerFinished, 1);
                    throw new TimeoutException("The queued JPX helper did not start.");
                }

                spinner.SpinOnce();
            }
        }

        /// <inheritdoc/>
        protected override void RunWorker()
        {
            if (Environment.CurrentManagedThreadId != _owner)
            {
                Volatile.Write(ref _entered, 1);
                SpinWait spinner = default;
                while (Volatile.Read(ref _callerFinished) == 0)
                {
                    spinner.SpinOnce();
                }

                var start = Stopwatch.GetTimestamp();
                while (Stopwatch.GetElapsedTime(start).TotalMicroseconds < _workMicroseconds)
                {
                    Thread.SpinWait(1);
                }
            }

            while (TryClaim(out _))
            {
                // Claiming exhausts the loop; the helper's CPU work above is the measured join workload.
            }

            if (Environment.CurrentManagedThreadId == _owner)
            {
                Volatile.Write(ref _callerFinished, 1);
            }
        }
    }
}
