// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Charts;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Fonts.Embedding;

namespace HyperPdf.Benchmarks;

/// <summary>Measures drawing charts on a page; allocations come from the EventPipe trace.</summary>
public class ChartBenchmarks
{
    /// <summary>The side of the page and chart, in points.</summary>
    private const float Side = 300F;

    /// <summary>The font size of the labels, in points.</summary>
    private const float FontSize = 9F;

    /// <summary>The category names.</summary>
    private static readonly string[] Names = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug"];

    /// <summary>The first series.</summary>
    private static readonly double[] North = [12, 18, 15, 22, 30, 28, 35, 41];

    /// <summary>The second series.</summary>
    private static readonly double[] South = [9, 11, 14, 13, 19, 25, 24, 29];

    /// <summary>The creator, holding the embedded font.</summary>
    private PdfDocumentCreator _creator = null!;

    /// <summary>The chart font.</summary>
    private PdfCanvasFont _font = null!;

    /// <summary>The column chart.</summary>
    private PdfChart _column = null!;

    /// <summary>The line chart.</summary>
    private PdfChart _line = null!;

    /// <summary>The pie chart.</summary>
    private PdfChart _pie = null!;

    /// <summary>Embeds the font and builds the charts.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _creator = new(PdfDocumentCreatorOptions.Default);
        _font = _creator.EmbedFont(PdfBundledFonts.Open(PdfBundledFontFamily.Sans, false, false).Source!);
        PdfChartSeries[] series = [new("Region one", North), new("Region two", South)];
        _column = new(PdfChartKind.Column, "Sales", Names, series);
        _line = new(PdfChartKind.Line, "Sales", Names, series);
        _pie = new(PdfChartKind.Pie, "Share", Names, new PdfChartSeries[] { new("Region one", North) });
    }

    /// <summary>Draws a column chart with two series.</summary>
    [Benchmark]
    public void DrawColumn() => Draw(_column);

    /// <summary>Draws a line chart with two series.</summary>
    [Benchmark]
    public void DrawLine() => Draw(_line);

    /// <summary>Draws a pie chart.</summary>
    [Benchmark]
    public void DrawPie() => Draw(_pie);

    /// <summary>Draws a chart on a new page and discards the page.</summary>
    /// <param name="chart">The chart.</param>
    private void Draw(PdfChart chart)
    {
        var canvas = _creator.BeginPage(Side, Side);
        try
        {
            canvas.DrawChart(chart, new(_font, FontSize), new(0, 0, Side, Side));
        }
        finally
        {
            canvas.Dispose();
        }
    }
}
