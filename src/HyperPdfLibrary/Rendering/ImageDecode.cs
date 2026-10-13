// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// One image decode in progress. The thread that runs the decode completes it; every other thread that needs the same image
/// waits on it, then looks the result up in the <see cref="ImageCache"/>. A waiter that stops waiting leaves the decode alone.
/// </summary>
[DebuggerDisplay("ImageDecode: finished {_finished}")]
internal sealed class ImageDecode
{
    /// <summary>Guards <see cref="_finished"/> and is the monitor that blocked waiters sleep on.</summary>
    private readonly object _sync = new();

    /// <summary>Completed when the decode has finished; created by the first async waiter, under <see cref="_sync"/>.</summary>
    private TaskCompletionSource? _completion;

    /// <summary>Whether the decode has finished, whether it produced an image, failed or threw.</summary>
    private bool _finished;

    /// <summary>The threads asleep in <see cref="Wait"/>; changed only under the lock.</summary>
    private int _sleepers;

    /// <summary>Gets the managed id of the thread running the decode.</summary>
    internal int Owner { get; } = Environment.CurrentManagedThreadId;

    /// <summary>Gets a value indicating whether the calling thread is the one running the decode, so waiting would wait on itself.</summary>
    internal bool IsOwnedByCurrentThread => Owner == Environment.CurrentManagedThreadId;

    /// <summary>Marks the decode as finished and wakes every waiter. Calling it again does nothing.</summary>
    internal void Complete()
    {
        TaskCompletionSource? completion;
        lock (_sync)
        {
            _finished = true;
            completion = _completion;
            WakeLocked();
        }

        _ = completion?.TrySetResult();
    }

    /// <summary>Blocks until the decode has finished. The thread is a render thread with nothing else to do.</summary>
    /// <param name="cancellationToken">Stops this wait only.</param>
    /// <exception cref="OperationCanceledException">The token was cancelled while waiting.</exception>
    internal void Wait(CancellationToken cancellationToken)
    {
        // A cancelled token wakes every sleeper to check its own token; the others go back to sleep.
        using var wake = cancellationToken.CanBeCanceled
            ? cancellationToken.UnsafeRegister(static state => ((ImageDecode)state!).WakeAll(), this)
            : default;
        lock (_sync)
        {
            _sleepers++;
            try
            {
                while (!_finished)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _ = Monitor.Wait(_sync);
                }
            }
            finally
            {
                _sleepers--;
            }
        }
    }

    /// <summary>Waits without holding a thread until the decode has finished.</summary>
    /// <param name="cancellationToken">Stops this wait only.</param>
    /// <returns>A task that completes when the decode has finished.</returns>
    internal Task WaitAsync(CancellationToken cancellationToken)
    {
        Task pending;
        lock (_sync)
        {
            if (_finished)
            {
                return Task.CompletedTask;
            }

            _completion ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
            pending = _completion.Task;
        }

        return pending.WaitAsync(cancellationToken);
    }

    /// <summary>Wakes every thread blocked in <see cref="Wait"/>.</summary>
    private void WakeAll()
    {
        lock (_sync)
        {
            WakeLocked();
        }
    }

    /// <summary>Wakes the sleeping threads, if any. The caller holds the lock; with none asleep there is nothing to pulse, which saves the runtime making its wait queue.</summary>
    private void WakeLocked()
    {
        if (_sleepers > 0)
        {
            Monitor.PulseAll(_sync);
        }
    }
}
