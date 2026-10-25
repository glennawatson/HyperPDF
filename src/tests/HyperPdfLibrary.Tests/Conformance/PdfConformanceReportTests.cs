// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>Tests for the PDF/A, PDF/UA and PDF/X reading report: claims, output intents and the observations.</summary>
public sealed class PdfConformanceReportTests
{
    /// <summary>The revision year of PDF/A-4.</summary>
    private const int Part4Revision = 2020;

    /// <summary>The components of a gray profile stream.</summary>
    private const int GrayComponents = 1;

    /// <summary>A constant alpha below 1.</summary>
    private const string HalfAlpha = "0.5";

    /// <summary>The claimed part 1.</summary>
    private const int PartOne = 1;

    /// <summary>The claimed part 2.</summary>
    private const int PartTwo = 2;

    /// <summary>The transparency features in the combined test: soft mask, blend mode and group.</summary>
    private const int TransparencyFeatures = 3;

    /// <summary>The claimed part 4.</summary>
    private const int PartFour = 4;

    /// <summary>Content that shows text in the font named <c>/F1</c>.</summary>
    private const string ShowF1 = "BT /F1 12 Tf (a) Tj ET";

    /// <summary>Content that draws the form named <c>/Fm</c>.</summary>
    private const string DrawForm = "/Fm Do";

    /// <summary>Content that sets the graphics state named <c>/GS</c> and fills.</summary>
    private const string SetState = "/GS gs 0 0 10 10 re f";

    /// <summary>The XMP claims map to the labels people write.</summary>
    /// <param name="part">The part.</param>
    /// <param name="conformance">The level, or null.</param>
    /// <param name="label">The expected label.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1, "B", "1B")]
    [Arguments(1, "A", "1A")]
    [Arguments(2, "B", "2B")]
    [Arguments(3, "b", "3B")]
    [Arguments(3, "U", "3U")]
    [Arguments(4, null, "4")]
    [Arguments(4, "E", "4E")]
    [Arguments(4, "F", "4F")]
    public async Task ClaimsReadFromXmp(int part, string? conformance, string label)
    {
        var pdf = ConformancePdf.Page();
        ConformancePdf.Claim(pdf, part, conformance, part == PartFour ? Part4Revision : null);

        var report = Report(pdf);

        await Assert.That(report.HasXmp).IsTrue();
        await Assert.That(report.PdfA).IsNotNull();
        await Assert.That(report.PdfA!.Label).IsEqualTo(label);
        await Assert.That(report.PdfA.Part).IsEqualTo(part);
        await Assert.That(report.PdfA.IsRecognised).IsTrue();
    }

    /// <summary>Part 4 keeps its revision year.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Part4KeepsRevision()
    {
        var pdf = ConformancePdf.Page();
        ConformancePdf.Claim(pdf, PartFour, "F", Part4Revision);

        var report = Report(pdf);

        await Assert.That(report.PdfA!.Revision).IsEqualTo(Part4Revision);
        await Assert.That(report.PdfA.Conformance).IsEqualTo("F");
    }

    /// <summary>A level that does not belong to the part is read but not recognised.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OddLevelIsNotRecognised()
    {
        var pdf = ConformancePdf.Page();
        ConformancePdf.Claim(pdf, PartOne, "U", null);

        var report = Report(pdf);

        await Assert.That(report.PdfA!.IsRecognised).IsFalse();
    }

    /// <summary>A file without <c>pdfaid</c> makes no claim, and one without XMP has none either.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NoClaimWithoutPdfAId()
    {
        var plain = Report(ConformancePdf.Page());
        var other = ConformancePdf.Page();
        ConformancePdf.AttachXmp(other, "xmlns:dc=\"http://purl.org/dc/elements/1.1/\"", "<dc:format>application/pdf</dc:format>");

        var withXmp = Report(other);

        await Assert.That(plain.HasXmp).IsFalse();
        await Assert.That(plain.PdfA).IsNull();
        await Assert.That(withXmp.HasXmp).IsTrue();
        await Assert.That(withXmp.PdfA).IsNull();
        await Assert.That(plain.Observations.Length).IsEqualTo(0);
    }

