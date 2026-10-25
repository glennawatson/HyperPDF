// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.PageObjects;
using HyperPdfLibrary.Redaction;
using HyperPdfLibrary.Tests.PageObjects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Redaction;

/// <summary>Redacting text: the glyphs are removed from the content, the rest stays where it was, and the bytes are gone from the file.</summary>
public sealed class RedactionTextTests
{
    /// <summary>The tolerance for positions in points.</summary>
    private const float Tolerance = 0.05F;

    /// <summary>The glyph count of "SECRET-" before "ALPHA".</summary>
    private const int SecretPrefix = 7;

    /// <summary>Text under the area cannot be found by extraction or in the saved file, compressed or not.</summary>
    /// <param name="compressed">Whether the content stream is compressed.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RedactedTextIsGoneFromTheFile(bool compressed)
    {
        var pdf = PageObjectSamples.Page(RedactionSamples.ThreeLines);
        pdf = compressed ? RedactionSamples.Compress(pdf) : pdf;
        var saved = RedactionSamples.Redact(pdf, PdfRedactionOptions.Default, RedactionSamples.SecretLine);
        var text = RedactionSamples.TextOf(saved);
        await Assert.That(RedactionSamples.Contains(pdf, RedactionSamples.Secret)).IsTrue();
        await Assert.That(RedactionSamples.Contains(saved, RedactionSamples.Secret)).IsFalse();
        await Assert.That(RedactionSamples.Contains(saved, RedactionSamples.SecretWord)).IsFalse();
        await Assert.That(text).DoesNotContain(RedactionSamples.SecretWord);
        await Assert.That(text).Contains(RedactionSamples.Public);
        await Assert.That(text).Contains("More public");
    }

    /// <summary>Hex strings are redacted too.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HexStringsAreRedacted()
    {
        var pdf = PageObjectSamples.Page("BT /F1 20 Tf 20 120 Td <5345435245542D414C504841> Tj ET");
        var saved = RedactionSamples.Redact(pdf, PdfRedactionOptions.Default, RedactionSamples.SecretLine);
        await Assert.That(RedactionSamples.TextOf(saved)).DoesNotContain(RedactionSamples.SecretWord);
        await Assert.That(RedactionSamples.Contains(saved, "5345435245542D414C504841")).IsFalse();
    }

    /// <summary>The replacement text a tagged sequence carries goes with the text it stands for.</summary>
    /// <param name="named">Whether the property list is a resource rather than written inline.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ActualTextOfRedactedTextIsRemoved(bool named)
    {
        const string inline = $"/Span << /MCID 4 /ActualText ({RedactionSamples.Secret}) >> BDC BT /F1 20 Tf 20 120 Td ({RedactionSamples.Secret}) Tj ET EMC";
        const string reference = $"/Span /MC0 BDC BT /F1 20 Tf 20 120 Td ({RedactionSamples.Secret}) Tj ET EMC";
        const string properties = $"/Properties << /MC0 << /MCID 4 /ActualText ({RedactionSamples.Secret}) >> >>";
        var pdf = named ? PageObjectSamples.Page(reference, properties) : PageObjectSamples.Page(inline);
        var saved = RedactionSamples.Redact(pdf, PdfRedactionOptions.Default, RedactionSamples.SecretLine);
        using var document = PdfDocumentReader.Open(saved, null);
        await Assert.That(RedactionSamples.Contains(pdf, "ActualText")).IsTrue();
        await Assert.That(RedactionSamples.Contains(saved, RedactionSamples.Secret)).IsFalse();
        await Assert.That(RedactionSamples.Contains(saved, "ActualText")).IsFalse();
        await Assert.That(RedactionSamples.Contains(saved, "/MCID 4")).IsTrue();
    }

    /// <summary>Glyphs the area does not reach stay, at the same positions.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OnlyGlyphsUnderTheAreaGo()
    {
        var pdf = PageObjectSamples.Page(RedactionSamples.ThreeLines);
        float[] before;
        using (var original = PdfDocumentReader.Open(pdf, null))
        {
            var text = (PdfTextObject)PdfDocumentPageContent.GetPageContent(original, 0).Objects[1];
            before = [text.Glyphs[0].Origin.X, text.Glyphs[SecretPrefix - 1].Origin.X];
        }

        var saved = RedactionSamples.Redact(pdf, PdfRedactionOptions.Default, RedactionSamples.AlphaArea);
        using var after = PdfDocumentReader.Open(saved, null);
        var kept = (PdfTextObject)PdfDocumentPageContent.GetPageContent(after, 0).Objects[1];
        await Assert.That(kept.Text).StartsWith("SECRET-");
        await Assert.That(kept.Text).DoesNotContain("ALPHA");
        await Assert.That(kept.Glyphs[0].Origin.X).IsEqualTo(before[0]).Within(Tolerance);
        await Assert.That(kept.Glyphs[SecretPrefix - 1].Origin.X).IsEqualTo(before[1]).Within(Tolerance);
    }

