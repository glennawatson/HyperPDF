// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.PageObjects;
using HyperPdfLibrary.Redaction;
using HyperPdfLibrary.Tests.PageObjects;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdfLibrary.Tests.Redaction;

/// <summary>Redacting images and line art: each mode removes or keeps what it says.</summary>
public sealed class RedactionContentTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = PageObjectSamples.Size;

    /// <summary>The largest channel difference accepted.</summary>
    private const int Tolerance = 3;

    /// <summary>The pixels along one side of the sample image.</summary>
    private const int ImageSide = 10;

    /// <summary>The highest channel value.</summary>
    private const byte Full = 255;

    /// <summary>A mask value that is half opaque.</summary>
    private const byte HalfOpaque = 128;

    /// <summary>The samples in an RGB pixel.</summary>
    private const int RgbChannels = 3;

    /// <summary>The centre column of the area over the image.</summary>
    private const int AreaColumn = 75;

    /// <summary>The centre row of the area over the image, counted from the top.</summary>
    private const int AreaRow = 125;

    /// <summary>A column of the image the area does not reach.</summary>
    private const int FarColumn = 130;

    /// <summary>A row of the image the area does not reach, counted from the top.</summary>
    private const int FarRow = 70;

    /// <summary>The page content that draws the image over 50 to 150 points.</summary>
    private const string DrawImage = "q 100 0 0 100 50 50 cm /Im1 Do Q";

    /// <summary>The area over part of the image.</summary>
    private static readonly PdfRectangle ImageArea = new(60, 60, 90, 90);

    /// <summary>Options that leave the area unpainted, so the page shows what is left.</summary>
    private static readonly PdfRedactionOptions NoOverlay = PdfRedactionOptions.Default with { DrawOverlay = false };

    /// <summary>The mode removes an image the area touches, whole.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemoveModeDropsTheWholeImage()
    {
        var saved = RedactionSamples.Redact(ImagePage(DrawImage), NoOverlay with { Images = PdfRedactionImageMode.Remove }, ImageArea);
        var image = PageObjectSamples.Render(saved);

        await Assert.That(image.IsNear(FarColumn, FarRow, Rgb.White, Tolerance)).IsTrue();
        await Assert.That(image.IsNear(AreaColumn, AreaRow, Rgb.White, Tolerance)).IsTrue();
    }

    /// <summary>Blank mode blanks only the pixels under the area.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BlankModeBlanksOnlyCoveredPixels()
    {
        var saved = RedactionSamples.Redact(ImagePage(DrawImage), NoOverlay, ImageArea);
        var image = PageObjectSamples.Render(saved);

        await Assert.That(image.IsNear(AreaColumn, AreaRow, Rgb.Black, Tolerance)).IsTrue();
        await Assert.That(image.IsNear(FarColumn, FarRow, Rgb.Red255, Tolerance)).IsTrue();
    }

    /// <summary>The blanked image keeps a valid soft mask of its own size.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BlankedImageKeepsAValidSoftMask()
    {
        var pdf = new RenderTestPdf(Size, Size);
        var mask = pdf.AddStream("/Type /XObject /Subtype /Image /Width 10 /Height 10 /ColorSpace /DeviceGray /BitsPerComponent 8", Fill(ImageSide * ImageSide, HalfOpaque));
        var image = pdf.AddStream($"/Type /XObject /Subtype /Image /Width 10 /Height 10 /ColorSpace /DeviceRGB /BitsPerComponent 8 /SMask {mask} 0 R", Fill(ImageSide * ImageSide * RgbChannels, Full));
        pdf.Resources = $"/XObject << /Im1 {image} 0 R >>";
        pdf.Content = DrawImage;
        var saved = RedactionSamples.Redact(pdf.ToBytes(), NoOverlay, ImageArea);
        using var document = PdfDocumentReader.Open(saved, null);
        var blanked = (PdfImageObject)PdfDocumentPageContent.GetPageContent(document, 0).Objects[0];
        var softMask = blanked.Dictionary.Get(KnownName.SMask).AsStream();

        await Assert.That(softMask).IsNotNull();
        await Assert.That(softMask!.Dictionary.GetInt32(KnownName.Width)).IsEqualTo(blanked.Width);
        await Assert.That(softMask.Dictionary.GetInt32(KnownName.Height)).IsEqualTo(blanked.Height);
        await Assert.That(blanked.DecodePixels(null)).IsNotNull();
    }

    /// <summary>A stencil mask stays a stencil mask, with the covered pixels no longer painting.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StencilMaskKeepsItsKindAndLosesCoveredPixels()
    {
        var pdf = new RenderTestPdf(Size, Size);
        var stencil = pdf.AddStream("/Type /XObject /Subtype /Image /Width 8 /Height 8 /ImageMask true /BitsPerComponent 1", new byte[8]);
        pdf.Resources = $"/XObject << /Im1 {stencil} 0 R >>";
        pdf.Content = $"1 0 0 rg {DrawImage}";
        var saved = RedactionSamples.Redact(pdf.ToBytes(), NoOverlay, ImageArea);
        var image = PageObjectSamples.Render(saved);
        using var document = PdfDocumentReader.Open(saved, null);

        await Assert.That(((PdfImageObject)PdfDocumentPageContent.GetPageContent(document, 0).Objects[0]).IsMask).IsTrue();
        await Assert.That(image.IsNear(AreaColumn, AreaRow, Rgb.White, Tolerance)).IsTrue();
        await Assert.That(image.IsNear(FarColumn, FarRow, Rgb.Red255, Tolerance)).IsTrue();
    }

    /// <summary>The unless-invisible mode leaves an image alone when the area only touches the clipped-away part.</summary>
    /// <param name="clipTouchesArea">Whether the visible part reaches the area.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RemoveUnlessInvisibleLooksAtTheVisiblePart(bool clipTouchesArea)
    {
        var content = clipTouchesArea ? $"50 50 100 100 re W n {DrawImage}" : $"100 100 50 50 re W n {DrawImage}";
        var options = NoOverlay with { Images = PdfRedactionImageMode.RemoveUnlessInvisible };
        var saved = RedactionSamples.Redact(ImagePage(content), options, ImageArea);
        using var document = PdfDocumentReader.Open(saved, null);

        await Assert.That(PdfDocumentPageContent.GetPageContent(document, 0).Objects.Count).IsEqualTo(clipTouchesArea ? 0 : 1);
    }

    /// <summary>Line art modes keep, remove covered, or remove touched paths.</summary>
    /// <param name="mode">The mode.</param>
    /// <param name="expectedCount">The paths left.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfRedactionLineArtMode.None, 2)]
    [Arguments(PdfRedactionLineArtMode.RemoveCovered, 1)]
    [Arguments(PdfRedactionLineArtMode.RemoveTouched, 0)]
    public async Task LineArtModesRemoveWhatTheySay(PdfRedactionLineArtMode mode, int expectedCount)
    {
        var pdf = PageObjectSamples.Page("0 0 1 rg 10 10 120 120 re f 1 0 0 rg 65 65 20 20 re f");
        var saved = RedactionSamples.Redact(pdf, NoOverlay with { LineArt = mode }, ImageArea);
        using var document = PdfDocumentReader.Open(saved, null);

        await Assert.That(PdfDocumentPageContent.GetPageContent(document, 0).Objects.Count).IsEqualTo(expectedCount);
    }

    /// <summary>A clipping path of a removed path still clips what follows it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovedPathKeepsItsClip()
    {
        var pdf = PageObjectSamples.Page("q 65 65 20 20 re W f 0 0 1 rg 0 0 200 200 re f Q");
        var saved = RedactionSamples.Redact(pdf, NoOverlay with { LineArt = PdfRedactionLineArtMode.RemoveCovered }, ImageArea);
        var image = PageObjectSamples.Render(saved);

        await Assert.That(image.IsNear(AreaColumn, AreaRow, Rgb.Blue255, Tolerance)).IsTrue();
        await Assert.That(image.IsNear(FarColumn, FarRow, Rgb.White, Tolerance)).IsTrue();
    }

    /// <summary>Builds a page with a red image of 10 by 10 pixels.</summary>
    /// <param name="content">The page content.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] ImagePage(string content)
    {
        var pdf = new RenderTestPdf(Size, Size);
        var samples = new byte[ImageSide * ImageSide * RgbChannels];
        for (var i = 0; i < samples.Length; i += RgbChannels)
        {
            samples[i] = Full;
        }

        var image = pdf.AddStream("/Type /XObject /Subtype /Image /Width 10 /Height 10 /ColorSpace /DeviceRGB /BitsPerComponent 8", samples);
        pdf.Resources = $"/XObject << /Im1 {image} 0 R >>";
        pdf.Content = content;
        return pdf.ToBytes();
    }

    /// <summary>Makes a buffer filled with one value.</summary>
    /// <param name="length">The length.</param>
    /// <param name="value">The value.</param>
    /// <returns>The buffer.</returns>
    private static byte[] Fill(int length, byte value)
    {
        var bytes = new byte[length];
        Array.Fill(bytes, value);
        return bytes;
    }
}
