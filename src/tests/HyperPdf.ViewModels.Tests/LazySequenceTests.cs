// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.ViewModels.Services;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks that a sequence of signals is asked for its next signal only once the one before has ended.</summary>
public sealed class LazySequenceTests
{
    /// <summary>The signals of a sequence that reads state an earlier signal changes.</summary>
    private const int Signals = 4;

    /// <summary>The length of the synchronous sequence, long enough to overflow a stack that grows with each signal.</summary>
    private const int LongSequence = 50_000;

    /// <summary>The asks to wait for before unsubscribing.</summary>
    private const int AsksBeforeStopping = 2;

    /// <summary>How many signal delays to wait after unsubscribing, to see that nothing more is asked.</summary>
    private const int DelaysToWatch = 4;

    /// <summary>How long each signal of the state-dependent sequence takes, so the next ask has to wait.</summary>
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(20);

    /// <summary>A sequence that waits on state changed by the signal before it is asked once per signal, and finishes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SequenceReadingStateIsAskedOncePerFinishedSignal()
    {
        var finished = 0;
        var asked = 0;

        IEnumerable<IObservable<int>> Source()
        {
            while (finished < Signals)
            {
                asked++;
                yield return Signal.FromAsync(async token =>
                {
                    await Task.Delay(Delay, token);
                    return ++finished;
                });
            }
        }

        var values = await LazySequence.Concat(Source()).ToList().ToTask();

        await Assert.That(values).IsEquivalentTo(Enumerable.Range(1, Signals));
        await Assert.That(asked).IsEqualTo(Signals);
    }

    /// <summary>A long run of signals that end at once is walked without growing the stack.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LongSynchronousSequenceDoesNotOverflow()
    {
        var count = await LazySequence.Concat(Enumerable.Range(0, LongSequence).Select(Signal.Return)).Count().ToTask();

        await Assert.That(count).IsEqualTo(LongSequence);
    }

    /// <summary>Unsubscribing stops the sequence being asked for more.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnsubscribingStopsAskingForMore()
    {
        var asked = 0;

        IEnumerable<IObservable<int>> Source()
        {
            while (asked < LongSequence)
            {
                asked++;
                yield return Signal.FromAsync(async token =>
                {
                    await Task.Delay(Delay, token);
                    return asked;
                });
            }
        }

        var subscription = LazySequence.Concat(Source()).SubscribeSafe(static _ => { }, static _ => { });
        await TestWait.UntilAsync(() => Volatile.Read(ref asked) >= AsksBeforeStopping);
        subscription.Dispose();
        var stopped = Volatile.Read(ref asked);
        await Task.Delay(Delay * DelaysToWatch);

        await Assert.That(Volatile.Read(ref asked)).IsEqualTo(stopped);
    }
}
