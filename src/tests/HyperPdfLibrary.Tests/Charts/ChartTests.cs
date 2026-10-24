// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Charts;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Charts;

/// <summary>Checks chart data rules, the palette, and that each kind of chart draws what it should.</summary>
public sealed class ChartTests
{
    /// <summary>The page side in points and pixels.</summary>
    private const int Side = 200;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The offset of the red channel in a BGRA pixel.</summary>
    private const int RedIndex = 2;

    /// <summary>The font size used for the labels.</summary>
    private const float FontSize = 8F;

    /// <summary>The level of the red channel below which a pixel counts as painted.</summary>
    private const int PaintedLevel = 200;

    /// <summary>The number of colours in the accessible palette.</summary>
    private const int PaletteSize = 8;

    /// <summary>The name of the first series.</summary>
    private const string North = "North";

    /// <summary>The name of the second series.</summary>
    private const string South = "South";

    /// <summary>The categories used by the tests.</summary>
    private static readonly string[] Categories = ["A", "B", "C"];

    /// <summary>Three values that rise.</summary>
    private static readonly double[] Rising = [1D, 2.5D, 3D];

    /// <summary>Three values that fall.</summary>
    private static readonly double[] Falling = [3D, 1D, 2D];

    /// <summary>Three values with a negative one.</summary>
    private static readonly double[] WithNegative = [1D, -2D, 3D];

    /// <summary>Two values, one too few for three categories.</summary>
    private static readonly double[] TooFew = [1D, 2D];

    /// <summary>The description of a chart lists its kind, title and every value.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DescribeListsTheValues()
    {
        var chart = new PdfChart(PdfChartKind.Column, "Sales", Categories, new PdfChartSeries[] { new(North, Rising) });

        await Assert.That(chart.Describe()).IsEqualTo("Column chart: Sales. North: A 1, B 2.5, C 3.");
    }

    /// <summary>A series that has the wrong number of values is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ASeriesNeedsAValueForEveryCategory()
    {
        var chart = new PdfChart(PdfChartKind.Line, string.Empty, Categories, new PdfChartSeries[] { new(North, TooFew) });

        await Assert.That(() => Draw(chart)).Throws<ArgumentException>();
    }

    /// <summary>A pie cannot show a negative value.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task APieRefusesANegativeValue()
    {
        var chart = new PdfChart(PdfChartKind.Pie, string.Empty, Categories, new PdfChartSeries[] { new(North, WithNegative) });

        await Assert.That(() => Draw(chart)).Throws<ArgumentException>();
    }

    /// <summary>A chart without categories is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AChartNeedsCategories()
    {
        var chart = new PdfChart(PdfChartKind.Column, string.Empty, ReadOnlyMemory<string>.Empty, new PdfChartSeries[] { new(North, ReadOnlyMemory<double>.Empty) });

        await Assert.That(() => Draw(chart)).Throws<ArgumentException>();
    }

    /// <summary>Every kind of chart paints something on the page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EveryKindPaints()
    {
        foreach (var kind in Enum.GetValues<PdfChartKind>())
        {
            var chart = new PdfChart(kind, "Title", Categories, new PdfChartSeries[] { new(North, Rising), new(South, Falling) });

            await Assert.That(CountPainted(Draw(chart))).IsGreaterThan(0);
        }
    }

    /// <summary>The accessible palette has eight different colours.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ThePaletteHasEightDistinctColours()
    {
        var palette = PdfChartPalette.Accessible.ToArray();

        await Assert.That(palette.Distinct().Count()).IsEqualTo(palette.Length);
        await Assert.That(palette.Length).IsEqualTo(PaletteSize);
    }

    /// <summary>Draws a chart that fills a page.</summary>
    /// <param name="chart">The chart.</param>
    /// <returns>The premultiplied BGRA pixels.</returns>
    private static byte[] Draw(PdfChart chart)
    {
        var creator = new PdfDocumentCreator(new());
        var font = creator.EmbedFont(PdfBundledFonts.Open(PdfBundledFontFamily.Sans, false, false).Source!);
        var canvas = creator.BeginPage(Side, Side);
        canvas.DrawChart(chart, new(font, FontSize), new(0, 0, Side, Side));
        _ = canvas.Finish();
        canvas.Dispose();

        using var document = SkiaTestRegistration.Open(creator.ToArray(), null);
        using var renderer = new PdfPageRenderer(document);
        var pixels = new byte[Side * Side * BytesPerPixel];
        _ = renderer.Render(new(0, 1, 0, 0, 0, PdfRenderFlags.None), new(pixels, Side, Side, Side * BytesPerPixel));
        return pixels;
    }

    /// <summary>Counts the pixels that are not close to white.</summary>
    /// <param name="pixels">The pixels.</param>
    /// <returns>The count.</returns>
    private static int CountPainted(byte[] pixels)
    {
        var painted = 0;
        for (var i = 0; i < pixels.Length; i += BytesPerPixel)
        {
            if (pixels[i + RedIndex] < PaintedLevel)
            {
                painted++;
            }
        }

        return painted;
    }
}
