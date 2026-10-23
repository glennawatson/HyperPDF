// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Graphics.Jpeg;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Robustness;

/// <summary>Tests that a truncated JPEG is reported while it decodes, and that a save gives every page a /Resources entry.</summary>
public sealed class ImageAndResourceRepairTests
{
    /// <summary>The catalog.</summary>
    private const string Catalog = "<< /Type /Catalog /Pages 2 0 R >>";

    /// <summary>The page tree node.</summary>
    private const string Pages = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>";

    /// <summary>A page with no /Resources of its own.</summary>
    private const string BarePage = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] >>";

    /// <summary>The object number of the image.</summary>
    private const int ImageNumber = 4;

    /// <summary>The object number of the page.</summary>
    private const int PageNumber = 3;

    /// <summary>The first sample value of the test JPEG.</summary>
    private const byte First = 40;

    /// <summary>The second sample value of the test JPEG.</summary>
    private const byte Second = 120;

    /// <summary>The third sample value of the test JPEG.</summary>
    private const byte Third = 200;

    /// <summary>The components of the test JPEG.</summary>
    private const int Components = 3;

    /// <summary>The bytes cut from the end of the JPEG: the end marker and a little scan data.</summary>
    private const int Cut = 3;

    /// <summary>A JPEG cut short is reported once, with its object number, and still decodes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TruncatedJpegIsReportedOncePerImage()
    {
        using var document = PdfDocumentReader.Open(ImageDocument(Cut), null);
        var stream = StoreReading.GetObject(document.Objects, new(ImageNumber, 0)).AsStream()!;
        var first = PdfImageDecoder.Decode(stream);
        var second = PdfImageDecoder.Decode(stream);
        var reports = PdfDocumentCheck.GetRepairs(document).Where(static repair => repair.Code == PdfDiagnosticCode.TruncatedStream).ToList();
        await Assert.That(first).IsNotNull();
        await Assert.That(second).IsNotNull();
        await Assert.That(reports.Count).IsEqualTo(1);
        await Assert.That(reports[0].ObjectNumber).IsEqualTo(ImageNumber);
        await Assert.That(PdfDocumentCheck.WasRepaired(document)).IsTrue();
    }

    /// <summary>A whole JPEG decodes without a report.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WholeJpegIsNotReported()
    {
        using var document = PdfDocumentReader.Open(ImageDocument(0), null);
        _ = PdfImageDecoder.Decode(StoreReading.GetObject(document.Objects, new(ImageNumber, 0)).AsStream()!);
        await Assert.That(PdfDocumentCheck.WasRepaired(document)).IsFalse();
    }

    /// <summary>A page with no /Resources, own or inherited, gets an empty one on save.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SaveAddsMissingResources()
    {
        using var document = PdfDocumentReader.Open(MiniPdf.Build(Catalog, Pages, BarePage), null);
        var saved = PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Classic);
        using var reopened = PdfDocumentReader.Open(saved, null);
        await Assert.That(PdfDocumentPages.GetPage(reopened, 0).Dictionary.GetDictionary(KnownName.Resources)).IsNotNull();
        await Assert.That(StoreRepairs.GetDiagnostics(document.Objects).Where(static fault => fault.Code == PdfDiagnosticCode.FixedOnSave && fault.ObjectNumber == PageNumber)).IsNotEmpty();
    }

    /// <summary>Resources inherited from the page tree are enough; the page is left alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InheritedResourcesAreNotDuplicated()
    {
        var file = MiniPdf.Build(Catalog, "<< /Type /Pages /Kids [3 0 R] /Count 1 /Resources << >> >>", BarePage);
        using var document = PdfDocumentReader.Open(file, null);
        var saved = PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Classic);
        await Assert.That(StoreRepairs.GetDiagnostics(document.Objects).Where(static fault => fault.Code == PdfDiagnosticCode.FixedOnSave)).IsEmpty();
        await Assert.That(Encoding.Latin1.GetString(saved)).DoesNotContain("/Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] /Resources");
    }

    /// <summary>Builds a one-page document with a JPEG image object.</summary>
    /// <param name="cut">The bytes cut from the end of the JPEG.</param>
    /// <returns>The file.</returns>
    private static byte[] ImageDocument(int cut)
    {
        var jpeg = JpegTestEncoder.Encode(new(1, 1, Components, JpegTestEncoder.NoAdobe, 0), [First, Second, Third]);
        var data = Encoding.Latin1.GetString(jpeg.AsSpan(0, jpeg.Length - cut));
        return MiniPdf.Build(Catalog, Pages, BarePage, MiniPdf.Stream("/Type /XObject /Subtype /Image /Width 8 /Height 8 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode", data));
    }
}
