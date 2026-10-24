// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>Tests for the PDF/A font rules: the fonts and glyphs text-showing operators use, as veraPDF checks them.</summary>
public sealed class PdfAFontRulesTests
{
    /// <summary>PDF/A-2 clause 6.2.11.4.1, embedded font programs.</summary>
    private const string Embedding2 = "6.2.11.4.1";

    /// <summary>PDF/A-2 clause 6.2.11.2, font dictionary entries.</summary>
    private const string Entries2 = "6.2.11.2";

    /// <summary>A Helvetica font resource with no descriptor or program.</summary>
    private const string Helvetica = "/Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >> >>";

    /// <summary>A font that is not embedded fails when its text is drawn.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnembeddedFontFailsWhenDrawn()
    {
        var report = Validate(Text(Helvetica, "BT /F1 12 Tf (Hello) Tj ET"), PdfALevel.PdfA2B);

        await Assert.That(Fails(report, Embedding2, "1")).IsTrue();
        await Assert.That(Fails(report, Entries2, "4")).IsFalse();
    }

    /// <summary>Invisible text (rendering mode 3) needs no embedded program.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InvisibleTextNeedsNoProgram()
    {
        var report = Validate(Text(Helvetica, "BT 3 Tr /F1 12 Tf (Hello) Tj ET"), PdfALevel.PdfA2B);

        await Assert.That(Fails(report, Embedding2, "1")).IsFalse();
    }

    /// <summary>A font resource no text uses is not checked.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnusedFontIsNotChecked()
    {
        var report = Validate(Text(Helvetica, "0 0 10 10 re f"), PdfALevel.PdfA2B);

        await Assert.That(Fails(report, Embedding2, "1")).IsFalse();
        await Assert.That(Array.Exists(report.Checked, static r => r.Clause == Embedding2 && r.Test == "1")).IsTrue();
    }

    /// <summary>A shown code that the encoding leaves unused selects .notdef.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NotdefGlyphFails()
    {
        var report = Validate(Text(Helvetica, "BT /F1 12 Tf <01> Tj ET"), PdfALevel.PdfA2B);

        await Assert.That(Fails(report, "6.2.11.8", "1")).IsTrue();
    }

    /// <summary>A TrueType font without /FirstChar, and a symbolic one with /Encoding, break the dictionary rules.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TrueTypeDictionaryRules()
    {
        var pdf = ConformancePdf.Page();
        var descriptor = pdf.AddObject("<< /Type /FontDescriptor /FontName /Sym /Flags 4 >>");
        pdf.Resources = $"/Font << /F1 << /Type /Font /Subtype /TrueType /BaseFont /Sym /Encoding /WinAnsiEncoding /FontDescriptor {descriptor} 0 R >> >>";
        pdf.Content = "BT /F1 12 Tf (A) Tj ET";

        var report = Validate(pdf, PdfALevel.PdfA2B);

        await Assert.That(Fails(report, Entries2, "4")).IsTrue();
        await Assert.That(Fails(report, "6.2.11.6", "3")).IsTrue();
    }

    /// <summary>A composite font whose CIDFont and predefined CMap name different character collections fails.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CompositeCollectionsMustAgree()
    {
        var pdf = ConformancePdf.Page();
        var cidFont = pdf.AddObject("<< /Type /Font /Subtype /CIDFontType0 /BaseFont /Kor /CIDSystemInfo << /Registry (Adobe) /Ordering (Korea1) /Supplement 0 >> >>");
        pdf.Resources = $"/Font << /F1 << /Type /Font /Subtype /Type0 /BaseFont /Kor /Encoding /UniJIS-UCS2-H /DescendantFonts [{cidFont} 0 R] >> >>";
        pdf.Content = "BT /F1 12 Tf <0041> Tj ET";

        var report = Validate(pdf, PdfALevel.PdfA2B);

        await Assert.That(Fails(report, "6.2.11.3.1", "1")).IsTrue();
        await Assert.That(Fails(report, "6.2.11.3.3", "1")).IsFalse();
    }

    /// <summary>A font name that is not valid UTF-8 breaks the name rule.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FontNamesMustBeUtf8()
    {
        var report = Validate(Text("/Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Bad#FFName >> >>", "0 0 1 1 re f"), PdfALevel.PdfA2B);

        await Assert.That(Fails(report, "6.1.8", "1")).IsTrue();
    }

    /// <summary>PDF/A-4 rejects private use characters in a marked-content /ActualText.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ActualTextPrivateUseFails()
    {
        var report = Validate(Text(Helvetica, "/Span << /ActualText <FEFFE000> >> BDC EMC"), PdfALevel.PdfA4);

        await Assert.That(Fails(report, "6.2.10.8", "1")).IsTrue();
    }

    /// <summary>A TrueType program that does not parse counts as not embedded.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BrokenTrueTypeProgramIsNotEmbedded()
    {
        var pdf = ConformancePdf.Page();
        var program = pdf.AddStream(string.Empty, "not a font");
        var descriptor = pdf.AddObject($"<< /Type /FontDescriptor /FontName /Broken /Flags 32 /FontFile2 {program} 0 R >>");
        pdf.Resources = $"/Font << /F1 << /Type /Font /Subtype /TrueType /BaseFont /Broken /FirstChar 65 /LastChar 65 /Widths [500] /Encoding /WinAnsiEncoding /FontDescriptor {descriptor} 0 R >> >>";
        pdf.Content = "BT /F1 12 Tf (A) Tj ET";

        var report = Validate(pdf, PdfALevel.PdfA2B);

        await Assert.That(Fails(report, Embedding2, "1")).IsTrue();
    }

    /// <summary>Validation stops when cancelled.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ValidationHonoursCancellation()
    {
        using var document = PdfDocumentReader.Open(Text(Helvetica, "BT /F1 12 Tf (Hello) Tj ET").ToBytes(), null);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.That(() => PdfAValidator.Validate(document, PdfALevel.PdfA2B, cancelled.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>Builds a page with resources and content.</summary>
    /// <param name="resources">The /Resources entries.</param>
    /// <param name="content">The content stream.</param>
    /// <returns>The builder.</returns>
    private static RenderTestPdf Text(string resources, string content)
    {
        var pdf = ConformancePdf.Page();
        pdf.Resources = resources;
        pdf.Content = content;
        return pdf;
    }

    /// <summary>Validates a built PDF at a level.</summary>
    /// <param name="pdf">The builder.</param>
    /// <param name="level">The level.</param>
    /// <returns>The report.</returns>
    private static PdfAValidationReport Validate(RenderTestPdf pdf, PdfALevel level)
    {
        using var document = PdfDocumentReader.Open(pdf.ToBytes(), null);
        return PdfAValidator.Validate(document, level);
    }

    /// <summary>Determines whether a rule failed.</summary>
    /// <param name="report">The report.</param>
    /// <param name="clause">The clause.</param>
    /// <param name="test">The test number.</param>
    /// <returns><see langword="true"/> when it failed.</returns>
    private static bool Fails(PdfAValidationReport report, string clause, string test) => Array.Exists(report.Failed, r => r.Clause == clause && r.Test == test);
}
