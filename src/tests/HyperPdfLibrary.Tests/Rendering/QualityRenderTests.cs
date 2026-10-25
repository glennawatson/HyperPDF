// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Tests.Fonts;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>
/// Pixel checks of the render quality settings at small sizes: sharp text edges, stroke adjustment (ISO 32000-2 10.7.5), the thinnest line and unsmoothed shapes
/// and images.
/// </summary>
[NotInParallel]
public sealed class QualityRenderTests
{
    /// <summary>The page width in points and pixels.</summary>
    private const int Width = FontTestDocument.PageWidth;

    /// <summary>The page height in points and pixels.</summary>
    private const int Height = FontTestDocument.PageHeight;

    /// <summary>The descriptor flags of the non-symbolic test font.</summary>
    private const int FontFlags = 32;

    /// <summary>Lines of small text.</summary>
    private const string Text = "BT /F1 9 Tf 2 80 Td (Hello, world! 0123) Tj 0 -12 Td (The quick brown fox) Tj ET";

    /// <summary>The value of a white channel.</summary>
    private const int White = 255;

    /// <summary>The ink, in channel levels, a measured column may differ by.</summary>
    private const int Tolerance = 10;

    /// <summary>The offset of the red byte in a BGRA pixel.</summary>
    private const int RedOffset = 2;

    /// <summary>The smallest difference between the red and blue channels that counts as a colour fringe.</summary>
    private const int FringeLevel = 24;

    /// <summary>The horizontal line used by the stroke tests: a width under a pixel on a fractional position.</summary>
    private const string ThinLine = "0.4 w 10 20.3 m 100 20.3 l S";

    /// <summary>The column the stroke tests read.</summary>
    private const int Column = 50;

    /// <summary>The ink, in channel levels, a thin line's column may differ by between one pass and two.</summary>
    private const int ThinLineInkBound = 64;

    /// <summary>The thinnest line width in pixels used by the test.</summary>
    private const int ThinnestWidth = 2;

    /// <summary>Sharp text has only fully dark and fully clear pixels.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SharpTextHasNoPartialPixels()
    {
        var image = RenderText(PdfRenderQuality.Default with { TextEdges = PdfTextEdges.Sharp });

        await Assert.That(CountPartial(image)).IsEqualTo(0);
        await Assert.That(Ink(image)).IsGreaterThan(0L);
    }

    /// <summary>Smooth text has partly covered pixels.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SmoothTextHasPartialPixels() =>
        await Assert.That(CountPartial(RenderText(PdfRenderQuality.Default))).IsGreaterThan(0);

    /// <summary>Black text has grey edges at every setting, with no coloured fringes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextEdgesHaveNoColourFringes()
    {
        await Assert.That(CountFringes(RenderText(PdfRenderQuality.Default))).IsEqualTo(0);
        await Assert.That(CountFringes(RenderText(PdfRenderQuality.Default with { TextEdges = PdfTextEdges.Sharp }))).IsEqualTo(0);
    }

    /// <summary>Always fitting lines draws a line under a pixel wide as one full row.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StrokeAdjustmentFitsAThinLineToOneRow()
    {
        var fitted = RenderContent(ThinLine, PdfRenderQuality.Default with { StrokeAdjustment = PdfStrokeAdjustment.Always });
        var rows = Enumerable.Range(0, Height).Where(y => fitted.Pixels[((y * Width) + Column) * RenderedImage.BytesPerPixel] < White).ToArray();

        await Assert.That(rows.Length).IsEqualTo(1);
        await Assert.That(fitted.Pixels[((rows[0] * Width) + Column) * RenderedImage.BytesPerPixel]).IsEqualTo((byte)0);
    }

    /// <summary>The page's own stroke adjustment applies only when the file asks for it and the setting follows the page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StrokeAdjustmentFollowsThePage()
    {
        var follow = PdfRenderQuality.Default with { StrokeAdjustment = PdfStrokeAdjustment.Document };
        var unasked = RenderContent(ThinLine, follow);
        var asked = RenderContent($"/SAOn gs {ThinLine}", follow, "/ExtGState << /SAOn << /SA true >> >>");

        // With the page matrix known a line under a pixel is drawn once, as a hairline, so it may differ a little from the
        // body-plus-hairline line drawn when the matrix is unknown. The ink of the column stays within a bound.
        var unknownMatrix = RenderContent(ThinLine, PdfRenderQuality.Default);
        await Assert.That(Math.Abs(ColumnInk(unasked) - ColumnInk(unknownMatrix))).IsLessThanOrEqualTo(ThinLineInkBound);
        await Assert.That(ColumnInk(asked)).IsEqualTo(White);
    }

    /// <summary>The thinnest line setting widens a thinner line to that many pixels.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ThinnestLineWidensThinLines()
    {
        var wide = RenderContent("0.1 w 10 20.3 m 100 20.3 l S", PdfRenderQuality.Default with { MinimumLineWidth = ThinnestWidth });
        var plain = RenderContent("0.1 w 10 20.3 m 100 20.3 l S", PdfRenderQuality.Default);

        await Assert.That(ColumnInk(wide)).IsGreaterThanOrEqualTo((ThinnestWidth * White) - Tolerance);
        await Assert.That(ColumnInk(plain)).IsLessThan(ColumnInk(wide));
    }

