// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Annotations;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;
namespace HyperPdf.Engine;

/// <summary>Performs HyperPdfAnnotationStrokes annotation operations.</summary>
internal static class HyperPdfAnnotationStrokes
{
    /// <summary>The line width used when an annotation does not record one.</summary>
    internal const float DefaultLineWidth = 1;

    /// <summary>The length of an arrow's head as a multiple of the line width, never shorter than <see cref="MinArrowHead"/>.</summary>
    internal const float ArrowHeadWidths = 4;

    /// <summary>The shortest arrow head in points.</summary>
    internal const float MinArrowHead = 8;

    /// <summary>The points of an arrow: tail to point, then the two sides of the head.</summary>
    internal const int ArrowPoints = 6;

    /// <summary>The strokes of an arrow: the shaft and the two sides of its head.</summary>
    internal const int ArrowStrokes = 3;

    /// <summary>The index of an arrow's first head side's far end.</summary>
    internal const int ArrowFirstSide = 3;

    /// <summary>The index of the second head side's start.</summary>
    internal const int ArrowSecondStart = 4;

    /// <summary>The index of the second head side's far end.</summary>
    internal const int ArrowSecondSide = 5;

    /// <summary>The index of the first head side's start.</summary>
    internal const int ArrowFirstStart = 2;

    /// <summary>The entries of a freehand highlight's graphics state: both opacities and the blend mode.</summary>
    private const int HighlighterStateEntries = 3;

    /// <summary>Gets the name of the graphics state a freehand highlight's appearance uses.</summary>
    private static ReadOnlySpan<byte> HighlighterState => "HL"u8;

    /// <summary>Gets an arrow head's length for a line width.</summary>
    /// <param name="width">The line width.</param>
    /// <returns>The length.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float ArrowHead(float width) => Math.Max(MinArrowHead, ArrowHeadWidths * width);

    /// <summary>Rebuilds an arrow's head from its shaft (the first stroke), sized to the line width.</summary>
    /// <param name="points">The arrow's points.</param>
    /// <param name="lengths">The stroke lengths.</param>
    /// <param name="width">The line width.</param>
    internal static void ShapeArrowHead(Span<Vector2> points, ReadOnlySpan<int> lengths, float width)
    {
        if (lengths.Length != ArrowStrokes || points.Length != ArrowPoints)
        {
            return;
        }

        var tip = points[1];
        var (first, second) = PdfAppearances.OpenArrowHead(tip, points[0], ArrowHead(width));
        points[ArrowFirstStart] = tip;
        points[ArrowFirstSide] = first;
        points[ArrowSecondStart] = tip;
        points[ArrowSecondSide] = second;
    }

    /// <summary>Reads an annotation's strokes: its ink list, or its vertices as one stroke.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="path">Receives the points.</param>
    internal static void ReadStrokes(PdfDictionary annotation, ref PdfStrokeBuffer path)
    {
        if (annotation.IsName(KnownName.Subtype, KnownName.Ink))
        {
            _ = PdfAnnotations.ReadInkList(annotation, ref path);
            return;
        }

        _ = PdfAnnotations.ReadPoints(annotation, KnownName.Vertices, ref path);
    }

    /// <summary>Writes an annotation's moved strokes back: its ink list, or its vertices.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="path">The points.</param>
    internal static void WriteStrokes(PdfDictionary annotation, ref PdfStrokeBuffer path)
    {
        if (annotation.IsName(KnownName.Subtype, KnownName.Ink))
        {
            PdfAnnotations.SetInkList(annotation, path.Points, path.Lengths);
            return;
        }

        PdfAnnotations.SetPoints(annotation, KnownName.Vertices, path.Points);
    }

    /// <summary>Adds the path of a kind of stroke: a cloud's scallops, a closed polygon, or open strokes.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="path">The points.</param>
    /// <param name="width">The line width.</param>
    internal static void AddPath(ref PdfContentBuilder builder, AnnotationKind kind, ref PdfStrokeBuffer path, float width)
    {
        switch (kind)
        {
            case AnnotationKind.Cloud:
                {
                    PdfAppearances.AddCloud(ref builder, path.Points, width);
                    break;
                }

            case AnnotationKind.Polygon:
                {
                    PdfAppearances.AddStrokes(ref builder, path.Points, path.Lengths);
                    builder.ClosePath();
                    break;
                }

            default:
                {
                    PdfAppearances.AddStrokes(ref builder, path.Points, path.Lengths);
                    break;
                }
        }
    }

