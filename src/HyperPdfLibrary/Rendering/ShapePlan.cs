// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// A square, circle, polygon, polyline or line annotation (ISO 32000-2 12.5.6.8 and 12.5.6.9): an optional interior
/// colour, a border and a path. A square or circle is drawn inside <c>/Rect</c> pulled in by half the border, so the
/// stroke stays within the rectangle; a cloudy border (<c>/BE</c>) is drawn inside <c>/Rect</c> less <c>/RD</c>.
/// </summary>
/// <param name="Shape">Which shape.</param>
/// <param name="Rect">The annotation rectangle, which the appearance is fitted to.</param>
/// <param name="Inner">The rectangle a square or circle is drawn in.</param>
/// <param name="Interior">The interior colour (<c>/IC</c>), or none.</param>
/// <param name="Outline">The border colour (<c>/C</c>), black when absent.</param>
/// <param name="Border">The border width and dash.</param>
/// <param name="Cloudy">Whether the border is cloudy.</param>
/// <param name="Points">The <c>/Vertices</c> of a polygon or polyline, read in place.</param>
/// <param name="Line">The end points and line endings of a line.</param>
[DebuggerDisplay("ShapePlan: {Shape}")]
internal readonly record struct ShapePlan(
    ShapeKind Shape,
    PdfRectangle Rect,
    PdfRectangle Inner,
    PaintColor Interior,
    PaintColor Outline,
    PaintStroke Border,
    bool Cloudy,
    PdfArray? Points,
    LineEnds Line) : IAppearancePlan<ShapePlan>
{
    /// <summary>The control point factor of the standard Bezier approximation of a quarter ellipse, 4 (sqrt 2 - 1) / 3.</summary>
    private const float Kappa = 0.5523F;

    /// <summary>The points a cloudy ellipse is approximated with.</summary>
    private const int CloudEllipsePoints = 16;

    /// <summary>The numbers in a point.</summary>
    private const int PointFields = 2;

    /// <summary>The fewest numbers a path needs: two points.</summary>
    private const int FewestPathNumbers = 4;

    /// <summary>The length of an arrow head's sides as a multiple of the line width.</summary>
    private const float ArrowWidths = 6;

    /// <summary>The shortest arrow head side, in points.</summary>
    private const float MinArrow = 4;

    /// <summary>The vertices kept on the stack before a pooled array is used.</summary>
    private const int StackPoints = 64;

    /// <summary>The corners of a rectangle.</summary>
    private const int RectangleCorners = 4;

    /// <inheritdoc/>
    public PdfRectangle Bounds => Rect;

    /// <inheritdoc/>
    public KnownName Blend => KnownName.Normal;

    /// <inheritdoc/>
    public static bool TryRead(PdfDictionary annotation, out ShapePlan plan)
    {
        plan = default;
        var shape = annotation.GetName(KnownName.Subtype).ToKnownName() switch
        {
            KnownName.Square => ShapeKind.Square,
            KnownName.Circle => ShapeKind.Circle,
            KnownName.Polygon => ShapeKind.Polygon,
            KnownName.PolyLine => ShapeKind.PolyLine,
            KnownName.Line => ShapeKind.Line,
            _ => ShapeKind.None,
        };
        var vertices = annotation.GetArray(KnownName.Vertices);
        var line = default(LineEnds);
        if (!HasGeometry(annotation, shape, vertices, ref line))
        {
            return false;
        }

        var rect = AppearanceRects.Read(annotation);
        var border = PaintStroke.Read(annotation);
        var cloudy = annotation.GetDictionary(KnownName.BE) is { } effect && effect.IsName(KnownName.S, KnownName.C);

        // Only closed areas and boxes have an interior; a line or polyline is just stroked.
        var interior = shape is ShapeKind.PolyLine or ShapeKind.Line ? PaintColor.None : PaintColor.Read(annotation.GetArray(KnownName.IC), PaintColor.None);
        var outline = PaintColor.Read(annotation.GetArray(KnownName.C), PaintColor.Black);
        plan = new(shape, rect, InnerRectangle(annotation, rect, border.Width, cloudy), interior, outline, border, cloudy, vertices, line);
        return true;
    }

    /// <inheritdoc/>
    public void Emit(ref PdfContentBuilder builder)
    {
        Interior.SetFill(ref builder);
        Outline.SetStroke(ref builder);
        if (Border.Width > 0)
        {
            Border.Apply(ref builder);
        }

        switch (Shape)
        {
            case ShapeKind.Square:
            {
                EmitSquare(ref builder);
                break;
            }

            case ShapeKind.Circle:
            {
                EmitCircle(ref builder);
                break;
            }

            case ShapeKind.Line:
            {
                EmitLine(ref builder);
                break;
            }

            default:
            {
                EmitPath(ref builder);
                break;
            }
        }
    }

    /// <summary>Checks that an annotation holds what its shape is drawn from: vertices for a path, <c>/L</c> for a line.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="shape">The shape.</param>
    /// <param name="vertices">The <c>/Vertices</c>, or <see langword="null"/>.</param>
    /// <param name="line">Receives the line's ends.</param>
    /// <returns><see langword="true"/> when the shape can be drawn.</returns>
    private static bool HasGeometry(PdfDictionary annotation, ShapeKind shape, PdfArray? vertices, ref LineEnds line) => shape switch
    {
        ShapeKind.None => false,
        ShapeKind.Polygon or ShapeKind.PolyLine => vertices is { Count: >= FewestPathNumbers },
        ShapeKind.Line => LineEnds.TryRead(annotation, out line),
        _ => true,
    };

    /// <summary>Gets the rectangle a square or circle is drawn in.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="rect">The annotation rectangle.</param>
    /// <param name="width">The border width.</param>
    /// <param name="cloudy">Whether the border is cloudy.</param>
    /// <returns>The rectangle.</returns>
    private static PdfRectangle InnerRectangle(PdfDictionary annotation, PdfRectangle rect, float width, bool cloudy)
    {
        if (cloudy)
        {
            return AppearanceRects.ApplyDifferences(annotation, rect, AppearanceRects.Inset(rect, PdfAppearances.CloudMargin(width)));
        }

        return width > 0 ? AppearanceRects.Inset(rect, width * AppearanceRects.Half) : rect;
    }

    /// <summary>Paints the path: close, fill and stroke; close and stroke; fill; or end it without painting.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="closed">Whether the shape is closed; an open path is stroked as it is.</param>
    private void Paint(ref PdfContentBuilder builder, bool closed)
    {
        var stroke = Border.Width > 0;
        var fill = closed && Interior.IsSet;
        if (stroke && fill)
        {
            builder.CloseFillAndStroke();
        }
        else if (stroke)
        {
            if (closed)
            {
                builder.CloseAndStroke();
            }
            else
            {
                builder.Stroke();
            }
        }
        else if (fill)
        {
            builder.Fill();
        }
        else
        {
            builder.EndPath();
        }
    }

    /// <summary>Writes a square: a rectangle, or a cloud round its corners.</summary>
    /// <param name="builder">The content.</param>
    private void EmitSquare(ref PdfContentBuilder builder)
    {
        if (Cloudy)
        {
            Span<Vector2> corners = stackalloc Vector2[RectangleCorners];
            corners[0] = new(Inner.Left, Inner.Bottom);
            corners[1] = new(Inner.Right, Inner.Bottom);
            corners[2] = new(Inner.Right, Inner.Top);
            corners[3] = new(Inner.Left, Inner.Top);
            PdfAppearances.AddCloud(ref builder, corners, Border.Width);
        }
        else
        {
            builder.Rectangle(Inner.Left, Inner.Bottom, Inner.Width, Inner.Height);
        }

        Paint(ref builder, true);
    }

    /// <summary>Writes a circle: four Bezier curves, or a cloud round sixteen points of the ellipse.</summary>
    /// <param name="builder">The content.</param>
    private void EmitCircle(ref PdfContentBuilder builder)
    {
        if (Cloudy)
        {
            EmitCloudyEllipse(ref builder);
        }
        else
        {
            var middleX = (Inner.Left + Inner.Right) * AppearanceRects.Half;
            var middleY = (Inner.Top + Inner.Bottom) * AppearanceRects.Half;
            var deltaX = Kappa * Inner.Width * AppearanceRects.Half;
            var deltaY = Kappa * Inner.Height * AppearanceRects.Half;
            builder.MoveTo(middleX, Inner.Top);
            builder.CurveTo(middleX + deltaX, Inner.Top, Inner.Right, middleY + deltaY, Inner.Right, middleY);
            builder.CurveTo(Inner.Right, middleY - deltaY, middleX + deltaX, Inner.Bottom, middleX, Inner.Bottom);
            builder.CurveTo(middleX - deltaX, Inner.Bottom, Inner.Left, middleY - deltaY, Inner.Left, middleY);
            builder.CurveTo(Inner.Left, middleY + deltaY, middleX - deltaX, Inner.Top, middleX, Inner.Top);
        }

        Paint(ref builder, true);
    }

    /// <summary>Writes a cloudy border round an ellipse, approximated by sixteen of its points.</summary>
    /// <param name="builder">The content.</param>
    private void EmitCloudyEllipse(ref PdfContentBuilder builder)
    {
        Span<Vector2> points = stackalloc Vector2[CloudEllipsePoints];
        var centre = new Vector2((Inner.Left + Inner.Right) * AppearanceRects.Half, (Inner.Top + Inner.Bottom) * AppearanceRects.Half);
        var radius = new Vector2(Inner.Width * AppearanceRects.Half, Inner.Height * AppearanceRects.Half);
        for (var i = 0; i < points.Length; i++)
        {
            var angle = MathF.Tau * (i / (float)points.Length);
            points[i] = centre + (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius);
        }

        PdfAppearances.AddCloud(ref builder, points, Border.Width);
    }

    /// <summary>Writes a line and its endings.</summary>
    /// <param name="builder">The content.</param>
    private void EmitLine(ref PdfContentBuilder builder)
    {
        builder.MoveTo(Line.Start.X, Line.Start.Y);
        builder.LineTo(Line.End.X, Line.End.Y);
        builder.Stroke();
        EmitEnding(ref builder, Line.StartEnding, Line.Start, Line.End);
        EmitEnding(ref builder, Line.EndEnding, Line.End, Line.Start);
    }

    /// <summary>Writes an open or closed arrow at one end of the line.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="ending">The <c>/LE</c> name.</param>
    /// <param name="tip">The end the arrow is at.</param>
    /// <param name="from">The other end.</param>
    private void EmitEnding(ref PdfContentBuilder builder, KnownName ending, Vector2 tip, Vector2 from)
    {
        if (ending is not (KnownName.OpenArrow or KnownName.ClosedArrow))
        {
            return;
        }

        var head = PdfAppearances.OpenArrowHead(tip, from, Math.Max(MinArrow, Border.Width * ArrowWidths));
        builder.MoveTo(head.First.X, head.First.Y);
        builder.LineTo(tip.X, tip.Y);
        builder.LineTo(head.Second.X, head.Second.Y);
        if (ending == KnownName.ClosedArrow)
        {
            builder.CloseAndStroke();
        }
        else
        {
            builder.Stroke();
        }
    }

    /// <summary>Writes a polygon or polyline path through the vertices, or a cloud round a polygon's.</summary>
    /// <param name="builder">The content.</param>
    private void EmitPath(ref PdfContentBuilder builder)
    {
        var vertices = Points!;
        var closed = Shape == ShapeKind.Polygon;
        if (closed && Cloudy)
        {
            EmitCloudyPolygon(ref builder, vertices);
        }
        else
        {
            builder.MoveTo(vertices.GetSingle(0), vertices.GetSingle(1));
            for (var i = PointFields; i + 1 < vertices.Count; i += PointFields)
            {
                builder.LineTo(vertices.GetSingle(i), vertices.GetSingle(i + 1));
            }

            if (closed)
            {
                builder.ClosePath();
            }
        }

        Paint(ref builder, closed);
    }

    /// <summary>Writes a cloudy border round a polygon.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="vertices">The vertices.</param>
    private void EmitCloudyPolygon(ref PdfContentBuilder builder, PdfArray vertices)
    {
        var count = vertices.Count / PointFields;
        Vector2[]? rented = null;
        Span<Vector2> points = count <= StackPoints ? stackalloc Vector2[StackPoints] : (rented = ArrayPool<Vector2>.Shared.Rent(count));
        try
        {
            for (var i = 0; i < count; i++)
            {
                points[i] = new(vertices.GetSingle(i * PointFields), vertices.GetSingle((i * PointFields) + 1));
            }

            PdfAppearances.AddCloud(ref builder, points[..count], Border.Width);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<Vector2>.Shared.Return(rented);
            }
        }
    }
}
