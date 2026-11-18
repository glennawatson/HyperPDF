// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Core.Reactive;

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
        return Signal.Create<T>(observer =>
        {
            var walk = new Walk<T>(sources.GetEnumerator(), observer);
            walk.Step();
            return walk;
        });
    }

    /// <summary>
    /// One walk through a sequence of signals. A signal may end on the thread that subscribed to it, before the subscribing
    /// returns, or on another thread at any moment after. Each ending asks for a step, and the steps asked for are counted, so
    /// one thread at a time reads the sequence and no ending is lost between the two.
    /// </summary>
    /// <typeparam name="T">The type of value the signals give.</typeparam>
    /// <param name="sequence">The sequence of signals.</param>
    /// <param name="observer">Receives the values of every signal.</param>
    [DebuggerDisplay("Walk: ended {_ended}")]
    private sealed class Walk<T>(IEnumerator<IObservable<T>> sequence, IObserver<T> observer) : IObserver<T>, IDisposable
    {
        /// <summary>The subscription to the signal that is running.</summary>
        private IDisposable? _running;

        /// <summary>The steps asked for and not yet taken. The thread that raises it from nothing takes them all.</summary>
        private int _steps;

        /// <summary>1 once the walk has ended, by reaching the end, by an error or by being stopped.</summary>
        private int _ended;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnCompleted() => Step();

        /// <inheritdoc/>
        public void OnError(Exception error)
        {
            if (End())
            {
                observer.OnError(error);
            }

            // The step lets go of the sequence.
            Step();
        }

        /// <inheritdoc/>
        public void OnNext(T value)
        {
            if (Volatile.Read(ref _ended) == 0)
            {
                observer.OnNext(value);
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _ = End();
            Interlocked.Exchange(ref _running, null)?.Dispose();

            // The step lets go of the sequence, on whichever thread is reading it.
            Step();
        }

        /// <summary>Asks for the next signal of the sequence. The first thread to ask takes every step asked for while it works.</summary>
        public void Step()
        {
            if (Interlocked.Increment(ref _steps) != 1)
            {
                return;
            }

            // A walk that has ended leaves the count raised, so nothing reads the sequence again.
            while (Advance() && Interlocked.Decrement(ref _steps) != 0)
            {
                // Another signal ended while this thread was subscribing to the one before it.
            }
        }

        /// <summary>Subscribes to the next signal of the sequence, or ends the walk when there is none.</summary>
        /// <returns><see langword="true"/> while the walk goes on.</returns>
        private bool Advance()
        {
            if (Volatile.Read(ref _ended) != 0 || !TryMoveNext(out var next))
            {
                sequence.Dispose();
                return false;
            }

            var running = next.Subscribe(this);
            Volatile.Write(ref _running, running);

            // A walk stopped while this signal was being subscribed to did not see it, so it is stopped here.
            if (Volatile.Read(ref _ended) != 0)
            {
                running.Dispose();
            }

            return true;
        }

        /// <summary>Reads the next signal of the sequence, ending the walk when there is none or the sequence fails.</summary>
        /// <param name="next">The next signal.</param>
        /// <returns><see langword="true"/> when there is a next signal.</returns>
        private bool TryMoveNext(out IObservable<T> next)
        {
            next = Signal.Empty<T>();
            try
            {
                if (sequence.MoveNext())
                {
                    next = sequence.Current;
                    return true;
                }
            }
            catch (Exception error)
            {
                // A sequence that fails ends the walk with its failure, as a signal that fails does.
                if (End())
                {
                    observer.OnError(error);
                }

                return false;
            }

            if (End())
            {
                observer.OnCompleted();
            }

            return false;
        }

        /// <summary>Marks the walk as ended.</summary>
        /// <returns><see langword="true"/> for the one caller that ended it.</returns>
        private bool End() => Interlocked.Exchange(ref _ended, 1) == 0;
    }
}
