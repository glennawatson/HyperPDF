// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Numerics;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
namespace HyperPdf.Engine;

/// <summary>Performs HyperPdfAnnotationShapes annotation operations.</summary>
internal static class HyperPdfAnnotationShapes
{
    /// <summary>The size of a sticky note icon in points.</summary>
    internal const float NoteSize = 20;

    /// <summary>Halves a line width for the margin around a rectangle or ellipse.</summary>
    internal const float Half = 0.5F;

    /// <summary>The corners of one marked line.</summary>
    internal const int QuadCorners = 4;

    /// <summary>The points converted on the stack before a pooled buffer is used.</summary>
    internal const int StackPoints = 128;

    /// <summary>The points in one straight stroke.</summary>
    internal const int StrokePoints = 2;

    /// <summary>The fewest points of a closed shape.</summary>
    internal const int MinPolygonPoints = 3;

    /// <summary>The fewest points of a run of lines.</summary>
    internal const int MinPolyLinePoints = 2;

    /// <summary>The index of the bottom-left corner of a quadrilateral.</summary>
    internal const int QuadBottomLeft = 2;

    /// <summary>The index of the bottom-right corner of a quadrilateral.</summary>
    internal const int QuadBottomRight = 3;

    /// <summary>Marks text with a highlight, underline, strike-out or squiggly line.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="kind">One of the text markup kinds.</param>
    /// <param name="lines">The line rectangles to mark, for example from a text selection.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <param name="contents">An optional note, or an empty string.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    internal static int AddMarkup(HyperPdfAnnotations annotationState, int pageIndex, AnnotationKind kind, ReadOnlySpan<PageRect> lines, uint color, string contents)
    {
        ArgumentNullException.ThrowIfNull(contents);
        if (kind == AnnotationKind.Redaction)
        {
            return HyperPdfAnnotationRedaction.AddRedaction(annotationState, pageIndex, lines);
        }

        var subtype = kind switch
        {
            AnnotationKind.Highlight => KnownName.Highlight,
            AnnotationKind.Underline => KnownName.Underline,
            AnnotationKind.StrikeOut => KnownName.StrikeOut,
            AnnotationKind.Squiggly => KnownName.Squiggly,
            _ => KnownName.None,
        };
        if (subtype == KnownName.None || lines.IsEmpty)
        {
            return -1;
        }

        lock (annotationState.Gate)
        {
            return HyperPdfAnnotationReading.GetPage(annotationState, pageIndex) is { } page ? AddMarkup(annotationState, pageIndex, page, subtype, lines, color, contents) : -1;
        }
    }

