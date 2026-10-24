// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Export;
using HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Optimizing;
using HyperPdfLibrary.PageObjects;
using HyperPdfLibrary.Tests.Graphics.Jbig2.Encoding;
using TUnit.Assertions.Enums;

namespace HyperPdfLibrary.Tests.Optimizing;

/// <summary>The optimiser's JBIG2 option for black-and-white images.</summary>
[NotInParallel]
public sealed class Jbig2OptimizationTests
{
    /// <summary>The share of the page, out of 1, that a lossy result may change.</summary>
    private const double LossyChangeLimit = 0.03;

    /// <summary>The size the scan is drawn at in points.</summary>
    private const int DrawPoints = 400;

    /// <summary>The 1-bit grey image entries.</summary>
    private const string OneBitGray = "/ColorSpace /DeviceGray /BitsPerComponent 1";

    /// <summary>The 1-bit stencil mask entries.</summary>
    private const string Stencil = "/ImageMask true /BitsPerComponent 1";

    /// <summary>The images the two image document draws on its first page.</summary>
    private const int ImagesInDocument = 2;

    /// <summary>The bytes of a JBIG2 file header after the id: the flags and the page count.</summary>
    private const int Jbig2HeaderTail = 5;

    /// <summary>The 8 byte id a JBIG2 file starts with.</summary>
    private static readonly byte[] Jbig2Id = [0x97, 0x4A, 0x42, 0x32, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>The text page the tests code.</summary>
    private static readonly Jbig2Size Page = new(640, 480);

    /// <summary>Text drawn from repeated glyphs with no stray pixels.</summary>
    private static readonly Jbig2TestTextStyle Clean = new(3, 17, 0);

    /// <summary>Text with the same glyphs laid out differently.</summary>
    private static readonly Jbig2TestTextStyle Other = new(3, 29, 0);

    /// <summary>The spacing of the pixels flipped to make repeated glyphs differ.</summary>
    private static readonly Jbig2Size Spacing = new(29, 11);

    /// <summary>Lossless JBIG2 wins on a page of text, keeps every pixel and is smaller than the fax coding.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LosslessJbig2BeatsFaxOnText()
    {
        var source = TextPdf(OneBitGray, false);
        var fax = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.Smaller);
        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.Smaller with { Jbig2 = PdfJbig2Mode.Lossless });
        var image = FirstImage(result.Bytes);

