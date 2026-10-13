// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Security;
namespace HyperPdf.Engine;

/// <summary>Implements Navigation over the document's owned state.</summary>
internal static class HyperPdfNavigation
{
    /// <summary>Gets the size of every page in points of physical size (1/72 inch): the user space size times the page's <c>/UserUnit</c> (ISO 32000-2 7.7.3.3), without loading the pages.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The page sizes, indexed by page.</returns>
    internal static PageSize[] GetPageSizes(HyperPdfDocument self) => (PageSize[])Volatile.Read(ref self.PageSizes).Clone();

    /// <summary>Gets the document metadata.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The metadata.</returns>
    internal static DocumentMetadata GetMetadata(HyperPdfDocument self)
    {
        ObjectDisposedException.ThrowIf(self.IsDisposed, self);
        var info = PdfDocumentMetadata.GetResolvedInfo(self.Document);
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
            Integrity = IntegrityOf(PdfDocumentMac.GetIntegrity(self.Document)),
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void PagesEdited(HyperPdfDocument self) =>PagesEdited(self, PdfCacheScopes.All);

    /// <summary>Refreshes page geometry and navigation after a committed edit that changed only some parts of the document.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="scopes">The parts of the document the edit can have changed.</param>
    internal static void PagesEdited(HyperPdfDocument self, PdfCacheScopes scopes)
    {
        HyperPdfEditing.Edited(self, scopes);
        if ((scopes & (PdfCacheScopes.PageSet | PdfCacheScopes.PageContent)) != PdfCacheScopes.None)
        {
            PdfDocumentPageContent.InvalidatePageContent(self.Document);
        }

        if (Volatile.Read(ref self.AnnotationState) is { } annotationState)
        {
            HyperPdfAnnotationReading.InvalidatePages(annotationState);
        }

        Volatile.Write(ref self.PageSizes, ReadPageSizes(self.Document, Volatile.Read(ref self.PageSizes)));
        Volatile.Write(ref self.Outline, null);
        Volatile.Write(ref self.Attachments, null);
    }

    /// <summary>Prevents page structure from changing during a synchronous read.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The scope held until the page data has been read.</returns>
    internal static HyperPdfPageAccess EnterPageRead(HyperPdfDocument self) => new(self.PageAccess, ref self.PageAccessLeases, false);

    /// <summary>
    /// Reads a page's link annotations as viewer links, followed by the web and email addresses written as text,
    /// so a printed address can be followed.
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
                links.Add(new(LinkTargets.ToPageRect(page.ToPhysicalRectangle(link.Bounds)), target));
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
            nodes[i] = new(item.Title, LinkTargets.From(self.Document, item.Action), HyperPdfNavigation.ConvertOutline(self, item.Children), item.IsOpen) { Id = item.Id.Number };
        }

        return nodes;
    }

    /// <summary>Converts the PDF MAC check to the viewer's integrity result.</summary>
    /// <param name="report">The library's report.</param>
    /// <returns>The result, or <see langword="null"/> when the document has no integrity protection to check.</returns>
    internal static DocumentIntegrity? IntegrityOf(PdfMacReport report) => report.Status switch
    {
        PdfMacStatus.Valid => new(DocumentIntegrityStatus.Intact, report.Summary),
        PdfMacStatus.Invalid => new(DocumentIntegrityStatus.Invalid, report.Summary),
        PdfMacStatus.Missing => new(DocumentIntegrityStatus.Missing, report.Summary),
        PdfMacStatus.Unsupported => new(DocumentIntegrityStatus.Unverified, report.Summary),
        _ => null,
    };

    /// <summary>Reads every page's size, keeping the array already published when the sizes have not changed.</summary>
    /// <param name="document">The document.</param>
    /// <param name="published">The sizes published before the edit.</param>
    /// <returns>The sizes; <paramref name="published"/> itself when nothing changed, since published arrays are never written to.</returns>
    private static PageSize[] ReadPageSizes(PdfDocument document, PageSize[]? published)
    {
        var count = document.PageCount;
        var fresh = published is not null && published.Length == count ? null : new PageSize[count];
        for (var i = 0; i < count; i++)
        {
            var page = PdfDocumentPages.GetPage(document, i);
            PageSize size = new(page.PhysicalWidth, page.PhysicalHeight);
            if (fresh is null)
            {
                if (published![i] == size)
                {
                    continue;
                }

                // The first size that differs: start a new array holding the unchanged ones before it.
                fresh = new PageSize[count];
                published.AsSpan(0, i).CopyTo(fresh);
            }

            fresh[i] = size;
        }

        return fresh ?? published!;
    }
}
