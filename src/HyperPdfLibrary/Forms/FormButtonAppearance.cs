// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Forms;

/// <summary>Draws check box and radio button appearances.</summary>
internal static class FormButtonAppearance
{
    /// <summary>Half, to centre a mark in its box.</summary>
    private const float Half = 0.5F;

    /// <summary>The check mark style (<c>/MK /CA</c> "4").</summary>
    private const char CheckStyle = '4';

    /// <summary>The cross style ("8").</summary>
    private const char CrossStyle = '8';

    /// <summary>The circle style ("l").</summary>
    private const char CircleStyle = 'l';

    /// <summary>The diamond style ("u").</summary>
    private const char DiamondStyle = 'u';

    /// <summary>The square style ("n").</summary>
    private const char SquareStyle = 'n';

    /// <summary>The star style ("H").</summary>
    private const char StarStyle = 'H';

    /// <summary>The fraction of the box a mark covers.</summary>
    private const float MarkScale = 0.8F;

    /// <summary>The line width of a cross as a fraction of the box.</summary>
    private const float CrossWeight = 0.12F;

    /// <summary>The number of points of a star.</summary>
    private const int StarPoints = 5;

    /// <summary>The number of corners of a star: its points and the notches between them.</summary>
    private const int StarCorners = StarPoints + StarPoints;

    /// <summary>The numbers that make up a corner: x and y.</summary>
    private const int CornerStride = 2;

    /// <summary>The radius of a star's notches as a fraction of its outer radius.</summary>
    private const float StarInset = 0.4F;

    /// <summary>The radius of a circle mark as a fraction of the box.</summary>
    private const float CircleRadius = 0.35F;

    /// <summary>The angle of a full turn.</summary>
    private const float FullTurn = MathF.PI * 2;

    /// <summary>The angle that points a star upwards.</summary>
    private const float QuarterTurn = MathF.PI / 2;

    /// <summary>The most bytes of a caption that are decoded to find the mark style.</summary>
    private const int StyleWindow = 64;

    /// <summary>The scale of the mark tables: their numbers are percentages of the box.</summary>
    private const float Percent = 100;

    /// <summary>Gets the corners of the check mark as x and y pairs, in percent of the box.</summary>
    private static ReadOnlySpan<byte> CheckCorners => [0x0A, 0x34, 0x18, 0x40, 0x2A, 0x28, 0x4E, 0x5C, 0x5C, 0x50, 0x2A, 0x0C];

    /// <summary>Gets the corners of the diamond as x and y pairs, in percent of the box.</summary>
    private static ReadOnlySpan<byte> DiamondCorners => [0x32, 0x64, 0x64, 0x32, 0x32, 0x00, 0x00, 0x32];

    /// <summary>Gets the corners of the square mark as x and y pairs, in percent of the box.</summary>
    private static ReadOnlySpan<byte> SquareCorners => [0x0F, 0x0F, 0x55, 0x0F, 0x55, 0x55, 0x0F, 0x55];

    /// <summary>Gets the ends of the two strokes of a cross as x and y pairs, in percent of the box.</summary>
    private static ReadOnlySpan<byte> CrossEnds => [0x0A, 0x0A, 0x5A, 0x5A, 0x0A, 0x5A, 0x5A, 0x0A];