    /// <summary>Unsmoothed shapes have only fully dark and fully clear pixels.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnsmoothedShapesHaveNoPartialPixels()
    {
        const string content = "10.4 10.4 60.3 20.2 re f 80.5 20.5 m 110.2 30.7 l 100.1 8.3 l f";
        var smooth = RenderContent(content, PdfRenderQuality.Default);
        var crisp = RenderContent(content, PdfRenderQuality.Default with { SmoothGraphics = false });

        await Assert.That(CountPartial(smooth)).IsGreaterThan(0);
        await Assert.That(CountPartial(crisp)).IsEqualTo(0);
    }

    /// <summary>Unsmoothed images show the nearest source pixel, so an enlarged image has no in-between greys.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnsmoothedImagesKeepSourcePixels()
    {
        var pdf = new RenderTestPdf(Width, Height) { Content = "q 100 0 0 30 10 5 cm /Im Do Q" };
        var image = pdf.AddStream("/Type /XObject /Subtype /Image /Width 2 /Height 2 /Interpolate true /ColorSpace /DeviceGray /BitsPerComponent 8 /Filter /ASCIIHexDecode", "00FFFF00>");
        pdf.Resources = $"/XObject << /Im {image} 0 R >>";
        var bytes = pdf.ToBytes();

        var smooth = Render(bytes, PdfRenderQuality.Default);
        var crisp = Render(bytes, PdfRenderQuality.Default with { SmoothImages = false });

        await Assert.That(CountPartial(smooth)).IsGreaterThan(0);
        await Assert.That(CountPartial(crisp)).IsEqualTo(0);
    }

    /// <summary>Builds the small text page.</summary>
    /// <returns>The PDF bytes.</returns>
    private static byte[] TextPdf() => FontTestDocument.Build(new FontSpec
    {
        Subtype = "TrueType",
        Program = TestFont.Create(),
        Flags = FontFlags,
        Entries = "/Encoding /WinAnsiEncoding",
        Content = Text,
    });

    /// <summary>Renders the text page.</summary>
    /// <param name="quality">The quality settings.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage RenderText(PdfRenderQuality quality) => Render(TextPdf(), quality);

    /// <summary>Renders a page of content.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="quality">The quality settings.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage RenderContent(string content, PdfRenderQuality quality) => RenderContent(content, quality, string.Empty);

    /// <summary>Renders a page of content with resources.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="quality">The quality settings.</param>
    /// <param name="resources">The resource entries.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage RenderContent(string content, PdfRenderQuality quality, string resources) =>
        Render(new RenderTestPdf(Width, Height) { Content = content, Resources = resources }.ToBytes(), quality);

    /// <summary>Renders a document's first page with quality settings.</summary>
    /// <param name="pdf">The PDF bytes.</param>
    /// <param name="quality">The quality settings.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage Render(byte[] pdf, PdfRenderQuality quality)
    {
        using var page = new RenderTestPage(pdf);
        using var renderer = new PdfPageRenderer(page.Document, PdfRenderOptions.Default with { Quality = quality });
        return Render(renderer);
    }

    /// <summary>Renders the whole first page with a renderer.</summary>
    /// <param name="renderer">The renderer.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage Render(PdfPageRenderer renderer)
    {
        var pixels = new byte[Width * Height * RenderedImage.BytesPerPixel];
        _ = renderer.Render(new(0, 1, 0, 0, 0, PdfRenderFlags.None), new(pixels, Width, Height, Width * RenderedImage.BytesPerPixel));
        return new(pixels, Width, Height);
    }

    /// <summary>Sums the darkness of the red channel over the page.</summary>
    /// <param name="image">The pixels.</param>
    /// <returns>The ink.</returns>
    private static long Ink(RenderedImage image)
    {
        long total = 0;
        for (var i = 0; i < image.Pixels.Length; i += RenderedImage.BytesPerPixel)
        {
            total += White - image.Pixels[i];
        }

        return total;
    }

    /// <summary>Sums the darkness of one column.</summary>
    /// <param name="image">The pixels.</param>
    /// <returns>The ink.</returns>
    private static long ColumnInk(RenderedImage image)
    {
        long total = 0;
        for (var y = 0; y < Height; y++)
        {
            total += White - image.Pixels[((y * Width) + Column) * RenderedImage.BytesPerPixel];
        }

        return total;
    }

    /// <summary>Counts pixels whose blue channel is neither fully dark nor fully clear.</summary>
    /// <param name="image">The pixels.</param>
    /// <returns>The count.</returns>
    private static int CountPartial(RenderedImage image)
    {
        var count = 0;
        for (var i = 0; i < image.Pixels.Length; i += RenderedImage.BytesPerPixel)
        {
            count += image.Pixels[i] is 0 or White ? 0 : 1;
        }

        return count;
    }

    /// <summary>Counts pixels whose red and blue channels differ clearly.</summary>
    /// <param name="image">The pixels.</param>
    /// <returns>The count.</returns>
    private static int CountFringes(RenderedImage image)
    {
        var count = 0;
        for (var i = 0; i < image.Pixels.Length; i += RenderedImage.BytesPerPixel)
        {
            count += Math.Abs(image.Pixels[i] - image.Pixels[i + RedOffset]) >= FringeLevel ? 1 : 0;
        }

        return count;
    }
}
