// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.App.Controls;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Tests;

/// <summary>Checks that an async handler for an older value is cancelled when a newer value arrives.</summary>
public sealed class SwitchAsyncTests
{
    /// <summary>The first value.</summary>
    private const int First = 1;

    /// <summary>The second value.</summary>
    private const int Second = 2;

    /// <summary>The older handler's token is cancelled by the newer value, and the newer handler's is not.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NewerValueCancelsTheOlderHandler()
    {
        using var source = new Signal<int>();
        var tokens = new Dictionary<int, CancellationToken>();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = source
            .SwitchAsync(async (value, token) =>
            {
                tokens[value] = token;
                await gate.Task.ConfigureAwait(false);
                return RxVoid.Default;
            })
            .SubscribeSafe(static _ => { }, static _ => { });

        source.OnNext(First);
        source.OnNext(Second);

        using (Assert.Multiple())
        {
            await Assert.That(tokens[First].IsCancellationRequested).IsTrue();
            await Assert.That(tokens[Second].IsCancellationRequested).IsFalse();
        }

        _ = gate.TrySetResult();
    }

    /// <summary>A handler that finishes without a newer value keeps its token live.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HandlerWithoutANewerValueIsNotCancelled()
    {
        using var source = new Signal<int>();
        var seen = CancellationToken.None;
        using var subscription = source
            .SwitchAsync((_, token) =>
            {
                seen = token;
                return Task.FromResult(RxVoid.Default);
            })
            .SubscribeSafe(static _ => { }, static _ => { });

        source.OnNext(First);

        await Assert.That(seen.IsCancellationRequested).IsFalse();
    }
}