    /// <summary>Creates the appearance of a check box or radio button in one state.</summary>
    /// <param name="context">The widget.</param>
    /// <param name="isOn">Whether to draw the mark.</param>
    /// <returns>The Form XObject.</returns>
    internal static PdfStream CreateButton(in AppearanceContext context, bool isOn)
    {
        var frame = FormFrame.Create(context);
        var radio = context.Type == PdfFieldType.RadioButton;
        var style = ReadMarkStyle(context.Characteristics, radio);
        var builder = default(PdfContentBuilder);
        try
        {
            FormBorderAppearance.Paint(ref builder, frame, context.Characteristics, radio && style == CircleStyle);
            if (isOn)
            {
                WriteMark(ref builder, frame, context.DefaultAppearance, style);
            }

            return FormAppearanceResources.Finish(ref builder, context, frame, (FormFont?)null);
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Reads the mark style (<c>/MK /CA</c>).</summary>
    /// <param name="characteristics">The widget's <c>/MK</c>, or <see langword="null"/>.</param>
    /// <param name="radio">Whether the widget is a radio button, which defaults to a circle.</param>
    /// <returns>The style character.</returns>
    private static char ReadMarkStyle(PdfDictionary? characteristics, bool radio)
    {
        var caption = characteristics?.Get(KnownName.CA) ?? default;
        if (caption.Kind == PdfKind.String)
        {
            // Only the first character is the style, so a long caption is cut before it is decoded.
            var bytes = caption.AsStringBytes();
            Span<char> first = stackalloc char[StyleWindow];
            var written = PdfText.Decode(bytes[..Math.Min(bytes.Length, StyleWindow)], first);
            if (written > 0)
            {
                return first[0];
            }
        }

        return radio ? CircleStyle : CheckStyle;
    }

    /// <summary>Draws the mark in the middle of the box.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="da">The default appearance, whose colour the mark takes.</param>
    /// <param name="style">The style character.</param>
    private static void WriteMark(ref PdfContentBuilder builder, in FormFrame frame, DefaultAppearance da, char style)
    {
        var side = (Math.Min(frame.Width, frame.Height) - frame.Border.Width - frame.Border.Width) * MarkScale;
        if (side <= 0)
        {
            return;
        }

        builder.SaveState();
        builder.Transform(side, 0, 0, side, (frame.Width - side) * Half, (frame.Height - side) * Half);
        da.Color.WriteFill(ref builder);
        da.Color.WriteStroke(ref builder);
        DrawMark(ref builder, style);
        builder.RestoreState();
    }

    /// <summary>Draws a mark in the unit square.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="style">The style character.</param>
    private static void DrawMark(ref PdfContentBuilder builder, char style)
    {
        switch (style)
        {
            case CrossStyle:
            {
                DrawCross(ref builder);
                break;
            }

            case CircleStyle:
            {
                FormBorderAppearance.Ellipse(ref builder, Half, Half, CircleRadius, CircleRadius);
                builder.Fill();
                break;
            }

            case DiamondStyle:
            {
                FillPolygon(ref builder, DiamondCorners);
                break;
            }

            case SquareStyle:
            {
                FillPolygon(ref builder, SquareCorners);
                break;
            }

            case StarStyle:
            {
                DrawStar(ref builder);
                break;
            }

            default:
            {
                FillPolygon(ref builder, CheckCorners);
                break;
            }
        }
    }

    /// <summary>Draws a cross in the unit square.</summary>
    /// <param name="builder">The content.</param>
    private static void DrawCross(ref PdfContentBuilder builder)
    {
        var ends = CrossEnds;
        builder.SetLineWidth(CrossWeight);
        for (var i = 0; i < ends.Length; i += CornerStride + CornerStride)
        {
            builder.MoveTo(ends[i] / Percent, ends[i + 1] / Percent);
            builder.LineTo(ends[i + CornerStride] / Percent, ends[i + CornerStride + 1] / Percent);
        }

        builder.Stroke();
    }

    /// <summary>Fills a polygon in the unit square.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="corners">The corners as x, y pairs, in percent of the box.</param>
    private static void FillPolygon(ref PdfContentBuilder builder, ReadOnlySpan<byte> corners)
    {
        builder.MoveTo(corners[0] / Percent, corners[1] / Percent);
        for (var i = CornerStride; i + 1 < corners.Length; i += CornerStride)
        {
            builder.LineTo(corners[i] / Percent, corners[i + 1] / Percent);
        }

        builder.ClosePath();
        builder.Fill();
    }

    /// <summary>Fills a five pointed star in the unit square.</summary>
    /// <param name="builder">The content.</param>
    private static void DrawStar(ref PdfContentBuilder builder)
    {
        for (var i = 0; i < StarCorners; i++)
        {
            var angle = (FullTurn * i / StarCorners) + QuarterTurn;
            var radius = i % CornerStride == 0 ? Half : Half * StarInset;
            var x = Half + (radius * MathF.Cos(angle));
            var y = Half + (radius * MathF.Sin(angle));
            if (i == 0)
            {
                builder.MoveTo(x, y);
            }
            else
            {
                builder.LineTo(x, y);
            }
        }

        builder.ClosePath();
        builder.Fill();
    }
}
