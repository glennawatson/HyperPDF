// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Reading;
using HyperPdfLibrary;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Engine;

/// <summary>
/// Gathers what assistive technology needs from a page beyond its reading blocks: empty table cells and artifacts from the
/// tags, form fields with their state, and annotations with their descriptions (PDF/UA, ISO 14289-1).
/// </summary>
internal static class HyperPdfPageSemantics
{
    /// <summary>Gets a page's semantic items.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The items; empty for a page that is out of range or whose structure cannot be read.</returns>
    internal static IReadOnlyList<PageSemanticItem> GetItems(HyperPdfDocument self, int pageIndex)
    {
        using var access = HyperPdfNavigation.EnterPageRead(self);
        if (!access.IsActive || self.IsDisposed || (uint)pageIndex >= (uint)self.PageCount)
        {
            return [];
        }

        var items = new List<PageSemanticItem>();
        try
        {
            HyperPdfSemanticTags.Add(self, pageIndex, items);
            HyperPdfSemanticFields.Add(self, pageIndex, items);
            HyperPdfSemanticAnnotations.Add(self, pageIndex, items);
        }
        catch (PdfException)
        {
            // Damaged structure only means fewer items; the page and its reading blocks are untouched.
        }

        return items;
    }

    /// <summary>Determines whether an annotation or widget is hidden: by its flags, or by optional content that is switched off.</summary>
    /// <param name="document">The document.</param>
    /// <param name="annotation">The annotation or widget dictionary.</param>
    /// <returns><see langword="true"/> when a viewer does not show it.</returns>
    internal static bool IsHidden(PdfDocument document, PdfDictionary annotation) =>
        (PdfAnnotations.GetFlags(annotation) & (PdfAnnotationFlags.Hidden | PdfAnnotationFlags.NoView)) != 0
        || (annotation.GetRaw(KnownName.OC) is { IsNull: false } content && !PdfDocumentLayers.GetOptionalContent(document).IsVisible(content));
}
