// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Rendering;
using PdfViewerLite.TestAssets;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Checks that the picture cache charges an image shared by many pages once, and still bounds its memory.</summary>
public sealed class SharedImagePictureTests
{
    /// <summary>The side of the gray test images in pixels.</summary>
    private const int Side = 200;

    /// <summary>The bytes of one gray test image.</summary>
    private const long ImageBytes = (long)Side * Side;

    /// <summary>The pages in the test documents.</summary>
    private const int Pages = 6;

    /// <summary>The tile size in pixels.</summary>
    private const int Tile = 64;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The number of images that stands for "more than one".</summary>
    private const int Twice = 2;

    /// <summary>A second zoom band above the one-point scale.</summary>
    private const float ZoomScale = 1.25F;

    /// <summary>A limit that fits two and a half images, which is more than one image and a few pictures.</summary>
    private const long RoomForTwoAndAHalf = (ImageBytes * Twice) + (ImageBytes / Twice);

    /// <summary>The object number of the first image: the catalog and the page tree come before it.</summary>
    private const int FirstImage = 3;

    /// <summary>A limit that fits one and a half images: one image and a few pictures, but not two images.</summary>
    private const long RoomForOneAndAHalf = ImageBytes + (ImageBytes / Twice);

    /// <summary>One logo drawn on every page is counted once, not once per page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ARepeatedLogoIsCountedOnce()
    {
        using var document = PdfDocumentReader.Open(Build(static _ => 0), null);
        using var renderer = new PdfPageRenderer(document);

        RenderAll(renderer);

        await Assert.That(renderer.PictureCount).IsEqualTo(Pages);
        await Assert.That(renderer.PictureBytes).IsGreaterThanOrEqualTo(ImageBytes);
        await Assert.That(renderer.PictureBytes).IsLessThan(ImageBytes * Twice);
    }

    /// <summary>Different images on each page are all counted.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DistinctImagesAreEachCounted()
    {
        using var document = PdfDocumentReader.Open(Build(static page => page), null);
        using var renderer = new PdfPageRenderer(document);

        RenderAll(renderer);

        await Assert.That(renderer.PictureBytes).IsGreaterThanOrEqualTo(ImageBytes * Pages);
    }

    /// <summary>With a limit that holds one image and a few pictures, pages sharing the image all stay.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PagesThatShareAnImageAllFitUnderALimitForOne()
    {
        using var document = PdfDocumentReader.Open(Build(static _ => 0), null);
        using var renderer = new PdfPageRenderer(document, PdfRenderOptions.Default with { PictureCacheBytes = RoomForTwoAndAHalf });

        RenderAll(renderer);

        await Assert.That(renderer.PictureCount).IsEqualTo(Pages);
        await Assert.That(renderer.PictureBytes).IsLessThanOrEqualTo(RoomForTwoAndAHalf);
    }

    /// <summary>Pages with their own images are still evicted, so the pictures stay within the limit.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DistinctImagesStillBoundTheMemory()
    {
        using var document = PdfDocumentReader.Open(Build(static page => page), null);
        using var renderer = new PdfPageRenderer(document, PdfRenderOptions.Default with { PictureCacheBytes = RoomForTwoAndAHalf });

        RenderAll(renderer);

        await Assert.That(renderer.PictureCount).IsLessThan(Pages);
        await Assert.That(renderer.PictureBytes).IsLessThanOrEqualTo(RoomForTwoAndAHalf);
    }

    /// <summary>An image stays charged while any page that drew it is kept, and is released with the last one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnImageIsReleasedWithItsLastPage()
    {
        using var document = PdfDocumentReader.Open(Build(static page => page < Twice ? 0 : 1), null);
        using var renderer = new PdfPageRenderer(document, PdfRenderOptions.Default with { PictureCacheBytes = RoomForOneAndAHalf });

        for (var page = 0; page < Pages; page++)
        {
            Render(renderer, page);
        }

        // Page 2 brings the second image, which does not fit beside the first, so pages 0 and 1 go together; pages 3 to 5
        // share the second image and all stay.
        await Assert.That(renderer.PictureCount).IsEqualTo(Pages - Twice);
        await Assert.That(renderer.PictureBytes).IsLessThan(ImageBytes * Twice);
    }

    /// <summary>Zoom variants remain warm together until the picture byte limit evicts one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ZoomBandsShareThePictureBudget()
    {
        using var document = PdfDocumentReader.Open(Build(static _ => 0), null);
        using var roomy = new PdfPageRenderer(document);
        Render(roomy, 0);
        Render(roomy, 0, ZoomScale);
        await Assert.That(roomy.PictureCount).IsEqualTo(Twice);

        using var limited = new PdfPageRenderer(document, PdfRenderOptions.Default with { PictureCacheBytes = 1 });
        Render(limited, 0);
        Render(limited, 0, ZoomScale);
        await Assert.That(limited.PictureCount).IsEqualTo(1);
    }

    /// <summary>Renders a tile of every page.</summary>
    /// <param name="renderer">The renderer.</param>
    private static void RenderAll(PdfPageRenderer renderer)
    {
        for (var page = 0; page < Pages; page++)
        {
            Render(renderer, page);
        }
    }

    /// <summary>Renders one tile of a page.</summary>
    /// <param name="renderer">The renderer.</param>
    /// <param name="page">The page index.</param>
    private static void Render(PdfPageRenderer renderer, int page) =>
        Render(renderer, page, 1);

    /// <summary>Renders one tile at a selected zoom.</summary>
    /// <param name="renderer">The renderer.</param>
    /// <param name="page">The page index.</param>
    /// <param name="scale">Device pixels per page point.</param>
    private static void Render(PdfPageRenderer renderer, int page, float scale)
    {
        var pixels = new byte[Tile * Tile * BytesPerPixel];
        _ = renderer.Render(new(page, scale, 0, 0, 0, PdfRenderFlags.None), new(pixels, Tile, Tile, Tile * BytesPerPixel));
    }

    /// <summary>Builds a document whose pages each draw one of a few gray images.</summary>
    /// <param name="imageOfPage">Chooses which image a page draws.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] Build(Func<int, int> imageOfPage)
    {
        var imageCount = 0;
        for (var page = 0; page < Pages; page++)
        {
            imageCount = Math.Max(imageCount, imageOfPage(page) + 1);
        }

        var samples = new string('A', Side * Side);
        var imageEntries = string.Create(CultureInfo.InvariantCulture, $"/Type /XObject /Subtype /Image /Width {Side} /Height {Side} /ColorSpace /DeviceGray /BitsPerComponent 8");
        var kids = new StringBuilder();
        var objects = new List<string> { "<< /Type /Catalog /Pages 2 0 R >>", string.Empty };
        for (var i = 0; i < imageCount; i++)
        {
            objects.Add(MiniPdf.Stream(imageEntries, samples));
        }

        objects.Add(MiniPdf.Stream(string.Empty, $"q {Side} 0 0 {Side} 0 0 cm /Im Do Q"));
        var content = objects.Count;
        for (var page = 0; page < Pages; page++)
        {
            _ = kids.Append(CultureInfo.InvariantCulture, $"{objects.Count + 1} 0 R ");
            var image = imageOfPage(page) + FirstImage;
            objects.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {Side} {Side}] /Resources << /XObject << /Im {image} 0 R >> >> /Contents {content} 0 R >>"));
        }

        objects[1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{kids}] /Count {Pages} >>");
        return MiniPdf.Build([.. objects]);
    }
}
