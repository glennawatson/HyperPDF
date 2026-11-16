// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Services;

/// <summary>Runs signals one after another, asking for the next only once the one before has ended.</summary>
public static class LazySequence
{
    /// <summary>Runs each signal of a sequence to its end, then asks the sequence for the next.</summary>
    /// <typeparam name="T">The type of value the signals give.</typeparam>
    /// <param name="sources">The signals; the sequence may read state that an earlier signal changed, so it is only asked for the next signal once the one before has ended.</param>
    /// <returns>A cold signal giving every value of every signal in turn; unsubscribing stops asking the sequence for more.</returns>
    public static IObservable<T> Concat<T>(IEnumerable<IObservable<T>> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        // The sequence is walked by hand because Signal.Concat reads a sequence ahead of the signals that run, which spins forever on a sequence that waits for state an earlier signal changes.
        return Signal.Using(
            sources.GetEnumerator,
            static enumerator => Signal.Defer(() => Next(enumerator))
                .Repeat()
                .TakeWhile(static step => step.More)
                .Select(static step => step.Value));
    }

    /// <summary>Asks the sequence for its next signal.</summary>
    /// <typeparam name="T">The type of value the signals give.</typeparam>
    /// <param name="enumerator">The walk through the sequence.</param>
    /// <returns>A signal giving each value of the next signal, or one closing step when the sequence has ended.</returns>
    private static IObservable<SequenceStep<T>> Next<T>(IEnumerator<IObservable<T>> enumerator) =>
        enumerator.MoveNext()
            ? enumerator.Current.Select(static value => new SequenceStep<T>(value, true))
            : Signal.Return(new SequenceStep<T>(default!, false));
}
