// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Draws the beveled and inset border of a radio button as a ring (ISO 32000-2 12.5.4, Table 168 border styles B and I):
/// a ring as wide as the border, its upper-left half in one tone and its lower-right half in the other (see
/// <see cref="FormBevelTone"/>).
/// </summary>
internal static class FormCircleBevel
{
    /// <summary>Half, to centre a line on a path.</summary>
    private const float Half = 0.5F;

    /// <summary>The control point offset that makes a quarter of a circle with one Bezier curve.</summary>
    private const float QuarterKappa = 0.5522847F;

    /// <summary>The angle, from the x axis, that turns the half circle path to cover the upper left.</summary>
    private const float UpperLeftAngle = MathF.PI / 4;

    /// <summary>The angle that turns the half circle path to cover the lower right.</summary>
    private const float LowerRightAngle = MathF.PI * 5 / 4;

    /// <summary>
    /// The gap, in points, between the ring and the widget's box. Anti-aliasing spreads an edge by about a pixel, so
    /// one point keeps the ring's outer edge from being cut by the box at normal zoom.
    /// </summary>
    private const float Margin = 1;

    /// <summary>Draws the circular background and bevel.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="border">The <c>/BC</c> colour, or <see langword="null"/>.</param>
    /// <param name="background">The <c>/BG</c> colour, or <see langword="null"/>.</param>
    internal static void Paint(ref PdfContentBuilder builder, in FormFrame frame, PdfArray? border, PdfArray? background)
    {
        var side = Math.Min(frame.Width, frame.Height) - Margin - Margin;
        if (side <= 0)
        {
            return;
        }

        var centerX = frame.Width * Half;
        var centerY = frame.Height * Half;
        var outer = side * Half;
        var lineWidth = frame.Border.Width;
        var raised = frame.Border.Style == FormBorderStyle.Beveled;

        builder.SaveState();
        if (background is not null && FormAppearanceColor.TryWriteColor(ref builder, background, false))
        {
            FormBorderAppearance.Ellipse(ref builder, centerX, centerY, outer, outer);
            builder.Fill();
        }

        // The stroke is centred on the path, so the path sits half a line width inside the outer edge.
        builder.SetLineWidth(lineWidth);
        var radius = outer - (lineWidth * Half);
        var tone = border ?? background;
        DrawArc(ref builder, new(centerX, centerY, radius), UpperLeftAngle, tone, raised);
        DrawArc(ref builder, new(centerX, centerY, radius), LowerRightAngle, tone, !raised);
        builder.RestoreState();
    }

    /// <summary>Strokes a half circle in a tone.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="circle">The circle the half belongs to.</param>
    /// <param name="angle">The rotation of the half circle about its centre.</param>
    /// <param name="tone">The base colour, or <see langword="null"/> for a mid grey.</param>
    /// <param name="lit">Whether the half is the lit one.</param>
    private static void DrawArc(ref PdfContentBuilder builder, in RingCircle circle, float angle, PdfArray? tone, bool lit)
    {
        builder.SaveState();
        if (!FormBevelTone.TryWrite(ref builder, tone, lit, true))
        {
            builder.RestoreState();
            return;
        }

        var cos = MathF.Cos(angle);
        var sin = MathF.Sin(angle);
        builder.Transform(cos, sin, -sin, cos, circle.CenterX, circle.CenterY);
        var control = circle.Radius * QuarterKappa;
        builder.MoveTo(-circle.Radius, 0);
        builder.CurveTo(-circle.Radius, control, -control, circle.Radius, 0, circle.Radius);
        builder.CurveTo(control, circle.Radius, circle.Radius, control, circle.Radius, 0);
        builder.Stroke();
        builder.RestoreState();
    }

    /// <summary>The centre and radius of a ring.</summary>
    /// <param name="CenterX">The centre's x.</param>
    /// <param name="CenterY">The centre's y.</param>
    /// <param name="Radius">The radius of the ring's centre line.</param>
    private readonly record struct RingCircle(float CenterX, float CenterY, float Radius);
}
