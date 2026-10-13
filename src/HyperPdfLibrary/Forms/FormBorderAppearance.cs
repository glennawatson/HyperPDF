// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Forms;

/// <summary>Draws widget backgrounds and borders.</summary>
internal static class FormBorderAppearance
{
    /// <summary>Half, to centre a line on a path.</summary>
    private const float Half = 0.5F;

    /// <summary>The control point offset that makes four Bezier curves a circle.</summary>
    private const float CircleKappa = 0.5522847F;

    /// <summary>The numbers that make up a corner: x and y.</summary>
    private const int CornerStride = 2;

    /// <summary>Draws a circle or ellipse path with four curves.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="centerX">The centre's x.</param>
    /// <param name="centerY">The centre's y.</param>
    /// <param name="radiusX">The horizontal radius.</param>
    /// <param name="radiusY">The vertical radius.</param>
    internal static void Ellipse(ref PdfContentBuilder builder, float centerX, float centerY, float radiusX, float radiusY)
    {
        var kx = radiusX * CircleKappa;
        var ky = radiusY * CircleKappa;
        builder.MoveTo(centerX + radiusX, centerY);
        builder.CurveTo(centerX + radiusX, centerY + ky, centerX + kx, centerY + radiusY, centerX, centerY + radiusY);
        builder.CurveTo(centerX - kx, centerY + radiusY, centerX - radiusX, centerY + ky, centerX - radiusX, centerY);
        builder.CurveTo(centerX - radiusX, centerY - ky, centerX - kx, centerY - radiusY, centerX, centerY - radiusY);
        builder.CurveTo(centerX + kx, centerY - radiusY, centerX + radiusX, centerY - ky, centerX + radiusX, centerY);
        builder.ClosePath();
    }

    /// <summary>Draws the background and border.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="characteristics">The widget's <c>/MK</c>, or <see langword="null"/>.</param>
    /// <param name="circle">Whether the background and border are a circle, as a radio button's are.</param>
    internal static void Paint(ref PdfContentBuilder builder, in FormFrame frame, PdfDictionary? characteristics, bool circle)
    {
        var background = characteristics?.GetArray(KnownName.BG);
        var raised = IsRaised(frame);
        if (circle && raised)
        {
            FormCircleBevel.Paint(ref builder, frame, characteristics?.GetArray(KnownName.BC), background);
            return;
        }

        PaintBackground(ref builder, frame, background, circle);
        if (frame.Border.Width <= 0)
        {
            return;
        }

        if (raised)
        {
            PaintBevel(ref builder, frame, characteristics?.GetArray(KnownName.BC), background);
            return;
        }

        PaintBorder(ref builder, frame, characteristics?.GetArray(KnownName.BC), circle);
    }

    /// <summary>Determines whether the border is a beveled or inset one that has a width.</summary>
    /// <param name="frame">The frame.</param>
    /// <returns><see langword="true"/> for a drawn bevel or inset border.</returns>
    private static bool IsRaised(in FormFrame frame) => frame.Border.Width > 0 && frame.Border.Style is FormBorderStyle.Beveled or FormBorderStyle.Inset;

    /// <summary>Fills the background in the widget's background colour, if it has one.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="color">The <c>/BG</c> colour, or <see langword="null"/>.</param>
    /// <param name="circle">Whether to fill an ellipse.</param>
    private static void PaintBackground(ref PdfContentBuilder builder, in FormFrame frame, PdfArray? color, bool circle)
    {
        if (color is null)
        {
            return;
        }

        builder.SaveState();
        if (FormAppearanceColor.TryWriteColor(ref builder, color, false))
        {
            FillBackground(ref builder, frame, circle);
        }

        builder.RestoreState();
    }

    /// <summary>Strokes the border in the widget's border colour, if it has one.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="color">The <c>/BC</c> colour, or <see langword="null"/>.</param>
    /// <param name="circle">Whether to stroke an ellipse.</param>
    private static void PaintBorder(ref PdfContentBuilder builder, in FormFrame frame, PdfArray? color, bool circle)
    {
        if (color is null)
        {
            return;
        }

        builder.SaveState();
        if (FormAppearanceColor.TryWriteColor(ref builder, color, true))
        {
            StrokeBorder(ref builder, frame, circle);
        }

        builder.RestoreState();
    }

    /// <summary>Fills the drawing area with the colour already set.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="circle">Whether to fill an ellipse.</param>
    private static void FillBackground(ref PdfContentBuilder builder, in FormFrame frame, bool circle)
    {
        if (circle)
        {
            Ellipse(ref builder, frame.Width * Half, frame.Height * Half, frame.Width * Half, frame.Height * Half);
        }
        else
        {
            builder.Rectangle(0, 0, frame.Width, frame.Height);
        }

        builder.Fill();
    }

    /// <summary>Strokes the border with the colour already set.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="circle">Whether to stroke an ellipse.</param>
    private static void StrokeBorder(ref PdfContentBuilder builder, in FormFrame frame, bool circle)
    {
        var width = frame.Border.Width;
        builder.SetLineWidth(width);
        if (frame.Border.Style == FormBorderStyle.Dashed)
        {
            builder.SetDash([frame.Border.Dash], 0);
        }

        if (circle)
        {
            Ellipse(ref builder, frame.Width * Half, frame.Height * Half, (frame.Width - width) * Half, (frame.Height - width) * Half);
        }
        else if (frame.Border.Style == FormBorderStyle.Underline)
        {
            builder.MoveTo(0, width * Half);
            builder.LineTo(frame.Width, width * Half);
        }
        else
        {
            builder.Rectangle(width * Half, width * Half, frame.Width - width, frame.Height - width);
        }

        builder.Stroke();
    }

    /// <summary>
    /// Draws a beveled or inset border (ISO 32000-2 12.5.4, Table 168) as a frame of the requested width, cut into four
    /// mitred strips like a picture frame. The top and left strips take one tone and the bottom and right strips the
    /// other (see <see cref="FormBevelTone"/>): a beveled border is lit from the upper left, an inset border is the
    /// reverse. The tones come from the border colour, else the background colour, else a mid grey.
    /// </summary>
    /// <param name="builder">The content.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="border">The <c>/BC</c> colour, or <see langword="null"/>.</param>
    /// <param name="background">The <c>/BG</c> colour, or <see langword="null"/>.</param>
    private static void PaintBevel(ref PdfContentBuilder builder, in FormFrame frame, PdfArray? border, PdfArray? background)
    {
        var edge = frame.Border.Width;
        var width = frame.Width;
        var height = frame.Height;
        var tone = border ?? background;
        var raised = frame.Border.Style == FormBorderStyle.Beveled;

        builder.SaveState();
        if (FormBevelTone.TryWrite(ref builder, tone, raised, false))
        {
            FillCorners(ref builder, [0, 0, 0, height, width, height, width - edge, height - edge, edge, height - edge, edge, edge]);
        }

        if (FormBevelTone.TryWrite(ref builder, tone, !raised, false))
        {
            FillCorners(ref builder, [width, height, width, 0, 0, 0, edge, edge, width - edge, edge, width - edge, height - edge]);
        }

        builder.RestoreState();
    }

    /// <summary>Fills a polygon given as x, y pairs.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="points">The corners as x, y pairs.</param>
    private static void FillCorners(ref PdfContentBuilder builder, scoped ReadOnlySpan<float> points)
    {
        builder.MoveTo(points[0], points[1]);
        for (var i = CornerStride; i + 1 < points.Length; i += CornerStride)
        {
            builder.LineTo(points[i], points[i + 1]);
        }

        builder.ClosePath();
        builder.Fill();
    }
}