        await Assert.That(image.Dictionary.GetName(KnownName.Filter).Is(KnownName.JBIG2Decode)).IsTrue();
        await Assert.That(image.Dictionary.GetInt32(KnownName.BitsPerComponent)).IsEqualTo(1);
        await Assert.That(result.Bytes.Length).IsLessThan(fax.Bytes.Length);
        await Assert.That(OptimizerTestKit.MaxDifference(OptimizerTestKit.Render(source), OptimizerTestKit.Render(result.Bytes))).IsEqualTo(0);
        await Assert.That(result.Report.Actions.Any(static action => action.Description.Contains("JBIG2", StringComparison.Ordinal))).IsTrue();
        await Assert.That(Encoding.Latin1.GetString(result.Bytes)).StartsWith("%PDF-1.");
    }

    /// <summary>A stencil mask of text codes as JBIG2 too and keeps its /ImageMask entry.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StencilMasksCanBeJbig2()
    {
        var source = TextPdf(Stencil, false);
        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.Smaller with { Jbig2 = PdfJbig2Mode.Lossless });
        var image = FirstImage(result.Bytes);

        await Assert.That(image.Dictionary.GetName(KnownName.Filter).Is(KnownName.JBIG2Decode)).IsTrue();
        await Assert.That(image.Dictionary.GetBoolean(KnownName.ImageMask)).IsTrue();
        await Assert.That(OptimizerTestKit.MaxDifference(OptimizerTestKit.Render(source), OptimizerTestKit.Render(result.Bytes))).IsEqualTo(0);
    }

    /// <summary>Images that share marks share one symbol dictionary in a /JBIG2Globals stream the optimiser adds, and still draw the same.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ImagesShareAGlobalsStream()
    {
        var source = TwoImagePdf();
        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.Smaller with { Jbig2 = PdfJbig2Mode.Lossless });
        using var document = PdfDocumentReader.Open(result.Bytes, null);
        var xobjects = PdfDocumentPages.GetPage(document, 0).Resources!.GetDictionary(KnownName.XObject)!;
        var first = xobjects.GetStream(document.Objects.Names.Intern("Im1"u8))!;
        var second = xobjects.GetStream(document.Objects.Names.Intern("Im2"u8))!;
        var firstGlobals = ImageCodec(first)?.GetRaw(KnownName.JBIG2Globals) ?? default;
        var secondGlobals = ImageCodec(second)?.GetRaw(KnownName.JBIG2Globals) ?? default;

        await Assert.That(first.Dictionary.GetName(KnownName.Filter).Is(KnownName.JBIG2Decode)).IsTrue();
        await Assert.That(second.Dictionary.GetName(KnownName.Filter).Is(KnownName.JBIG2Decode)).IsTrue();
        await Assert.That(firstGlobals.IsReference).IsTrue();
        await Assert.That(secondGlobals.IsReference && secondGlobals.AsReference().Number == firstGlobals.AsReference().Number).IsTrue();
        await Assert.That(OptimizerTestKit.MaxDifference(OptimizerTestKit.Render(source), OptimizerTestKit.Render(result.Bytes))).IsEqualTo(0);
        await Assert.That(result.Report.Actions.Any(static action => action.Description.Contains("shared by 2 images", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>Extracting a JBIG2 image gives a standalone JBIG2 file: the file header, then the globals, then the stored segments.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExtractedJbig2ImageIsAStandaloneFileWithItsGlobals()
    {
        var result = OptimizerTestKit.Optimize(TwoImagePdf(), PdfOptimizeOptions.Smaller with { Jbig2 = PdfJbig2Mode.Lossless });
        using var document = PdfDocumentReader.Open(result.Bytes, null);
        var image = (PdfImageObject)PdfDocumentPageContent.GetPageContent(document, 0).Objects.First(static item => item is PdfImageObject);
        var globals = ImageCodec(image.Stream!)!.GetStream(KnownName.JBIG2Globals)!.DecodeToArray();

        var files = PdfImageExtraction.GetImages(document, 0);

        await Assert.That(files.Length).IsEqualTo(ImagesInDocument);
        await Assert.That(files[0].Name).EndsWith(".jb2");
        await Assert.That(files[0].Data[..Jbig2Id.Length]).IsEquivalentTo(Jbig2Id, CollectionOrdering.Matching);
        await Assert.That(files[0].Data.Length).IsEqualTo(Jbig2Id.Length + Jbig2HeaderTail + globals.Length + image.Stream!.DecodeToArray().Length);
    }

    /// <summary>With the option off, which is the default, no image becomes JBIG2.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task JbigIsOffByDefault()
    {
        var result = OptimizerTestKit.Optimize(TextPdf(OneBitGray, false), PdfOptimizeOptions.Smaller);

        await Assert.That(FirstImage(result.Bytes).Dictionary.GetName(KnownName.Filter).Is(KnownName.JBIG2Decode)).IsFalse();
    }

    /// <summary>A PDF/A-1 claim forbids JBIG2, so the image keeps its fax or Flate coding and the report says why.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PdfAOneNeverGetsJbig2()
    {
        var result = OptimizerTestKit.Optimize(TextPdf(OneBitGray, true), PdfOptimizeOptions.Smaller with { Jbig2 = PdfJbig2Mode.Lossless });

        await Assert.That(result.Report.PdfAPart).IsEqualTo(1);
        await Assert.That(FirstImage(result.Bytes).Dictionary.GetName(KnownName.Filter).Is(KnownName.JBIG2Decode)).IsFalse();
        await Assert.That(result.Report.Skipped.Any(static skip => skip.Category == PdfOptimizeCategory.Images && skip.Reason.Contains("JBIG2", StringComparison.Ordinal))).IsTrue();
        using var saved = PdfDocumentReader.Open(result.Bytes, null);
        await Assert.That(PdfAValidator.Validate(saved)!.Passed).IsTrue();
    }

    /// <summary>Lossy mode merges near-identical marks, reports that it did, and stays close to the original page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LossyModeIsReportedAndStaysClose()
    {
        var source = TextPdf(OneBitGray, false, true);
        var lossless = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.Smaller with { Jbig2 = PdfJbig2Mode.Lossless });
        var lossy = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.Smaller with { Jbig2 = PdfJbig2Mode.Lossy });

        await Assert.That(lossy.Bytes.Length).IsLessThan(lossless.Bytes.Length);
        await Assert.That(lossy.Report.Actions.Any(static action => action.Description.Contains("lossy", StringComparison.OrdinalIgnoreCase))).IsTrue();
        await Assert.That(OptimizerTestKit.MeanDifference(OptimizerTestKit.Render(source), OptimizerTestKit.Render(lossy.Bytes))).IsLessThan(LossyChangeLimit * byte.MaxValue);
    }

    /// <summary>Lossy JBIG2 is not used when lossy images are not allowed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LossyModeNeedsLossyImagesAllowed()
    {
        var source = TextPdf(OneBitGray, false, true);
        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.KeepQuality with { Jbig2 = PdfJbig2Mode.Lossy });

        await Assert.That(result.Report.Actions.Any(static action => action.Description.Contains("lossy", StringComparison.OrdinalIgnoreCase))).IsFalse();
        await Assert.That(OptimizerTestKit.MaxDifference(OptimizerTestKit.Render(source), OptimizerTestKit.Render(result.Bytes))).IsEqualTo(0);
    }

    /// <summary>An image that is already JBIG2 is left as it is.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExistingJbig2IsLeftAlone()
    {
        var rows = Jbig2TestPages.Text(Page, Clean);
        var coded = Jbig2Encoder.Encode(rows, Page.Width, Page.Height);
        var image = MiniPdf.Stream(
            OptimizerSamples.Format($"/Type /XObject /Subtype /Image /Width {Page.Width} /Height {Page.Height} {OneBitGray} /Filter /JBIG2Decode"),
            OptimizerTestKit.Latin1(coded.Data));
        var source = OptimizerSamples.ImagePage(image, DrawPoints);
        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.Smaller with { Jbig2 = PdfJbig2Mode.Lossy });

        await Assert.That(FirstImage(result.Bytes).CopyRawData().AsSpan().SequenceEqual(coded.Data)).IsTrue();
    }

    /// <summary>Builds a page drawing a text-like 1-bit image.</summary>
    /// <param name="entries">The image entries after the size.</param>
    /// <param name="pdfA1">Whether the file claims PDF/A-1b.</param>
    /// <returns>The file.</returns>
    private static byte[] TextPdf(string entries, bool pdfA1) => TextPdf(entries, pdfA1, false);

    /// <summary>Builds a page drawing a text-like 1-bit image.</summary>
    /// <param name="entries">The image entries after the size.</param>
    /// <param name="pdfA1">Whether the file claims PDF/A-1b.</param>
    /// <param name="perturb">Whether pixels are flipped so repeated glyphs are no longer identical.</param>
    /// <returns>The file.</returns>
    private static byte[] TextPdf(string entries, bool pdfA1, bool perturb)
    {
        var rows = Jbig2TestPages.Text(Page, Clean);
        if (perturb)
        {
            for (var y = 0; y < Page.Height; y += Spacing.Height)
            {
                for (var x = 1; x < Page.Width; x += Spacing.Width)
                {
                    Jbig2TestPages.Flip(rows, Page.Width, x, y);
                }
            }
        }

        var source = OptimizerSamples.ImagePage(OptimizerSamples.ImageObject(rows, Page.Width, Page.Height, entries), DrawPoints);
        return pdfA1 ? ClaimPdfAOne(source) : source;
    }

    /// <summary>Gets the /DecodeParms dictionary of an image.</summary>
    /// <param name="image">The image.</param>
    /// <returns>The parameters, or null.</returns>
    private static PdfDictionary? ImageCodec(PdfStream image) => image.Dictionary.GetDictionary(KnownName.DecodeParms);

    /// <summary>Builds a page that draws two 1-bit text images made of the same glyphs.</summary>
    /// <returns>The file.</returns>
    private static byte[] TwoImagePdf()
    {
        var first = OptimizerSamples.ImageObject(Jbig2TestPages.Text(Page, Clean), Page.Width, Page.Height, OneBitGray);
        var second = OptimizerSamples.ImageObject(Jbig2TestPages.Text(Page, Other), Page.Width, Page.Height, OneBitGray);
        var content = OptimizerSamples.Format($"q {DrawPoints} 0 0 {DrawPoints} 20 20 cm /Im1 Do Q q {DrawPoints} 0 0 {DrawPoints} 20 400 cm /Im2 Do Q\n");
        return MiniPdf.Build(
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 900] /Contents 4 0 R /Resources << /XObject << /Im1 5 0 R /Im2 6 0 R >> >> >>",
            MiniPdf.Stream(string.Empty, content),
            first,
            second,
        ]);
    }

    /// <summary>Adds an XMP packet that claims PDF/A-1b, and sets the 1.4 header.</summary>
    /// <param name="source">The file.</param>
    /// <returns>The file with the claim.</returns>
    private static byte[] ClaimPdfAOne(byte[] source)
    {
        const string Xmp =
            "<?xpacket begin=\"\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?><x:xmpmeta xmlns:x=\"adobe:ns:meta/\">"
            + "<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\"><rdf:Description rdf:about=\"\" xmlns:pdfaid=\"http://www.aiim.org/pdfa/ns/id/\">"
            + "<pdfaid:part>1</pdfaid:part><pdfaid:conformance>B</pdfaid:conformance></rdf:Description></rdf:RDF></x:xmpmeta><?xpacket end=\"w\"?>";
        var text = Encoding.Latin1.GetString(source).Replace("%PDF-1.7", "%PDF-1.4", StringComparison.Ordinal);
        return IncrementalPdf.Append(
            Encoding.Latin1.GetBytes(text),
            new Dictionary<int, string> { [1] = "<< /Type /Catalog /Pages 2 0 R /Metadata 7 0 R >>", [7] = MiniPdf.Stream("/Type /Metadata /Subtype /XML", Xmp), });
    }

    /// <summary>Gets the first page's /Im1.</summary>
    /// <param name="pdf">The document.</param>
    /// <returns>The image stream.</returns>
    private static PdfStream FirstImage(byte[] pdf)
    {
        using var document = PdfDocumentReader.Open(pdf, null);
        var image = PdfDocumentPages.GetPage(document, 0).Resources!.GetDictionary(KnownName.XObject)!.GetStream(document.Objects.Names.Intern("Im1"u8))!;
        return new(image.Dictionary.Clone(), image.CopyRawData());
    }
}