    /// <summary>PDF/UA and PDF/X identification are read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PdfUaAndPdfXAreRead()
    {
        var pdf = ConformancePdf.Page();
        ConformancePdf.AttachXmp(
            pdf,
            "xmlns:pdfuaid=\"http://www.aiim.org/pdfua/ns/id/\" xmlns:pdfxid=\"http://www.npes.org/pdfx/ns/id/\"",
            "<pdfuaid:part>1</pdfuaid:part><pdfxid:GTS_PDFXVersion>PDF/X-4</pdfxid:GTS_PDFXVersion>");

        var report = Report(pdf);

        await Assert.That(report.PdfUaPart).IsEqualTo(1);
        await Assert.That(report.PdfXVersion).IsEqualTo("PDF/X-4");
    }

    /// <summary>The Info dictionary's version is preferred for PDF/X.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PdfXVersionComesFromInfo()
    {
        var pdf = ConformancePdf.Page();
        var info = pdf.AddObject("<< /GTS_PDFXVersion (PDF/X-1a:2001) >>");
        var bytes = pdf.ToBytes();
        var text = System.Text.Encoding.Latin1.GetString(bytes).Replace("/Root 1 0 R", string.Create(CultureInfo.InvariantCulture, $"/Root 1 0 R /Info {info} 0 R"), StringComparison.Ordinal);

        using var document = PdfDocumentReader.Open(System.Text.Encoding.Latin1.GetBytes(text), null);
        var report = PdfDocumentConformance.GetConformance(document);

        await Assert.That(report.PdfXVersion).IsEqualTo("PDF/X-1a:2001");
    }

    /// <summary>The output intent is listed with its identifier, registry, info and profile component count.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OutputIntentIsRead()
    {
        var pdf = ConformancePdf.Page();
        ConformancePdf.Intent(pdf, ConformancePdf.PdfASubtype, ConformancePdf.CmykProfile(), ConformancePdf.CmykComponents);

        var report = Report(pdf);

        await Assert.That(report.OutputIntents.Length).IsEqualTo(1);
        var intent = report.OutputIntents[0];
        await Assert.That(intent.Subtype).IsEqualTo(ConformancePdf.PdfASubtype);
        await Assert.That(intent.OutputConditionIdentifier).IsEqualTo("Test condition");
        await Assert.That(intent.RegistryName).IsEqualTo("http://www.color.org");
        await Assert.That(intent.Info).IsEqualTo("Test info");
        await Assert.That(intent.HasProfile).IsTrue();
        await Assert.That(intent.ProfileComponents).IsEqualTo(ConformancePdf.CmykComponents);
        await Assert.That(intent.ProfileColorSpace).IsEqualTo("CMYK");
    }

    /// <summary>A gray profile reports the gray colour space.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GrayProfileReportsGray()
    {
        var pdf = ConformancePdf.Page();
        ConformancePdf.Intent(pdf, "GTS_PDFX", [0], GrayComponents);

        var report = Report(pdf);

        await Assert.That(report.OutputIntents[0].ProfileColorSpace).IsEqualTo("GRAY");
    }

    /// <summary>JavaScript in an action is reported, and a plain file has none.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task JavaScriptIsReported()
    {
        var pdf = ConformancePdf.Page();
        ConformancePdf.Claim(pdf, PartTwo, "B", null);
        pdf.CatalogEntries += " /OpenAction << /S /JavaScript /JS (app.alert\\(1\\)) >>";

        var report = Report(pdf);
        var clean = Report(ClaimedPage(PartTwo));

        await Assert.That(report.TryGet(PdfConformanceFinding.JavaScript, out var seen)).IsTrue();
        await Assert.That(seen.Count).IsEqualTo(1);
        await Assert.That(seen.ConflictsWithClaim).IsTrue();
        await Assert.That(clean.TryGet(PdfConformanceFinding.JavaScript, out _)).IsFalse();
    }

