// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Drawing;

namespace HyperPdfLibrary.Creation;

/// <summary>
/// Shapes, clips and transforms for a <see cref="PdfPageCanvas"/>: rectangles, rounded rectangles, ellipses, arcs, pies,
/// polygons and curves, each filled, outlined with a <see cref="PdfPen"/>, or both. Coordinates are in points, with the
/// origin at the bottom left of the page and angles in degrees, anticlockwise from the right. In a tagged document the
/// painting methods must be inside <see cref="PdfPageCanvas.BeginContent(PdfTagElement)"/> or
/// <see cref="PdfPageCanvas.BeginArtifact(PdfArtifactKind)"/>.
/// </summary>
public static class PdfCanvasShapeMixins
{
    /// <summary>The shortest list of points a polygon has.</summary>
    private const int MinimumPolygonPoints = 2;

    /// <summary>The degrees in a full turn.</summary>
    private const float FullTurn = 360F;

    /// <summary>The usual tension of a smooth curve through points.</summary>
    private const float DefaultTension = 0.5F;

    /// <summary>Half, for finding the middle of a rectangle.</summary>
    private const float Half = 0.5F;

    /// <summary>Adds shape, clip and transform methods to a page canvas.</summary>
    /// <param name="canvas">The page canvas.</param>
    extension(ref PdfPageCanvas canvas)
    {
        /// <summary>Paints a rectangle.</summary>
        /// <param name="style">How the rectangle is painted.</param>
        /// <param name="x">The left edge.</param>
        /// <param name="y">The bottom edge.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <exception cref="ArgumentOutOfRangeException">The pen has a negative width or dash.</exception>
        /// <exception cref="InvalidOperationException">The document is tagged and no content or artifact is open.</exception>
        public void DrawRectangle(PdfShapeStyle style, float x, float y, float width, float height)
        {
            canvas.RequireMarked();
            canvas.Content.SaveState();
            style.Apply(ref canvas.Content);
            canvas.Content.Rectangle(x, y, width, height);
            style.Paint(ref canvas.Content, false);
            canvas.Content.RestoreState();
        }

        /// <summary>Paints a rectangle with rounded corners.</summary>
        /// <param name="style">How the shape is painted.</param>
        /// <param name="x">The left edge.</param>
        /// <param name="y">The bottom edge.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="radius">The radius of the corners; it is cut to half the shorter side.</param>
        /// <exception cref="ArgumentOutOfRangeException">The radius is negative, or the pen has a negative width or dash.</exception>
        /// <exception cref="InvalidOperationException">The document is tagged and no content or artifact is open.</exception>
        public void DrawRoundedRectangle(PdfShapeStyle style, float x, float y, float width, float height, float radius)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(radius);
            canvas.RequireMarked();
            canvas.Content.SaveState();
            style.Apply(ref canvas.Content);
            PdfCanvasPaths.AddRoundedRectangle(ref canvas.Content, x, y, width, height, radius);
            style.Paint(ref canvas.Content, false);
            canvas.Content.RestoreState();
        }

        /// <summary>Paints an ellipse that fits a rectangle; a square gives a circle.</summary>
        /// <param name="style">How the ellipse is painted.</param>
        /// <param name="x">The left edge of the rectangle.</param>
        /// <param name="y">The bottom edge of the rectangle.</param>
        /// <param name="width">The width of the rectangle.</param>
        /// <param name="height">The height of the rectangle.</param>
        /// <exception cref="ArgumentOutOfRangeException">The pen has a negative width or dash.</exception>
        /// <exception cref="InvalidOperationException">The document is tagged and no content or artifact is open.</exception>
        public void DrawEllipse(PdfShapeStyle style, float x, float y, float width, float height)
        {
            canvas.RequireMarked();
            canvas.Content.SaveState();
            style.Apply(ref canvas.Content);
            AddEllipse(ref canvas, x, y, width, height);
            style.Paint(ref canvas.Content, false);
            canvas.Content.RestoreState();
        }

