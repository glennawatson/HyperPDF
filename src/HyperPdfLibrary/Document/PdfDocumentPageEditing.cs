// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Loads page resources asynchronously before applying cancellable page edits.</summary>
public static class PdfDocumentPageEditing
{
    /// <summary>Turns selected pages by a number of degrees clockwise.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pages">Zero-based page indexes.</param>
    /// <param name="degrees">The clockwise turn.</param>
    /// <param name="cancellationToken">Cancels resource reads and editing.</param>
    /// <returns>A task completing after the edit.</returns>
    public static async ValueTask RotatePagesAsync(PdfDocument document, ReadOnlyMemory<int> pages, int degrees, CancellationToken cancellationToken)
    {
        await PrefetchAsync(document, pages, cancellationToken).ConfigureAwait(false);
        using var scope = PdfCancellation.Enter(cancellationToken);
        PdfCancellation.ThrowIfCancelled();
        PdfDocumentPageOperations.RotatePages(document, pages.Span, degrees);
    }

    /// <summary>Deletes selected pages, keeping at least one page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pages">Zero-based page indexes.</param>
    /// <param name="cancellationToken">Cancels resource reads and editing.</param>
    /// <returns>A task completing after the edit.</returns>
    public static async ValueTask DeletePagesAsync(PdfDocument document, ReadOnlyMemory<int> pages, CancellationToken cancellationToken)
    {
        await PrefetchAsync(document, pages, cancellationToken).ConfigureAwait(false);
        using var scope = PdfCancellation.Enter(cancellationToken);
        PdfCancellation.ThrowIfCancelled();
        PdfDocumentPageOperations.DeletePages(document, pages.Span);
    }

    /// <summary>Moves selected pages to an index in the resulting document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pages">Zero-based page indexes in output order.</param>
    /// <param name="destination">The first moved page's resulting index.</param>
    /// <param name="cancellationToken">Cancels resource reads and editing.</param>
    /// <returns>A task completing after the edit.</returns>
    public static async ValueTask MovePagesAsync(PdfDocument document, ReadOnlyMemory<int> pages, int destination, CancellationToken cancellationToken)
    {
        await PrefetchAsync(document, pages, cancellationToken).ConfigureAwait(false);
        using var scope = PdfCancellation.Enter(cancellationToken);
        PdfCancellation.ThrowIfCancelled();
        PdfDocumentPageOperations.MovePages(document, pages.Span, destination);
    }

    /// <summary>Copies selected pages into a document, retaining their structure.</summary>
    /// <param name="document">The destination document.</param>
    /// <param name="index">The first inserted page's index.</param>
    /// <param name="source">The source, kept open until the destination is saved.</param>
    /// <param name="pages">Source page indexes in insertion order.</param>
    /// <param name="cancellationToken">Cancels resource reads and editing.</param>
    /// <returns>A task completing after the edit.</returns>
    public static async ValueTask InsertPagesAsync(PdfDocument document, int index, PdfDocument source, ReadOnlyMemory<int> pages, CancellationToken cancellationToken)
    {
        await PrefetchAsync(source, pages, cancellationToken).ConfigureAwait(false);
        using var scope = PdfCancellation.Enter(cancellationToken);
        PdfCancellation.ThrowIfCancelled();
        PdfDocumentPageOperations.InsertPages(document, index, source, pages.Span);
    }

    /// <summary>Writes selected pages to a new PDF, retaining their structure.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pages">Zero-based indexes in output order.</param>
    /// <param name="cancellationToken">Cancels resource reads and writing.</param>
    /// <returns>The new PDF bytes.</returns>
    public static async ValueTask<byte[]> ExtractPagesAsync(PdfDocument document, ReadOnlyMemory<int> pages, CancellationToken cancellationToken)
    {
        await PrefetchAsync(document, pages, cancellationToken).ConfigureAwait(false);
        using var scope = PdfCancellation.Enter(cancellationToken);
        PdfCancellation.ThrowIfCancelled();
        return PdfDocumentPageOperations.ExtractPages(document, pages.Span);
    }

    /// <summary>Loads selected pages before entering the synchronous editing scope.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pages">Zero-based indexes.</param>
    /// <param name="cancellationToken">Cancels resource reads.</param>
    /// <returns>A task completing when page resources are loaded.</returns>
    private static async ValueTask PrefetchAsync(PdfDocument document, ReadOnlyMemory<int> pages, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        for (var index = 0; index < pages.Length; index++)
        {
            _ = await PdfDocumentPages.GetPageAsync(document, pages.Span[index], cancellationToken).ConfigureAwait(false);
        }
    }
}
