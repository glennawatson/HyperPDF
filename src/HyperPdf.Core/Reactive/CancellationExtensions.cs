// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Core.Reactive;

/// <summary>Ties a signal to a cancellation token that something else owns, such as a Stop button.</summary>
public static class CancellationExtensions
{
    /// <summary>Signals that can be stopped by a token.</summary>
    /// <typeparam name="T">The type of value the signal gives.</typeparam>
    /// <param name="source">The signal.</param>
    extension<T>(IObservable<T> source)
    {
        /// <summary>Stops the signal when the token is cancelled, and says so with the cancellation error rather than ending as if it finished.</summary>
        /// <param name="cancellationToken">Stops the signal when it is cancelled.</param>
        /// <returns>A signal that unsubscribes from the source when the token is cancelled, then fails with <see cref="OperationCanceledException"/>.</returns>
        public IObservable<T> ThrowWhenCancelled(CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(source);
            return source
                .TakeUntil(cancellationToken)
                .Concat(Signal.Defer(() => cancellationToken.IsCancellationRequested
                    ? Signal.Throw<T>(new OperationCanceledException(cancellationToken))
                    : Signal.Empty<T>()));
        }
    }
}