        /// <summary>Strokes an arc of an ellipse that fits a rectangle.</summary>
        /// <param name="pen">The pen.</param>
        /// <param name="x">The left edge of the rectangle.</param>
        /// <param name="y">The bottom edge of the rectangle.</param>
        /// <param name="width">The width of the rectangle.</param>
        /// <param name="height">The height of the rectangle.</param>
        /// <param name="startAngle">Where the arc starts, in degrees anticlockwise from the right.</param>
        /// <param name="sweepAngle">How far the arc goes, in degrees; negative goes clockwise.</param>
        /// <exception cref="ArgumentNullException"><paramref name="pen"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The pen has a negative width or dash.</exception>
        /// <exception cref="InvalidOperationException">The document is tagged and no content or artifact is open.</exception>
        public void DrawArc(PdfPen pen, float x, float y, float width, float height, float startAngle, float sweepAngle)
        {
            ArgumentNullException.ThrowIfNull(pen);
            canvas.RequireMarked();
            canvas.Content.SaveState();
            pen.Apply(ref canvas.Content);
            PdfCanvasPaths.AddArc(ref canvas.Content, Fit(x, y, width, height), startAngle, Math.Clamp(sweepAngle, -FullTurn, FullTurn), true);
            canvas.Content.Stroke();
            canvas.Content.RestoreState();
        }

        /// <summary>Paints a pie slice: an arc closed by two lines to the middle of its ellipse.</summary>
        /// <param name="style">How the slice is painted.</param>
        /// <param name="x">The left edge of the rectangle.</param>
        /// <param name="y">The bottom edge of the rectangle.</param>
        /// <param name="width">The width of the rectangle.</param>
        /// <param name="height">The height of the rectangle.</param>
        /// <param name="startAngle">Where the slice starts, in degrees anticlockwise from the right.</param>
        /// <param name="sweepAngle">How far the slice goes, in degrees; negative goes clockwise.</param>
        /// <exception cref="ArgumentOutOfRangeException">The pen has a negative width or dash.</exception>
        /// <exception cref="InvalidOperationException">The document is tagged and no content or artifact is open.</exception>
        public void DrawPie(PdfShapeStyle style, float x, float y, float width, float height, float startAngle, float sweepAngle)
        {
            canvas.RequireMarked();
            canvas.Content.SaveState();
            style.Apply(ref canvas.Content);
            var box = Fit(x, y, width, height);
            var start = PdfCanvasPaths.ToRadians(startAngle);
            canvas.Content.MoveTo(box.CenterX, box.CenterY);
            canvas.Content.LineTo(box.CenterX + (box.RadiusX * MathF.Cos(start)), box.CenterY + (box.RadiusY * MathF.Sin(start)));
            PdfCanvasPaths.AddArc(ref canvas.Content, box, startAngle, Math.Clamp(sweepAngle, -FullTurn, FullTurn), false);
            style.Paint(ref canvas.Content, true);
            canvas.Content.RestoreState();
        }

        /// <summary>Paints a polygon: straight sides through the points, closed back to the first.</summary>
        /// <param name="style">How the polygon is painted.</param>
        /// <param name="points">The corners; at least two.</param>
        /// <exception cref="ArgumentException">There are fewer than two points.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The pen has a negative width or dash.</exception>
        /// <exception cref="InvalidOperationException">The document is tagged and no content or artifact is open.</exception>
        public void DrawPolygon(PdfShapeStyle style, scoped ReadOnlySpan<PdfPoint> points)
        {
            RequirePoints(points);
            canvas.RequireMarked();
            canvas.Content.SaveState();
            style.Apply(ref canvas.Content);
            PdfCanvasPaths.AddPolygon(ref canvas.Content, points, true);
            style.Paint(ref canvas.Content, false);
            canvas.Content.RestoreState();
        }

        /// <summary>Strokes straight lines through the points, leaving the shape open.</summary>
        /// <param name="pen">The pen.</param>
        /// <param name="points">The points; at least two.</param>
        /// <exception cref="ArgumentNullException"><paramref name="pen"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">There are fewer than two points.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The pen has a negative width or dash.</exception>
        /// <exception cref="InvalidOperationException">The document is tagged and no content or artifact is open.</exception>
        public void DrawPolyline(PdfPen pen, scoped ReadOnlySpan<PdfPoint> points)
        {
            ArgumentNullException.ThrowIfNull(pen);
            RequirePoints(points);
            canvas.RequireMarked();
            canvas.Content.SaveState();
            pen.Apply(ref canvas.Content);
            PdfCanvasPaths.AddPolygon(ref canvas.Content, points, false);
            canvas.Content.Stroke();
            canvas.Content.RestoreState();
        }

