// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Reading;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Media;
using HyperPdfLibrary.Media.U3d;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Engine;

/// <summary>Describes the annotations of a page that carry a description, such as a note, a link's alternate text or a stamp.</summary>
internal static class HyperPdfSemanticAnnotations
{
    /// <summary>The most annotations read for a page.</summary>
    private const int MaxAnnotations = 1024;

    /// <summary>The largest 3D stream read for its description, in encoded bytes.</summary>
    private const int MaxModelBytes = 64 * 1024 * 1024;

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

        var models = ThreeDStreams(self.Document, page);
        var modelAt = 0;
        var count = Math.Min(annotations.Count, MaxAnnotations);
        for (var i = 0; i < count; i++)
        {
            if (annotations.GetDictionary(i) is not { } annotation)
            {
                continue;
            }

            var isModel = annotation.GetName(KnownName.Subtype).ToKnownName() == KnownName.ThreeD;
            var item = isModel ? DescribeModel(self.Document, page, annotation, modelAt < models.Count ? models[modelAt] : null) : Describe(self.Document, page, annotation);
            modelAt += isModel ? 1 : 0;
            if (item is not null)
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

    /// <summary>Gets the 3D streams of a page's 3D annotations, in annotation order.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <returns>One entry per 3D annotation; <see langword="null"/> where the annotation has no 3D stream.</returns>
    private static List<Pdf3DStream?> ThreeDStreams(PdfDocument document, PdfPage page)
    {
        var streams = new List<Pdf3DStream?>();
        foreach (var multimedia in PdfDocumentMedia.GetMultimediaAnnotations(document, page))
        {
            if (multimedia.Kind == PdfMultimediaKind.ThreeD)
            {
                streams.Add(multimedia.ThreeD?.Stream);
            }
        }

        return streams;
    }

    /// <summary>Describes a 3D annotation: its own description when it has one, and what the model declares.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="model">The annotation's 3D stream, or <see langword="null"/>.</param>
    /// <returns>The item.</returns>
    private static PageSemanticItem DescribeModel(PdfDocument document, PdfPage page, PdfDictionary annotation, Pdf3DStream? model) =>
        new(
            PageSemanticKind.Annotation,
            annotation.GetText(KnownName.Contents) is { Length: > 0 } description ? description : "3D model",
            HyperPdfAnnotationReading.ToPageRect(page, PdfAnnotations.GetRectangle(annotation)))
        {
            Role = "3D model",
            Description = SummaryOf(model),
            IsHidden = HyperPdfPageSemantics.IsHidden(document, annotation),
        };

    /// <summary>Describes what a 3D stream holds, reading only the structure of a U3D file.</summary>
    /// <param name="model">The 3D stream, or <see langword="null"/>.</param>
    /// <returns>The description, or <see langword="null"/> when the stream is not U3D or is too large to read.</returns>
    private static string? SummaryOf(Pdf3DStream? model) =>
        model is { Subtype: "U3D" } && model.Data.RawLength <= MaxModelBytes ? U3dSummary.Describe(U3dReader.Read(model.Data.DecodeToArray())) : null;

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
