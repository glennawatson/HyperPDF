// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Primitives;
using OwnThread = HyperPdf.Core.Reactive.DedicatedThread;

namespace HyperPdf.Core.Tests.Reactive;

/// <summary>Checks that blocking work on a thread of its own reports its result, its failure, and stops quietly when unsubscribed.</summary>
public sealed class DedicatedThreadTests
{
    /// <summary>The answer the sample work gives.</summary>
    private const int Answer = 42;

    /// <summary>The time to wait for the work to notice it was stopped, in seconds.</summary>
    private const int StopSeconds = 10;

    /// <summary>The work's result is given once.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GivesTheResult() =>
        await Assert.That(await OwnThread.Run("test", static _ => Answer).ToTask()).IsEqualTo(Answer);

    /// <summary>A failure of the work reaches the subscriber.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReportsAFailure() =>
        await Assert.That(static async () => await OwnThread.Run<int>("test", static _ => throw new InvalidOperationException("Broken.")).ToTask()).Throws<InvalidOperationException>();

    /// <summary>Unsubscribing cancels the token, and work that then fails because what it blocked on was closed does not crash the process.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnsubscribingStopsBlockedWorkWithoutAFault()
    {
        using var started = new ManualResetEventSlim();
        using var ended = new ManualResetEventSlim();
        var subscription = OwnThread.Run<int>(
                "test",
                token =>
                {
                    started.Set();
                    try
                    {
                        _ = token.WaitHandle.WaitOne();
                        throw new ObjectDisposedException("pipe");
                    }
                    finally
                    {
                        ended.Set();
                    }
                })
            .SubscribeSafe(static _ => { }, static _ => { });

        _ = started.Wait(TimeSpan.FromSeconds(StopSeconds));
        subscription.Dispose();

        await Assert.That(ended.Wait(TimeSpan.FromSeconds(StopSeconds))).IsTrue();
    }
}