        /// <summary>Strokes a smooth open curve through the points.</summary>
        /// <param name="pen">The pen.</param>
        /// <param name="points">The points the curve passes through; at least two.</param>
        /// <param name="tension">How tightly the curve follows the points: 0 gives straight sides, 0.5 is the usual value.</param>
        /// <exception cref="ArgumentNullException"><paramref name="pen"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">There are fewer than two points.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The tension is negative, or the pen has a negative width or dash.</exception>
        /// <exception cref="InvalidOperationException">The document is tagged and no content or artifact is open.</exception>
        public void DrawCurve(PdfPen pen, scoped ReadOnlySpan<PdfPoint> points, float tension)
        {
            ArgumentNullException.ThrowIfNull(pen);
            ArgumentOutOfRangeException.ThrowIfNegative(tension);
            RequirePoints(points);
            canvas.RequireMarked();
            canvas.Content.SaveState();
            pen.Apply(ref canvas.Content);
            PdfCanvasPaths.AddSpline(ref canvas.Content, points, tension, false);
            canvas.Content.Stroke();
            canvas.Content.RestoreState();
        }

        /// <summary>Strokes a smooth open curve through the points with the usual tension of 0.5.</summary>
        /// <param name="pen">The pen.</param>
        /// <param name="points">The points the curve passes through; at least two.</param>
        /// <exception cref="ArgumentNullException"><paramref name="pen"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">There are fewer than two points.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The pen has a negative width or dash.</exception>
        /// <exception cref="InvalidOperationException">The document is tagged and no content or artifact is open.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DrawCurve(PdfPen pen, scoped ReadOnlySpan<PdfPoint> points) => canvas.DrawCurve(pen, points, DefaultTension);

