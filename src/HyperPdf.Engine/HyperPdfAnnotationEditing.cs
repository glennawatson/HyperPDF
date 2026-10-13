// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
namespace HyperPdf.Engine;

/// <summary>Performs HyperPdfAnnotationEditing annotation operations.</summary>
internal static class HyperPdfAnnotationEditing
{
    /// <summary>Changes an annotation's colour.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The annotation index.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <returns>
    /// <see langword="true" /> when changed.</returns>
    internal static bool SetColor(HyperPdfAnnotations annotationState, int pageIndex, int index, uint color)
    {
        lock (annotationState.Gate)
        {
            if (!HyperPdfAnnotationReading.TryEdit(annotationState, pageIndex, index, out var page, out var annotation))
            {
                return false;
            }

            var subtype = annotation.GetName(KnownName.Subtype).ToKnownName();
            var changed = subtype switch
            {
                KnownName.Stamp => RecolorAppearance(annotationState, annotation, color),
                KnownName.FreeText => IsWrittenHere(annotationState, annotation) && RecolorAppearance(annotationState, annotation, color),
                KnownName.Ink or KnownName.Polygon or KnownName.PolyLine => RecolorStrokes(annotationState, annotation, color),
                KnownName.Redact => false,
                _ => RecolorEntries(annotation, color),
            };
            return changed && HyperPdfAnnotationReading.Commit(annotationState, pageIndex, page, index, annotation);
        }
    }

    /// <summary>
    /// Moves or resizes an annotation. Drawings, lines and polygons are redrawn through their moved points; other
    /// annotations scale their appearance into the new bounds. Text markup follows its text and cannot be moved.
    /// </summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The annotation index.</param>
    /// <param name="bounds">The new bounds in page space.</param>
    /// <returns>
    /// <see langword="true" /> when changed.</returns>
    internal static bool SetBounds(HyperPdfAnnotations annotationState, int pageIndex, int index, PageRect bounds)
    {
        lock (annotationState.Gate)
        {
            if (!HyperPdfAnnotationReading.TryEdit(annotationState, pageIndex, index, out var page, out var annotation) || HyperPdfAnnotationKinds.GetKind(
                annotationState,
                annotation) is not { } kind || !HyperPdfAnnotationKinds.IsEditable(annotationState, annotation, kind))
            {
                return false;
            }

            var map = new RectangleMap(PdfAnnotations.GetRectangle(annotation), HyperPdfAnnotationReading.ToUserRectangle(page, bounds));
            if (!Move(annotationState, annotation, kind, map))
            {
                return false;
            }

            HyperPdfAnnotationReading.SetModified(annotationState, annotation);
            return HyperPdfAnnotationReading.Commit(annotationState, pageIndex, page, index, annotation);
        }
    }

    /// <summary>Changes the line width of a drawing, shape, line or polygon.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The annotation index.</param>
    /// <param name="width">The line width in points.</param>
    /// <returns>
    /// <see langword="true" /> when changed.</returns>
    internal static bool SetLineWidth(HyperPdfAnnotations annotationState, int pageIndex, int index, float width)
    {
        if (width <= 0)
        {
            return false;
        }

        lock (annotationState.Gate)
        {
            if (!HyperPdfAnnotationReading.TryEdit(annotationState, pageIndex, index, out var page, out var annotation) || HyperPdfAnnotationKinds.GetKind(
                annotationState,
                annotation) is not { } kind || !HyperPdfAnnotationKinds.IsEditable(annotationState, annotation, kind))
            {
                return false;
            }

            if (!Restyle(annotationState, annotation, kind, width / page.UserUnit))
            {
                return false;
            }

            HyperPdfAnnotationReading.SetModified(annotationState, annotation);
            return HyperPdfAnnotationReading.Commit(annotationState, pageIndex, page, index, annotation);
        }
    }

    /// <summary>Recolours an annotation readers draw from its entries, dropping the old appearance.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="color">The colour.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    internal static bool RecolorEntries(PdfDictionary annotation, uint color)
    {
        _ = PdfAnnotations.RemoveAppearance(annotation);
        PdfAnnotations.SetColor(annotation, KnownName.C, color);
        return true;
    }

