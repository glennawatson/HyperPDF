// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Checks that a large image reduction averages the source pixels, and that the options turn it and smoothing off.</summary>
public sealed class ImageAreaReductionRenderTests
{
    /// <summary>The side of the source checkerboard in pixels.</summary>
    private const int SourceSide = 64;

    /// <summary>The page side in points, so the image shrinks eight times.</summary>
    private const int PageSide = 8;

    /// <summary>The sRGB value of an equal mix of black and white light.</summary>
    private const int LightMix = 188;

    /// <summary>The largest channel difference accepted.</summary>
    private const int Tolerance = 4;

    /// <summary>A reduction by area gives the grey of an equal mix of light everywhere.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AreaReductionAveragesTheCheckerboardAsLight()
    {
        var image = Render(PdfRenderQuality.Default);

        for (var i = 0; i < PageSide * PageSide; i++)
        {
            await Assert.That(Math.Abs(image.Pixels[i * RenderedImage.BytesPerPixel] - LightMix)).IsLessThanOrEqualTo(Tolerance);
        }
    }

    /// <summary>Without area reduction the picture draws the full image through Skia's own filtering, which gives another value.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TurningAreaReductionOffKeepsTheFullDecode()
    {
        var reduced = Render(PdfRenderQuality.Default);
        var full = Render(PdfRenderQuality.Default with { AreaImageReduction = false });

        await Assert.That(full.Pixels.AsSpan().SequenceEqual(reduced.Pixels)).IsFalse();
    }

    /// <summary>The same image gives the same pixels whether it is stored inline or as an image XObject.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InlineImagesReduceLikeStoredImages()
    {
        var stored = Render(PdfRenderQuality.Default);
        var inline = RenderInline(PdfRenderQuality.Default);

        await Assert.That(inline.Pixels.AsSpan().SequenceEqual(stored.Pixels)).IsTrue();
        await Assert.That(Math.Abs(inline.Pixels[0] - LightMix)).IsLessThanOrEqualTo(Tolerance);
    }

    /// <summary>With area reduction off, an inline image still draws the full decode, as a stored image does.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InlineImagesKeepTheFullDecodeWhenReductionIsOff()
    {
        var off = PdfRenderQuality.Default with { AreaImageReduction = false };

        await Assert.That(RenderInline(off).Pixels.AsSpan().SequenceEqual(Render(off).Pixels)).IsTrue();
        await Assert.That(RenderInline(off).Pixels.AsSpan().SequenceEqual(RenderInline(PdfRenderQuality.Default).Pixels)).IsFalse();
    }

    /// <summary>Renders the checkerboard stored inline in the page content.</summary>
    /// <param name="quality">The quality settings.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage RenderInline(PdfRenderQuality quality)
    {
        var content = $"q {PageSide} 0 0 {PageSide} 0 0 cm BI /W {SourceSide} /H {SourceSide} /CS /G /BPC 8 /F /AHx ID {Checkerboard()} EI Q";
        return RenderPage(new(PageSide, PageSide) { Content = content }, quality);
    }

    /// <summary>Renders the checkerboard page, a tile covering all of it.</summary>
    /// <param name="quality">The quality settings.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage Render(PdfRenderQuality quality)
    {
        var pdf = new RenderTestPdf(PageSide, PageSide) { Content = $"q {PageSide} 0 0 {PageSide} 0 0 cm /Im Do Q" };
        var image = pdf.AddStream(
            $"/Type /XObject /Subtype /Image /Width {SourceSide} /Height {SourceSide} /ColorSpace /DeviceGray /BitsPerComponent 8 /Filter /ASCIIHexDecode",
            Checkerboard());
        pdf.Resources = $"/XObject << /Im {image} 0 R >>";
        return RenderPage(pdf, quality);
    }

    /// <summary>Renders a one page test document, a tile covering all of it.</summary>
    /// <param name="pdf">The document.</param>
    /// <param name="quality">The quality settings.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage RenderPage(RenderTestPdf pdf, PdfRenderQuality quality)
    {
        using var page = new RenderTestPage(pdf.ToBytes());
        using var renderer = new PdfPageRenderer(page.Document, PdfRenderOptions.Default with { Quality = quality });
        var pixels = new byte[PageSide * PageSide * RenderedImage.BytesPerPixel];
        _ = renderer.Render(new(0, 1, 0, 0, 0, PdfRenderFlags.None), new(pixels, PageSide, PageSide, PageSide * RenderedImage.BytesPerPixel));
        return new(pixels, PageSide, PageSide);
    }

    /// <summary>Builds the hexadecimal samples of a one pixel checkerboard.</summary>
    /// <returns>The hexadecimal text, ending with the filter's end marker.</returns>
    private static string Checkerboard()
    {
        var text = new StringBuilder();
        for (var y = 0; y < SourceSide; y++)
        {
            for (var x = 0; x < SourceSide; x++)
            {
                _ = text.Append(((x + y) & 1) == 0 ? "00" : "FF");
            }
        }

        return text.Append('>').ToString();
    }
}
