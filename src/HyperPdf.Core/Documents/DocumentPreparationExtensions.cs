// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Core.Documents;

/// <summary>Gives the PDF library's page preparation, which is its own async call, to the rest of the app as a signal; this is the one place that call is awaited.</summary>
public static class DocumentPreparationExtensions
{
    /// <summary>Awaits the library's page preparation.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The zero based page.</param>
    /// <param name="token">Cancels the preparation.</param>
    /// <returns>The completion value.</returns>
    private static async Task<RxVoid> PrepareAsync(IDocument document, int page, CancellationToken token)
    {
        await document.PreparePageAsync(page, token).ConfigureAwait(false);
        return RxVoid.Default;
    }

    /// <summary>Preparing pages of a document.</summary>
    /// <param name="document">The document.</param>
    extension(IDocument document)
    {
        /// <summary>Prepares a page so it can be read or drawn.</summary>
        /// <param name="page">The zero based page.</param>
        /// <returns>A cold signal giving one value once the page is prepared; unsubscribing cancels the preparation.</returns>
        public IObservable<RxVoid> PreparePage(int page)
        {
            ArgumentNullException.ThrowIfNull(document);
            return Signal.FromAsync(token => PrepareAsync(document, page, token));
        }

        /// <summary>Prepares a page, also stopping when a token the caller owns is cancelled.</summary>
        /// <param name="page">The zero based page.</param>
        /// <param name="cancellationToken">Cancels the preparation, which then fails with the cancellation.</param>
        /// <returns>A cold signal giving one value once the page is prepared.</returns>
        public IObservable<RxVoid> PreparePage(int page, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(document);
            return Signal.FromAsync(token => PrepareAsync(document, page, token), cancellationToken);
        }
    }
}