    /// <summary>Changes a line width: strokes are drawn again, shapes are left for readers to draw.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="kind">Its kind.</param>
    /// <param name="width">The width.</param>
    /// <returns><see langword="true"/> when changed.</returns>
    internal static bool Restyle(HyperPdfAnnotations annotationState, PdfDictionary annotation, AnnotationKind kind, float width)
    {
        switch (annotation.GetName(KnownName.Subtype).ToKnownName())
        {
            case KnownName.Ink or KnownName.Polygon or KnownName.PolyLine:
                {
                    PdfAnnotations.SetBorderWidth(annotation, width);
                    return HyperPdfAnnotationStrokes.MoveStrokes(annotationState, annotation, kind, RectangleMap.Identity);
                }

            case KnownName.Square or KnownName.Circle:
                {
                    PdfAnnotations.SetBorderWidth(annotation, width);
                    _ = PdfAnnotations.RemoveAppearance(annotation);
                    return true;
                }

            default:
                {
                    return false;
                }
        }
    }

    /// <summary>Moves an annotation the way its kind needs.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="kind">Its kind.</param>
    /// <param name="map">The move.</param>
    /// <returns><see langword="true"/> when moved.</returns>
    internal static bool Move(HyperPdfAnnotations annotationState, PdfDictionary annotation, AnnotationKind kind, in RectangleMap map)
    {
        switch (annotation.GetName(KnownName.Subtype).ToKnownName())
        {
            case KnownName.Ink or KnownName.Polygon or KnownName.PolyLine:
                {
                    return HyperPdfAnnotationStrokes.MoveStrokes(annotationState, annotation, kind, map);
                }

            case KnownName.Square or KnownName.Circle or KnownName.Text:
                {
                    PdfAnnotations.SetRectangle(annotation, map.Target);
                    _ = PdfAnnotations.RemoveAppearance(annotation);
                    return true;
                }

            case KnownName.Redact:
                {
                    return HyperPdfAnnotationRedaction.MoveRedaction(annotationState, annotation, map);
                }

            default:
                {
                    PdfAnnotations.SetRectangle(annotation, map.Target);
                    HyperPdfAnnotationCallouts.MoveCallout(annotationState, annotation, map);
                    return true;
                }
        }
    }

    /// <summary>Recolours an annotation whose appearance is kept: every colour it draws with becomes the new one.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="color">The colour.</param>
    /// <returns><see langword="true"/> when its appearance was recoloured.</returns>
    internal static bool RecolorAppearance(HyperPdfAnnotations annotationState, PdfDictionary annotation, uint color)
    {
        if (PdfAnnotations.GetNormalAppearance(annotation) is not { } appearance || PdfAppearanceColors.Recolor(appearance, color) is not { } recolored)
        {
            return false;
        }

        _ = PdfAnnotations.SetNormalAppearance(annotationState.Store, annotation, recolored);
        PdfAnnotations.SetColor(annotation, KnownName.C, color);
        HyperPdfAnnotationReading.SetModified(annotationState, annotation);
        return true;
    }

    /// <summary>Recolours strokes, drawing them again so they still print.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="color">The colour.</param>
    /// <returns><see langword="true"/> when drawn again.</returns>
    internal static bool RecolorStrokes(HyperPdfAnnotations annotationState, PdfDictionary annotation, uint color)
    {
        PdfAnnotations.SetColor(annotation, KnownName.C, color);
        if (!HyperPdfAnnotationStrokes.Redraw(annotationState, annotation, HyperPdfAnnotationKinds.GetKind(annotationState, annotation) ?? AnnotationKind.Ink, color))
        {
            return false;
        }

        HyperPdfAnnotationReading.SetModified(annotationState, annotation);
        return true;
    }

    /// <summary>Determines whether free text was written by this viewer, which keeps its text to lay it out again.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="annotation">The annotation.</param>
    /// <returns><see langword="true"/> when written here.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsWrittenHere(HyperPdfAnnotations annotationState, PdfDictionary annotation) => PdfAnnotations.HasText(annotation, annotationState.Names.Text);
}
