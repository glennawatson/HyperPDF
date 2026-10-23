// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Renders tiling patterns whose steps overlap or leave gaps, whose cells are clipped to their box, and zoomed patterns.</summary>
public sealed class TilingPatternRenderTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>The largest channel difference accepted.</summary>
    private const int Tolerance = 8;

    /// <summary>A device column in the first step.</summary>
    private const int FirstStep = 5;

    /// <summary>A device column in the second step.</summary>
    private const int SecondStep = 15;

    /// <summary>A device row in the bottom step of the page.</summary>
    private const int BottomRow = 95;

    /// <summary>A device row in the second step up from the bottom.</summary>
    private const int SecondRow = 85;

    /// <summary>The middle of the page.</summary>
    private const int Middle = 50;

    /// <summary>Cell content that paints the whole 10 point cell red.</summary>
    private const string RedSquare = "1 0 0 rg 0 0 10 10 re f";

    /// <summary>The green and blue channel of red covering half the area over white.</summary>
    private const int TinyGreen = 128;

    /// <summary>The channel difference accepted for the averaged tiny cell.</summary>
    private const int TinyTolerance = 48;

    /// <summary>The zoom of the high resolution test.</summary>
    private const float Zoom = 4;

    /// <summary>The zoom of the low resolution test.</summary>
    private const float LowZoom = 0.25F;

    /// <summary>The colour channels compared.</summary>
    private const int ColorChannels = 3;

    /// <summary>The mean channel difference accepted between a 25% page and the averaged 400% page; a fixed 4x period measured 19.8 and the scaled one 20.4.</summary>
    private const double LowZoomMeanDifference = 24;

    /// <summary>The mean channel difference accepted between a 100% page and the averaged 400% page.</summary>
    private const double FullZoomMeanDifference = 12;

    /// <summary>The device column, at four times zoom, in the painted half of a two point cell.</summary>
    private const int PaintedColumn = 2;

    /// <summary>The device column, at four times zoom, in the empty half of a two point cell.</summary>
    private const int EmptyColumn = 6;

    /// <summary>The device row, at four times zoom, near the page bottom.</summary>
    private const int ZoomedRow = 398;

    /// <summary>A cell twice its step wide repeats so the copies overlap: the right half of each cell covers the next step.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OverlappingCellsCoverTheirNeighbours()
    {
        var image = RenderPattern("/BBox [0 0 20 20] /XStep 10 /YStep 10", "1 0 0 rg 10 0 10 20 re f", 1);

        await RenderCheck.Near(image, FirstStep, Middle, Rgb.Red255, Tolerance, nameof(OverlappingCellsCoverTheirNeighbours));
        await RenderCheck.Near(image, SecondStep, Middle, Rgb.Red255, Tolerance, nameof(OverlappingCellsCoverTheirNeighbours));
    }

    /// <summary>A step larger than the cell leaves gaps between copies.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LargeStepsLeaveGaps()
    {
        var image = RenderPattern("/BBox [0 0 10 10] /XStep 20 /YStep 20", RedSquare, 1);

        await RenderCheck.Near(image, FirstStep, BottomRow, Rgb.Red255, Tolerance, nameof(LargeStepsLeaveGaps));
        await RenderCheck.Near(image, SecondStep, BottomRow, Rgb.White, Tolerance, nameof(LargeStepsLeaveGaps));
        await RenderCheck.Near(image, FirstStep, SecondRow, Rgb.White, Tolerance, nameof(LargeStepsLeaveGaps));
    }

    /// <summary>A cell's content is clipped to its /BBox.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CellsAreClippedToTheirBox()
    {
        var image = RenderPattern("/BBox [0 0 10 10] /XStep 20 /YStep 20", "1 0 0 rg 0 0 20 20 re f", 1);

        await RenderCheck.Near(image, SecondStep, BottomRow, Rgb.White, Tolerance, nameof(CellsAreClippedToTheirBox));
    }

    /// <summary>A zero step draws nothing, as in PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ZeroStepDrawsNothing()
    {
        var image = RenderPattern("/BBox [0 0 10 10] /XStep 0 /YStep 10", RedSquare, 1);

        await RenderCheck.Near(image, FirstStep, BottomRow, Rgb.White, Tolerance, nameof(ZeroStepDrawsNothing));
    }

    /// <summary>A small cell stays sharp when zoomed: its painted and empty halves keep their colours.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SmallCellsStaySharpWhenZoomed()
    {
        var image = RenderPattern("/BBox [0 0 2 2] /XStep 2 /YStep 2", "1 0 0 rg 0 0 1 2 re f", Zoom);

        await RenderCheck.Near(image, PaintedColumn, ZoomedRow, Rgb.Red255, Tolerance, nameof(SmallCellsStaySharpWhenZoomed));
        await RenderCheck.Near(image, EmptyColumn, ZoomedRow, Rgb.White, Tolerance, nameof(SmallCellsStaySharpWhenZoomed));
    }

    /// <summary>A page drawn at 25% zoom matches the 400% page averaged down to the same size, so the lower period resolution loses no visible detail.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LowZoom_MatchesTheHighZoomPageAveragedDown()
    {
        const string Entries = "/BBox [0 0 10 10] /XStep 10 /YStep 10";
        const string Cell = "1 0 0 rg 0 0 3.3 10 re f 0 0 1 rg 3.3 0 3.4 5 re f";
        var low =RenderPattern(Entries, Cell, LowZoom);
        var high = RenderPattern(Entries, Cell, Zoom);

        var difference = MeanDifferenceFromAveraged(low, high, (int)(Zoom / LowZoom));

        await Assert.That(difference).IsLessThanOrEqualTo(LowZoomMeanDifference);
    }

    /// <summary>A page drawn at 100% zoom matches the 400% page averaged down to the same size.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FullZoom_MatchesTheHighZoomPageAveragedDown()
    {
        const string Entries = "/BBox [0 0 10 10] /XStep 10 /YStep 10";
        const string Cell = "1 0 0 rg 0 0 3.3 10 re f 0 0 1 rg 3.3 0 3.4 5 re f";
        var full =RenderPattern(Entries, Cell, 1);
        var high = RenderPattern(Entries, Cell, Zoom);

        var difference = MeanDifferenceFromAveraged(full, high, (int)Zoom);

        await Assert.That(difference).IsLessThanOrEqualTo(FullZoomMeanDifference);
    }

    /// <summary>Negative steps (allowed by ISO 32000-2 8.7.3.2) lay the cells on the same lattice as positive ones.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NegativeStepsTileLikePositiveOnes()
    {
        const string Cell = "1 0 0 rg 0 0 6 6 re f 0 0 1 rg 6 6 4 4 re f";
        var positive = RenderPattern("/BBox [0 0 10 10] /XStep 10 /YStep 10", Cell, 1);
        var negative = RenderPattern("/BBox [0 0 10 10] /XStep -10 /YStep -10", Cell, 1);

        await RenderCheck.Near(negative, FirstStep, BottomRow, Rgb.Red255, Tolerance, nameof(NegativeStepsTileLikePositiveOnes));
        await Assert.That(negative.Pixels.AsSpan().SequenceEqual(positive.Pixels)).IsTrue();
    }

    /// <summary>Cells that overlap in both directions cover the page when their painted part reaches their neighbours.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CellsOverlapInBothDirections()
    {
        var image = RenderPattern("/BBox [0 0 20 20] /XStep 10 /YStep 10", "1 0 0 rg 10 10 10 10 re f", 1);

        await RenderCheck.Near(image, FirstStep, BottomRow, Rgb.Red255, Tolerance, nameof(CellsOverlapInBothDirections));
        await RenderCheck.Near(image, SecondStep, SecondRow, Rgb.Red255, Tolerance, nameof(CellsOverlapInBothDirections));
        await RenderCheck.Near(image, Middle, Middle, Rgb.Red255, Tolerance, nameof(CellsOverlapInBothDirections));
    }

    /// <summary>A cell far smaller than a pixel averages its painted share rather than vanishing or aliasing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TinyCellsAverageTheirCoverage()
    {
        var image = RenderPattern("/BBox [0 0 0.5 0.5] /XStep 0.5 /YStep 0.5", "1 0 0 rg 0 0 0.25 0.5 re f", 1);

        await RenderCheck.Near(image, Middle, Middle, new(Rgb.Red255.Red, TinyGreen, TinyGreen), TinyTolerance, nameof(TinyCellsAverageTheirCoverage));
    }

    /// <summary>A step far larger than the page shows only the cell at the pattern origin.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HugeStepsShowOneCell()
    {
        var image = RenderPattern("/BBox [0 0 10 10] /XStep 1000000 /YStep 1000000", RedSquare, 1);

        await RenderCheck.Near(image, FirstStep, BottomRow, Rgb.Red255, Tolerance, nameof(HugeStepsShowOneCell));
        await RenderCheck.Near(image, SecondStep, BottomRow, Rgb.White, Tolerance, nameof(HugeStepsShowOneCell));
        await RenderCheck.Near(image, FirstStep, SecondRow, Rgb.White, Tolerance, nameof(HugeStepsShowOneCell));
        await RenderCheck.Near(image, Middle, Middle, Rgb.White, Tolerance, nameof(HugeStepsShowOneCell));
    }

    /// <summary>Fills the page with a coloured tiling pattern.</summary>
    /// <param name="entries">The pattern's box and steps.</param>
    /// <param name="cell">The cell content.</param>
    /// <param name="scale">The render scale.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage RenderPattern(string entries, string cell, float scale)
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "/Pattern cs /P1 scn 0 0 100 100 re f" };
        var pattern = pdf.AddStream($"/Type /Pattern /PatternType 1 /PaintType 1 /TilingType 1 {entries} /Resources << >>", cell);
        pdf.Resources = $"/Pattern << /P1 {pattern} 0 R >>";
        using var page = new RenderTestPage(pdf.ToBytes());
        return page.RenderPage(scale, 0, PdfRenderFlags.None);
    }

    /// <summary>Gets the mean channel difference between a small render and a larger one averaged down in whole blocks.</summary>
    /// <param name="small">The smaller render.</param>
    /// <param name="large">The larger render.</param>
    /// <param name="block">The pixels of the large render that make one pixel of the small one, along each side.</param>
    /// <returns>The mean absolute difference over the colour channels.</returns>
    private static double MeanDifferenceFromAveraged(RenderedImage small, RenderedImage large, int block)
    {
        long total = 0;
        for (var y = 0; y < small.Height; y++)
        {
            for (var x = 0; x < small.Width; x++)
            {
                for (var channel = 0; channel < ColorChannels; channel++)
                {
                    var sum = 0;
                    for (var by = 0; by < block; by++)
                    {
                        for (var bx = 0; bx < block; bx++)
                        {
                            var pixel = (((y * block) + by) * large.Width) + (x * block) + bx;
                            sum += large.Pixels[(pixel * RenderedImage.BytesPerPixel) + channel];
                        }
                    }

                    var expected = small.Pixels[(((y * small.Width) + x) * RenderedImage.BytesPerPixel) + channel];
                    total += Math.Abs((sum / (block * block)) - expected);
                }
            }
        }

        return (double)total / (small.Width * small.Height * ColorChannels);
    }
}