    /// <summary>Adds text markup over the given lines.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="page">The page.</param>
    /// <param name="subtype">The markup subtype.</param>
    /// <param name="lines">The marked lines.</param>
    /// <param name="color">The colour.</param>
    /// <param name="contents">The note text.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    internal static int AddMarkup(HyperPdfAnnotations annotationState, int pageIndex, PdfPage page, KnownName subtype, ReadOnlySpan<PageRect> lines, uint color, string contents)
    {
        var count = lines.Length * QuadCorners;
        Vector2[]? rented = null;
        var corners = count <= StackPoints ? stackalloc Vector2[StackPoints] : (rented = ArrayPool<Vector2>.Shared.Rent(count));
        try
        {
            var bounds = default(UserBounds);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var quad = corners.Slice(i * QuadCorners, QuadCorners);
                quad[0] = HyperPdfAnnotationReading.ToUser(page, new(line.Left, line.Top));
                quad[1] = HyperPdfAnnotationReading.ToUser(page, new(line.Right, line.Top));
                quad[QuadBottomLeft] = HyperPdfAnnotationReading.ToUser(page, new(line.Left, line.Bottom));
                quad[QuadBottomRight] = HyperPdfAnnotationReading.ToUser(page, new(line.Right, line.Bottom));
                foreach (var corner in quad)
                {
                    bounds.Add(corner);
                }
            }

            var markup = PdfAnnotations.Create(annotationState.Store, subtype, bounds.ToRectangle(0));
            PdfAnnotations.SetQuadPoints(markup, corners[..count]);
            return HyperPdfAnnotationReading.Add(annotationState, pageIndex, page, markup, color, contents, []);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<Vector2>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Adds a freehand drawing, or a drawn signature.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="points">Every point, stroke after stroke.</param>
    /// <param name="strokeLengths">The number of points in each stroke.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <param name="width">The line width in points.</param>
    /// <param name="kind">
    /// <see cref="F:HyperPdf.Core.Annotations.AnnotationKind.Ink" /> or <see cref="F:HyperPdf.Core.Annotations.AnnotationKind.Signature" />.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    internal static int AddInk(HyperPdfAnnotations annotationState, int pageIndex, ReadOnlySpan<PagePoint> points, ReadOnlySpan<int> strokeLengths, uint color, float width, AnnotationKind kind)
    {
        if (points.IsEmpty || strokeLengths.IsEmpty)
        {
            return -1;
        }

        lock (annotationState.Gate)
        {
            var inkKind = kind == AnnotationKind.Signature ? AnnotationKind.Signature : AnnotationKind.Ink;
            return HyperPdfAnnotationReading.GetPage(annotationState, pageIndex) is { } page ? AddStrokes(
                annotationState,
                pageIndex,
                page,
                points,
                strokeLengths,
                new(KnownName.Ink, inkKind, color, width)) : -1;
        }
    }

    /// <summary>Adds a sticky note.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="location">The top-left corner of the note icon.</param>
    /// <param name="contents">The note text.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    internal static int AddNote(HyperPdfAnnotations annotationState, int pageIndex, PagePoint location, string contents, uint color)
    {
        ArgumentNullException.ThrowIfNull(contents);
        lock (annotationState.Gate)
        {
            if (HyperPdfAnnotationReading.GetPage(annotationState, pageIndex) is not { } page)
            {
                return -1;
            }

            var bounds = default(UserBounds);
            bounds.Add(HyperPdfAnnotationReading.ToUser(page, location));
            bounds.Add(HyperPdfAnnotationReading.ToUser(page, new(location.X + NoteSize, location.Y + NoteSize)));
            var note = PdfAnnotations.Create(annotationState.Store, KnownName.Text, bounds.ToRectangle(0));
            return HyperPdfAnnotationReading.Add(annotationState, pageIndex, page, note, color, contents, []);
        }
    }

    /// <summary>Draws a rectangle, ellipse, arrow or line between two points.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="kind">A rectangle, ellipse, arrow or line.</param>
    /// <param name="start">Where the drag started: a corner, or the arrow's tail.</param>
    /// <param name="end">Where the drag ended: the opposite corner, or the arrow's point.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <param name="width">The line width in points.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    internal static int AddShape(HyperPdfAnnotations annotationState, int pageIndex, AnnotationKind kind, PagePoint start, PagePoint end, uint color, float width)
    {
        if (kind == AnnotationKind.Redaction)
        {
            return HyperPdfAnnotationRedaction.AddRedaction(annotationState, pageIndex, start, end);
        }

        lock (annotationState.Gate)
        {
            return HyperPdfAnnotationReading.GetPage(annotationState, pageIndex) is not { } page ? -1 : kind switch
            {
                AnnotationKind.Rectangle => AddBox(annotationState, pageIndex, page, start, end, new(KnownName.Square, kind, color, width)),
                AnnotationKind.Ellipse => AddBox(annotationState, pageIndex, page, start, end, new(KnownName.Circle, kind, color, width)),
                AnnotationKind.Arrow => AddArrow(annotationState, pageIndex, page, start, end, new(KnownName.Ink, kind, color, width)),
                AnnotationKind.Line => AddStrokes(annotationState, pageIndex, page, [start, end], [StrokePoints], new(KnownName.Ink, kind, color, width)),
                _ => -1,
            };
        }
    }

    /// <summary>Draws a polygon, a cloud or a run of lines through points.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="kind">A polygon, cloud or polyline.</param>
    /// <param name="vertices">The corners in order; a polygon or cloud closes back to the first.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <param name="width">The line width in points.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    internal static int AddPolygon(HyperPdfAnnotations annotationState, int pageIndex, AnnotationKind kind, ReadOnlySpan<PagePoint> vertices, uint color, float width)
    {
        var subtype = kind switch
        {
            AnnotationKind.Polygon or AnnotationKind.Cloud => KnownName.Polygon,
            AnnotationKind.PolyLine => KnownName.PolyLine,
            _ => KnownName.None,
        };
        var fewest = subtype == KnownName.PolyLine ? MinPolyLinePoints : MinPolygonPoints;
        if (subtype == KnownName.None || vertices.Length < fewest || width <= 0)
        {
            return -1;
        }

        lock (annotationState.Gate)
        {
            return HyperPdfAnnotationReading.GetPage(annotationState, pageIndex) is { } page ? AddStrokes(
                annotationState,
                pageIndex,
                page,
                vertices,
                [vertices.Length],
                new(subtype, kind, color, width)) : -1;
        }
    }

    /// <summary>Gets the subject that marks a kind drawn as strokes.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The subject, or empty for freehand ink.</returns>
    internal static ReadOnlySpan<byte> StrokeSubject(AnnotationKind kind) => kind switch
    {
        AnnotationKind.Signature => HyperPdfAnnotationKinds.GetSignatureSubject(),
        AnnotationKind.Arrow => HyperPdfAnnotationKinds.GetArrowSubject(),
        AnnotationKind.Line => HyperPdfAnnotationKinds.GetLineSubject(),
        AnnotationKind.Polygon => HyperPdfAnnotationKinds.GetPolygonSubject(),
        AnnotationKind.Cloud => HyperPdfAnnotationKinds.GetCloudSubject(),
        AnnotationKind.PolyLine => HyperPdfAnnotationKinds.GetPolyLineSubject(),
        _ => [],
    };

    /// <summary>Converts strokes to user space, stopping at the first stroke that runs past the points.</summary>
    /// <param name="page">The page.</param>
    /// <param name="points">The points in page space.</param>
    /// <param name="lengths">The number of points in each stroke.</param>
    /// <param name="path">Receives the strokes.</param>
    internal static void ConvertStrokes(PdfPage page, ReadOnlySpan<PagePoint> points, ReadOnlySpan<int> lengths, ref PdfStrokeBuffer path)
    {
        var offset = 0;
        foreach (var length in lengths)
        {
            if (length <= 0 || offset + length > points.Length)
            {
                return;
            }

            foreach (var point in points.Slice(offset, length))
            {
                path.Add(HyperPdfAnnotationReading.ToUser(page, point));
            }

            path.EndStroke();
            offset += length;
        }
    }

    /// <summary>Adds a rectangle or ellipse filling the box between two corners.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="page">The page.</param>
    /// <param name="start">One corner.</param>
    /// <param name="end">The opposite corner.</param>
    /// <param name="style">Square or Circle, the colour and the line width.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    internal static int AddBox(HyperPdfAnnotations annotationState, int pageIndex, PdfPage page, PagePoint start, PagePoint end, StrokeStyle style)
    {
        var bounds = default(UserBounds);
        bounds.Add(HyperPdfAnnotationReading.ToUser(page, start));
        bounds.Add(HyperPdfAnnotationReading.ToUser(page, end));
        var box = PdfAnnotations.Create(annotationState.Store, style.Subtype, bounds.ToRectangle(style.Width * Half));
        PdfAnnotations.SetBorderWidth(box, style.Width);
        return HyperPdfAnnotationReading.Add(annotationState, pageIndex, page, box, style.Color, string.Empty, []);
    }

    /// <summary>Adds an arrow: a shaft from tail to point and two short strokes forming the head.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="page">The page.</param>
    /// <param name="tailPoint">The tail.</param>
    /// <param name="tipPoint">The point.</param>
    /// <param name="style">The ink subtype, the colour and the line width.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    internal static int AddArrow(HyperPdfAnnotations annotationState, int pageIndex, PdfPage page, PagePoint tailPoint, PagePoint tipPoint, StrokeStyle style)
    {
        var tail = new Vector2(tailPoint.X, tailPoint.Y);
        var tip = new Vector2(tipPoint.X, tipPoint.Y);
        if ((tail - tip).LengthSquared() <= 0)
        {
            return -1;
        }

        var (first, second) = PdfAppearances.OpenArrowHead(tip, tail, HyperPdfAnnotationStrokes.ArrowHead(style.Width));
        Span<PagePoint> points = [tailPoint, tipPoint, tipPoint, new(first.X, first.Y), tipPoint, new(second.X, second.Y)];
        Span<int> strokes = [StrokePoints, StrokePoints, StrokePoints];
        return AddStrokes(annotationState, pageIndex, page, points, strokes, style);
    }

    /// <summary>
    /// Adds strokes: ink, or a polygon's vertices. The points become the annotation's <c>/InkList</c> or
    /// <c>/Vertices</c> and are drawn into its appearance.
    /// </summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="page">The page.</param>
    /// <param name="points">The points in page space, stroke after stroke.</param>
    /// <param name="lengths">The number of points in each stroke.</param>
    /// <param name="style">The subtype, the kind (which picks the subject and how points join), the colour and the line width.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    internal static int AddStrokes(HyperPdfAnnotations annotationState, int pageIndex, PdfPage page, ReadOnlySpan<PagePoint> points, ReadOnlySpan<int> lengths, StrokeStyle style)
    {
        var path = default(PdfStrokeBuffer);
        try
        {
            ConvertStrokes(page, points, lengths, ref path);
            if (path.PointCount == 0)
            {
                return -1;
            }

            var annotation = PdfAnnotations.Create(annotationState.Store, style.Subtype, default);
            PdfAnnotations.SetBorderWidth(annotation, style.Width);
            HyperPdfAnnotationStrokes.WriteStrokes(annotation, ref path);
            WritePolygonEntries(annotationState, annotation, style.Kind);
            HyperPdfAnnotationStrokes.Redraw(annotationState, annotation, style.Kind, ref path, style.Color, style.Width);
            return HyperPdfAnnotationReading.Add(annotationState, pageIndex, page, annotation, style.Color, string.Empty, StrokeSubject(style.Kind));
        }
        finally
        {
            path.Dispose();
        }
    }

    /// <summary>Gives a cloud its intent and cloudy border effect.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="annotation">The polygon.</param>
    /// <param name="kind">Its kind.</param>
    internal static void WritePolygonEntries(HyperPdfAnnotations annotationState, PdfDictionary annotation, AnnotationKind kind)
    {
        if (kind != AnnotationKind.Cloud)
        {
            return;
        }

        annotation.Set(KnownName.IT, PdfValue.FromName(annotationState.Names.PolygonCloud));
        var effect = new PdfDictionary(annotationState.Store, StrokePoints);
        effect.Set(KnownName.S, PdfValue.FromName(KnownName.C));
        effect.Set(KnownName.I, PdfValue.FromInteger(1));
        annotation.Set(KnownName.BE, PdfValue.FromDictionary(effect));
    }
}
