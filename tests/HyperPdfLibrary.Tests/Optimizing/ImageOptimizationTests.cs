// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Optimizing;
using HyperPdfLibrary.Tests.Graphics.Jpx;

namespace HyperPdfLibrary.Tests.Optimizing;

/// <summary>Image downsampling and re-encoding.</summary>
[NotInParallel]
public sealed class ImageOptimizationTests
{
    /// <summary>The photo's size in pixels: 600 pixels over 144 points is 300 ppi.</summary>
    private const int PhotoPixels = 600;

    /// <summary>The photo's drawn size: two inches.</summary>
    private const int TwoInches = 144;

    /// <summary>The width the smaller preset's 150 ppi gives two inches.</summary>
    private const int SmallerWidth = 300;

    /// <summary>The mean channel difference allowed between renders of a lossy image, out of 255.</summary>
    private const double LossyTolerance = 6;

    /// <summary>The bilevel image's width.</summary>
    private const int BilevelWidth = 480;

    /// <summary>The bilevel image's height.</summary>
    private const int BilevelHeight = 320;

    /// <summary>The bilevel image's drawn size.</summary>
    private const int BilevelPoints = 300;

    /// <summary>The image's object number in the samples.</summary>
    private const int ImageNumber = 5;

    /// <summary>The mask's object number in the samples.</summary>
    private const int MaskNumber = 6;

    /// <summary>The components of the photo.</summary>
    private const int Rgb = 3;

    /// <summary>The object number of the XMP stream in the PDF/A-1 sample.</summary>
    private const int XmpNumber = 7;

    /// <summary>A catalog that points at the XMP stream.</summary>
    private const string CatalogWithXmp = "<< /Type /Catalog /Pages 2 0 R /Metadata 7 0 R >>";

    /// <summary>XMP that claims PDF/A-1b.</summary>
    private const string PdfAOneXmp =
        "<?xpacket begin=\"\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?><x:xmpmeta xmlns:x=\"adobe:ns:meta/\">"
        + "<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\"><rdf:Description rdf:about=\"\" xmlns:pdfaid=\"http://www.aiim.org/pdfa/ns/id/\">"
        + "<pdfaid:part>1</pdfaid:part><pdfaid:conformance>B</pdfaid:conformance></rdf:Description></rdf:RDF></x:xmpmeta><?xpacket end=\"w\"?>";

