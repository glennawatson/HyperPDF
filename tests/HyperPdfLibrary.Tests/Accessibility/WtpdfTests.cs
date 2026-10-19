// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Accessibility;

/// <summary>Checks Well-Tagged PDF 1.0 (PDF Association): the structure rules of clause 8.2.5 and the reuse and accessibility declarations of clause 6.</summary>
public sealed class WtpdfTests
{
    /// <summary>Each rule's clause is reported for the tag that breaks it, and a plain paragraph breaks none.</summary>
    /// <param name="type">The tag's type.</param>
    /// <param name="extra">Extra entries on the tag.</param>
    /// <param name="clause">The expected clause, or empty for none.</param>
    /// <param name="profile">The expected lowest level.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("P", "", "", PdfWtpdfProfile.Reuse)]
    [Arguments("H", "", "8.2.5.12", PdfWtpdfProfile.Reuse)]
    [Arguments("Note", "", "8.2.5.14.1", PdfWtpdfProfile.Reuse)]
    [Arguments("Figure", "", "8.2.5.28.2", PdfWtpdfProfile.Accessibility)]
    [Arguments("Figure", "/Alt (A box)", "", PdfWtpdfProfile.Reuse)]
    [Arguments("Ruby", "", "8.2.5.23", PdfWtpdfProfile.Reuse)]
    [Arguments("Warichu", "", "8.2.5.24", PdfWtpdfProfile.Reuse)]
    public async Task StructureRulesNameTheirClause(string type, string extra, string clause, PdfWtpdfProfile profile, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build(type, extra), null, cancellationToken);

        var report = PdfDocumentWtpdf.Check(document, cancellationToken);