    /// <summary>Moves and draws again a drawing, line, arrow or polygon point by point.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="kind">Its kind.</param>
    /// <param name="map">The move, or <see cref="RectangleMap.Identity"/> to draw again in place.</param>
    /// <returns><see langword="true"/> when moved.</returns>
    internal static bool MoveStrokes(HyperPdfAnnotations annotationState, PdfDictionary annotation, AnnotationKind kind, in RectangleMap map)
    {
        var path = default(PdfStrokeBuffer);
        try
        {
            ReadStrokes(annotation, ref path);
            if (path.PointCount == 0)
            {
                return false;
            }

            map.Apply(path.Points);
            var width = PdfAnnotations.GetBorderWidth(annotation);
            if (kind == AnnotationKind.Arrow)
            {
                ShapeArrowHead(path.Points, path.Lengths, width);
            }

            WriteStrokes(annotation, ref path);
            Redraw(annotationState, annotation, kind, ref path, HyperPdfAnnotationKinds.GetColor(annotationState, annotation, kind), width);
            return true;
        }
        finally
        {
            path.Dispose();
        }
    }

    /// <summary>Draws a drawing, line, arrow or polygon again in a colour and width, reading its points.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="kind">Its kind.</param>
    /// <param name="color">The colour.</param>
    /// <returns><see langword="true"/> when drawn.</returns>
    internal static bool Redraw(HyperPdfAnnotations annotationState, PdfDictionary annotation, AnnotationKind kind, uint color)
    {
        var path = default(PdfStrokeBuffer);
        try
        {
            ReadStrokes(annotation, ref path);
            if (path.PointCount == 0)
            {
                return false;
            }

            Redraw(annotationState, annotation, kind, ref path, color, PdfAnnotations.GetBorderWidth(annotation));
            return true;
        }
        finally
        {
            path.Dispose();
        }
    }

    /// <summary>Sizes the annotation to its points and gives it an appearance stream drawing them.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="kind">Its kind, which decides how the points are joined.</param>
    /// <param name="path">The points in user space.</param>
    /// <param name="color">The colour.</param>
    /// <param name="width">The line width.</param>
    internal static void Redraw(HyperPdfAnnotations annotationState, PdfDictionary annotation, AnnotationKind kind, ref PdfStrokeBuffer path, uint color, float width)
    {
        if (width <= 0)
        {
            width = DefaultLineWidth;
        }

        var bounds = path.GetBounds();
        var margin = kind == AnnotationKind.Cloud ? PdfAppearances.CloudMargin(width) : width;
        var rectangle = new PdfRectangle(bounds.Left - margin, bounds.Bottom - margin, bounds.Right + margin, bounds.Top + margin);
        PdfAnnotations.SetRectangle(annotation, rectangle);
        var builder = default(PdfContentBuilder);
        try
        {
            builder.SaveState();
            var resources = UseHighlighterState(annotationState, annotation, kind, ref builder);
            PdfAppearances.SetColors(ref builder, color);
            PdfAppearances.SetRoundLine(ref builder, width);
            AddPath(ref builder, kind, ref path, width);
            builder.Stroke();
            builder.RestoreState();
            _ = PdfAnnotations.SetNormalAppearance(annotationState.Store, annotation, builder.ToFormXObject(annotationState.Store, rectangle, resources));
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>
    /// For a freehand highlight, applies a see-through multiplying graphics state to the appearance so the text under the
    /// pen stays readable, using the annotation's own constant opacity.
    /// </summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="kind">Its kind.</param>
    /// <param name="builder">The appearance being written.</param>
    /// <returns>The resources the appearance needs, or <see langword="null"/> for any other kind.</returns>
    private static PdfDictionary? UseHighlighterState(HyperPdfAnnotations annotationState, PdfDictionary annotation, AnnotationKind kind, ref PdfContentBuilder builder)
    {
        if (kind != AnnotationKind.FreehandHighlight)
        {
            return null;
        }

        var store = annotationState.Store;
        var opacity = annotation.Get(KnownName.CA) is { IsNumber: true } alpha ? (float)alpha.AsNumber() : HyperPdfAnnotationShapes.HighlighterOpacity;
        var state = new PdfDictionary(store, HighlighterStateEntries);
        state.Set(KnownName.CA, PdfNumber.ToValue(opacity));
        state.Set(KnownName.FillAlpha, PdfNumber.ToValue(opacity));
        state.Set(KnownName.BM, PdfValue.FromName(KnownName.Multiply));
        var states = new PdfDictionary(store, 1);
        states.Set(store.Names.Intern(HighlighterState), PdfValue.FromDictionary(state));
        var resources = new PdfDictionary(store, 1);
        resources.Set(KnownName.ExtGState, PdfValue.FromDictionary(states));
        builder.SetGraphicsState(HighlighterState);
        return resources;
    }
}
