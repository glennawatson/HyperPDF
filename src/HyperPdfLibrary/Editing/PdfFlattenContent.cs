// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Draws annotation appearances into a page's content. The page keeps its own streams, wrapped so the drawings start from
/// a clean graphics state. Each appearance is a form drawn under a matrix that fits it to its annotation's rectangle, inside
/// the annotation's optional content, and marked as content of the annotation's structure element or as an artifact. The
/// annotation's CA entry is not applied, because it only governs regenerated appearances and an appearance stream sets its
/// own transparency; its BM entry is applied as a blend mode set before the form is drawn (ISO 32000-2 8.10, 8.11.3, 11.3.5,
/// 12.5.2, 12.5.5, 14.6, 14.8).
/// </summary>
internal static class PdfFlattenContent
{
    /// <summary>Gets the tag of marked content that is not part of the logical structure.</summary>
    private static ReadOnlySpan<byte> ArtifactTag => "Artifact"u8;

    /// <summary>Gets the tag of marked content that belongs to an optional content group.</summary>
    private static ReadOnlySpan<byte> LayerTag => "OC"u8;

    /// <summary>Gets the tag given to content whose structure type cannot be written as a plain name.</summary>
    private static ReadOnlySpan<byte> FallbackTag => "Span"u8;

    /// <summary>Draws appearances after a page's content.</summary>
    /// <param name="session">The flatten.</param>
    /// <param name="page">The page as it was before the edit, for its inherited resources.</param>
    /// <param name="copy">The copy of the page dictionary being changed.</param>
    /// <param name="draws">The appearances to draw, in order.</param>
    internal static void Add(PdfFlattenSession session, PdfPage page, PdfDictionary copy, List<PdfFlattenDraw> draws)
    {
        var store = session.Document.Objects;
        var resources = new PdfFlattenResources(store, copy.GetDictionary(KnownName.Resources) ?? page.Resources);
        var builder = default(PdfContentBuilder);
        try
        {
            // The page's content ends in the state it left; the Q closes the q put before it, so the drawings start clean.
            builder.RestoreState();
            foreach (var draw in draws)
            {
                session.CancellationToken.ThrowIfCancellationRequested();
                WriteDraw(ref builder, store.Names, resources, draw);
            }

            copy.Set(KnownName.Resources, PdfValue.FromDictionary(resources.Finish()));
            PdfPageStamping.Wrap(session.Transaction, store, copy, "q\n"u8, builder.WrittenSpan);
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Writes the operators that draw one appearance.</summary>
    /// <param name="builder">The content being written.</param>
    /// <param name="names">The document's name table.</param>
    /// <param name="resources">The resources the drawing is added to.</param>
    /// <param name="draw">The appearance.</param>
    private static void WriteDraw(ref PdfContentBuilder builder, PdfNameTable names, PdfFlattenResources resources, in PdfFlattenDraw draw)
    {
        var layerName = draw.Layer.IsNull ? default : resources.UseLayer(draw.Layer);
        builder.SaveState();
        WriteMarking(ref builder, names, draw.Marking);
        if (!layerName.IsNone)
        {
            builder.BeginMarkedContent(LayerTag, names.GetSpelling(layerName));
        }

        if (!draw.Blend.IsNone)
        {
            builder.SetGraphicsState(names.GetSpelling(resources.UseBlend(draw.Blend)));
        }

        WriteForm(ref builder, names, resources, draw.Form, draw.Overlay is not null);
        if (draw.Overlay is { } overlay)
        {
            WriteForm(ref builder, names, resources, overlay, true);
        }

        if (!layerName.IsNone)
        {
            builder.EndMarkedContent();
        }

        if (draw.Marking.Kind != PdfFlattenMarkingKind.None)
        {
            builder.EndMarkedContent();
        }

        builder.RestoreState();
    }

    /// <summary>Writes the operators that fit a form to its rectangle and draw it.</summary>
    /// <param name="builder">The content being written.</param>
    /// <param name="names">The document's name table.</param>
    /// <param name="resources">The resources the form is added to.</param>
    /// <param name="form">The form.</param>
    /// <param name="isolate">Whether the matrix is kept from the drawing that follows, in its own saved graphics state.</param>
    private static void WriteForm(ref PdfContentBuilder builder, PdfNameTable names, PdfFlattenResources resources, in PdfFlattenForm form, bool isolate)
    {
        var name = resources.UseForm(form.Form);
        if (isolate)
        {
            builder.SaveState();
        }

        var m = form.Mapping;
        builder.Transform(m.M11, m.M12, m.M21, m.M22, m.M31, m.M32);
        builder.DrawXObject(names.GetSpelling(name));
        if (isolate)
        {
            builder.RestoreState();
        }
    }

    /// <summary>Writes the operator that opens the marked content of a drawing.</summary>
    /// <param name="builder">The content being written.</param>
    /// <param name="names">The document's name table.</param>
    /// <param name="marking">How the drawing is marked.</param>
    private static void WriteMarking(ref PdfContentBuilder builder, PdfNameTable names, in PdfFlattenMarking marking)
    {
        switch (marking.Kind)
        {
            case PdfFlattenMarkingKind.Artifact:
            {
                builder.BeginMarkedContent(ArtifactTag);
                break;
            }

            case PdfFlattenMarkingKind.Content:
            {
                WriteContentMarking(ref builder, names, marking.Tag, marking.Mcid);
                break;
            }

            default:
            {
                break;
            }
        }
    }

    /// <summary>Writes <c>/Tag &lt;&lt;/MCID n&gt;&gt; BDC</c>.</summary>
    /// <param name="builder">The content being written.</param>
    /// <param name="names">The document's name table.</param>
    /// <param name="tag">The structure type of the owning element.</param>
    /// <param name="mcid">The marked content id.</param>
    private static void WriteContentMarking(ref PdfContentBuilder builder, PdfNameTable names, PdfName tag, int mcid)
    {
        var properties = new PdfDictionary(null);
        properties.Set(KnownName.MCID, PdfValue.FromInteger(mcid));
        builder.BeginMarkedContent(tag.IsNone ? FallbackTag : names.GetSpelling(tag), properties, names);
    }
}
