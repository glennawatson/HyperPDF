// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Drawing;

namespace HyperPdfLibrary.Charts;

/// <summary>Lays out and draws a chart's legend: a row of swatches and names that wraps to more rows when it must.</summary>
internal static class ChartLegend
{
    /// <summary>The width of a filled swatch, as a multiple of the font size.</summary>
    private const float SwatchScale = 0.9F;

    /// <summary>The width of a line swatch, as a multiple of the font size.</summary>
    private const float LineSwatchScale = 2.4F;

    /// <summary>The gap between a swatch and its name, as a multiple of the font size.</summary>
    private const float GapScale = 0.4F;

    /// <summary>The space after an entry, as a multiple of the font size.</summary>
    private const float EntrySpaceScale = 1.2F;

    /// <summary>The width of the line in a line swatch.</summary>
    private const float SwatchLineWidth = 2F;

    /// <summary>The marker radius in a line swatch, as a multiple of the font size.</summary>
    private const float MarkerScale = 0.28F;

    /// <summary>Half, for finding middles.</summary>
    private const float Half = 0.5F;

    /// <summary>Works out where each entry goes.</summary>
    /// <param name="context">The chart.</param>
    /// <param name="names">The entry names.</param>
    /// <param name="width">The width the legend may take.</param>
    /// <returns>The spots, in order.</returns>
    internal static List<LegendSpot> Layout(ChartContext context, string[] names, float width)
    {
        var spots = new List<LegendSpot>(names.Length);
        var x = 0F;
        var row = 0;
        for (var i = 0; i < names.Length; i++)
        {
            var entry = GetEntryWidth(context, names[i]);
            if (x > 0F && x + entry > width)
            {
                x = 0F;
                row++;
            }

            spots.Add(new(i, x, row));
            x += entry;
        }

        return spots;
    }

    /// <summary>Gets the height the legend needs.</summary>
    /// <param name="context">The chart.</param>
    /// <param name="spots">The laid out entries.</param>
    /// <returns>The height; zero with no entries.</returns>
    internal static float GetHeight(ChartContext context, List<LegendSpot> spots) =>
        spots.Count == 0 ? 0F : (spots[^1].Row + 1) * context.LineHeight;

    /// <summary>Draws the legend.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="context">The chart.</param>
    /// <param name="names">The entry names.</param>
    /// <param name="spots">The laid out entries.</param>
    /// <param name="box">The box the legend fills, from its left edge and top.</param>
    internal static void Draw(ref PdfPageCanvas canvas, ChartContext context, string[] names, List<LegendSpot> spots, PdfCanvasBox box)
    {
        var size = context.Style.FontSize;
        var swatch = GetSwatchWidth(context);
        foreach (var (index, x, row) in spots)
        {
            var top = box.Top - (row * context.LineHeight);
            var middle = top - (context.LineHeight * Half);
            var left = box.X + x;
            DrawSwatch(ref canvas, context, index, new(left, middle), swatch);
            canvas.DrawAlignedText(context.Left, new(left + swatch + (size * GapScale), top - context.LineHeight, context.Measure(names[index]) + size, context.LineHeight), names[index]);
        }
    }

    /// <summary>Gets the width of one entry, with the space after it.</summary>
    /// <param name="context">The chart.</param>
    /// <param name="name">The entry name.</param>
    /// <returns>The width.</returns>
    private static float GetEntryWidth(ChartContext context, string name) =>
        GetSwatchWidth(context) + (context.Style.FontSize * (GapScale + EntrySpaceScale)) + context.Measure(name);

    /// <summary>Gets the width of a swatch: wide enough for a dashed line with a marker in a line chart, a square otherwise.</summary>
    /// <param name="context">The chart.</param>
    /// <returns>The width.</returns>
    private static float GetSwatchWidth(ChartContext context) =>
        context.Style.FontSize * (context.Chart.Kind == PdfChartKind.Line ? LineSwatchScale : SwatchScale);

    /// <summary>Draws one swatch centred on a height.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="context">The chart.</param>
    /// <param name="index">The entry.</param>
    /// <param name="left">The swatch's left edge and the height it is centred on.</param>
    /// <param name="width">The swatch's width.</param>
    private static void DrawSwatch(ref PdfPageCanvas canvas, ChartContext context, int index, PdfPoint left, float width)
    {
        var color = context.GetColor(index);
        if (context.Chart.Kind != PdfChartKind.Line)
        {
            canvas.DrawRectangle(PdfShapeStyle.Filled(color), left.X, left.Y - (width * Half), width, width);
            return;
        }

        var pen = new PdfPen(color, SwatchLineWidth) { Dash = ChartMarkers.GetDash(index) };
        canvas.DrawPolyline(pen, [left, new(left.X + width, left.Y)]);
        ChartMarkers.Draw(ref canvas, index, color, new(left.X + (width * Half), left.Y), context.Style.FontSize * MarkerScale);
    }
}
