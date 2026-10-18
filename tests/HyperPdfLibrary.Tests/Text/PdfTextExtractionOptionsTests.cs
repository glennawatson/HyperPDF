// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.Tests.Text;

/// <summary>Checks opt-in filtering of hidden text without discarding default OCR and accessibility text.</summary>
public sealed class PdfTextExtractionOptionsTests
{
    /// <summary>The text whose paint remains visible.</summary>
    private const string VisibleText = "Visible";

    /// <summary>Non-visible text does not reach the filtered text or its character list.</summary>
    /// <param name="content">The hidden glyph's graphics state.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("BT /F1 10 Tf 3 Tr (Secret) Tj ET")]
    [Arguments("BT /F1 10 Tf 7 Tr (Secret) Tj ET")]
    [Arguments("BT /F1 0 Tf (Secret) Tj ET")]
    [Arguments("BT /F1 10 Tf 0 Tz (Secret) Tj ET")]
    [Arguments("/Zero gs BT /F1 10 Tf (Secret) Tj ET")]
    [Arguments("/FillZero gs BT /F1 10 Tf (Secret) Tj ET")]
    [Arguments("/StrokeZero gs BT /F1 10 Tf 1 Tr (Secret) Tj ET")]
    [Arguments("/FillZero gs BT /F1 10 Tf 4 Tr (Secret) Tj ET")]
    [Arguments("/StrokeZero gs BT /F1 10 Tf 5 Tr (Secret) Tj ET")]
    [Arguments("W n BT /F1 10 Tf (Secret) Tj ET")]
    [Arguments("0 0 0 100 re W n BT /F1 10 Tf (Secret) Tj ET")]
    [Arguments("0 0 m 100 0 l W n BT /F1 10 Tf (Secret) Tj ET")]
    [Arguments("0 0 m 100 100 l W n BT /F1 10 Tf (Secret) Tj ET")]
    [Arguments("0 0 0 1 0 0 cm BT /F1 10 Tf (Secret) Tj ET")]
    [Arguments("BT /F1 10 Tf 1 1 1 1 0 0 Tm (Secret) Tj ET")]
    [Arguments("/Span << /ActualText (Replacement) >> BDC BT /F1 10 Tf 3 Tr (Secret) Tj ET EMC")]
    public async Task SkipNonVisibleText_OmitsHiddenGlyphs(string content)
    {
        using var document = Open(content);
        var text = PdfDocumentText.GetTextPage(document, 0, new(true, false));
        await Assert.That(text.Text).IsEqualTo(string.Empty);
        await Assert.That(text.CharCount).IsEqualTo(0);
    }

    /// <summary>Filters do not alter the default OCR text cache.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SkipNonVisibleText_PreservesDefaultOcrCache()
    {
        using var document = Open("BT /F1 10 Tf 3 Tr (Secret) Tj ET");
        _ = PdfDocumentText.GetTextPage(document, 0, new(true, false));
        await Assert.That(PdfDocumentText.GetTextPage(document, 0).Text).IsEqualTo("Secret");
    }

    /// <summary>A restored clip allows visible text again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SkipNonVisibleText_RestoreRestoresClip()
    {
        using var document = Open("q 0 0 0 100 re W n BT /F1 10 Tf (Secret) Tj ET Q BT /F1 10 Tf (Visible) Tj ET");
        await Assert.That(PdfDocumentText.GetTextPage(document, 0, new(true, false)).Text).IsEqualTo(VisibleText);
    }

    /// <summary>Non-overlapping clipping rectangles leave no paintable area.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SkipNonVisibleText_OmitsDisjointClipIntersection()
    {
        using var document = Open("0 0 10 10 re W n 20 20 10 10 re W n BT /F1 10 Tf (Secret) Tj ET");
        await Assert.That(PdfDocumentText.GetTextPage(document, 0, new(true, false)).Text).IsEqualTo(string.Empty);
    }

    /// <summary>Opacity filtering respects the text's painted component.</summary>
    /// <param name="content">Visible stroke or fill text.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("/FillZero gs BT /F1 10 Tf 1 Tr (Visible) Tj ET")]
    [Arguments("/FillZero gs BT /F1 10 Tf 2 Tr (Visible) Tj ET")]
    [Arguments("/StrokeZero gs BT /F1 10 Tf (Visible) Tj ET")]
    [Arguments("0 0 m 100 100 l 100 0 l W n BT /F1 10 Tf (Visible) Tj ET")]
    [Arguments("0 0 100 100 re W n BT /F1 10 Tf (Visible) Tj ET")]
    public async Task SkipNonVisibleText_KeepsRemainingPaint(string content)
    {
        using var document = Open(content);
        await Assert.That(PdfDocumentText.GetTextPage(document, 0, new(true, false)).Text).IsEqualTo(VisibleText);
    }

