// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Features;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Reads the preseparated pages of a document: pages that each paint one colourant of a printed page, linked by their
/// separation dictionaries (ISO 32000-1 and ISO 32000-2 §14.11.4; deprecated in PDF 2.0 but still read). The page order
/// is never changed; ordinary composite pages belong to no group.
/// </summary>
public static class PdfDocumentSeparations
{
    /// <summary>Gets the separation groups, loading the separation dictionaries first.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the loading and the read.</param>
    /// <returns>The groups and the problems found; <see cref="PdfSeparationInfo.Empty"/> when no page is a separation.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<PdfSeparationInfo> GetSeparationsAsync(PdfDocument document, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Volatile.Read(ref document.State.Separations) is { } cached)
        {
            return cached;
        }

        var pages = PdfDocumentPages.GetPageSet(document).Pages;
        var key = SeparationReader.InfoKey(document.Objects.Names);
        foreach (var page in pages)
        {
            if (page.Dictionary.ContainsKey(key))
            {
                await PdfPrefetcher.PrefetchAsync(document.Objects, page.Dictionary, PdfPrefetchKind.Separation, cancellationToken).ConfigureAwait(false);
            }
        }

        return PdfDocumentSeparations.Read(document, cancellationToken);
    }

    /// <summary>Gets the separation groups.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The groups and the problems found; <see cref="PdfSeparationInfo.Empty"/> when no page is a separation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfSeparationInfo GetSeparations(PdfDocument document) =>
        Volatile.Read(ref document.State.Separations) ?? PdfDocumentSeparations.Read(document, CancellationToken.None);

    /// <summary>Finds the group a page belongs to.</summary>
    /// <param name="info">The groups.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The group, or <see langword="null"/> for an ordinary page.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="info"/> is null.</exception>
    public static PdfSeparationGroup? FindGroup(PdfSeparationInfo info, int pageIndex)
    {
        ArgumentNullException.ThrowIfNull(info);
        foreach (var group in info.Groups)
        {
            if (PdfDocumentSeparations.IndexOf(group, pageIndex) >= 0)
            {
                return group;
            }
        }

        return null;
    }

    /// <summary>Gets the position of a page within a group.</summary>
    /// <param name="group">The group.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The position in <see cref="PdfSeparationGroup.Separations"/>, or -1 when the page is not in the group.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="group"/> is null.</exception>
    public static int IndexOf(PdfSeparationGroup group, int pageIndex)
    {
        ArgumentNullException.ThrowIfNull(group);
        var separations = group.Separations;
        for (var i = 0; i < separations.Length; i++)
        {
            if (separations[i].PageIndex == pageIndex)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Reads the groups and caches them until the document is edited.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The groups.</returns>
    private static PdfSeparationInfo Read(PdfDocument document, CancellationToken cancellationToken)
    {
        // Read before the objects are, so an edit that lands while the groups are read keeps them out of the cache.
        var revision = PdfDocumentCaches.Revision(document);
        var info = SeparationReader.Read(document, cancellationToken);
        return PdfDocumentCaches.Publish(document, ref document.State.Separations, info, revision);
    }
}
