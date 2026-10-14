// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests;

/// <summary>Runs test work on its own thread, so tests that make threads meet at a barrier never wait for the thread pool.</summary>
internal static class DedicatedThread
{
    /// <summary>Runs work that returns nothing.</summary>
    /// <typeparam name="TState">The type of the arguments of the work.</typeparam>
    /// <param name="state">The arguments of the work.</param>
    /// <param name="work">The work.</param>
    /// <param name="cancellationToken">Cancels the work before it starts.</param>
    /// <returns>A task that completes when the work does.</returns>
    internal static Task Run<TState>(TState state, Action<TState> work, CancellationToken cancellationToken) =>
        Task.Factory.StartNew(
            static boxed =>
            {
                LowerPriority();
                var job = (Job<TState>)boxed!;
                job.Work(job.State);
            },
            new Job<TState>(state, work),
            cancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

    /// <summary>Runs work that returns a value.</summary>
    /// <typeparam name="TState">The type of the arguments of the work.</typeparam>
    /// <typeparam name="TResult">The type of the value.</typeparam>
    /// <param name="state">The arguments of the work.</param>
    /// <param name="work">The work.</param>
    /// <param name="cancellationToken">Cancels the work before it starts.</param>
    /// <returns>A task with the value.</returns>
    internal static Task<TResult> Run<TState, TResult>(TState state, Func<TState, TResult> work, CancellationToken cancellationToken) =>
        Task.Factory.StartNew(
            static boxed =>
            {
                LowerPriority();
                var job = (Job<TState, TResult>)boxed!;
                return job.Work(job.State);
            },
            new Job<TState, TResult>(state, work),
            cancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

    /// <summary>Runs asynchronous work, starting it on its own thread.</summary>
    /// <typeparam name="TState">The type of the arguments of the work.</typeparam>
    /// <typeparam name="TResult">The type of the value.</typeparam>
    /// <param name="state">The arguments of the work.</param>
    /// <param name="work">The work.</param>
    /// <param name="cancellationToken">Cancels the work before it starts.</param>
    /// <returns>A task with the value.</returns>
    internal static Task<TResult> RunAsync<TState, TResult>(TState state, Func<TState, Task<TResult>> work, CancellationToken cancellationToken) =>
        Run(state, work, cancellationToken).Unwrap();

    /// <summary>
    /// Lowers the priority of the calling dedicated thread. These threads often spin until the test body stops them, and on
    /// Windows a runner with few cores lets many equal-priority spinners starve the test body and every other test.
    /// </summary>
    private static void LowerPriority() => Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;

    /// <summary>The work and the arguments it reads.</summary>
    /// <typeparam name="TState">The type of the arguments.</typeparam>
    /// <param name="State">The arguments.</param>
    /// <param name="Work">The work.</param>
    private sealed record Job<TState>(TState State, Action<TState> Work);

    /// <summary>The work and the arguments it reads.</summary>
    /// <typeparam name="TState">The type of the arguments.</typeparam>
    /// <typeparam name="TResult">The type of the value.</typeparam>
    /// <param name="State">The arguments.</param>
    /// <param name="Work">The work.</param>
    private sealed record Job<TState, TResult>(TState State, Func<TState, TResult> Work);
}