        /// <summary>Paints a smooth closed curve through the points.</summary>
        /// <param name="style">How the shape is painted.</param>
        /// <param name="points">The points the curve passes through; at least two.</param>
        /// <param name="tension">How tightly the curve follows the points: 0 gives straight sides, 0.5 is the usual value.</param>
        /// <exception cref="ArgumentException">There are fewer than two points.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The tension is negative, or the pen has a negative width or dash.</exception>
        /// <exception cref="InvalidOperationException">The document is tagged and no content or artifact is open.</exception>
        public void DrawClosedCurve(PdfShapeStyle style, scoped ReadOnlySpan<PdfPoint> points, float tension)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(tension);
            RequirePoints(points);
            canvas.RequireMarked();
            canvas.Content.SaveState();
            style.Apply(ref canvas.Content);
            PdfCanvasPaths.AddSpline(ref canvas.Content, points, tension, true);
            style.Paint(ref canvas.Content, false);
            canvas.Content.RestoreState();
        }

        /// <summary>Saves the graphics state, so a later <see cref="RestoreState"/> brings back the clip, transform and colours.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SaveState() => canvas.Content.SaveState();

        /// <summary>Brings back the graphics state saved by the matching <see cref="SaveState"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RestoreState() => canvas.Content.RestoreState();

        /// <summary>Moves the origin for what is drawn next.</summary>
        /// <param name="dx">How far to move right.</param>
        /// <param name="dy">How far to move up.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Translate(float dx, float dy) => canvas.Content.Transform(1F, 0F, 0F, 1F, dx, dy);

        /// <summary>Scales what is drawn next, about the origin.</summary>
        /// <param name="sx">The horizontal factor.</param>
        /// <param name="sy">The vertical factor.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Scale(float sx, float sy) => canvas.Content.Transform(sx, 0F, 0F, sy, 0F, 0F);

        /// <summary>Turns what is drawn next about the origin.</summary>
        /// <param name="degrees">The angle in degrees, anticlockwise.</param>
        public void Rotate(float degrees)
        {
            var radians = PdfCanvasPaths.ToRadians(degrees);
            var sin = MathF.Sin(radians);
            var cos = MathF.Cos(radians);
            canvas.Content.Transform(cos, sin, -sin, cos, 0F, 0F);
        }

        /// <summary>Skews what is drawn next about the origin.</summary>
        /// <param name="horizontalDegrees">How far the vertical lines lean, in degrees.</param>
        /// <param name="verticalDegrees">How far the horizontal lines lean, in degrees.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Skew(float horizontalDegrees, float verticalDegrees) =>
            canvas.Content.Transform(1F, MathF.Tan(PdfCanvasPaths.ToRadians(verticalDegrees)), MathF.Tan(PdfCanvasPaths.ToRadians(horizontalDegrees)), 1F, 0F, 0F);

        /// <summary>Applies a general transformation to what is drawn next.</summary>
        /// <param name="a">The matrix's a entry.</param>
        /// <param name="b">The matrix's b entry.</param>
        /// <param name="c">The matrix's c entry.</param>
        /// <param name="d">The matrix's d entry.</param>
        /// <param name="e">The horizontal translation.</param>
        /// <param name="f">The vertical translation.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Transform(float a, float b, float c, float d, float e, float f) => canvas.Content.Transform(a, b, c, d, e, f);

        /// <summary>Limits what is drawn next to a rectangle, until the state is restored.</summary>
        /// <param name="x">The left edge.</param>
        /// <param name="y">The bottom edge.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        public void ClipRectangle(float x, float y, float width, float height)
        {
            canvas.Content.Rectangle(x, y, width, height);
            canvas.Content.Clip();
            canvas.Content.EndPath();
        }

        /// <summary>Limits what is drawn next to a rectangle with rounded corners, until the state is restored.</summary>
        /// <param name="x">The left edge.</param>
        /// <param name="y">The bottom edge.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="radius">The radius of the corners.</param>
        /// <exception cref="ArgumentOutOfRangeException">The radius is negative.</exception>
        public void ClipRoundedRectangle(float x, float y, float width, float height, float radius)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(radius);
            PdfCanvasPaths.AddRoundedRectangle(ref canvas.Content, x, y, width, height, radius);
            canvas.Content.Clip();
            canvas.Content.EndPath();
        }

        /// <summary>Limits what is drawn next to an ellipse that fits a rectangle, until the state is restored.</summary>
        /// <param name="x">The left edge of the rectangle.</param>
        /// <param name="y">The bottom edge of the rectangle.</param>
        /// <param name="width">The width of the rectangle.</param>
        /// <param name="height">The height of the rectangle.</param>
        public void ClipEllipse(float x, float y, float width, float height)
        {
            AddEllipse(ref canvas, x, y, width, height);
            canvas.Content.Clip();
            canvas.Content.EndPath();
        }

        /// <summary>Limits what is drawn next to a polygon, until the state is restored.</summary>
        /// <param name="points">The corners; at least two.</param>
        /// <exception cref="ArgumentException">There are fewer than two points.</exception>
        public void ClipPolygon(scoped ReadOnlySpan<PdfPoint> points)
        {
            RequirePoints(points);
            PdfCanvasPaths.AddPolygon(ref canvas.Content, points, true);
            canvas.Content.Clip();
            canvas.Content.EndPath();
        }
    }

    /// <summary>Writes an ellipse as four curves, closed.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="x">The left edge of the rectangle.</param>
    /// <param name="y">The bottom edge of the rectangle.</param>
    /// <param name="width">The width of the rectangle.</param>
    /// <param name="height">The height of the rectangle.</param>
    private static void AddEllipse(ref PdfPageCanvas canvas, float x, float y, float width, float height)
    {
        PdfCanvasPaths.AddArc(ref canvas.Content, Fit(x, y, width, height), 0F, FullTurn, true);
        canvas.Content.ClosePath();
    }

    /// <summary>Gets the ellipse that fits a rectangle.</summary>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The bottom edge.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The centre and radii.</returns>
    private static ArcBox Fit(float x, float y, float width, float height) =>
        new(x + (width * Half), y + (height * Half), MathF.Abs(width) * Half, MathF.Abs(height) * Half);

    /// <summary>Checks that there are enough points for a polygon or curve.</summary>
    /// <param name="points">The points.</param>
    /// <exception cref="ArgumentException">There are fewer than two points.</exception>
    private static void RequirePoints(ReadOnlySpan<PdfPoint> points)
    {
        if (points.Length < MinimumPolygonPoints)
        {
            throw new ArgumentException("A shape needs at least two points.", nameof(points));
        }
    }
}