    /// <summary>LZW conflicts with parts 1 to 3 but not with part 4.</summary>
    /// <param name="part">The claimed part.</param>
    /// <param name="conflicts">Whether the claim forbids LZW.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1, true)]
    [Arguments(2, true)]
    [Arguments(3, true)]
    [Arguments(4, false)]
    public async Task LzwConflictsByPart(int part, bool conflicts)
    {
        var pdf = ClaimedPage(part);
        _ = pdf.AddStream("/Filter /LZWDecode", "x");

        var report = Report(pdf);
        var clean = Report(ClaimedPage(part));

        await Assert.That(report.TryGet(PdfConformanceFinding.LzwFilter, out var seen)).IsTrue();
        await Assert.That(seen.ConflictsWithClaim).IsEqualTo(conflicts);
        await Assert.That(clean.TryGet(PdfConformanceFinding.LzwFilter, out _)).IsFalse();
    }

    /// <summary>LZW inside a filter array is found too.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LzwInsideFilterArrayIsFound()
    {
        var pdf = ClaimedPage(PartOne);
        _ = pdf.AddStream("/Filter [/ASCIIHexDecode /LZWDecode]", "x");

        var report = Report(pdf);

        await Assert.That(report.TryGet(PdfConformanceFinding.LzwFilter, out _)).IsTrue();
    }

    /// <summary>A font without an embedded program is listed by name; an embedded one is not.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NonEmbeddedFontIsListed()
    {
        var pdf = ClaimedPage(PartOne);
        pdf.Resources = "/Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >>";
        pdf.Content = ShowF1;

        var report = Report(pdf);

        await Assert.That(report.TryGet(PdfConformanceFinding.NonEmbeddedFont, out var seen)).IsTrue();
        await Assert.That(seen.Count).IsEqualTo(1);
        await Assert.That(seen.Detail).IsEqualTo("Helvetica");
        await Assert.That(seen.ConflictsWithClaim).IsTrue();
    }

    /// <summary>A font with a font file, a Type 3 font and a composite font with an embedded descendant are not listed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EmbeddedAndType3FontsAreNotListed()
    {
        var pdf = ClaimedPage(PartOne);
        var file = pdf.AddStream(string.Empty, "font");
        var descriptor = pdf.AddObject(string.Create(CultureInfo.InvariantCulture, $"<< /Type /FontDescriptor /FontName /Embedded /FontFile2 {file} 0 R >>"));
        var descendant = pdf.AddObject(string.Create(CultureInfo.InvariantCulture, $"<< /Type /Font /Subtype /CIDFontType2 /BaseFont /Embedded /FontDescriptor {descriptor} 0 R >>"));
        const string Type3 = "/F2 << /Type /Font /Subtype /Type3 /FontBBox [0 0 1 1] /FontMatrix [1 0 0 1 0 0] /CharProcs << >> /Encoding << /Differences [] >> >>";
        pdf.Resources = string.Create(
            CultureInfo.InvariantCulture,
            $"/Font << /F1 << /Type /Font /Subtype /TrueType /BaseFont /Embedded /FontDescriptor {descriptor} 0 R >> {Type3} "
            + $"/F3 << /Type /Font /Subtype /Type0 /BaseFont /Embedded /Encoding /Identity-H /DescendantFonts [{descendant} 0 R] >> >>");
        pdf.Content = "BT /F1 12 Tf (a) Tj /F2 12 Tf (a) Tj /F3 12 Tf <0001> Tj ET";

        var report = Report(pdf);

        await Assert.That(report.TryGet(PdfConformanceFinding.NonEmbeddedFont, out _)).IsFalse();
    }

    /// <summary>A font used only inside a form XObject is found.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FontInsideFormIsFound()
    {
        var pdf = ClaimedPage(PartTwo);
        var form = pdf.AddStream("/Type /XObject /Subtype /Form /BBox [0 0 10 10] /Resources << /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Courier >> >> >>", ShowF1);
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/XObject << /Fm {form} 0 R >>");
        pdf.Content = DrawForm;

        var report = Report(pdf);

        await Assert.That(report.TryGet(PdfConformanceFinding.NonEmbeddedFont, out var seen)).IsTrue();
        await Assert.That(seen.Detail).IsEqualTo("Courier");
    }

