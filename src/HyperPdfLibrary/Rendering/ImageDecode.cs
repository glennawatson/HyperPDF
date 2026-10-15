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
    /// <summary>Completed when the decode has finished; published by the first async waiter.</summary>
    private TaskCompletionSource? _completion;

    /// <summary>Whether the decode has finished, whether it produced an image, failed or threw.</summary>
    private int _finished;

    /// <summary>Gets the managed id of the thread running the decode.</summary>
    internal int Owner { get; } = Environment.CurrentManagedThreadId;

    /// <summary>Gets a value indicating whether the calling thread is the one running the decode, so waiting would wait on itself.</summary>
    internal bool IsOwnedByCurrentThread => Owner == Environment.CurrentManagedThreadId;

    /// <summary>Marks the decode as finished and wakes every waiter. Calling it again does nothing.</summary>
    internal void Complete()
    {
        _ = Interlocked.Exchange(ref _finished, 1);
        _ = Volatile.Read(ref _completion)?.TrySetResult();
    }

    /// <summary>Blocks until the decode has finished. The thread is a render thread with nothing else to do.</summary>
    /// <param name="cancellationToken">Stops this wait only.</param>
    /// <exception cref="OperationCanceledException">The token was cancelled while waiting.</exception>
    internal void Wait(CancellationToken cancellationToken)
    {
        SpinWait spinner = default;
        while (Volatile.Read(ref _finished) == 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            spinner.SpinOnce();
        }
    }

    /// <summary>Waits without holding a thread until the decode has finished.</summary>
    /// <param name="cancellationToken">Stops this wait only.</param>
    /// <returns>A task that completes when the decode has finished.</returns>
    internal Task WaitAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _finished) != 0)
        {
            return Task.CompletedTask;
        }

        var completion = Volatile.Read(ref _completion);
        if (completion is null)
        {
            var created = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            completion = Interlocked.CompareExchange(ref _completion, created, null) ?? created;
        }

        // Completion can precede publication; either this check or Complete observes the other side's publication.
        if (Volatile.Read(ref _finished) != 0)
        {
            _ = completion.TrySetResult();
        }

        return completion.Task.WaitAsync(cancellationToken);
    }
}
