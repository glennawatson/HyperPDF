// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Export;
using HyperPdfLibrary.Fonts.Embedding;
using SkiaSharp;
using TUnit.Assertions.Enums;

namespace HyperPdfLibrary.Tests.Export;

/// <summary>Checks taking the images and the embedded fonts of a page out of a document as files.</summary>
public sealed class AssetExtractionTests
{
    /// <summary>The page width and height in points.</summary>
    private const int PageSize = 200;

    /// <summary>The width and height of the test images in pixels.</summary>
    private const int ImageSize = 4;

    /// <summary>The quality the test JPEG is encoded with.</summary>
    private const int JpegQuality = 90;

    /// <summary>The size an image is drawn at, in points.</summary>
    private const float DrawnSize = 40;

    /// <summary>The gap before the first image and the distance between images, in points.</summary>
    private const float Margin = 10;

    /// <summary>The distance from one image's left edge to the next's, in points.</summary>
    private const float Step = 50;

    /// <summary>The font size of the test text.</summary>
    private const float FontSize = 12;

    /// <summary>Where the text sits on the page.</summary>
    private const float TextBaseline = 150;

    /// <summary>The bytes in an RGB pixel.</summary>
    private const int RgbChannels = 3;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BgraChannels = 4;

    /// <summary>The red of the opaque test image.</summary>
    private const byte OpaqueRed = 200;

    /// <summary>The green of the opaque test image.</summary>
    private const byte OpaqueGreen = 100;

    /// <summary>The blue of the opaque test image.</summary>
    private const byte OpaqueBlue = 50;

    /// <summary>The alpha of the translucent test image.</summary>
    private const byte HalfAlpha = 128;

    /// <summary>The images drawn on the first page.</summary>
    private const int ImagesOnFirstPage = 3;

    /// <summary>The fewest bytes a font program has.</summary>
    private const int MinimumFontBytes = 1000;

