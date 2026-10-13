// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>Loads, with asynchronous reads, the objects a catalog-level edit reads, so the synchronous edit that follows finds them cached.</summary>
internal static class PdfEditPrefetch
{
    /// <summary>Loads what a catalog entry leads to.</summary>
    /// <param name="document">The document.</param>
    /// <param name="key">The catalog key, such as <c>/OCProperties</c>.</param>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>A task that completes when the objects are cached.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    internal static ValueTask CatalogEntryAsync(PdfDocument document, KnownName key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();
        return document.Catalog.GetDictionary(key) is { } start
            ? PdfPrefetcher.PrefetchAsync(document.Objects, start, PdfPrefetchKind.Page, cancellationToken)
            : ValueTask.CompletedTask;
    }

    /// <summary>Loads a page and what it leads to.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>A task that completes when the objects are cached.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The page does not exist.</exception>
    internal static ValueTask PageAsync(PdfDocument document, int pageIndex, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();
        return PdfPrefetcher.PrefetchAsync(document.Objects, PdfDocumentPages.GetPage(document, pageIndex).Dictionary, PdfPrefetchKind.Page, cancellationToken);
    }
}
