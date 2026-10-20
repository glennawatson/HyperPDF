// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Printing;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Engine;

/// <summary>Reads what a document asks of the viewer when it opens, from its catalog and viewer preferences.</summary>
internal static class HyperPdfOpenHints
{
    /// <summary>Reads a document's open hints.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The hints.</returns>
    internal static DocumentOpenHints Read(PdfDocument document)
    {
        var preferences = PdfDocumentViewerPreferences.GetViewerPreferences(document);
        return new(
            OpenPage(document),
            PanelOf(preferences.PageMode),
            LayoutOf(preferences.PageLayout),
            string.Equals(preferences.Direction, "R2L", StringComparison.Ordinal),
            preferences.DisplayDocTitle,
            PrintOf(preferences.PrintScaling, preferences.NumCopies, preferences.Duplex),
            document.Catalog.GetDictionary(KnownName.MarkInfo)?.GetBoolean(KnownName.Marked) ?? false);
    }

    /// <summary>Gets the page the document's open action goes to.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The zero-based page, or -1 when the document has no open action that goes to a page.</returns>
    private static int OpenPage(PdfDocument document) =>
        PdfDocumentActions.GetOpenAction(document)?.Action.Value is GoToAction goTo && (uint)goTo.Destination.PageIndex < (uint)document.PageCount
            ? goTo.Destination.PageIndex
            : -1;

    /// <summary>Gets the panel a page mode asks for.</summary>
    /// <param name="pageMode">The <c>/PageMode</c> name.</param>
    /// <returns>The panel; none for a mode that shows no panel.</returns>
    private static OpenPanel PanelOf(string pageMode) => pageMode switch
    {
        "UseOutlines" => OpenPanel.Outline,
        "UseThumbs" => OpenPanel.Thumbnails,
        "UseAttachments" => OpenPanel.Attachments,
        "UseOC" => OpenPanel.Layers,
        _ => OpenPanel.None,
    };

    /// <summary>
    /// Gets the arrangement a page layout asks for. A layout that puts odd pages on the left starts with a two page spread;
    /// one that puts them on the right starts with the first page alone.
    /// </summary>
    /// <param name="pageLayout">The <c>/PageLayout</c> name.</param>
    /// <returns>The arrangement.</returns>
    private static PageLayoutMode LayoutOf(string pageLayout) => pageLayout switch
    {
        "TwoColumnLeft" or "TwoPageLeft" => PageLayoutMode.Dual,
        "TwoColumnRight" or "TwoPageRight" => PageLayoutMode.DualCover,
        _ => PageLayoutMode.Single,
    };

    /// <summary>Gets the print defaults a document asks for.</summary>
    /// <param name="scaling">The <c>/PrintScaling</c> name.</param>
    /// <param name="copies">The <c>/NumCopies</c> value.</param>
    /// <param name="duplex">The <c>/Duplex</c> name, or <see langword="null"/>.</param>
    /// <returns>The defaults.</returns>
    private static PrintDefaults PrintOf(string scaling, int copies, string? duplex) => new(
        string.Equals(scaling, "None", StringComparison.Ordinal),
        Math.Max(1, copies),
        duplex is "DuplexFlipShortEdge" or "DuplexFlipLongEdge",
        string.Equals(duplex, "DuplexFlipShortEdge", StringComparison.Ordinal) ? DuplexBinding.ShortEdge : DuplexBinding.LongEdge);
}
