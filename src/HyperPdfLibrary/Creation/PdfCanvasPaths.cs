// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Creation;

/// <summary>Writes the path operators of curved and many-sided shapes: arcs and circles as cubic curves, and splines through points.</summary>
internal static class PdfCanvasPaths
{
    /// <summary>The degrees in a half turn.</summary>
    private const float HalfTurn = 180F;

    /// <summary>How many points past a segment's start the point after its end lies.</summary>
    private const int TwoAhead = 2;

    /// <summary>Half, for halving a length.</summary>
    private const float Half = 0.5F;

    /// <summary>The widest sweep, in degrees, one cubic curve stands for.</summary>
    private const float QuarterTurn = 90F;

    /// <summary>The factor of the control handle length: four thirds.</summary>
    private const float HandleFactor = 4F / 3F;

    /// <summary>A quarter of a turn's angle, used to find the handle length of a curve.</summary>
    private const float FourthOfSweep = 4F;

    /// <summary>The fewest points a spline needs.</summary>
    private const int MinimumSplinePoints = 2;

    /// <summary>The share of the span between neighbours that sets a spline's handle length: one third.</summary>
    private const float SplineThird = 1F / 3F;

    /// <summary>Converts degrees to radians.</summary>
    /// <param name="degrees">The angle in degrees.</param>
    /// <returns>The angle in radians.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float ToRadians(float degrees) => degrees * MathF.PI / HalfTurn;

    /// <summary>Starts a path at the start of an arc and adds curves along it.</summary>
    /// <param name="builder">The content being written.</param>
    /// <param name="box">The box the ellipse of the arc fits in.</param>
    /// <param name="start">The angle the arc starts at, in degrees anticlockwise from the right.</param>
    /// <param name="sweep">The angle the arc covers, in degrees; negative goes clockwise.</param>
    /// <param name="moveToStart">Whether to start a new subpath at the arc's start; otherwise the current point is taken to be there.</param>
    internal static void AddArc(ref PdfContentBuilder builder, in ArcBox box, float start, float sweep, bool moveToStart)
    {
        var segments = Math.Max(1, (int)MathF.Ceiling(MathF.Abs(sweep) / QuarterTurn));
        var step = ToRadians(sweep / segments);
        var handle = HandleFactor * MathF.Tan(step / FourthOfSweep);
        var angle = ToRadians(start);
        if (moveToStart)
        {
            builder.MoveTo(box.CenterX + (box.RadiusX * MathF.Cos(angle)), box.CenterY + (box.RadiusY * MathF.Sin(angle)));
        }

        for (var i = 0; i < segments; i++)
        {
            var next = angle + step;
            var sin0 = MathF.Sin(angle);
            var cos0 = MathF.Cos(angle);
            var sin1 = MathF.Sin(next);
            var cos1 = MathF.Cos(next);
            builder.CurveTo(
                box.CenterX + (box.RadiusX * (cos0 - (handle * sin0))),
                box.CenterY + (box.RadiusY * (sin0 + (handle * cos0))),
                box.CenterX + (box.RadiusX * (cos1 + (handle * sin1))),
                box.CenterY + (box.RadiusY * (sin1 - (handle * cos1))),
                box.CenterX + (box.RadiusX * cos1),
                box.CenterY + (box.RadiusY * sin1));
            angle = next;
        }
    }

    /// <summary>Writes a rounded rectangle as a closed path.</summary>
    /// <param name="builder">The content being written.</param>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The bottom edge.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="radius">The corner radius; it is cut to half the shorter side.</param>
    internal static void AddRoundedRectangle(ref PdfContentBuilder builder, float x, float y, float width, float height, float radius)
    {
        var r = Math.Clamp(radius, 0F, Math.Min(width, height) * Half);
        var right = x + width;
        var top = y + height;
        builder.MoveTo(x + r, y);
        builder.LineTo(right - r, y);
        AddArc(ref builder, new(right - r, y + r, r, r), -QuarterTurn, QuarterTurn, false);
        builder.LineTo(right, top - r);
        AddArc(ref builder, new(right - r, top - r, r, r), 0F, QuarterTurn, false);
        builder.LineTo(x + r, top);
        AddArc(ref builder, new(x + r, top - r, r, r), QuarterTurn, QuarterTurn, false);
        builder.LineTo(x, y + r);
        AddArc(ref builder, new(x + r, y + r, r, r), HalfTurn, QuarterTurn, false);
        builder.ClosePath();
    }

    /// <summary>Writes a polygon or polyline path through points with straight sides.</summary>
    /// <param name="builder">The content being written.</param>
    /// <param name="points">The points.</param>
    /// <param name="close">Whether to close the path back to the first point.</param>
    internal static void AddPolygon(ref PdfContentBuilder builder, scoped ReadOnlySpan<PdfPoint> points, bool close)
    {
        builder.MoveTo(points[0].X, points[0].Y);
        for (var i = 1; i < points.Length; i++)
        {
            builder.LineTo(points[i].X, points[i].Y);
        }

        if (close)
        {
            builder.ClosePath();
        }
    }

    /// <summary>Writes a smooth curve through points, as a cardinal spline.</summary>
    /// <param name="builder">The content being written.</param>
    /// <param name="points">The points the curve passes through; at least two.</param>
    /// <param name="tension">How tightly the curve follows the points, from 0 (straight sides) upwards; 0.5 is the usual value.</param>
    /// <param name="close">Whether the curve closes back to the first point.</param>
    /// <exception cref="ArgumentException">There are fewer than two points.</exception>
    internal static void AddSpline(ref PdfContentBuilder builder, scoped ReadOnlySpan<PdfPoint> points, float tension, bool close)
    {
        if (points.Length < MinimumSplinePoints)
        {
            throw new ArgumentException("A curve needs at least two points.", nameof(points));
        }

        builder.MoveTo(points[0].X, points[0].Y);
        var segments = close ? points.Length : points.Length - 1;
        for (var i = 0; i < segments; i++)
        {
            var previous = PointAt(points, i - 1, close);
            var from = points[i];
            var to = PointAt(points, i + 1, close);
            var after = PointAt(points, i + TwoAhead, close);
            var k = tension * SplineThird;
            builder.CurveTo(
                from.X + (k * (to.X - previous.X)),
                from.Y + (k * (to.Y - previous.Y)),
                to.X - (k * (after.X - from.X)),
                to.Y - (k * (after.Y - from.Y)),
                to.X,
                to.Y);
        }

        if (close)
        {
            builder.ClosePath();
        }
    }

    /// <summary>Gets a point of a spline, wrapping round a closed one and repeating the end points of an open one.</summary>
    /// <param name="points">The points.</param>
    /// <param name="index">The index, which may be one before the first or past the last.</param>
    /// <param name="close">Whether the curve is closed.</param>
    /// <returns>The point.</returns>
    private static PdfPoint PointAt(ReadOnlySpan<PdfPoint> points, int index, bool close)
    {
        var count = points.Length;
        return close ? points[((index % count) + count) % count] : points[Math.Clamp(index, 0, count - 1)];
    }
}
