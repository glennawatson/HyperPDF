// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Checks embedded /Thumb thumbnails and the fallback low resolution render.</summary>
public sealed class ThumbnailRenderTests
{
    /// <summary>The page width in points.</summary>
    private const int Width = 200;

    /// <summary>The page height in points.</summary>
    private const int Height = 100;

    /// <summary>The longest thumbnail side in pixels.</summary>
    private const int MaxEdge = 50;

    /// <summary>The thumbnail height for a page twice as wide as it is high.</summary>
    private const int ThumbnailHeight = 25;

    /// <summary>The side of the embedded thumbnail image.</summary>
    private const int ThumbSide = 2;

    /// <summary>The largest channel difference accepted.</summary>
    private const int Tolerance = 8;

    /// <summary>A pixel in the middle of the thumbnail's width.</summary>
    private const int MiddleColumn = 25;

    /// <summary>A pixel in the middle of the thumbnail's height.</summary>
    private const int MiddleRow = 12;

    /// <summary>A page's /Thumb image is decoded at its own size and stretched into the thumbnail.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EmbeddedThumbnailIsUsed()
    {
        var pdf = new RenderTestPdf(Width, Height) { Content = "0 0 1 rg 0 0 200 100 re f" };
        var thumb = pdf.AddStream("/Width 2 /Height 2 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /ASCIIHexDecode", "FF0000 FF0000 FF0000 FF0000>");
        pdf.PageEntries = $"/Thumb {thumb} 0 R";
        using var page = new RenderTestPage(pdf.ToBytes());

        using var embedded = page.Renderer.GetEmbeddedThumbnail(0);
        var image = RenderThumbnail(page);

        await Assert.That(embedded).IsNotNull();
        await Assert.That(embedded!.Width).IsEqualTo(ThumbSide);
        await RenderCheck.Near(image, MiddleColumn, MiddleRow, Rgb.Red255, Tolerance, nameof(EmbeddedThumbnailIsUsed));
    }

    /// <summary>Without /Thumb the page content is rendered small, fitted to the longest side.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PagesWithoutThumbnailsAreRenderedSmall()
    {
        var pdf = new RenderTestPdf(Width, Height) { Content = "0 0 1 rg 0 0 200 100 re f" };
        using var page = new RenderTestPage(pdf.ToBytes());

        PdfPageRenderer.GetThumbnailSize(HyperPdfLibrary.Document.PdfDocumentPages.GetPage(page.Document, 0), MaxEdge, out var width, out var height);
        using var embedded = page.Renderer.GetEmbeddedThumbnail(0);
        var image = RenderThumbnail(page);

        await Assert.That(width).IsEqualTo(MaxEdge);
        await Assert.That(height).IsEqualTo(ThumbnailHeight);
        await Assert.That(embedded).IsNull();
        await RenderCheck.Near(image, MiddleColumn, MiddleRow, Rgb.Blue255, Tolerance, nameof(PagesWithoutThumbnailsAreRenderedSmall));
    }

    /// <summary>Renders the first page's thumbnail.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The pixels.</returns>
    /// <exception cref="InvalidOperationException">The thumbnail could not be rendered.</exception>
    private static RenderedImage RenderThumbnail(RenderTestPage page)
    {
        PdfPageRenderer.GetThumbnailSize(HyperPdfLibrary.Document.PdfDocumentPages.GetPage(page.Document, 0), MaxEdge, out var width, out var height);
        var pixels = new byte[width * height * RenderedImage.BytesPerPixel];
        return page.Renderer.RenderThumbnail(0, MaxEdge, new(pixels, width, height, width * RenderedImage.BytesPerPixel))
            ? new(pixels, width, height)
            : throw new InvalidOperationException("The thumbnail could not be rendered.");
    }
}
