// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Tests for making pages from images.</summary>
public sealed class ImagePageTests
{
    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The width of the test image in pixels.</summary>
    private const int ImageWidth = 144;

    /// <summary>The height of the test image in pixels.</summary>
    private const int ImageHeight = 72;

    /// <summary>The resolution that makes the test image half its pixel size in points.</summary>
    private const float Dpi = 144;

    /// <summary>The width of the test image's page at the test resolution, in points.</summary>
    private const float PageWidthPoints = 72;

    /// <summary>The height of the test image's page at the test resolution, in points.</summary>
    private const float PageHeightPoints = 36;

    /// <summary>The paper width in points.</summary>
    private const float PaperWidth = 200;

    /// <summary>The paper height in points.</summary>
    private const float PaperHeight = 100;

    /// <summary>The blank border on the paper in points.</summary>
    private const float Margin = 10;

    /// <summary>The pages in the target document.</summary>
    private const int Pages = 2;

    /// <summary>A page made from an image is as large as the image at its resolution, and draws the image over the whole page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageSizeFollowsTheResolution()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        PdfImagePages.InsertPage(document, 1, Image(), new() { Dpi = Dpi });
        var page = PdfDocumentPages.GetPage(document, 1);
        var image = page.Resources!.GetDictionary(KnownName.XObject)!.GetStream(document.Objects.Names.Intern("Im0"u8))!;

        await Assert.That(document.PageCount).IsEqualTo(Pages + 1);
        await Assert.That(page.Width).IsEqualTo(PageWidthPoints);
        await Assert.That(page.Height).IsEqualTo(PageHeightPoints);
        await Assert.That(image.Dictionary.IsName(KnownName.Subtype, KnownName.Image)).IsTrue();
        await Assert.That(EditingTestDocuments.PageTexts(document)[0]).IsEqualTo("Page 1");
    }

    /// <summary>On paper the image is fitted inside the margin and centred.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ImageIsFittedOnPaper()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);
        PdfImagePages.InsertPage(document, 0, Image(), new() { PaperWidth = PaperWidth, PaperHeight = PaperHeight, Margin = Margin });
        var page = PdfDocumentPages.GetPage(document, 0);
        var content = System.Text.Encoding.Latin1.GetString(page.Dictionary.Get(KnownName.Contents).AsStream()!.DecodeToArray());

        await Assert.That(page.Width).IsEqualTo(PaperWidth);
        await Assert.That(page.Height).IsEqualTo(PaperHeight);

        // The 2:1 image has 180 by 80 points to fit in, so it is 160 by 80 points, 20 from the sides and 10 from the top and bottom.
        await Assert.That(content).Contains("160 0 0 80 20 10 cm");
    }

    /// <summary>A new document has one page for each image, and the page survives a save.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CreateDocumentMakesAPagePerImage()
    {
        var bytes = PdfImagePages.CreateDocument([Image(), Image()], PdfImagePageOptions.Default, CancellationToken.None);
        using var document = PdfDocumentReader.Open(bytes, null);

        await Assert.That(document.PageCount).IsEqualTo(Pages);
        await Assert.That(PdfDocumentPages.GetPage(document, 1).Width).IsEqualTo((float)ImageWidth);
    }

    /// <summary>A missing size, short pixels, half a paper size and an index past the end are refused, and nothing changes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BadArgumentsAreRefused()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);
        var shortImage = new PdfPageImage(new byte[BytesPerPixel], ImageWidth, ImageHeight);

        await Assert.That(() => PdfImagePages.InsertPage(document, 0, shortImage, PdfImagePageOptions.Default)).Throws<ArgumentException>();
        await Assert.That(() => PdfImagePages.InsertPage(document, 0, Image(), new() { PaperWidth = PaperWidth })).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => PdfImagePages.InsertPage(document, Pages, Image(), PdfImagePageOptions.Default)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(static () => PdfImagePages.CreateDocument([], PdfImagePageOptions.Default, CancellationToken.None)).Throws<ArgumentException>();
        await Assert.That(StoreEditing.HasEdits(document.Objects)).IsFalse();
    }

    /// <summary>Makes an opaque test image.</summary>
    /// <returns>The image.</returns>
    private static PdfPageImage Image()
    {
        var pixels = new byte[ImageWidth * ImageHeight * BytesPerPixel];
        pixels.AsSpan().Fill(byte.MaxValue);
        return new(pixels, ImageWidth, ImageHeight);
    }
}