    /// <summary>Constant alpha below 1 conflicts with part 1 only; alpha of 1 is not reported.</summary>
    /// <param name="part">The claimed part.</param>
    /// <param name="conflicts">Whether the claim forbids transparency.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1, true)]
    [Arguments(2, false)]
    public async Task AlphaConflictsWithPart1Only(int part, bool conflicts)
    {
        var pdf = ClaimedPage(part);
        var state = pdf.AddObject($"<< /Type /ExtGState /ca {HalfAlpha} >>");
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/ExtGState << /GS {state} 0 R >>");
        pdf.Content = SetState;
        var opaque = ClaimedPage(part);
        var one = opaque.AddObject("<< /Type /ExtGState /ca 1 /CA 1 /BM /Normal /SMask /None >>");
        opaque.Resources = string.Create(CultureInfo.InvariantCulture, $"/ExtGState << /GS {one} 0 R >>");
        opaque.Content = SetState;

        var report = Report(pdf);
        var clean = Report(opaque);

        await Assert.That(report.TryGet(PdfConformanceFinding.Transparency, out var seen)).IsTrue();
        await Assert.That(seen.ConflictsWithClaim).IsEqualTo(conflicts);
        await Assert.That(clean.TryGet(PdfConformanceFinding.Transparency, out _)).IsFalse();
    }

    /// <summary>A blend mode, a soft mask and a transparency group are each counted.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BlendModeSoftMaskAndGroupAreCounted()
    {
        var pdf = ClaimedPage(PartOne);
        var mask = pdf.AddObject("<< /S /Alpha /G 1 0 R >>");
        var state = pdf.AddObject(string.Create(CultureInfo.InvariantCulture, $"<< /Type /ExtGState /BM /Multiply /SMask {mask} 0 R >>"));
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/ExtGState << /GS {state} 0 R >>");
        pdf.Content = SetState;
        pdf.PageEntries = "/Group << /S /Transparency /CS /DeviceRGB >>";

        var report = Report(pdf);

        await Assert.That(report.TryGet(PdfConformanceFinding.Transparency, out var seen)).IsTrue();
        await Assert.That(seen.Count).IsEqualTo(TransparencyFeatures);
        await Assert.That(seen.Detail).Contains("soft masks 1");
        await Assert.That(seen.Detail).Contains("blend modes 1");
        await Assert.That(seen.Detail).Contains("groups 1");
    }

    /// <summary>Embedded files conflict with part 1 only.</summary>
    /// <param name="part">The claimed part.</param>
    /// <param name="conflicts">Whether the claim forbids embedded files.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1, true)]
    [Arguments(2, false)]
    [Arguments(3, false)]
    public async Task EmbeddedFilesConflictWithPart1Only(int part, bool conflicts)
    {
        var pdf = ClaimedPage(part);
        var data = pdf.AddStream("/Type /EmbeddedFile", "notes");
        var spec = pdf.AddObject(string.Create(CultureInfo.InvariantCulture, $"<< /Type /Filespec /F (notes.txt) /UF (notes.txt) /EF << /F {data} 0 R /UF {data} 0 R >> >>"));
        pdf.CatalogEntries += string.Create(CultureInfo.InvariantCulture, $" /Names << /EmbeddedFiles << /Names [(notes.txt) {spec} 0 R] >> >>");

        var report = Report(pdf);
        var clean = Report(ClaimedPage(part));

        await Assert.That(report.TryGet(PdfConformanceFinding.EmbeddedFiles, out var seen)).IsTrue();
        await Assert.That(seen.Count).IsEqualTo(1);
        await Assert.That(seen.ConflictsWithClaim).IsEqualTo(conflicts);
        await Assert.That(clean.TryGet(PdfConformanceFinding.EmbeddedFiles, out _)).IsFalse();
    }