    /// <summary>The text after a redacted line keeps its place.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextAfterTheAreaDoesNotMove()
    {
        var pdf = PageObjectSamples.Page("BT /F1 20 Tf 20 120 Td (SECRET-ALPHA public tail) Tj ET");
        PdfRectangle tailBefore;
        using (var original = PdfDocumentReader.Open(pdf, null))
        {
            var text = (PdfTextObject)PdfDocumentPageContent.GetPageContent(original, 0).Objects[0];
            tailBefore = text.Glyphs[^1].Box;
        }

        var saved = RedactionSamples.Redact(pdf, PdfRedactionOptions.Default, RedactionSamples.PrefixArea);
        using var after = PdfDocumentReader.Open(saved, null);
        var kept = (PdfTextObject)PdfDocumentPageContent.GetPageContent(after, 0).Objects[0];
        await Assert.That(kept.Glyphs[^1].Box.Left).IsEqualTo(tailBefore.Left).Within(Tolerance);
        await Assert.That(kept.Text).EndsWith("tail");
    }

    /// <summary>Invisible text under the area goes when asked and stays when not.</summary>
    /// <param name="remove">Whether invisible text is removed.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task InvisibleTextFollowsTheOption(bool remove)
    {
        var pdf = PageObjectSamples.Page("BT /F1 20 Tf 3 Tr 20 120 Td (SECRET-ALPHA) Tj ET");
        var options = PdfRedactionOptions.Default with
        {
            RemoveInvisibleText = remove
        };
        var saved = RedactionSamples.Redact(pdf, options, RedactionSamples.SecretLine);
        await Assert.That(RedactionSamples.TextOf(saved).Contains(RedactionSamples.SecretWord, StringComparison.Ordinal)).IsEqualTo(!remove);
    }

    /// <summary>Text inside a form XObject is redacted in a copy, and the shared original is unchanged.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextInsideFormsIsRedacted()
    {
        var pdf = new HyperPdfLibrary.Tests.Rendering.RenderTestPdf(PageObjectSamples.Size, PageObjectSamples.Size);
        var font = pdf.AddObject("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        var form = pdf.AddStream($"/Type /XObject /Subtype /Form /BBox [0 0 200 200] /Resources << /Font << /F1 {font} 0 R >> >>", "BT /F1 20 Tf 20 120 Td (SECRET-ALPHA) Tj ET");
        pdf.Resources = $"/XObject << /Fm1 {form} 0 R >>";
        pdf.Content = "/Fm1 Do";
        var saved = RedactionSamples.Redact(pdf.ToBytes(), PdfRedactionOptions.Default, RedactionSamples.SecretLine);
        await Assert.That(RedactionSamples.Contains(saved, RedactionSamples.Secret)).IsFalse();
        await Assert.That(RedactionSamples.TextOf(saved)).DoesNotContain(RedactionSamples.SecretWord);
    }

    /// <summary>The report counts what was removed, and nothing changes when there is nothing marked.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReportCountsRemovedGlyphs()
    {
        using var document = PdfDocumentReader.Open(PageObjectSamples.Page(RedactionSamples.ThreeLines), null);
        var empty = PdfRedactor.Apply(document, PdfRedactionOptions.Default);
        RedactionSamples.Mark(document, RedactionSamples.SecretLine);
        var report = PdfRedactor.Apply(document, PdfRedactionOptions.Default);
        await Assert.That(empty.IsEmpty).IsTrue();
        await Assert.That(report.GlyphsRemoved).IsEqualTo(RedactionSamples.Secret.Length);
        await Assert.That(report.Pages).IsEqualTo(1);
        await Assert.That(report.Regions).IsEqualTo(1);
    }

    /// <summary>Once redactions are applied the document refuses an incremental save, which would keep the old bytes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IncrementalSaveIsRefusedAfterApplying()
    {
        using var document = PdfDocumentReader.Open(PageObjectSamples.Page(RedactionSamples.ThreeLines), null);
        RedactionSamples.Mark(document, RedactionSamples.SecretLine);
        _ = PdfRedactor.Apply(document, PdfRedactionOptions.Default);
        await Assert.That(StoreRedaction.RequiresCompactSave(document.Objects)).IsTrue();
        await Assert.That(() => PdfIncrementalWriter.Save(document.Objects)).Throws<InvalidOperationException>();
        await Assert.That(RedactionSamples.Contains(RedactionSamples.Save(document), RedactionSamples.Secret)).IsFalse();
    }

    /// <summary>A cancelled token stops the apply and leaves the document as it was.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancellationLeavesTheDocumentUnchanged()
    {
        using var document = PdfDocumentReader.Open(PageObjectSamples.Page(RedactionSamples.ThreeLines), null);
        RedactionSamples.Mark(document, RedactionSamples.SecretLine);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        await Assert.That(() => PdfRedactor.Apply(document, PdfRedactionOptions.Default, source.Token)).Throws<OperationCanceledException>();
        await Assert.That(StoreRedaction.RequiresCompactSave(document.Objects)).IsFalse();
        await Assert.That(PdfDocumentText.GetTextPage(document, 0).Text).Contains(RedactionSamples.SecretWord);
    }

    /// <summary>The async save writes the same file as the sync one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AsyncSaveRemovesTheText()
    {
        using var document = PdfDocumentReader.Open(PageObjectSamples.Page(RedactionSamples.ThreeLines), null);
        RedactionSamples.Mark(document, RedactionSamples.SecretLine);
        await using var output = new MemoryStream();
        var report = await PdfRedactor.ApplyAndSaveAsync(document, output, PdfRedactionOptions.Default, CancellationToken.None);
        await Assert.That(report.GlyphsRemoved).IsGreaterThan(0);
        await Assert.That(RedactionSamples.Contains(output.ToArray(), RedactionSamples.Secret)).IsFalse();
    }
}
