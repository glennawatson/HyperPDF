// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Engine;

/// <summary>Loads what an edit reads with asynchronous I/O, before the edit takes the edit gate.</summary>
internal static class HyperPdfEditPrefetch
{
    /// <summary>Loads the bookmark entries.</summary>
    /// <param name="document">The library document.</param>
    /// <param name="cancellationToken">Cancels loading.</param>
    /// <returns>A task that completes when the entries are cached.</returns>
    internal static ValueTask OutlineAsync(PdfDocument document, CancellationToken cancellationToken) =>
        document.Catalog.GetDictionary(KnownName.Outlines) is { } root
            ? PdfPrefetcher.PrefetchAsync(document.Objects, root, PdfPrefetchKind.Outline, cancellationToken)
            : ValueTask.CompletedTask;

    /// <summary>Loads the name tree of attached files.</summary>
    /// <param name="document">The library document.</param>
    /// <param name="cancellationToken">Cancels loading.</param>
    /// <returns>A task that completes when the nodes are cached.</returns>
    internal static ValueTask AttachmentsAsync(PdfDocument document, CancellationToken cancellationToken) =>
        document.Catalog.GetDictionary(KnownName.Names)?.GetDictionary(KnownName.EmbeddedFiles) is { } root
            ? PdfPrefetcher.PrefetchAsync(document.Objects, root, PdfPrefetchKind.PageTree, cancellationToken)
            : ValueTask.CompletedTask;

    /// <summary>Loads every page, which a form field edit reads for its widgets.</summary>
    /// <param name="document">The library document.</param>
    /// <param name="cancellationToken">Cancels loading.</param>
    /// <returns>A task that completes when the pages are cached.</returns>
    internal static async ValueTask PagesAsync(PdfDocument document, CancellationToken cancellationToken)
    {
        for (var page = 0; page < document.PageCount; page++)
        {
            _ = await PdfDocumentPages.GetPageAsync(document, page, cancellationToken).ConfigureAwait(false);
        }
    }
}
