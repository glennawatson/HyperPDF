// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Renders image XObjects, inline images and stencil masks.</summary>
public sealed class ImageRenderTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>The largest channel difference accepted.</summary>
    private const int Tolerance = 6;

    /// <summary>A coordinate inside the first image column or row.</summary>
    private const int Near = 25;

    /// <summary>A coordinate inside the second image column or row.</summary>
    private const int Far = 75;

    /// <summary>The image drawn into the whole page: red and green on the top row, blue and yellow below.</summary>
    private const string ColorImageData = "FF0000 00FF00 0000FF FFFF00>";

    /// <summary>The entries of the 2x2 RGB test image.</summary>
    private const string ColorImageEntries = "/Type /XObject /Subtype /Image /Width 2 /Height 2 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /ASCIIHexDecode";

    /// <summary>An image XObject is scaled into the unit square with its first row at the top.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ImageXObjectFillsTheUnitSquare()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "q 100 0 0 100 0 0 cm /Im Do Q" };
        var image = pdf.AddStream(ColorImageEntries, ColorImageData);
        pdf.Resources = $"/XObject << /Im {image} 0 R >>";
        using var page = new RenderTestPage(pdf.ToBytes());

        var result = page.RenderPage();

        await Assert.That(result.IsNear(Near, Near, Rgb.Red255, Tolerance)).IsTrue();
        await Assert.That(result.IsNear(Far, Near, Rgb.Green255, Tolerance)).IsTrue();
        await Assert.That(result.IsNear(Near, Far, Rgb.Blue255, Tolerance)).IsTrue();
        await Assert.That(result.IsNear(Far, Far, Rgb.Yellow, Tolerance)).IsTrue();
    }

    /// <summary>An inline image is decoded with its abbreviated keys.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InlineImageIsDrawn()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "q 100 0 0 100 0 0 cm BI /W 2 /H 2 /CS /RGB /BPC 8 /F /AHx ID FF0000 00FF00 0000FF FFFF00> EI Q" };
        using var page = new RenderTestPage(pdf.ToBytes());

        var result = page.RenderPage();

        await Assert.That(result.IsNear(Near, Near, Rgb.Red255, Tolerance)).IsTrue();
        await Assert.That(result.IsNear(Far, Far, Rgb.Yellow, Tolerance)).IsTrue();
    }

    /// <summary>A stencil mask paints the fill colour where its samples are 0.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StencilMaskUsesTheFillColor()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "1 0 0 rg q 100 0 0 100 0 0 cm /Im Do Q" };
        var image = pdf.AddStream("/Type /XObject /Subtype /Image /Width 2 /Height 2 /ImageMask true /BitsPerComponent 1 /Filter /ASCIIHexDecode", "40 80>");
        pdf.Resources = $"/XObject << /Im {image} 0 R >>";
        using var page = new RenderTestPage(pdf.ToBytes());

        var result = page.RenderPage();

        await Assert.That(result.IsNear(Near, Near, Rgb.Red255, Tolerance)).IsTrue();
        await Assert.That(result.IsNear(Far, Near, Rgb.White, Tolerance)).IsTrue();
        await Assert.That(result.IsNear(Near, Far, Rgb.White, Tolerance)).IsTrue();
        await Assert.That(result.IsNear(Far, Far, Rgb.Red255, Tolerance)).IsTrue();
    }

    /// <summary>An image XObject hidden by an optional content group is not drawn.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HiddenImageIsSkipped()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "q 100 0 0 100 0 0 cm /Im Do Q" };
        var group = pdf.AddObject("<< /Type /OCG /Name (Hidden) >>");
        var image = pdf.AddStream($"{ColorImageEntries} /OC {group} 0 R", ColorImageData);
        pdf.Resources = $"/XObject << /Im {image} 0 R >>";
        pdf.CatalogEntries = $"/OCProperties << /OCGs [{group} 0 R] /D << /OFF [{group} 0 R] >> >>";
        using var page = new RenderTestPage(pdf.ToBytes());

        var result = page.RenderPage();

        await Assert.That(result.IsNear(Near, Near, Rgb.White, Tolerance)).IsTrue();
    }
}