    /// <summary>The bytes of a PNG file's signature.</summary>
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47];

    /// <summary>The bytes that start a JPEG file.</summary>
    private static readonly byte[] JpegStart = [0xFF, 0xD8];

    /// <summary>Verifies that a page's JPEG is written as the document stores it, and its other images as PNG files.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Images_AreWrittenAsJpegOrPng()
    {
        using var document = PdfDocumentReader.Open(CreateDocument(), null);

        var images = PdfImageExtraction.GetImages(document, 0);

        await Assert.That(images.Select(static file => file.Name).ToArray()).IsEquivalentTo(
            ["page-1-image-1.jpg", "page-1-image-2.png", "page-1-image-3.png"],
            CollectionOrdering.Matching);
        await Assert.That(images[0].Data[..JpegStart.Length]).IsEquivalentTo(JpegStart, CollectionOrdering.Matching);
        await Assert.That(images[1].Data[..PngSignature.Length]).IsEquivalentTo(PngSignature, CollectionOrdering.Matching);
        await Assert.That(Array.TrueForAll(images, static file => file.Kind == PdfExtractedFileKind.Image && file.PageIndex == 0)).IsTrue();
    }

    /// <summary>Verifies that the opaque RGB image decodes back to its own colours, and the translucent one keeps its alpha.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PngImages_DecodeToTheOriginalPixels()
    {
        using var document = PdfDocumentReader.Open(CreateDocument(), null);
        var images = PdfImageExtraction.GetImages(document, 0);

        using var opaque = SKBitmap.Decode(images[1].Data);
        using var translucent = SKBitmap.Decode(images[ImagesOnFirstPage - 1].Data);

        await Assert.That(opaque.Width).IsEqualTo(ImageSize);
        await Assert.That(opaque.GetPixel(0, 0)).IsEqualTo(new(OpaqueRed, OpaqueGreen, OpaqueBlue));
        await Assert.That(translucent.GetPixel(0, 0).Alpha).IsEqualTo(HalfAlpha);
    }

    /// <summary>Verifies that an image drawn on two pages is taken once when the whole document is walked.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ImageDrawnOnTwoPages_IsTakenOnceWhenTheDocumentIsWalked()
    {
        using var document = PdfDocumentReader.Open(CreateDocument(), null);
        var all = new List<PdfExtractedFile>();

        await foreach (var file in PdfImageExtraction.GetImagesAsync(document, CancellationToken.None))
        {
            all.Add(file);
        }

        await Assert.That(all.Count).IsEqualTo(ImagesOnFirstPage);
        await Assert.That(PdfImageExtraction.GetImages(document, 1).Length).IsEqualTo(1);
    }

    /// <summary>Verifies that the embedded font a page's text uses is written with its program's own extension.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmbeddedFont_IsWrittenAsItsProgram()
    {
        using var document = PdfDocumentReader.Open(CreateDocument(), null);

        var fonts = PdfFontExtraction.GetFonts(document, 0);

        await Assert.That(fonts.Length).IsEqualTo(1);
        await Assert.That(fonts[0].Kind).IsEqualTo(PdfExtractedFileKind.Font);
        await Assert.That(Path.GetExtension(fonts[0].Name)).IsIn(".ttf", ".otf", ".cff", ".t1");
        await Assert.That(fonts[0].Data.Length).IsGreaterThan(MinimumFontBytes);
    }

    /// <summary>Verifies that a page with no images and no embedded font gives no files.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PageWithNothingToTake_GivesNoFiles()
    {
        var creator = new PdfDocumentCreator(new());
        var page = creator.BeginPage(PageSize, PageSize);
        _ = page.Finish();
        page.Dispose();
        using var document = PdfDocumentReader.Open(creator.ToArray(), null);

        await Assert.That(PdfImageExtraction.GetImages(document, 0)).IsEmpty();
        await Assert.That(PdfFontExtraction.GetFonts(document, 0)).IsEmpty();
    }

    /// <summary>Creates a two page document. The first has text in an embedded font, a JPEG, an opaque RGB image and a translucent one. The second draws the JPEG again.</summary>
    /// <returns>The file.</returns>
    private static byte[] CreateDocument()
    {
        var creator = new PdfDocumentCreator(new());
        var font = creator.EmbedFont(PdfBundledFonts.Open(PdfBundledFontFamily.Sans, false, false).Source!);
        var jpeg = creator.AddJpeg(EncodeJpeg(), PdfImageOptions.Default);
        var opaque = creator.AddRaster(new(PdfRasterFormat.Rgb24, ImageSize, ImageSize, Pixels(RgbChannels, OpaqueRed, OpaqueGreen, OpaqueBlue)), PdfImageOptions.Default, CancellationToken.None);
        var bgra = Pixels(BgraChannels, OpaqueBlue, OpaqueGreen, OpaqueRed, HalfAlpha);
        var translucent = creator.AddRaster(new(PdfRasterFormat.Bgra32, ImageSize, ImageSize, bgra), PdfImageOptions.Default, CancellationToken.None);
        var first = creator.BeginPage(PageSize, PageSize);
        first.DrawText(font, FontSize, Margin, TextBaseline, "Words");
        first.DrawImage(jpeg, Margin, Margin, DrawnSize, DrawnSize);
        first.DrawImage(opaque, Margin + Step, Margin, DrawnSize, DrawnSize);
        first.DrawImage(translucent, Margin + Step + Step, Margin, DrawnSize, DrawnSize);
        _ = first.Finish();
        first.Dispose();
        var second = creator.BeginPage(PageSize, PageSize);
        second.DrawImage(jpeg, Margin, Margin, DrawnSize, DrawnSize);
        _ = second.Finish();
        second.Dispose();
        return creator.ToArray();
    }

    /// <summary>Fills a square image with one pixel repeated.</summary>
    /// <param name="width">The bytes in a pixel.</param>
    /// <param name="pixel">The bytes of the pixel.</param>
    /// <returns>The buffer.</returns>
    private static byte[] Pixels(int width, params byte[] pixel)
    {
        var buffer = new byte[ImageSize * ImageSize * width];
        for (var i = 0; i < buffer.Length; i++)
        {
            buffer[i] = pixel[i % width];
        }

        return buffer;
    }

    /// <summary>Encodes a small JPEG.</summary>
    /// <returns>The file.</returns>
    private static byte[] EncodeJpeg()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(ImageSize, ImageSize, SKColorType.Bgra8888, SKAlphaType.Opaque));
        bitmap.Erase(new(OpaqueRed, OpaqueGreen, OpaqueBlue));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
        return data.ToArray();
    }
}
