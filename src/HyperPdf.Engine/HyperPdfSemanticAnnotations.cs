// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Reading;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Engine;

/// <summary>Describes the annotations of a page that carry a description, such as a note, a link's alternate text or a stamp.</summary>
internal static class HyperPdfSemanticAnnotations
{
    /// <summary>The most annotations read for a page.</summary>
    private const int MaxAnnotations = 1024;

    /// <summary>Adds the annotations of a page that have a description.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="items">Receives the items.</param>
    internal static void Add(HyperPdfDocument self, int pageIndex, List<PageSemanticItem> items)
    {
        var page = PdfDocumentPages.GetPage(self.Document, pageIndex);
        if (PdfPageAnnotations.GetArray(self.Document.Objects, page) is not { } annotations)
        {
            return;
        }

        var count = Math.Min(annotations.Count, MaxAnnotations);
        for (var i = 0; i < count; i++)
        {
            if (annotations.GetDictionary(i) is { } annotation && Describe(self.Document, page, annotation) is { } item)
            {
                items.Add(item);
            }
        }
    }

    /// <summary>Gets the words for a kind of annotation.</summary>
    /// <param name="subtype">The annotation subtype.</param>
    /// <returns>The role, such as "link".</returns>
    internal static string RoleOf(KnownName subtype) => subtype switch
    {
        KnownName.Text => "note",
        KnownName.Link => "link",
        KnownName.FreeText => "text box",
        KnownName.Highlight => "highlight",
        KnownName.Underline => "underline",
        KnownName.StrikeOut => "strike-out",
        KnownName.Squiggly => "squiggly underline",
        KnownName.Stamp => "stamp",
        KnownName.Caret => "insertion point",
        KnownName.Ink => "drawing",
        KnownName.FileAttachment => "file attachment",
        _ => "annotation",
    };

    /// <summary>Describes one annotation, or nothing for form widgets, pop-ups and annotations with no description.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The item, or <see langword="null"/>.</returns>
    private static PageSemanticItem? Describe(PdfDocument document, PdfPage page, PdfDictionary annotation)
    {
        var subtype = annotation.GetName(KnownName.Subtype).ToKnownName();
        return subtype is KnownName.Widget or KnownName.Popup || annotation.GetText(KnownName.Contents) is not { Length: > 0 } description
            ? null
            : new PageSemanticItem(PageSemanticKind.Annotation, description, HyperPdfAnnotationReading.ToPageRect(page, PdfAnnotations.GetRectangle(annotation)))
            {
                Role = RoleOf(subtype),
                IsHidden = HyperPdfPageSemantics.IsHidden(document, annotation),
            };
    }
}
