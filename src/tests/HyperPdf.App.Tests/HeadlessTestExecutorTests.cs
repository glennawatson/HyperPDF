// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using TUnit.Core.Executors;
using TUnit.Core.Interfaces;

namespace HyperPdf.App.Tests;

/// <summary>Checks cancellation from outside the headless UI thread.</summary>
[TestExecutor<DirectExecutor>]
public sealed class HeadlessTestExecutorTests
{
    /// <summary>The maximum time allowed for dispatch coordination.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <summary>An already cancelled test never reaches the UI action.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancellationBeforeDispatchSkipsAction()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var invoked = false;
        await Assert.That(async () => await HeadlessTestExecutor.ExecuteAsync(
            () =>
            {
                invoked = true;
                return ValueTask.CompletedTask;
            },
            cancellation.Token)).Throws<OperationCanceledException>();
        await Assert.That(invoked).IsFalse();
    }

    /// <summary>Cancellation ends the executor's wait while the UI action remains incomplete.</summary>
    /// <param name="cancellationToken">Cancels the test's own coordination.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancellationStopsWaitingForIncompleteAction(CancellationToken cancellationToken)
    {
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var execution = HeadlessTestExecutor.ExecuteAsync(
            () =>
            {
                started.SetResult();
                return new(release.Task);
            },
            cancellation.Token).AsTask();
        try
        {
            await started.Task.WaitAsync(Timeout, cancellationToken);
            await cancellation.CancelAsync();
            await Assert.That(async () => await execution.WaitAsync(Timeout, cancellationToken)).Throws<OperationCanceledException>();
            await Assert.That(release.Task.IsCompleted).IsFalse();
        }
        finally
        {
            _ = release.TrySetResult();
            _ = await HeadlessSession.Instance.Dispatch(static () => true, cancellationToken).WaitAsync(Timeout, cancellationToken);
        }
    }

    /// <summary>A test cancelled behind another UI action never runs when it reaches the front of the queue.</summary>
    /// <param name="cancellationToken">Cancels the test's own coordination.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancellationSkipsQueuedAction(CancellationToken cancellationToken)
    {
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = HeadlessTestExecutor.ExecuteAsync(
            () =>
            {
                started.SetResult();
                return new(release.Task);
            },
            cancellationToken).AsTask();
        var invoked = false;
        try
        {
            await started.Task.WaitAsync(Timeout, cancellationToken);
            var queued = HeadlessTestExecutor.ExecuteAsync(
                () =>
                {
                    invoked = true;
                    return ValueTask.CompletedTask;
                },
                cancellation.Token).AsTask();
            await cancellation.CancelAsync();
            await Assert.That(async () => await queued.WaitAsync(Timeout, cancellationToken)).Throws<OperationCanceledException>();
        }
        finally
        {
            _ = release.TrySetResult();
            await first.WaitAsync(Timeout, cancellationToken);
            _ = await HeadlessSession.Instance.Dispatch(static () => true, cancellationToken).WaitAsync(Timeout, cancellationToken);
        }

        await Assert.That(invoked).IsFalse();
    }

    /// <summary>Runs these coordination tests outside the dispatcher they exercise.</summary>
    public sealed class DirectExecutor : ITestExecutor
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask ExecuteTest(TestContext context, Func<ValueTask> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            return action();
        }
    }
}
