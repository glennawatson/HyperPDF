// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Layout;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>Changes the ink of a drawing pad, and draws it straight into the frame, so the strokes are quads the GPU draws and never pixels the CPU fills.</summary>
internal static class PadInks
{
    /// <summary>The distance between two quads along a stroke, in device pixels. A smaller step than the ink is thick leaves no gaps.</summary>
    private const double Step = 1;

    /// <summary>The share of the ink's thickness that lies each side of the line.</summary>
    private const double Half = 0.5;

    /// <summary>The floats that make one point of a mark.</summary>
    private const int Coordinates = 2;

    /// <summary>The share of the pad that a remembered drawing may fill: all of it but a margin at each edge.</summary>
    private const double Fill = 0.8;

    /// <summary>The share of the free room put before the drawing, so it is in the middle.</summary>
    private const double HalfShare = 0.5;

    /// <summary>The smallest width or height a drawing is scaled from, so a dot or a straight line does not divide by zero.</summary>
    private const float LeastExtent = 0.001F;

    /// <summary>Takes a stroke into the ink: a finished stroke joins the finished ones, and a stroke being drawn replaces the one before it.</summary>
    /// <param name="ink">The ink so far.</param>
    /// <param name="stroke">The stroke.</param>
    /// <returns>The ink with the stroke in it.</returns>
    internal static PadInk Follow(PadInk ink, in PadStroke stroke) => stroke.Done
        ? ink with { Done = [.. ink.Done, stroke.Points], Live = [] }
        : ink with { Live = stroke.Points };

    /// <summary>Makes the ink of a drawing the view model remembered, fitted into the middle of the pad with a margin round it.</summary>
    /// <param name="mark">The remembered drawing, with its points in its own units.</param>
    /// <param name="area">Where the pad lies, in device pixels.</param>
    /// <returns>The ink, or <see langword="null"/> when the mark is not a drawing.</returns>
    internal static PadInk? FromMark(SignatureMark? mark, in LayoutRect area)
    {
        if (mark is not { Style: SignatureMarkStyle.Drawn, IsValid: true } || mark.StrokeLengths.IsEmpty)
        {
            return null;
        }

        var points = mark.Points.Span;
        var bounds = BoundsOf(points);
        var scale = Math.Min(area.Width * Fill / bounds.Width, area.Height * Fill / bounds.Height);
        var left = (area.Width - (bounds.Width * scale)) * HalfShare;
        var top = (area.Height - (bounds.Height * scale)) * HalfShare;
        var strokes = new List<PagePoint[]>();
        var at = 0;
        foreach (var length in mark.StrokeLengths.Span)
        {
            var stroke = new PagePoint[length];
            for (var i = 0; i < length; i++)
            {
                stroke[i] = new((float)(left + ((points[at] - bounds.Left) * scale)), (float)(top + ((points[at + 1] - bounds.Top) * scale)));
                at += Coordinates;
            }

            strokes.Add(stroke);
        }

        return new([.. strokes], []);
    }

    /// <summary>Draws the ink in an area of the frame.</summary>
    /// <param name="pass">The frame.</param>
    /// <param name="area">Where the pad is, in device pixels.</param>
    /// <param name="ink">The ink.</param>
    /// <param name="color">The colour of the ink.</param>
    /// <param name="thickness">How thick the ink is, in device pixels.</param>
    internal static void Draw(IFramePass pass, in LayoutRect area, PadInk ink, uint color, double thickness)
    {
        for (var i = 0; i < ink.Done.Length; i++)
        {
            DrawStroke(pass, area, ink.Done[i], color, thickness);
        }

        DrawStroke(pass, area, ink.Live, color, thickness);
    }

    /// <summary>Finds the box round the points of a mark.</summary>
    /// <param name="points">The points, as x and y one after the other.</param>
    /// <returns>The box. Its width and height are never less than <see cref="LeastExtent"/>.</returns>
    private static PointBox BoundsOf(ReadOnlySpan<float> points)
    {
        var left = float.MaxValue;
        var top = float.MaxValue;
        var right = float.MinValue;
        var bottom = float.MinValue;
        for (var i = 0; i + 1 < points.Length; i += Coordinates)
        {
            left = Math.Min(left, points[i]);
            right = Math.Max(right, points[i]);
            top = Math.Min(top, points[i + 1]);
            bottom = Math.Max(bottom, points[i + 1]);
        }

        return new(left, top, Math.Max(LeastExtent, right - left), Math.Max(LeastExtent, bottom - top));
    }

    /// <summary>Draws one stroke as square quads along it: one for a single point, and one for each step between points otherwise.</summary>
    /// <param name="pass">The frame.</param>
    /// <param name="area">Where the pad is.</param>
    /// <param name="stroke">The points of the stroke.</param>
    /// <param name="color">The colour of the ink.</param>
    /// <param name="thickness">How thick the ink is.</param>
    private static void DrawStroke(IFramePass pass, in LayoutRect area, PagePoint[] stroke, uint color, double thickness)
    {
        for (var i = 0; i < stroke.Length; i++)
        {
            var to = stroke[i];
            var from = stroke[Math.Max(0, i - 1)];
            var length = Math.Max(Math.Abs(to.X - from.X), Math.Abs(to.Y - from.Y));
            var steps = Math.Max(1, (int)Math.Ceiling(length / Step));
            for (var step = 1; step <= steps; step++)
            {
                var t = (double)step / steps;
                pass.FillRect(new(area.X + from.X + ((to.X - from.X) * t) - (thickness * Half), area.Y + from.Y + ((to.Y - from.Y) * t) - (thickness * Half), thickness, thickness), color);
            }
        }
    }

    /// <summary>The box round the points of a drawing.</summary>
    /// <param name="Left">The smallest x.</param>
    /// <param name="Top">The smallest y.</param>
    /// <param name="Width">The width of the box.</param>
    /// <param name="Height">The height of the box.</param>
    private readonly record struct PointBox(float Left, float Top, float Width, float Height);
}
