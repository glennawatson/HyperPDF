// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Accessibility;

/// <summary>Checks the Well-Tagged PDF rules for optional content (8.7), destinations (8.8) and embedded file descriptions (8.14.1).</summary>
public sealed class WtpdfNavigationTests
{
    /// <summary>The object number of the file specification, the first object after the base ones.</summary>
    private const int FilespecObject = 9;

    /// <summary>The object number of the embedded file stream.</summary>
    private const int EmbeddedStreamObject = 10;

    /// <summary>An optional content configuration that has a name and no AS entry passes; one without a name, when configurations exist, or with AS is reported.</summary>
    /// <param name="catalogExtra">The entries added to the catalog.</param>
    /// <param name="expected">The expected number of 8.7 findings.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("/OCProperties << /OCGs [] /D << /Name (Default) >> /Configs [<< /Name (Alt) >>] >>", 0)]
    [Arguments("/OCProperties << /OCGs [] /D << /Name (Default) >> /Configs [<< >>] >>", 1)]
    [Arguments("/OCProperties << /OCGs [] /D << >> /Configs [<< /Name (Alt) >>] >>", 1)]
    [Arguments("/OCProperties << /OCGs [] /D << >> >>", 0)]
    [Arguments("/OCProperties << /OCGs [] /D << /AS [] >> >>", 1)]
    public async Task OptionalContentConfigurationsNeedANameAndNoAutomaticState(string catalogExtra, int expected, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build(catalogExtra, string.Empty, []), null, cancellationToken);

        var report = PdfDocumentWtpdf.Check(document, cancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(report.Findings.Count(static f => f.Clause == "8.7")).IsEqualTo(expected);
            await Assert.That(Array.TrueForAll(report.Findings, static f => f.Clause != "8.7" || f.Profile == PdfWtpdfProfile.Accessibility)).IsTrue();
        }
    }

    /// <summary>A link that points at a page is reported; one that points at a structure element is not.</summary>
    /// <param name="link">The link annotation's destination entries.</param>
    /// <param name="expected">The expected number of 8.8 findings.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("/Dest [3 0 R /Fit]", 1)]
    [Arguments("/Dest [6 0 R /Fit]", 0)]
    [Arguments("/A << /S /GoTo /D [3 0 R /Fit] >>", 1)]
    [Arguments("/A << /S /GoTo /D [6 0 R /Fit] >>", 0)]
    [Arguments("/A << /S /GoTo /D [3 0 R /Fit] /SD [6 0 R /Fit] >>", 0)]
    [Arguments("/A << /S /URI /URI (https://example.com) >>", 0)]
    public async Task LinksNeedStructureDestinations(string link, int expected, CancellationToken cancellationToken)
    {
        var annotation = $"<< /Type /Annot /Subtype /Link /Rect [0 0 10 10] /Contents (Go) {link} >>";
        using var document = await PdfDocumentReader.OpenAsync(Build(string.Empty, $"/Tabs /S /Annots [{annotation}]", []), null, cancellationToken);

        var report = PdfDocumentWtpdf.Check(document, cancellationToken);

        await Assert.That(report.Findings.Count(static f => f.Clause == "8.8")).IsEqualTo(expected);
    }

    /// <summary>An embedded file whose file specification has no Desc is reported.</summary>
    /// <param name="description">The file specification's Desc entry, or empty.</param>
    /// <param name="expected">The expected number of 8.14.1 findings.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("", 1)]
    [Arguments("/Desc (A table of figures)", 0)]
    public async Task EmbeddedFilesNeedADescription(string description, int expected, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(BuildWithAttachment(description), null, cancellationToken);

        var report = PdfDocumentWtpdf.Check(document, cancellationToken);

        await Assert.That(report.Findings.Count(static f => f.Clause == "8.14.1")).IsEqualTo(expected);
    }

    /// <summary>A graphic in a signature's appearance needs marked content with an Alt entry.</summary>
    /// <param name="content">The appearance stream's content.</param>
    /// <param name="expected">The expected number of 8.10.3.5 findings.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("0 0 10 10 re f", 1)]
    [Arguments("/Figure << /Alt (Seal) >> BDC 0 0 10 10 re f EMC", 0)]
    [Arguments("/Artifact BMC 0 0 10 10 re f EMC", 1)]
    [Arguments("/Figure << /Alt (Seal) >> BDC /Span BMC 0 0 10 10 re S EMC EMC", 0)]
    [Arguments("q Q", 0)]
    public async Task SignatureGraphicsNeedAlternativeText(string content, int expected, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(BuildWithSignature(content), null, cancellationToken);

        var report = PdfDocumentWtpdf.Check(document, cancellationToken);

        await Assert.That(report.Findings.Count(static f => f.Clause == "8.10.3.5")).IsEqualTo(expected);
    }

    /// <summary>Builds a PDF 2.0 file with one signature field whose appearance has the content.</summary>
    /// <param name="content">The appearance stream's content.</param>
    /// <returns>The file.</returns>
    internal static byte[] BuildWithSignature(string content)
    {
        string[] more =
        [
            "<< /Type /Annot /Subtype /Widget /FT /Sig /T (sig) /Rect [0 0 10 10] /Contents (Signed) /AP << /N 10 0 R >> "
            + "/V << /Type /Sig /Filter /Adobe.PPKLite /SubFilter /adbe.pkcs7.detached /ByteRange [0 0 0 0] /Contents <00> >> >>",
            MiniPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 10 10]", content),
        ];
        return Build("/AcroForm << /Fields [9 0 R] /SigFlags 1 >>", "/Tabs /S /Annots [9 0 R]", more);
    }

    /// <summary>Builds a PDF 2.0 file with one embedded file.</summary>
    /// <param name="description">The file specification's Desc entry, or empty.</param>
    /// <returns>The file.</returns>
    internal static byte[] BuildWithAttachment(string description)
    {
        string[] more =
        [
            $"<< /Type /Filespec /F (figures.csv) /UF (figures.csv) /EF << /F {EmbeddedStreamObject} 0 R >> {description} >>",
            MiniPdf.Stream("/Type /EmbeddedFile", "a,b"),
        ];
        return Build($"/Names << /EmbeddedFiles << /Names [(figures.csv) {FilespecObject} 0 R] >> >>", string.Empty, more);
    }

    /// <summary>Builds a PDF 2.0 file with a Document holding a paragraph, plus extra objects.</summary>
    /// <param name="catalogExtra">Entries for the catalog.</param>
    /// <param name="pageExtra">Entries for the page.</param>
    /// <param name="more">Objects numbered after the base ones.</param>
    /// <returns>The file.</returns>
    internal static byte[] Build(string catalogExtra, string pageExtra, string[] more) => MiniPdf.Build(
    [
        $"<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 5 0 R /MarkInfo << /Marked true >> /Version /2.0 /Metadata 8 0 R /Lang (en-US) {catalogExtra} >>",
        "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
        $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] {pageExtra} >>",
        "<< /Type /StructElem /S /Document /NS 7 0 R /P 5 0 R /K [6 0 R] >>",
        "<< /Type /StructTreeRoot /K 4 0 R /Namespaces [7 0 R] >>",
        "<< /Type /StructElem /S /P /NS 7 0 R /P 4 0 R /Pg 3 0 R /K 0 >>",
        "<< /Type /Namespace /NS (http://iso.org/pdf2/ssn) >>",
        MiniPdf.Stream("/Type /Metadata /Subtype /XML", AccessibilityPdfs.Packet(PdfUaClaim.Part2, true, "2024")),
        .. more,
    ]);
}
