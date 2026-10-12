// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Navigation;
namespace HyperPdf.Engine;

/// <summary>Implements Navigation over the document's owned state.</summary>
internal static class HyperPdfNavigation
{
    /// <summary>Gets the size of every page, in points, without loading the pages.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The page sizes, indexed by page.</returns>
    internal static PageSize[] GetPageSizes(HyperPdfDocument self) => (PageSize[])Volatile.Read(ref self.PageSizes).Clone();

    /// <summary>Gets the document metadata.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The metadata.</returns>
    internal static DocumentMetadata GetMetadata(HyperPdfDocument self)
    {
        ObjectDisposedException.ThrowIf(self.IsDisposed, self);
        var info = PdfDocumentMetadata.GetInfo(self.Document);
        var payload = PdfDocumentWrapper.GetEncryptedPayload(self.Document);
        return new()
        {
            Title = info.Title,
            Author = info.Author,
            Subject = info.Subject,
            Keywords = info.Keywords,
            Creator = info.Creator,
            Producer = info.Producer,
            Created = info.Created,
            Modified = info.Modified,
            FormatVersion = info.Version,
            IsEncrypted = info.IsEncrypted,
            IsLinearized = PdfDocumentLinearization.GetInfo(self.Document) is not null,
            WrappedFileName = payload?.FileName,
            WrappedCryptographicFilter = payload?.CryptographicFilter,
        };
    }

    /// <summary>Gets the display label of a page, for example "iv", when the document defines one.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The label or <see langword="null"/>.</returns>
    internal static string? GetPageLabel(HyperPdfDocument self, int pageIndex)
    {
        using var access = HyperPdfNavigation.EnterPageRead(self);
        return self.IsDisposed ? null : PdfDocumentLabels.GetPageLabel(self.Document, pageIndex);
    }

    /// <summary>Gets the outline (bookmarks) tree.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The root entries.</returns>
    internal static IReadOnlyList<OutlineNode> GetOutline(HyperPdfDocument self)
    {
        using var access = HyperPdfNavigation.EnterPageRead(self);
        if (self.IsDisposed)
        {
            return [];
        }

        var outline = Volatile.Read(ref self.Outline);
        if (outline is null)
        {
            outline = HyperPdfNavigation.ConvertOutline(self, PdfDocumentNavigation.GetOutline(self.Document));
            Volatile.Write(ref self.Outline, outline);
        }

        return outline;
    }

    /// <summary>Gets the links on a page.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The links.</returns>
    internal static IReadOnlyList<PageLink> GetLinks(HyperPdfDocument self, int pageIndex)
    {
        using var access = HyperPdfNavigation.EnterPageRead(self);
        if (self.IsDisposed || (uint)pageIndex >= (uint)self.PageCount)
        {
            return [];
        }

        if (Volatile.Read(ref self.Links) is null)
        {
            _ = Interlocked.CompareExchange(ref self.Links, new PageLink[]?[self.PageCount], null);
        }

        var cache = Volatile.Read(ref self.Links)!;
        if ((uint)pageIndex >= (uint)cache.Length)
        {
            return [];
        }

        var links = Volatile.Read(ref cache[pageIndex]);
        if (links is null)
        {
            links = HyperPdfNavigation.ReadLinks(self, pageIndex);
            Volatile.Write(ref cache[pageIndex], links);
        }

        return links;
    }

    /// <summary>Excludes page readers during a synchronous edit.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The scope held until the edit and cache refresh complete.</returns>
    internal static HyperPdfPageAccess EnterPageWrite(HyperPdfDocument self) => new(self.PageAccess, ref self.PageAccessLeases, true);

    /// <summary>Refreshes page geometry and navigation after a committed page edit, undo or redo.</summary>
    /// <param name="self">The owning document.</param>
    internal static void PagesEdited(HyperPdfDocument self)
    {
        HyperPdfEditing.Edited(self);
        PdfDocumentPageContent.InvalidatePageContent(self.Document);
        if (Volatile.Read(ref self.AnnotationState) is { } annotationState)
        {
            HyperPdfAnnotationReading.InvalidatePages(annotationState);
        }

        var sizes = new PageSize[self.Document.PageCount];
        for (var i = 0; i < sizes.Length; i++)
        {
            var page = PdfDocumentPages.GetPage(self.Document, i);
            sizes[i] = new(page.Width, page.Height);
        }

        Volatile.Write(ref self.PageSizes, sizes);
        Volatile.Write(ref self.Outline, null);
    }

    /// <summary>Prevents page structure from changing during a synchronous read.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The scope held until the page data has been read.</returns>
    internal static HyperPdfPageAccess EnterPageRead(HyperPdfDocument self) => new(self.PageAccess, ref self.PageAccessLeases, false);

    /// <summary>
    /// Reads a page's link annotations as viewer links, followed by the web and email addresses written as text, as
    /// the PDFium engine lists them.
    /// </summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The page index.</param>
    /// <returns>The links.</returns>
    internal static PageLink[] ReadLinks(HyperPdfDocument self, int pageIndex)
    {
        var links = HyperPdfNavigation.ReadAnnotationLinks(self, pageIndex);
        HyperPdfText.GetWebLinksNative(self, pageIndex, links);
        return [.. links];
    }

    /// <summary>Reads a page's link annotations as viewer links.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The page index.</param>
    /// <returns>The links.</returns>
    internal static List<PageLink> ReadAnnotationLinks(HyperPdfDocument self, int pageIndex)
    {
        var page = PdfDocumentPages.GetPage(self.Document, pageIndex);
        var source = PdfDocumentLinks.GetLinks(self.Document, pageIndex);
        var links = new List<PageLink>(source.Count);
        foreach (var link in source)
        {
            var target = LinkTargets.From(self.Document, link.Action, pageIndex);
            if (target.Kind != LinkTargetKind.None)
            {
                links.Add(new(LinkTargets.ToPageRect(page.ToViewerRectangle(link.Bounds)), target));
            }
        }

        return links;
    }

    /// <summary>Converts outline entries.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="items">The library entries.</param>
    /// <returns>The viewer entries.</returns>
    internal static OutlineNode[] ConvertOutline(HyperPdfDocument self, IReadOnlyList<PdfOutlineItem> items)
    {
        var nodes = new OutlineNode[items.Count];
        for (var i = 0; i < nodes.Length; i++)
        {
            var item = items[i];
            nodes[i] = new(item.Title, LinkTargets.From(self.Document, item.Action), HyperPdfNavigation.ConvertOutline(self, item.Children), item.IsOpen);
        }

        return nodes;
    }
}
