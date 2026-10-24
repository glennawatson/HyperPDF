// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Charts;

/// <summary>Draws a pie chart: one slice for each category of the first series, starting at the top and going clockwise.</summary>
internal static class ChartPiePainter
{
    /// <summary>The degrees in a full turn.</summary>
    private const float FullTurn = 360F;

    /// <summary>The angle of the top of the pie, in degrees anticlockwise from the right.</summary>
    private const float TopAngle = 90F;

    /// <summary>How much of the room the pie takes; the rest is for the labels round it.</summary>
    private const float PieShare = 0.7F;

    /// <summary>How far past the edge a label's middle sits, as a multiple of the radius.</summary>
    private const float LabelReach = 1.15F;

    /// <summary>The width of the white line between slices.</summary>
    private const float EdgeWidth = 1F;

    /// <summary>The degrees in half a turn, to convert to radians.</summary>
    private const float HalfTurn = 180F;

    /// <summary>The factor from a share to a percentage.</summary>
    private const float PercentScale = 100F;

    /// <summary>How far to the side a label's direction must point, as the cosine of its angle, for the label to sit beside its slice instead of over it.</summary>
    private const float SideThreshold = 0.3F;

    /// <summary>Half, for finding middles.</summary>
    private const float Half = 0.5F;

    /// <summary>Draws the pie and the label of each slice.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="context">The chart.</param>
    /// <param name="area">The area the pie and its labels fill.</param>
    internal static void Draw(ref PdfPageCanvas canvas, ChartContext context, PdfCanvasBox area)
    {
        var values = context.Chart.Series.Span[0].Values.Span;
        var total = 0D;
        foreach (var value in values)
        {
            total += value;
        }

        if (total <= 0D)
        {
            return;
        }

        var diameter = MathF.Min(area.Width, area.Height) * PieShare;
        var centerX = area.X + (area.Width * Half);
        var centerY = area.Y + (area.Height * Half);
        var left = centerX - (diameter * Half);
        var bottom = centerY - (diameter * Half);
        var start = TopAngle;
        for (var slice = 0; slice < values.Length; slice++)
        {
            var fraction = (float)(values[slice] / total);
            var sweep = -fraction * FullTurn;
            if (values[slice] > 0D)
            {
                var style = new PdfShapeStyle(context.GetColor(slice), new PdfPen(PdfCanvasColor.White, EdgeWidth));
                canvas.DrawPie(style, left, bottom, diameter, diameter, start, sweep);
                DrawLabel(ref canvas, context, new(centerX, centerY, diameter * Half, values[slice]), start + (sweep * Half), fraction);
            }

            start += sweep;
        }
    }

    /// <summary>Writes a slice's share, and its value when values are shown, outside the slice.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="context">The chart.</param>
    /// <param name="pie">The pie's middle and radius, and the slice's value.</param>
    /// <param name="angle">The angle of the slice's middle, in degrees anticlockwise from the right.</param>
    /// <param name="fraction">The slice's share of the whole.</param>
    private static void DrawLabel(ref PdfPageCanvas canvas, ChartContext context, PieSlice pie, float angle, float fraction)
    {
        var percent = $"{(fraction * PercentScale):0.#}%";
        var text = context.Style.ShowValues ? $"{ChartNumberFormat.Format(pie.Value)} ({percent})" : percent;
        var radians = angle * MathF.PI / HalfTurn;
        var x = pie.CenterX + (MathF.Cos(radians) * pie.Radius * LabelReach);
        var y = pie.CenterY + (MathF.Sin(radians) * pie.Radius * LabelReach);
        var width = context.Measure(text) + context.Style.FontSize;
        var cos = MathF.Cos(radians);
        var left = cos switch
        {
            > SideThreshold => x,
            < -SideThreshold => x - width,
            _ => x - (width * Half),
        };
        var style = cos switch
        {
            > SideThreshold => context.Left,
            < -SideThreshold => context.Right,
            _ => context.Center,
        };
        canvas.DrawAlignedText(style, new(left, y - (context.LineHeight * Half), width, context.LineHeight), text);
    }
}