    /// <summary>A stream that points at an external file is reported.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExternalStreamIsReported()
    {
        var pdf = ClaimedPage(PartTwo);
        _ = pdf.AddStream("/F (data.bin)", string.Empty);

        var report = Report(pdf);

        await Assert.That(report.TryGet(PdfConformanceFinding.ExternalContent, out var seen)).IsTrue();
        await Assert.That(seen.ConflictsWithClaim).IsTrue();
        await Assert.That(Report(ClaimedPage(PartTwo)).TryGet(PdfConformanceFinding.ExternalContent, out _)).IsFalse();
    }

    /// <summary>Device colour with no output intent is reported; an output intent, or no device colour, clears it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeviceColorWithoutOutputIntentIsReported()
    {
        var bare = ClaimedPage(PartTwo);
        bare.Content = "0 0 0 1 k 0 0 10 10 re f";
        var covered = ClaimedPage(PartTwo);
        covered.Content = "0 0 0 1 k 0 0 10 10 re f";
        ConformancePdf.Intent(covered, ConformancePdf.PdfASubtype, ConformancePdf.CmykProfile(), ConformancePdf.CmykComponents);
        var colorless = ClaimedPage(PartTwo);
        colorless.Content = "0 0 10 10 re n";

        var missing = Report(bare);

        await Assert.That(missing.TryGet(PdfConformanceFinding.MissingOutputIntent, out var seen)).IsTrue();
        await Assert.That(seen.ConflictsWithClaim).IsTrue();
        await Assert.That(Report(covered).TryGet(PdfConformanceFinding.MissingOutputIntent, out _)).IsFalse();
        await Assert.That(Report(colorless).TryGet(PdfConformanceFinding.MissingOutputIntent, out _)).IsFalse();
    }

    /// <summary>A colour space selected by name counts as device colour.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeviceColorSpaceNameIsReported()
    {
        var pdf = ClaimedPage(PartOne);
        pdf.Content = "/DeviceRGB cs 1 0 0 sc 0 0 10 10 re f";

        var report = Report(pdf);

        await Assert.That(report.TryGet(PdfConformanceFinding.MissingOutputIntent, out _)).IsTrue();
    }

    /// <summary>An encrypted file is reported; a plain one is not.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EncryptionIsReported()
    {
        using var document = PdfDocumentReader.Open(EncryptedFixture.Build(), null);

        var report = PdfDocumentConformance.GetConformance(document);

        await Assert.That(report.TryGet(PdfConformanceFinding.Encryption, out var seen)).IsTrue();
        await Assert.That(seen.Count).IsEqualTo(1);
        await Assert.That(Report(ClaimedPage(PartOne)).TryGet(PdfConformanceFinding.Encryption, out _)).IsFalse();
    }

    /// <summary>Observations conflict only when the file claims a part, and the report summarises conflicts.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ConflictsNeedAClaim()
    {
        var pdf = ConformancePdf.Page();
        pdf.Resources = "/Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >>";
        pdf.Content = ShowF1;
        var claimed = ClaimedPage(PartOne);
        claimed.Resources = pdf.Resources;
        claimed.Content = ShowF1;

        var unclaimed = Report(pdf);
        var withClaim = Report(claimed);

        await Assert.That(unclaimed.TryGet(PdfConformanceFinding.NonEmbeddedFont, out var seen)).IsTrue();
        await Assert.That(seen.ConflictsWithClaim).IsFalse();
        await Assert.That(unclaimed.HasConflicts).IsFalse();
        await Assert.That(withClaim.HasConflicts).IsTrue();
    }

    /// <summary>Builds a page that claims a part at a level that suits it.</summary>
    /// <param name="part">The part.</param>
    /// <returns>The builder.</returns>
    private static RenderTestPdf ClaimedPage(int part)
    {
        var pdf = ConformancePdf.Page();
        ConformancePdf.Claim(pdf, part, part == PartFour ? null : "B", part == PartFour ? Part4Revision : null);
        return pdf;
    }

    /// <summary>Opens a built file and reads its report.</summary>
    /// <param name="pdf">The builder.</param>
    /// <returns>The report.</returns>
    private static PdfConformanceReport Report(RenderTestPdf pdf)
    {
        using var document = PdfDocumentReader.Open(pdf.ToBytes(), null);
        return PdfDocumentConformance.GetConformance(document);
    }
}
