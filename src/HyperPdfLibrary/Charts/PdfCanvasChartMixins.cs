// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Charts;

/// <summary>
/// Draws a <see cref="PdfChart"/> on a <see cref="PdfPageCanvas"/> as vector shapes and text, with a title, axes, grid lines
/// and a legend. Colour is never the only cue: line series differ by dash and marker, and values can be written on the data.
/// In a tagged document the call must be inside <see cref="PdfPageCanvas.BeginContent(PdfTagElement)"/> or
/// <see cref="PdfPageCanvas.BeginArtifact(PdfArtifactKind)"/>; use <see cref="PdfChart.Describe"/> as the figure's alternative text.
/// </summary>
public static class PdfCanvasChartMixins
{
    /// <summary>The title's size as a multiple of the label size.</summary>
    private const float TitleScale = 1.3F;

    /// <summary>The gap between the parts of a chart, as a multiple of the font size.</summary>
    private const float GapScale = 0.6F;

    /// <summary>The space past the plot's right edge for the last tick label, as a multiple of the font size.</summary>
    private const float RightMarginScale = 2F;

    /// <summary>Half, for finding middles.</summary>
    private const float Half = 0.5F;

    /// <summary>Adds chart drawing to a page canvas.</summary>
    /// <param name="canvas">The page canvas.</param>
    extension(ref PdfPageCanvas canvas)
    {
        /// <summary>Draws a chart to fill a box.</summary>
        /// <param name="chart">The chart.</param>
        /// <param name="style">The font, colours and options.</param>
        /// <param name="box">The box to fill.</param>
        /// <exception cref="ArgumentNullException"><paramref name="chart"/> or <paramref name="style"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">The chart has no categories or series, a series has the wrong number of values, or a value is not valid for the kind of chart.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The box or the font size is not positive.</exception>
        /// <exception cref="InvalidOperationException">The document is tagged and no content or artifact is open.</exception>
        public void DrawChart(PdfChart chart, PdfChartStyle style, PdfCanvasBox box)
        {
            ArgumentNullException.ThrowIfNull(chart);
            ArgumentNullException.ThrowIfNull(style);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(style.FontSize);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(box.Width);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(box.Height);
            chart.Validate();
            canvas.RequireMarked();
            canvas.Content.SaveState();
            var context = ChartContext.Create(chart, style);
            var gap = style.FontSize * GapScale;
            var area = DrawTitle(ref canvas, context, box, gap);
            area = DrawLegend(ref canvas, context, area, gap);
            if (area.Width > 0F && area.Height > 0F)
            {
                DrawBody(ref canvas, context, area, gap);
            }

            canvas.Content.RestoreState();
        }
    }

    /// <summary>Draws the title along the top, if there is one.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="context">The chart.</param>
    /// <param name="box">The whole chart's box.</param>
    /// <param name="gap">The gap between the parts.</param>
    /// <returns>The box below the title.</returns>
    private static PdfCanvasBox DrawTitle(ref PdfPageCanvas canvas, ChartContext context, PdfCanvasBox box, float gap)
    {
        if (context.Chart.Title.Length == 0)
        {
            return box;
        }

        var size = context.Style.FontSize * TitleScale;
        var height = size * (context.LineHeight / context.Style.FontSize);
        canvas.DrawAlignedText(context.Center with { Size = size }, new(box.X, box.Top - height, box.Width, height), context.Chart.Title);
        return new(box.X, box.Y, box.Width, box.Height - height - gap);
    }

    /// <summary>Draws the legend along the bottom, if it is shown.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="context">The chart.</param>
    /// <param name="area">The box left for the legend and the chart.</param>
    /// <param name="gap">The gap between the parts.</param>
    /// <returns>The box above the legend.</returns>
    private static PdfCanvasBox DrawLegend(ref PdfPageCanvas canvas, ChartContext context, PdfCanvasBox area, float gap)
    {
        if (!context.Style.ShowLegend)
        {
            return area;
        }

        var names = ChartData.GetLegendNames(context.Chart);
        var spots = ChartLegend.Layout(context, names, area.Width);
        var height = ChartLegend.GetHeight(context, spots);
        ChartLegend.Draw(ref canvas, context, names, spots, new(area.X, area.Y, area.Width, height));
        return new(area.X, area.Y + height + gap, area.Width, area.Height - height - gap);
    }

    /// <summary>Draws the pie, or the axes and series.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="context">The chart.</param>
    /// <param name="area">The box for the chart itself.</param>
    /// <param name="gap">The gap between the parts.</param>
    private static void DrawBody(ref PdfPageCanvas canvas, ChartContext context, PdfCanvasBox area, float gap)
    {
        if (context.Chart.Kind == PdfChartKind.Pie)
        {
            ChartPiePainter.Draw(ref canvas, context, area);
            return;
        }

        var scale = ChartData.GetScale(context.Chart);
        var left = ChartAxes.GetLeftMargin(context, scale);
        var bottom = context.LineHeight + gap;
        var right = context.Style.FontSize * RightMarginScale;
        var plot = new PdfCanvasBox(area.X + left, area.Y + bottom, area.Width - left - right, area.Height - bottom - (context.LineHeight * Half));
        if (plot.Width <= 0F || plot.Height <= 0F)
        {
            return;
        }

        var frame = new ChartFrame(plot, scale, context.Chart.Categories.Length, context.Chart.Kind == PdfChartKind.Bar);
        ChartAxes.DrawGrid(ref canvas, context, frame);
        ChartSeriesPainter.Draw(ref canvas, context, frame);
        ChartAxes.DrawAxisLines(ref canvas, context, frame);
    }
}
