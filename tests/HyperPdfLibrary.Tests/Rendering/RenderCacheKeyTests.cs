// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Checks that cached pattern cells and soft masks are only reused under the settings they were recorded for.</summary>
public sealed class RenderCacheKeyTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>The largest channel difference accepted.</summary>
    private const int Tolerance = 8;

    /// <summary>A device column inside the painted corner of the recursive pattern's cell.</summary>
    private const int CornerColumn = 2;

    /// <summary>A device row inside the painted corner, near the page bottom.</summary>
    private const int CornerRow = 97;

    /// <summary>Two renders: one for the screen and one for printing.</summary>
    private const int TwoModes = 2;

    /// <summary>A pattern cell recorded for the screen is not reused for printing, where optional content follows print usage.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PatternCellsAreRecordedAgainForPrinting()
    {
        using var page = new RenderTestPage(CreatePatternPage());

        _ = page.RenderPage(1, 0, PdfRenderFlags.None);
        var afterScreen = page.Renderer.Cache.Cells.Count;
        _ = page.RenderPage(1, 0, PdfRenderFlags.Printing);

        await Assert.That(afterScreen).IsEqualTo(1);
        await Assert.That(page.Renderer.Cache.Cells.Count).IsEqualTo(TwoModes);
    }

    /// <summary>A soft mask recorded for the screen is not reused for printing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SoftMasksAreRecordedAgainForPrinting()
    {
        using var page = new RenderTestPage(CreateSoftMaskPage());

        _ = page.RenderPage(1, 0, PdfRenderFlags.None);
        var afterScreen = page.Renderer.Cache.SoftMasks.Count;
        _ = page.RenderPage(1, 0, PdfRenderFlags.Printing);

        await Assert.That(afterScreen).IsEqualTo(1);
        await Assert.That(page.Renderer.Cache.SoftMasks.Count).IsEqualTo(TwoModes);
    }

    /// <summary>A newer layer version drops the cells and soft masks of older versions, which can no longer be asked for.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ANewLayerVersionDropsOlderCellsAndMasks()
    {
        using var patterns = new RenderTestPage(CreatePatternPage());
        _ = patterns.RenderPage();
        var cells = patterns.Renderer.Cache.Cells.Count;
        patterns.Renderer.Cache.ObserveLayerVersion(1);

        using var masks = new RenderTestPage(CreateSoftMaskPage());
        _ = masks.RenderPage();
        var softMasks = masks.Renderer.Cache.SoftMasks.Count;
        masks.Renderer.Cache.ObserveLayerVersion(1);

        await Assert.That(cells).IsEqualTo(1);
        await Assert.That(patterns.Renderer.Cache.Cells.Count).IsEqualTo(0);
        await Assert.That(softMasks).IsEqualTo(1);
        await Assert.That(masks.Renderer.Cache.SoftMasks.Count).IsEqualTo(0);
    }

    /// <summary>Closing the cache and reloading fonts both release the pattern paints that hold old cells.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PatternPaintsAreReleasedWithTheirCells()
    {
        using var page = new RenderTestPage(CreatePatternPage());
        _ = page.RenderPage();
        var cache = page.Renderer.Cache;
        var before = cache.PatternPaints.Count;

        cache.InvalidateFonts();
        var afterFonts = cache.PatternPaints.Count;
        _ = page.RenderPage();
        cache.Close();

        await Assert.That(before).IsEqualTo(1);
        await Assert.That(afterFonts).IsEqualTo(0);
        await Assert.That(cache.PatternPaints.Count).IsEqualTo(0);
    }

    /// <summary>A pattern that selects itself is cut off at the depth limit, and that cut-off is not remembered as the pattern's cell.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ADepthLimitedPatternStillDrawsItsOwnContent()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "/Pattern cs /P1 scn 0 0 100 100 re f" };
        var number = pdf.NextNumber;
        _ = pdf.AddStream(
            $"/Type /Pattern /PatternType 1 /PaintType 1 /TilingType 1 /BBox [0 0 10 10] /XStep 10 /YStep 10 /Resources << /Pattern << /P1 {number} 0 R >> >>",
            "1 0 0 rg 0 0 5 5 re f /Pattern cs /P1 scn 5 5 5 5 re f");
        pdf.Resources = $"/Pattern << /P1 {number} 0 R >>";
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage();

        await RenderCheck.Near(image, CornerColumn, CornerRow, Rgb.Red255, Tolerance, nameof(ADepthLimitedPatternStillDrawsItsOwnContent));
    }

    /// <summary>Builds a page painted with a tiling pattern.</summary>
    /// <returns>The PDF.</returns>
    private static byte[] CreatePatternPage()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "/Pattern cs /P1 scn 0 0 100 100 re f" };
        var pattern = pdf.AddStream("/Type /Pattern /PatternType 1 /PaintType 1 /TilingType 1 /BBox [0 0 10 10] /XStep 10 /YStep 10 /Resources << >>", "1 0 0 rg 0 0 10 10 re f");
        pdf.Resources = $"/Pattern << /P1 {pattern} 0 R >>";
        return pdf.ToBytes();
    }

    /// <summary>Builds a page painted through a luminosity soft mask.</summary>
    /// <returns>The PDF.</returns>
    private static byte[] CreateSoftMaskPage()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "/GS gs 1 0 0 rg 0 0 100 100 re f" };
        var group = pdf.AddStream(
            "/Type /XObject /Subtype /Form /BBox [0 0 100 100] /Group << /S /Transparency /CS /DeviceGray >>",
            "1 g 0 0 50 100 re f");
        var state = pdf.AddObject($"<< /Type /ExtGState /SMask << /Type /Mask /S /Luminosity /G {group} 0 R >> >>");
        pdf.Resources = $"/ExtGState << /GS {state} 0 R >>";
        return pdf.ToBytes();
    }
}
