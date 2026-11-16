// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Core.Reactive;

/// <summary>Runs work that blocks, such as decoding or waiting for a sound to be heard, on a thread of its own so no pool thread is held.</summary>
public static class DedicatedThread
{
    /// <summary>Makes a signal that runs the work on a new thread for each subscriber.</summary>
    /// <typeparam name="T">The type of result.</typeparam>
    /// <param name="name">The thread's name.</param>
    /// <param name="work">The work, which must stop soon after the token it is given is cancelled.</param>
    /// <returns>A cold signal giving the work's result once; unsubscribing cancels the token, and a result that comes after that is dropped.</returns>
    public static IObservable<T> Run<T>(string name, Func<CancellationToken, T> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        return Signal.Create<T>(observer =>
        {
            var worker = new Worker<T>(name, work, observer);
            worker.Start();
            return worker;
        });
    }

    /// <summary>One run of the work.</summary>
    /// <typeparam name="T">The type of result.</typeparam>
    private sealed class Worker<T> : IDisposable
    {
        /// <summary>The thread's name.</summary>
        private readonly string _name;

        /// <summary>The work.</summary>
        private readonly Func<CancellationToken, T> _work;

        /// <summary>Told how the work ended.</summary>
        private readonly IObserver<T> _observer;

        /// <summary>Cancels the work.</summary>
        private readonly CancellationTokenSource _stop = new();

        /// <summary>Tells the work to stop; taken once, so a thread that starts after disposal never reads a disposed source.</summary>
        private readonly CancellationToken _token;

        /// <summary>Initializes a new instance of the <see cref="Worker{T}"/> class.</summary>
        /// <param name="name">The thread's name.</param>
        /// <param name="work">The work.</param>
        /// <param name="observer">Told how the work ended.</param>
        internal Worker(string name, Func<CancellationToken, T> work, IObserver<T> observer)
        {
            _name = name;
            _work = work;
            _observer = observer;
            _token = _stop.Token;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _stop.Cancel();
            _stop.Dispose();
        }

        /// <summary>Starts the thread.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Start() => new Thread(Execute) { IsBackground = true, Name = _name }.Start();

        /// <summary>Runs the work and reports how it ended, unless it was stopped.</summary>
        private void Execute()
        {
            try
            {
                var result = _work(_token);
                if (!_token.IsCancellationRequested)
                {
                    _observer.OnNext(result);
                    _observer.OnCompleted();
                }
            }
            catch (OperationCanceledException)
            {
                // Unsubscribing stops the work, which is the end of it.
            }
            catch (Exception error) when (!_token.IsCancellationRequested)
            {
                _observer.OnError(error);
            }
        }
    }
}
