// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// A loop of independent items that the calling thread shares with idle thread-pool threads. The job object is the work
/// item it queues, so running the loop allocates nothing: the items are claimed from one counter, the caller does its
/// share of the work before it waits, and a helper that starts late finds the loop closed and returns. One job belongs to
/// one owner, which runs one loop at a time.
/// </summary>
internal abstract class JpxParallelJob : IThreadPoolWorkItem
{
    /// <summary>The bit of <see cref="_state"/> that is set while the loop accepts helpers; the bits below it count the workers inside <see cref="RunWorker"/>.</summary>
    private const int OpenBit = 1 << 30;

    /// <summary>The next item to claim.</summary>
    private int _next;

    /// <summary>The number of items in the loop; zero once a worker has failed so the others stop claiming.</summary>
    private int _count;

    /// <summary>The open bit and the worker count, changed only with interlocked operations, so helpers that start together never contend for a lock.</summary>
    private int _state;

    /// <summary>The first exception a worker threw.</summary>
    private Exception? _failure;

    /// <summary>Runs the loop's items on the calling thread and on as many idle pool threads as there are spare items and processors.</summary>
    /// <param name="count">The number of items.</param>
    /// <param name="parallel">Whether helpers may join; <see langword="false"/> runs every item on the calling thread.</param>
    internal void Dispatch(int count, bool parallel)
    {
        Volatile.Write(ref _next, 0);
        Volatile.Write(ref _count, count);
        _ = Interlocked.Exchange(ref _failure, null);
        _ = Interlocked.Or(ref _state, OpenBit);

        try
        {
            var helpers = parallel ? Math.Min(count, Environment.ProcessorCount) - 1 : 0;
            for (var i = 0; i < helpers; i++)
            {
                QueueHelper();
            }

            Join();
        }
        finally
        {
            // Workers still inside the loop wake the caller when the last one leaves; none can join once the bit is clear.
            // This also runs when queuing a helper throws, so the caller never returns its buffers while a helper still writes.
            if ((Interlocked.And(ref _state, ~OpenBit) & ~OpenBit) != 0)
            {
                WaitForWorkers();
            }
        }

        if (Interlocked.Exchange(ref _failure, null) is { } failure)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IThreadPoolWorkItem.Execute() => Join();

    /// <summary>Queues one helper on the thread pool; the helper joins the open loop when it starts.</summary>
    protected virtual void QueueHelper() => _ = ThreadPool.UnsafeQueueUserWorkItem(this, preferLocal: true);

    /// <summary>Runs one worker's share of the items: it claims items with <see cref="TryClaim"/> until none are left.</summary>
    protected abstract void RunWorker();

    /// <summary>Claims the next item of the loop.</summary>
    /// <param name="index">Receives the item.</param>
    /// <returns><see langword="false"/> when every item is claimed or a worker has failed.</returns>
    protected bool TryClaim(out int index)
    {
        index = Interlocked.Increment(ref _next) - 1;
        return index < Volatile.Read(ref _count);
    }

    /// <summary>Takes part in the open loop, if there is one, and records a failure.</summary>
    private void Join()
    {
        var state = Volatile.Read(ref _state);
        var joined = false;
        while (!joined && (state & OpenBit) != 0)
        {
            var seen = Interlocked.CompareExchange(ref _state, state + 1, state);
            joined = seen == state;
            state = seen;
        }

        if (!joined)
        {
            return;
        }

        try
        {
            RunWorker();
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
        finally
        {
            _ = Interlocked.Decrement(ref _state);
        }
    }

    /// <summary>Waits until the loop is closed and no worker is inside it.</summary>
    private void WaitForWorkers()
    {
        SpinWait spinner = default;
        while (Volatile.Read(ref _state) != 0)
        {
            spinner.SpinOnce();
        }
    }

    /// <summary>Keeps the first failure and stops the other workers from claiming items.</summary>
    /// <param name="failure">The exception.</param>
    private void Fail(Exception failure)
    {
        _ = Interlocked.Exchange(ref _count, 0);
        _ = Interlocked.CompareExchange(ref _failure, failure, null);
    }
}