    /// <summary>A form group's opacity still hides text when the interpreter resets the form's inner paint state.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SkipNonVisibleText_OmitsZeroOpacityFormGroup()
    {
        using var document = PdfDocumentReader.Open(
            MiniPdf.Build(
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Resources << /Font << /F1 5 0 R >> /XObject << /Fm 6 0 R >> /ExtGState << /Zero << /ca 0 /CA 0 >> >> >> /Contents 4 0 R >>",
                MiniPdf.Stream(string.Empty, "q /Zero gs /Fm Do Q BT /F1 10 Tf 0 50 Td (Visible) Tj ET"),
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
                MiniPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 100 100] /Group << /S /Transparency /I true >> /Resources << /Font << /F1 5 0 R >> >>", "BT /F1 10 Tf (Secret) Tj ET")),
            null);
        await Assert.That(PdfDocumentText.GetTextPage(document, 0, new(true, false)).Text).IsEqualTo(VisibleText);
    }

    /// <summary>Replacement text cannot inject formatting controls into a filtered page.</summary>
    /// <param name="control">A Unicode formatting control as UTF-16BE hex.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("202E")]
    [Arguments("202D")]
    [Arguments("202A")]
    [Arguments("202B")]
    [Arguments("202C")]
    [Arguments("2066")]
    [Arguments("2067")]
    [Arguments("2068")]
    [Arguments("2069")]
    [Arguments("200B")]
    [Arguments("200C")]
    [Arguments("200D")]
    [Arguments("200E")]
    [Arguments("200F")]
    [Arguments("061C")]
    [Arguments("2060")]
    [Arguments("FEFF")]
    [Arguments("00AD")]
    [Arguments("DB40DC01")]
    public async Task StripFormattingControls_RemovesActualTextControlsBeforeReordering(string control)
    {
        using var document = Open($"/Span << /ActualText <FEFF0041{control}0042> >> BDC BT /F1 10 Tf (X) Tj ET EMC");
        var text = PdfDocumentText.GetTextPage(document, 0, new(false, true));
        await Assert.That(text.Text).IsEqualTo("AB");
        await Assert.That(text.CharCount).IsEqualTo("AB".Length);
    }

    /// <summary>The ToUnicode mapping cannot inject a bidirectional override through glyph text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StripFormattingControls_RemovesGlyphUnicodeControls()
    {
        using var document = PdfDocumentReader.Open(
            MiniPdf.Build(
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
                MiniPdf.Stream(string.Empty, "BT /F1 10 Tf (X) Tj ET"),
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /ToUnicode 6 0 R >>",
                MiniPdf.Stream(string.Empty, "begincmap 1 begincodespacerange <00> <FF> endcodespacerange 1 beginbfchar <58> <0041202E200B0042> endbfchar endcmap")),
            null);
        await Assert.That(PdfDocumentText.GetTextPage(document, 0, new(false, true)).Text).IsEqualTo("AB");
    }

    /// <summary>Formatting controls are preserved unless stripping is explicitly requested.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StripFormattingControls_DefaultPreservesJoiners()
    {
        using var document = Open("/Span << /ActualText <FEFF0041200D0042> >> BDC BT /F1 10 Tf (X) Tj ET EMC");
        await Assert.That(PdfDocumentText.GetTextPage(document, 0).Text).IsEqualTo("A\u200DB");
    }

    /// <summary>The async filtered path returns the same visibility-filtered text.</summary>
    /// <param name="cancellationToken">The test token.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task GetTextPageAsync_AppliesOptions(CancellationToken cancellationToken)
    {
        using var document = Open("BT /F1 10 Tf 3 Tr (Secret) Tj ET");
        var text = await PdfDocumentText.GetTextPageAsync(document, 0, new(true, false), cancellationToken);
        await Assert.That(text.Text).IsEqualTo(string.Empty);
    }

    /// <summary>Filtering still honours cancellation.</summary>
    /// <param name="cancellationToken">The test token.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task GetTextPageAsync_CancelledTokenStopsExtraction(CancellationToken cancellationToken)
    {
        using var document = Open("BT /F1 10 Tf (Visible) Tj ET");
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        await cancelled.CancelAsync();
        await Assert.That(async () => await PdfDocumentText.GetTextPageAsync(document, 0, new(true, false), cancelled.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>Creates synthetic text and independently configurable paint opacity.</summary>
    /// <param name="content">The content operators.</param>
    /// <returns>The open document.</returns>
    private static PdfDocument Open(string content)
    {
        var pdf = TextTestDocument.Create(content);
        pdf.Resources += " /ExtGState << /Zero << /ca 0 /CA 0 >> /FillZero << /ca 0 /CA 1 >> /StrokeZero << /ca 1 /CA 0 >> >>";
        return TextTestDocument.Open(pdf.ToBytes());
    }
}
