// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Document;

/// <summary>Reads tagged content and structure trees.</summary>
public static class PdfDocumentTagged
{
    /// <summary>Gets the structure tree, read on first use; <see langword="null"/> for a document that has none.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The structure tree, or null when the document has none.</returns>
    public static PdfStructureTree? GetStructureTree(PdfDocument document)
    {
        // Read before the tree is, so an edit that lands while it loads keeps it out of the cache.
        var revision = PdfDocumentCaches.Revision(document);
        var read = Volatile.Read(ref document.State.StructureTree);
        if (read is null)
        {
            lock (document.State.StructureGate)
            {
                read = document.State.StructureTree ?? PdfDocumentCaches.Publish(document, ref document.State.StructureTree, new(PdfStructureTree.Load(document)), revision);
            }
        }

        return read.Tree;
    }

    /// <summary>Gets what a page draws inside marked content: each glyph's text, box, marked content id and artifact flag.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The page's marked content, recorded on first use and then shared.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> is not a page.</exception>
    public static PdfMarkedContentPage GetMarkedContent(PdfDocument document, int pageIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, document.PageCount);

        // Read before the page count and the page are, so an edit that lands meanwhile keeps the record out of the cache.
        var revision = PdfDocumentCaches.Revision(document);
        var pages = PdfDocumentCaches.PerPage(document, ref document.State.MarkedContent, revision);
        if ((uint)pageIndex < (uint)pages.Length && Volatile.Read(ref pages[pageIndex]) is { } recorded)
        {
            return recorded;
        }

        var page = MarkedContentRecorder.Record(document, PdfDocumentPages.GetPage(document, pageIndex));

        // The array was made for the page count before an edit changed it, so a page beyond it is recorded but not kept.
        return (uint)pageIndex < (uint)pages.Length ? PdfDocumentCaches.PublishFirst(document, ref pages[pageIndex], page, revision) : page;
    }
}
