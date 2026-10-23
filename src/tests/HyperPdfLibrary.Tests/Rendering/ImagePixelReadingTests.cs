// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>
/// Tests that the interpreter decodes images for a device that reads pixels and reports only where they land for a device
/// that does not, and that a page render still draws the decoded pixels.
/// </summary>
public sealed class ImagePixelReadingTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>The middle of the page.</summary>
    private const int Middle = 50;

    /// <summary>The largest channel difference accepted.</summary>
    private const int Tolerance = 2;

    /// <summary>A decodable one pixel black image XObject covering the page.</summary>
    private const string StoredImage = "/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8 /Filter /ASCIIHexDecode";

    /// <summary>The content that draws the image resource over the page.</summary>
    private const string DrawStored = "q 100 0 0 100 0 0 cm /Im0 Do Q";

    /// <summary>The content that draws a one pixel black inline image over the page.</summary>
    private const string DrawInline = "q 100 0 0 100 0 0 cm BI /W 1 /H 1 /CS /G /BPC 8 /F /AHx ID 00> EI Q";

    /// <summary>A device that reads pixels receives the decoded image, not the area call.</summary>
    /// <param name="inline">Whether the image is inline.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task DeviceThatReadsPixelsReceivesTheDecodedImage(bool inline)
    {
        var device = GraphicsStateRecorder.RunWithImages(Pdf(inline), true);

        await Assert.That(device.Images).IsEqualTo(1);
        await Assert.That(device.ImageAreas).IsEqualTo(0);
    }

    /// <summary>A device that does not read pixels receives the area call once and no decoded image.</summary>
    /// <param name="inline">Whether the image is inline.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task DeviceThatSkipsPixelsReceivesTheAreaOnly(bool inline)
    {
        var device = GraphicsStateRecorder.RunWithImages(Pdf(inline), false);

        await Assert.That(device.Images).IsEqualTo(0);
        await Assert.That(device.ImageAreas).IsEqualTo(1);
    }

    /// <summary>A page render, which reads pixels, still draws the image's pixels.</summary>
    /// <param name="inline">Whether the image is inline.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task RenderedPageStillDrawsTheImagePixels(bool inline)
    {
        using var page = new RenderTestPage(Pdf(inline).ToBytes());

        var image = page.RenderPage();

        await RenderCheck.Near(image, Middle, Middle, new(0, 0, 0), Tolerance, nameof(RenderedPageStillDrawsTheImagePixels));
    }

    /// <summary>Builds a page that draws a black image over its whole area.</summary>
    /// <param name="inline">Whether the image is inline.</param>
    /// <returns>The document.</returns>
    private static RenderTestPdf Pdf(bool inline)
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = inline ? DrawInline : DrawStored };
        var image = pdf.AddStream(StoredImage, "00>");
        pdf.Resources = $"/XObject << /Im0 {image} 0 R >>";
        return pdf;
    }
}
