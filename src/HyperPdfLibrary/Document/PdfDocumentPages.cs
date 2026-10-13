// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Reads pages and manages their cached indexes.</summary>
public static class PdfDocumentPages
{
    /// <summary>Loads what a page needs, so that reading or rendering it does not wait on the file.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>A task that completes when the page's objects are cached.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The page does not exist.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask PrefetchPageAsync(PdfDocument document, int pageIndex, CancellationToken cancellationToken) =>
        PrefetchPageAsync(document, GetPage(document, pageIndex), cancellationToken);

    /// <summary>Loads the resources of a captured page, even when its position in the document changes.</summary>
    /// <param name="document">The document owning the page.</param>
    /// <param name="page">The captured page.</param>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>A task completing when the page's objects and font bytes are cached.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is null.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask PrefetchPageAsync(PdfDocument document, PdfPage page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        cancellationToken.ThrowIfCancellationRequested();
        await PdfPrefetcher.PrefetchAsync(document.Objects, page.Dictionary, PdfPrefetchKind.Page, cancellationToken).ConfigureAwait(false);
        await FontDataPrefetcher.PrepareAsync(document, page, cancellationToken).ConfigureAwait(false);
        PdfDocumentRendering.RefreshFontData(document);
    }

    /// <summary>Gets a page, loading what it needs first.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>The page.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The page does not exist.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<PdfPage> GetPageAsync(PdfDocument document, int pageIndex, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var page = PdfDocumentPages.GetPage(document, pageIndex);
        var load = PdfPrefetcher.PrefetchAsync(document.Objects, page.Dictionary, PdfPrefetchKind.Page, cancellationToken);
        return load.IsCompletedSuccessfully ? new(page) : PdfDocumentPages.PageAfterAsync(load, page);
    }

    /// <summary>Gets a page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="index">The zero based page index.</param>
    /// <returns>The page.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a page.</exception>
    public static PdfPage GetPage(PdfDocument document, int index)
    {
        var pages = PdfDocumentPages.GetPageSet(document);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)pages.Count, nameof(index));
        return pages.GetPage(index);
    }

    /// <summary>Gets the index of a page object.</summary>
    /// <param name="document">The document.</param>
    /// <param name="id">The page object's id.</param>
    /// <returns>The zero based page index, or -1 when the object is not a page.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetPageIndex(PdfDocument document, PdfObjectId id) => PdfDocumentPages.GetPageSet(document).Indexes.GetValueOrDefault(id.Number, -1);

    /// <summary>Gets the pages, reading the page tree again when an edit dropped them.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The cached pages and their indexes.</returns>
    internal static PdfPageSet GetPageSet(PdfDocument document)
    {
        if (Volatile.Read(ref document.State.PageSet) is { } pages)
        {
            return pages;
        }

        lock (document.Objects.Gate)
        {
            if (Volatile.Read(ref document.State.PageSet) is { } current)
            {
                return current;
            }

            // Publish while holding the edit lock so undo cannot invalidate a tree that is still being read.
            pages = PdfPageSet.Read(document.Objects);
            Volatile.Write(ref document.State.PageSet, pages);
            return pages;
        }
    }

    /// <summary>Waits for a page's load to finish, then hands the page back.</summary>
    /// <param name="load">The pending load.</param>
    /// <param name="page">The page that was being loaded.</param>
    /// <returns>The page.</returns>
    private static async ValueTask<PdfPage> PageAfterAsync(ValueTask load, PdfPage page)
    {
        await load.ConfigureAwait(false);
        return page;
    }
}