    /// <summary>A photo drawn at 300 ppi is downsampled to the target and saved as JPEG, the file shrinks and the page looks the same.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DownsamplesHighResolutionPhoto()
    {
        var source = OptimizerSamples.Photo(PhotoPixels, TwoInches, string.Empty);
        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.Smaller);
        var image = FirstImage(result.Bytes);
        await Assert.That(result.Bytes.Length).IsLessThan(source.Length);
        await Assert.That(image.Dictionary.GetInt32(KnownName.Width)).IsEqualTo(SmallerWidth);
        await Assert.That(image.Dictionary.GetName(KnownName.Filter).Is(KnownName.DCTDecode)).IsTrue();
        await Assert.That(result.Report.GetSaving(PdfOptimizeCategory.Images).BytesSaved).IsGreaterThan(0);
        await Assert.That(OptimizerTestKit.MeanDifference(OptimizerTestKit.Render(source), OptimizerTestKit.Render(result.Bytes))).IsLessThan(LossyTolerance);
    }

    /// <summary>Choosing JPEG 2000 downsamples a photo and saves it under JPXDecode, smaller than the source, and the page looks the same.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavesPhotoAsJpeg2000WhenAsked()
    {
        var source = OptimizerSamples.Photo(PhotoPixels, TwoInches, string.Empty);
        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.Smaller with { ImageCodec = PdfOptimizeImageCodec.Jpeg2000 });
        var image = FirstImage(result.Bytes);
        await Assert.That(result.Bytes.Length).IsLessThan(source.Length);
        await Assert.That(image.Dictionary.GetInt32(KnownName.Width)).IsEqualTo(SmallerWidth);
        await Assert.That(image.Dictionary.GetName(KnownName.Filter).Is(KnownName.JPXDecode)).IsTrue();
        await Assert.That(Encoding.Latin1.GetString(result.Bytes)).DoesNotStartWith("%PDF-1.4");
        await Assert.That(OptimizerTestKit.MeanDifference(OptimizerTestKit.Render(source), OptimizerTestKit.Render(result.Bytes))).IsLessThan(LossyTolerance);
    }

    /// <summary>With lossy changes off, JPEG 2000 is lossless: the page is drawn exactly as before, and the image is kept as it was unless the new one is smaller.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Jpeg2000WithoutLossChangesNoPixel()
    {
        var source = OptimizerSamples.Photo(PhotoPixels, TwoInches, string.Empty);
        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.KeepQuality with { ImageCodec = PdfOptimizeImageCodec.Jpeg2000 });
        var image = FirstImage(result.Bytes);
        await Assert.That(image.Dictionary.GetInt32(KnownName.Width)).IsEqualTo(PhotoPixels);
        await Assert.That(result.Bytes.Length).IsLessThanOrEqualTo(source.Length);

        // The page draws a reduced resolution of a JPEG 2000 image, so compare the file's own samples with the original.
        if (!image.Dictionary.GetName(KnownName.Filter).Is(KnownName.JPXDecode))
        {
            return;
        }

        var planes = JpxDecoderTests.DecodePlanes(image.CopyRawData());
        var original = OptimizerSamples.PhotoPixels(PhotoPixels, PhotoPixels);
        await Assert.That(planes).IsNotNull();
        for (var c = 0; c < Rgb; c++)
        {
            var expected = Enumerable.Range(0, PhotoPixels * PhotoPixels).Select(i => (int)original[(i * Rgb) + c]);
            await Assert.That(planes![c].AsSpan().SequenceEqual(expected.ToArray())).IsTrue();
        }
    }

    /// <summary>A document that claims PDF/A-1 keeps JPEG, as that level forbids JPEG 2000.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PdfAOneNeverGetsJpeg2000()
    {
        var photo = OptimizerSamples.Photo(PhotoPixels, TwoInches, string.Empty);
        var source = IncrementalPdf.Append(photo, new Dictionary<int, string> { [1] = CatalogWithXmp, [XmpNumber] = MiniPdf.Stream("/Type /Metadata /Subtype /XML", PdfAOneXmp) });
        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.Smaller with { ImageCodec = PdfOptimizeImageCodec.Jpeg2000 });
        await Assert.That(result.Report.PdfAPart).IsEqualTo(1);
        await Assert.That(FirstImage(result.Bytes).Dictionary.GetName(KnownName.Filter).Is(KnownName.JPXDecode)).IsFalse();
    }

    /// <summary>The lossless preset never changes pixels: no downsampling and no JPEG.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepQualityChangesNoPixel()
    {
        var source = OptimizerSamples.Photo(PhotoPixels, TwoInches, string.Empty);
        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.KeepQuality);
        var image = FirstImage(result.Bytes);
        await Assert.That(image.Dictionary.GetInt32(KnownName.Width)).IsEqualTo(PhotoPixels);
        await Assert.That(image.Dictionary.GetName(KnownName.Filter).Is(KnownName.DCTDecode)).IsFalse();
        await Assert.That(OptimizerTestKit.MaxDifference(OptimizerTestKit.Render(source), OptimizerTestKit.Render(result.Bytes))).IsEqualTo(0);
    }

    /// <summary>A greyscale image holding only black and white becomes 1-bit, coded losslessly, and draws the same.</summary>
    /// <param name="bits">The source's bits per component.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(8)]
    [Arguments(1)]
    public async Task CodesBlackAndWhiteLosslessly(int bits)
    {
        var grey = OptimizerSamples.Strokes(BilevelWidth, BilevelHeight);
        var samples = bits == 1 ? OptimizerSamples.Pack(grey, BilevelWidth, BilevelHeight) : grey;
        var entries = OptimizerSamples.Format($"/ColorSpace /DeviceGray /BitsPerComponent {bits}");
        var source = OptimizerSamples.ImagePage(OptimizerSamples.ImageObject(samples, BilevelWidth, BilevelHeight, entries), BilevelPoints);
        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.Smaller);
        var image = FirstImage(result.Bytes);
        await Assert.That(image.Dictionary.GetInt32(KnownName.BitsPerComponent)).IsEqualTo(1);
        await Assert.That(image.Dictionary.GetInt32(KnownName.Width)).IsEqualTo(BilevelWidth);
        await Assert.That(result.Bytes.Length).IsLessThan(source.Length);
        await Assert.That(OptimizerTestKit.MaxDifference(OptimizerTestKit.Render(source), OptimizerTestKit.Render(result.Bytes))).IsEqualTo(0);
    }

    /// <summary>A soft mask is never compressed lossily, while the image it masks may be.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsMasksLossless()
    {
        var source = OptimizerSamples.Masked(PhotoPixels, TwoInches);
        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.Smaller);
        using var document = PdfDocumentReader.Open(result.Bytes, null);
        var image = FirstImage(document);
        var mask = image.Dictionary.GetStream(KnownName.SMask)!;
        await Assert.That(image.Dictionary.GetName(KnownName.Filter).Is(KnownName.DCTDecode)).IsTrue();
        await Assert.That(mask.Dictionary.GetName(KnownName.Filter).Is(KnownName.DCTDecode)).IsFalse();
        await Assert.That(mask.Dictionary.GetInt32(KnownName.Width)).IsEqualTo(PhotoPixels);
        await Assert.That(OptimizerTestKit.MeanDifference(OptimizerTestKit.Render(source), OptimizerTestKit.Render(result.Bytes))).IsLessThan(LossyTolerance);
    }

    /// <summary>A colour-keyed image keeps exact samples, so it is never saved as JPEG.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsColourKeyedImagesExact()
    {
        var source = OptimizerSamples.ColorKeyed(PhotoPixels, TwoInches);
        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.Smaller);
        var image = FirstImage(result.Bytes);
        await Assert.That(image.Dictionary.GetName(KnownName.Filter).Is(KnownName.DCTDecode)).IsFalse();
        await Assert.That(OptimizerTestKit.MaxDifference(OptimizerTestKit.Render(source), OptimizerTestKit.Render(result.Bytes))).IsEqualTo(0);
    }

    /// <summary>An image drawn by a pattern, whose drawn size the scan cannot know, keeps its resolution.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsResolutionOfPatternImages()
    {
        var source = OptimizerSamples.PatternPhoto(PhotoPixels);
        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.Smaller);
        using var document = PdfDocumentReader.Open(result.Bytes, null);
        var pattern = PdfDocumentPages.GetPage(document, 0).Resources!.GetDictionary(KnownName.Pattern)!.GetStream(document.Objects.Names.Intern("P1"u8))!;
        var image = pattern.Dictionary.GetDictionary(KnownName.Resources)!.GetDictionary(KnownName.XObject)!.GetStream(document.Objects.Names.Intern("Im1"u8))!;
        await Assert.That(image.Dictionary.GetInt32(KnownName.Width)).IsEqualTo(PhotoPixels);
    }

    /// <summary>The image and mask numbers in the samples are what the tests read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SamplesNumberImagesAsExpected()
    {
        using var document = PdfDocumentReader.Open(OptimizerSamples.Masked(PhotoPixels, TwoInches), null);
        await Assert.That(StoreReading.GetObject(document.Objects, new(ImageNumber, 0)).AsStream()).IsNotNull();
        await Assert.That(StoreReading.GetObject(document.Objects, new(MaskNumber, 0)).AsStream()).IsNotNull();
    }

    /// <summary>Gets the first page's /Im1.</summary>
    /// <param name="pdf">The document.</param>
    /// <returns>The image stream.</returns>
    private static PdfStream FirstImage(byte[] pdf)
    {
        using var document = PdfDocumentReader.Open(pdf, null);
        var image = FirstImage(document);
        return new(image.Dictionary.Clone(), image.CopyRawData());
    }

    /// <summary>Gets the first page's /Im1.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The image stream.</returns>
    private static PdfStream FirstImage(PdfDocument document) => PdfDocumentPages.GetPage(document, 0).Resources!.GetDictionary(KnownName.XObject)!.GetStream(document.Objects.Names.Intern("Im1"u8))!;
}
