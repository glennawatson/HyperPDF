// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Drawing;

namespace HyperPdfLibrary.Charts;

/// <summary>Draws the bars, lines or areas of a bar, column, line or area chart.</summary>
internal static class ChartSeriesPainter
{
    /// <summary>The marker radius, as a multiple of the line width.</summary>
    private const float MarkerScale = 2F;

    /// <summary>The width of the white edge between bars and areas.</summary>
    private const float EdgeWidth = 0.75F;

    /// <summary>Half, for finding middles.</summary>
    private const float Half = 0.5F;

    /// <summary>The two sides a gap is on.</summary>
    private const float BothSides = 2F;

    /// <summary>The gap between a bar and its value label, as a multiple of the font size.</summary>
    private const float LabelGapScale = 0.25F;

    /// <summary>Draws the series.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="context">The chart.</param>
    /// <param name="frame">The plot area and axes.</param>
    internal static void Draw(ref PdfPageCanvas canvas, ChartContext context, in ChartFrame frame)
    {
        switch (context.Chart.Kind)
        {
            case PdfChartKind.Line:
            {
                DrawLines(ref canvas, context, frame);
                break;
            }

            case PdfChartKind.Area:
            {
                DrawAreas(ref canvas, context, frame);
                break;
            }

            default:
            {
                DrawBars(ref canvas, context, frame);
                break;
            }
        }
    }

    /// <summary>Draws one bar for each series in each category.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="context">The chart.</param>
    /// <param name="frame">The plot area and axes.</param>
    private static void DrawBars(ref PdfPageCanvas canvas, ChartContext context, in ChartFrame frame)
    {
        var all = context.Chart.Series.Span;
        for (var series = 0; series < all.Length; series++)
        {
            var style = new PdfShapeStyle(context.GetColor(series), new PdfPen(PdfCanvasColor.White, EdgeWidth));
            var values = all[series].Values.Span;
            for (var category = 0; category < values.Length; category++)
            {
                var bar = frame.GetBar(category, series, all.Length, values[category]);
                canvas.DrawRectangle(style, bar.X, bar.Y, bar.Width, bar.Height);
                if (context.Style.ShowValues)
                {
                    DrawBarLabel(ref canvas, context, frame, bar, values[category]);
                }
            }
        }
    }

    /// <summary>Writes a bar's value just past its end.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="context">The chart.</param>
    /// <param name="frame">The plot area and axes.</param>
    /// <param name="bar">The bar.</param>
    /// <param name="value">The bar's value.</param>
    private static void DrawBarLabel(ref PdfPageCanvas canvas, ChartContext context, in ChartFrame frame, PdfCanvasBox bar, double value)
    {
        var text = ChartNumberFormat.Format(value);
        var width = context.Measure(text);
        var gap = context.Style.FontSize * LabelGapScale;
        var height = context.LineHeight;
        if (frame.Horizontal)
        {
            var x = value < 0D ? bar.X - gap - width : bar.Right + gap;
            canvas.DrawAlignedText(context.Left, new(x, bar.Y + (bar.Height * Half) - (height * Half), width + gap, height), text);
            return;
        }

        var y = value < 0D ? bar.Y - gap - height : bar.Top + gap;
        canvas.DrawAlignedText(context.Center, new(bar.X + (bar.Width * Half) - (width * Half) - gap, y, width + (gap * BothSides), height), text);
    }

    /// <summary>Draws a line through each series with its dash and markers.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="context">The chart.</param>
    /// <param name="frame">The plot area and axes.</param>
    private static void DrawLines(ref PdfPageCanvas canvas, ChartContext context, in ChartFrame frame)
    {
        var all = context.Chart.Series.Span;
        for (var series = 0; series < all.Length; series++)
        {
            var values = all[series].Values.Span;
            var points = new PdfPoint[values.Length];
            for (var category = 0; category < values.Length; category++)
            {
                points[category] = new(frame.GetSlotCenter(category), frame.MapValue(values[category]));
            }

            var color = context.GetColor(series);
            var pen = new PdfPen(color, context.Style.LineWidth) { Dash = ChartMarkers.GetDash(series), Join = PdfLineJoin.Round, Cap = PdfLineCap.Round };
            if (points.Length > 1)
            {
                canvas.DrawPolyline(pen, points);
            }

            for (var category = 0; category < points.Length; category++)
            {
                ChartMarkers.Draw(ref canvas, series, color, points[category], context.Style.LineWidth * MarkerScale);
                if (context.Style.ShowValues)
                {
                    DrawPointLabel(ref canvas, context, points[category], values[category]);
                }
            }
        }
    }

    /// <summary>Writes a value above a point on a line.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="context">The chart.</param>
    /// <param name="point">The point.</param>
    /// <param name="value">The value.</param>
    private static void DrawPointLabel(ref PdfPageCanvas canvas, ChartContext context, PdfPoint point, double value)
    {
        var text = ChartNumberFormat.Format(value);
        var width = context.Measure(text);
        var gap = context.Style.FontSize * LabelGapScale;
        canvas.DrawAlignedText(context.Center, new(point.X - (width * Half) - gap, point.Y + (context.Style.LineWidth * MarkerScale) + gap, width + (gap * BothSides), context.LineHeight), text);
    }

    /// <summary>Draws the series as areas stacked on each other.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="context">The chart.</param>
    /// <param name="frame">The plot area and axes.</param>
    private static void DrawAreas(ref PdfPageCanvas canvas, ChartContext context, in ChartFrame frame)
    {
        var all = context.Chart.Series.Span;
        var count = context.Chart.Categories.Length;
        var below = new double[count];
        for (var series = 0; series < all.Length; series++)
        {
            var values = all[series].Values.Span;
            var polygon = new PdfPoint[count + count];
            for (var category = 0; category < count; category++)
            {
                var x = frame.GetSlotCenter(category);
                polygon[category] = new(x, frame.MapValue(below[category] + values[category]));
                polygon[polygon.Length - 1 - category] = new(x, frame.MapValue(below[category]));
                below[category] += values[category];
            }

            canvas.DrawPolygon(new(context.GetColor(series), new PdfPen(PdfCanvasColor.White, EdgeWidth)), polygon);
        }
    }
}