        var match = report.Findings.Where(f => f.Clause == clause && clause.Length > 0).ToArray();
        var structural = report.Findings.Count(static f => f.Clause.StartsWith("8.2.5", StringComparison.Ordinal));
        using (Assert.Multiple())
        {
            await Assert.That(structural).IsEqualTo(clause.Length == 0 ? 0 : 1);
            await Assert.That(match.Length).IsEqualTo(clause.Length == 0 ? 0 : 1);
            await Assert.That(Array.TrueForAll(match, f => f.Profile == profile)).IsTrue();
        }
    }

    /// <summary>A font whose program is not embedded is reported under the font clause, for both levels.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnembeddedFontsAreReported(CancellationToken cancellationToken)
    {
        var bytes = AccessibilityPdfs.Build(new() { Font = static _ => "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>" });
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);

        var report = PdfDocumentWtpdf.Check(document, cancellationToken);

        await Assert.That(Array.Exists(report.Findings, static f => f.Clause == "8.4.5.5" && f.Profile == PdfWtpdfProfile.Reuse)).IsTrue();
    }

    /// <summary>A symbolic TrueType font with an Encoding is reported under 8.4.5.7 on the first check and again on the next, which reuses the result.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task FontRulesAreReportedOnEveryCheck(CancellationToken cancellationToken)
    {
        const string font = "<< /Type /Font /Subtype /TrueType /BaseFont /Sym /FirstChar 0 /LastChar 0 /Widths [500] /Encoding /WinAnsiEncoding "
            + "/FontDescriptor << /Type /FontDescriptor /FontName /Sym /Flags 4 /FontBBox [0 0 1 1] /ItalicAngle 0 /Ascent 1 /Descent 0 /CapHeight 1 /StemV 1 >> >>";
        using var document = await PdfDocumentReader.OpenAsync(AccessibilityPdfs.Build(new() { Font = static _ => font }), null, cancellationToken);

        var first = PdfDocumentWtpdf.Check(document, cancellationToken).Findings.Count(static f => f.Clause == "8.4.5.7");
        var second = PdfDocumentWtpdf.Check(document, cancellationToken).Findings.Count(static f => f.Clause == "8.4.5.7");

        using (Assert.Multiple())
        {
            await Assert.That(first).IsEqualTo(1);
            await Assert.That(second).IsEqualTo(1);
        }
    }

    /// <summary>The accessibility level needs a PDF/UA-2 claim, which a file without one does not make.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task AccessibilityNeedsAPdfUa2Claim(CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build("P", string.Empty), null, cancellationToken);

        var report = PdfDocumentWtpdf.Check(document, cancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(Array.Exists(report.Findings, static f => f.Clause == "6" && f.Profile == PdfWtpdfProfile.Accessibility)).IsTrue();
            await Assert.That(report.CountFor(PdfWtpdfProfile.Reuse)).IsEqualTo(0);
            await Assert.That(report.CountFor(PdfWtpdfProfile.Accessibility)).IsGreaterThan(0);
        }
    }

    /// <summary>A file that meets the reuse rules can declare reuse, the declaration reads back, and accessibility is still refused.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeclaringReuseWritesTheDeclaration(CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build("P", string.Empty), null, cancellationToken);

        PdfDocumentWtpdf.Declare(document, PdfWtpdfProfile.Reuse);
        var report = PdfDocumentWtpdf.Check(document, cancellationToken);
        var error = Assert.Throws<InvalidOperationException>(() => PdfDocumentWtpdf.Declare(document, PdfWtpdfProfile.Accessibility));

        using (Assert.Multiple())
        {
            await Assert.That(report.DeclaresReuse).IsTrue();
            await Assert.That(report.DeclaresAccessibility).IsFalse();
            await Assert.That(error.Message).Contains("PDF/UA-2");
        }
    }

    /// <summary>A declared level can be withdrawn.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task WithdrawingRemovesTheDeclaration(CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build("P", string.Empty), null, cancellationToken);
        PdfDocumentWtpdf.Declare(document, PdfWtpdfProfile.Reuse);

        var removed = PdfDocumentWtpdf.Withdraw(document, PdfWtpdfProfile.Reuse);

        using (Assert.Multiple())
        {
            await Assert.That(removed).IsTrue();
            await Assert.That(PdfDocumentWtpdf.Check(document, cancellationToken).DeclaresReuse).IsFalse();
        }
    }

    /// <summary>A file that breaks a reuse rule is refused, so the claim stays true.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeclaringRefusesAFileThatBreaksARule(CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build("H", string.Empty), null, cancellationToken);

        var error = Assert.Throws<InvalidOperationException>(() => PdfDocumentWtpdf.Declare(document, PdfWtpdfProfile.Reuse));

        using (Assert.Multiple())
        {
            await Assert.That(error.Message).Contains("8.2.5.12");
            await Assert.That(PdfDocumentWtpdf.Check(document, cancellationToken).DeclaresReuse).IsFalse();
        }
    }

    /// <summary>An unknown level is refused.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeclaringNoLevelThrows(CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build("P", string.Empty), null, cancellationToken);

        await Assert.That(() => PdfDocumentWtpdf.Declare(document, PdfWtpdfProfile.None)).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>Annotation rules name their clause, a page without Tabs is reported, and a missing language is reported.</summary>
    /// <param name="annotation">The annotation's dictionary text.</param>
    /// <param name="tabs">The page's Tabs entry.</param>
    /// <param name="clause">The expected clause.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("<< /Type /Annot /Subtype /Ink /Rect [0 0 10 10] >>", "/Tabs /S", "8.9.2.4.8")]
    [Arguments("<< /Type /Annot /Subtype /Ink /Rect [0 0 10 10] /Contents (A sketch) >>", "", "8.9.3.3")]
    [Arguments("<< /Type /Annot /Subtype /Sound /Rect [0 0 10 10] >>", "/Tabs /S", "8.9.2.4")]
    [Arguments("<< /Type /Annot /Subtype /Screen /Rect [0 0 10 10] >>", "/Tabs /S", "8.9.2.4.12")]
    [Arguments("<< /Type /Annot /Subtype /Stamp /Rect [0 0 10 10] >>", "/Tabs /S", "8.9.2.4.7")]
    [Arguments("<< /Type /Annot /Subtype /Square /Rect [0 0 10 10] /RC (one) /Contents (two) >>", "/Tabs /S", "8.9.2.3")]
    [Arguments("<< /Type /Annot /Subtype /Widget /Rect [0 0 10 10] /FT /Tx /T (a) /RV (x) /Contents (a) >>", "/Tabs /S", "8.10.3.3")]
    [Arguments("<< /Type /Annot /Subtype /Widget /Rect [0 0 10 10] /FT /Tx /T (a) /RV (x) /V (y) /Contents (a) >>", "/Tabs /S", "8.10.3.3")]
    public async Task AnnotationRulesNameTheirClause(string annotation, string tabs, string clause, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build("P", string.Empty, annotation, tabs, true), null, cancellationToken);

        var report = PdfDocumentWtpdf.Check(document, cancellationToken);

        await Assert.That(report.Findings.Count(f => f.Clause == clause)).IsEqualTo(1);
    }

    /// <summary>A catalog without a language is reported under clause 8.4.4.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingCatalogLanguageIsReported(CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build("P", string.Empty, null, string.Empty, false), null, cancellationToken);

        var report = PdfDocumentWtpdf.Check(document, cancellationToken);

        await Assert.That(report.Findings.Count(static f => f.Clause == "8.4.4")).IsEqualTo(1);
    }

    /// <summary>Builds a file with a PDF 2.0 Document holding one tag.</summary>
    /// <param name="type">The tag's type.</param>
    /// <param name="extra">Extra entries on the tag.</param>
    /// <returns>The file.</returns>
    private static byte[] Build(string type, string extra) => Build(type, extra, null, string.Empty, true);

    /// <summary>Builds a file with a PDF 2.0 Document holding one tag.</summary>
    /// <param name="type">The tag's type.</param>
    /// <param name="extra">Extra entries on the tag.</param>
    /// <param name="annotation">An annotation on the page, or <see langword="null"/>.</param>
    /// <param name="tabs">The page's extra entries.</param>
    /// <param name="language">Whether the catalog has a language.</param>
    /// <returns>The file.</returns>
    private static byte[] Build(string type, string extra, string? annotation, string tabs, bool language) => MiniPdf.Build(
    [
        $"<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 5 0 R /MarkInfo << /Marked true >> /Version /2.0 /Metadata 8 0 R{(language ? " /Lang (en-US)" : string.Empty)} >>",
        "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
        $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] {tabs}{(annotation is null ? string.Empty : $" /Annots [{annotation}]")} >>",
        "<< /Type /StructElem /S /Document /NS 7 0 R /P 5 0 R /K [6 0 R] >>",
        "<< /Type /StructTreeRoot /K 4 0 R /Namespaces [7 0 R] >>",
        $"<< /Type /StructElem /S /{type} /NS 7 0 R /P 4 0 R /Pg 3 0 R /K 0 {extra} >>",
        "<< /Type /Namespace /NS (http://iso.org/pdf2/ssn) >>",
        MiniPdf.Stream("/Type /Metadata /Subtype /XML", AccessibilityPdfs.Packet(PdfUaClaim.Part2, true, "2024")),
    ]);
}
