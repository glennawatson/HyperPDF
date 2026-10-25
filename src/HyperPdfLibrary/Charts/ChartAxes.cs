// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Charts;

/// <summary>Draws the grid, axis lines and labels of a bar, column, line or area chart.</summary>
internal static class ChartAxes
{
    /// <summary>The gap between a label and the plot, as a multiple of the font size.</summary>
    private const float GapScale = 0.5F;

    /// <summary>The width of the grid and axis lines.</summary>
    private const float LineWidth = 0.75F;

    /// <summary>Half, for finding middles.</summary>
    private const float Half = 0.5F;

    /// <summary>The two sides a gap is on.</summary>
    private const float BothSides = 2F;

    /// <summary>Gets the width the labels along the left of the plot need: the value labels of a column chart or the category names of a bar chart.</summary>
    /// <param name="context">The chart.</param>
    /// <param name="scale">The value axis.</param>
    /// <returns>The width in points.</returns>
    internal static float GetLeftMargin(ChartContext context, ChartScale scale)
    {
        var widest = 0F;
        if (context.Chart.Kind == PdfChartKind.Bar)
        {
            foreach (var category in context.Chart.Categories.Span)
            {
                widest = MathF.Max(widest, context.Measure(category));
            }
        }
        else
        {
            widest = GetWidestTick(context, scale);
        }

        return widest + (context.Style.FontSize * GapScale);
    }

    /// <summary>Draws the grid lines and every label, to go behind the series.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="context">The chart.</param>
    /// <param name="frame">The plot area and axes.</param>
    internal static void DrawGrid(ref PdfPageCanvas canvas, ChartContext context, in ChartFrame frame)
    {
        DrawValueTicks(ref canvas, context, frame, new(context.Style.GridColor, LineWidth));
        DrawCategoryLabels(ref canvas, context, frame);
    }

    /// <summary>Draws the axis lines, to go over the series.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="context">The chart.</param>
    /// <param name="frame">The plot area and axes.</param>
    internal static void DrawAxisLines(ref PdfPageCanvas canvas, ChartContext context, in ChartFrame frame)
    {
        var axisPen = new PdfPen(context.Style.AxisColor, LineWidth);
        var zero = frame.MapValue(0D);
        var plot = frame.Plot;
        if (frame.Horizontal)
        {
            canvas.DrawPolyline(axisPen, [new(zero, plot.Y), new(zero, plot.Top)]);
        }
        else
        {
            canvas.DrawPolyline(axisPen, [new(plot.X, zero), new(plot.Right, zero)]);
            canvas.DrawPolyline(axisPen, [new(plot.X, plot.Y), new(plot.X, plot.Top)]);
        }
    }

    /// <summary>Gets the width of the widest value label.</summary>
    /// <param name="context">The chart.</param>
    /// <param name="scale">The value axis.</param>
    /// <returns>The width.</returns>
    private static float GetWidestTick(ChartContext context, ChartScale scale)
    {
        var widest = 0F;
        for (var i = 0; i <= scale.StepCount; i++)
        {
            widest = MathF.Max(widest, context.Measure(ChartNumberFormat.Format(scale.GetTick(i), scale.Step)));
        }

        return widest;
    }

    /// <summary>Draws a grid line and a label at each tick of the value axis.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="context">The chart.</param>
    /// <param name="frame">The plot area and axes.</param>
    /// <param name="gridPen">The pen for the grid lines.</param>
    private static void DrawValueTicks(ref PdfPageCanvas canvas, ChartContext context, in ChartFrame frame, PdfPen gridPen)
    {
        var scale = frame.Scale;
        var plot = frame.Plot;
        var height = context.LineHeight;
        var gap = context.Style.FontSize * GapScale;
        for (var i = 0; i <= scale.StepCount; i++)
        {
            var position = frame.MapValue(scale.GetTick(i));
            var label = ChartNumberFormat.Format(scale.GetTick(i), scale.Step);
            if (frame.Horizontal)
            {
                canvas.DrawPolyline(gridPen, [new(position, plot.Y), new(position, plot.Top)]);
                canvas.DrawAlignedText(context.Center, new(position - (context.Measure(label) * Half) - gap, plot.Y - height, context.Measure(label) + (gap * BothSides), height), label);
            }
            else
            {
                canvas.DrawPolyline(gridPen, [new(plot.X, position), new(plot.Right, position)]);
                canvas.DrawAlignedText(context.Right, new(plot.X - gap - context.Measure(label) - gap, position - (height * Half), context.Measure(label) + gap, height), label);
            }
        }
    }

    /// <summary>Draws the category names, skipping some when they would run into each other.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="context">The chart.</param>
    /// <param name="frame">The plot area and axes.</param>
    private static void DrawCategoryLabels(ref PdfPageCanvas canvas, ChartContext context, in ChartFrame frame)
    {
        var categories = context.Chart.Categories.Span;
        var plot = frame.Plot;
        var height = context.LineHeight;
        var gap = context.Style.FontSize * GapScale;
        var stride = frame.Horizontal ? 1 : GetStride(context, frame);
        for (var i = 0; i < categories.Length; i += stride)
        {
            var width = context.Measure(categories[i]);
            if (frame.Horizontal)
            {
                canvas.DrawAlignedText(context.Right, new(plot.X - gap - width - gap, frame.GetSlotCenter(i) - (height * Half), width + gap, height), categories[i]);
            }
            else
            {
                canvas.DrawAlignedText(context.Center, new(frame.GetSlotCenter(i) - (width * Half) - gap, plot.Y - height - gap, width + (gap * BothSides), height), categories[i]);
            }
        }
    }

    /// <summary>Works out how many categories to step over between labels so that the labels do not overlap.</summary>
    /// <param name="context">The chart.</param>
    /// <param name="frame">The plot area and axes.</param>
    /// <returns>1 to label every category, 2 for every second one, and so on.</returns>
    private static int GetStride(ChartContext context, in ChartFrame frame)
    {
        var widest = 0F;
        foreach (var category in context.Chart.Categories.Span)
        {
            widest = MathF.Max(widest, context.Measure(category));
        }

        return Math.Max(1, (int)MathF.Ceiling((widest + (context.Style.FontSize * GapScale)) / frame.SlotSize));
    }
}
