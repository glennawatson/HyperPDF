// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Redaction;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Tests.PageObjects;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdfLibrary.Tests.Redaction;

/// <summary>Annotations, overlays, clean-up and marking.</summary>
public sealed class RedactionApplyTests
{
    /// <summary>The largest channel difference accepted.</summary>
    private const int Tolerance = 3;

    /// <summary>The column of the secret line's left end.</summary>
    private const int LineColumn = 25;

    /// <summary>The row, counted from the top, in the middle of the secret line's area.</summary>
    private const int LineRow = 73;

    /// <summary>A column inside the secret line's area, away from the overlay text.</summary>
    private const int OverlayColumn = 20;

    /// <summary>A row, counted from the top, inside the secret line's area and above the overlay text.</summary>
    private const int OverlayRow = 64;

    /// <summary>The width of one letter at 20 points, plus a little.</summary>
    private const float OneLetter = 14;

    /// <summary>How many times the image marker text repeats to fill 10 by 10 RGB pixels.</summary>
    private const int MarkerRepeats = 25;

    /// <summary>The areas of the multi-area mark.</summary>
    private const int TwoAreas = 2;

    /// <summary>The text drawn over a redacted area.</summary>
    private const string Overlay = "REDACTED";

    /// <summary>The red fill colour.</summary>
    private const uint Red = 0xFF0000;

    /// <summary>The link over the secret line and the one clear of it, then a note over the secret.</summary>
    private const string Annotations =
        "/Annots [ << /Type /Annot /Subtype /Link /Rect [20 110 180 140] /A << /S /URI /URI (http://secret.example) >> >> "
        + "<< /Type /Annot /Subtype /Link /Rect [20 10 80 40] /A << /S /URI /URI (http://public.example) >> >> "
        + "<< /Type /Annot /Subtype /Text /Rect [150 115 170 135] /Contents (NoteOverSecret) >> ]";

    /// <summary>A CMap that maps three codes.</summary>
    private const string Cmap =
        "/CIDInit /ProcSet findresource begin 12 dict begin begincmap 1 begincodespacerange <00> <FF> endcodespacerange "
        + "3 beginbfchar <53> <0053> <45> <0045> <5A> <005A> endbfchar endcmap end end";

    /// <summary>Links and annotations under the area go, with their actions; the rest stay, and so does nothing of the redact annotation.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnnotationsUnderTheAreaAreRemoved()
    {
        var pdf = Page(RedactionSamples.ThreeLines, Annotations);
        var saved = RedactionSamples.Redact(pdf, PdfRedactionOptions.Default, RedactionSamples.SecretLine);
        using var document = PdfDocumentReader.Open(saved, null);
        var annotations = PdfPageAnnotations.GetArray(document.Objects, PdfDocumentPages.GetPage(document, 0))!;

        await Assert.That(annotations.Count).IsEqualTo(1);
        await Assert.That(RedactionSamples.Contains(saved, "secret.example")).IsFalse();
        await Assert.That(RedactionSamples.Contains(saved, "NoteOverSecret")).IsFalse();
        await Assert.That(RedactionSamples.Contains(saved, "public.example")).IsTrue();
    }

    /// <summary>The annotation mode chooses what is removed.</summary>
    /// <param name="mode">The mode.</param>
    /// <param name="expected">The annotations left.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfRedactionAnnotationMode.Keep, 3)]
    [Arguments(PdfRedactionAnnotationMode.RemoveLinks, 2)]
    [Arguments(PdfRedactionAnnotationMode.RemoveTouched, 1)]
    public async Task AnnotationModeChoosesWhatGoes(PdfRedactionAnnotationMode mode, int expected)
    {
        var pdf = Page(RedactionSamples.ThreeLines, Annotations);
        var saved = RedactionSamples.Redact(pdf, PdfRedactionOptions.Default with { Annotations = mode }, RedactionSamples.SecretLine);
        using var document = PdfDocumentReader.Open(saved, null);

        await Assert.That(PdfPageAnnotations.GetArray(document.Objects, PdfDocumentPages.GetPage(document, 0))!.Count).IsEqualTo(expected);
    }

    /// <summary>The area is painted with the fill colour, and the overlay text is drawn and can be read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OverlayPaintsTheFillAndText()
    {
        using var document = PdfDocumentReader.Open(PageObjectSamples.Page(RedactionSamples.ThreeLines), null);
        var appearance = new PdfRedactionAppearance(Red, Overlay, false, 0, 0xFFFFFFU, PdfRedactionAlignment.Left);
        _ = PdfRedactions.Add(document, 0, [RedactionSamples.SecretLine], appearance);
        await using var output = new MemoryStream();
        _ = PdfRedactor.ApplyAndSave(document, output, PdfRedactionOptions.Default);
        var saved = output.ToArray();
        var image = PageObjectSamples.Render(saved);

        await Assert.That(RedactionSamples.TextOf(saved)).Contains(Overlay);
        await Assert.That(RedactionSamples.TextOf(saved)).DoesNotContain(RedactionSamples.SecretWord);
        await Assert.That(image.IsNear(OverlayColumn, OverlayRow, Rgb.Red255, Tolerance)).IsTrue();
    }

    /// <summary>A black bar with no overlay text is painted, and no redact annotation is left.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BarIsPaintedAndMarksAreGone()
    {
        var saved = RedactionSamples.Redact(PageObjectSamples.Page(RedactionSamples.ThreeLines), PdfRedactionOptions.Default, RedactionSamples.SecretLine);
        using var document = PdfDocumentReader.Open(saved, null);
        var marks = new List<PdfRedaction>();
        PdfRedactions.GetAll(document, marks);

        await Assert.That(PageObjectSamples.Render(saved).IsNear(LineColumn, LineRow, Rgb.Black, Tolerance)).IsTrue();
        await Assert.That(marks.Count).IsEqualTo(0);
        await Assert.That(RedactionSamples.Contains(saved, "/Redact")).IsFalse();
    }

    /// <summary>Resources the changed page no longer uses are dropped, with the thumbnail.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnusedResourcesAndThumbnailAreDropped()
    {
        var pdf = new RenderTestPdf(PageObjectSamples.Size, PageObjectSamples.Size);
        var font = pdf.AddObject("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        var marker = string.Concat(Enumerable.Repeat("IMAGEMARKER-", MarkerRepeats));
        var picture = pdf.AddStream("/Type /XObject /Subtype /Image /Width 10 /Height 10 /ColorSpace /DeviceRGB /BitsPerComponent 8", marker);
        var thumb = pdf.AddStream("/Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8", "THUMBDATA");
        pdf.Resources = $"/Font << /F1 {font} 0 R >> /XObject << /Im1 {picture} 0 R >>";
        pdf.PageEntries = $"/Thumb {thumb} 0 R";
        pdf.Content = "BT /F1 20 Tf 20 150 Td (Public text) Tj ET q 30 0 0 30 20 108 cm /Im1 Do Q";
        using var document = PdfDocumentReader.Open(pdf.ToBytes(), null);
        RedactionSamples.Mark(document, RedactionSamples.SecretLine);
        await using var output = new MemoryStream();
        var options = PdfRedactionOptions.Default with { Images = PdfRedactionImageMode.Remove };
        var report = PdfRedactor.ApplyAndSave(document, output, options);
        using var saved = PdfDocumentReader.Open(output.ToArray(), null);

        await Assert.That(report.ResourcesRemoved).IsEqualTo(1);
        await Assert.That(PdfDocumentPages.GetPage(saved, 0).Resources!.GetDictionary(KnownName.XObject)!.Count).IsEqualTo(0);
        await Assert.That(RedactionSamples.Contains(output.ToArray(), "THUMBDATA")).IsFalse();
        await Assert.That(RedactionSamples.Contains(output.ToArray(), "IMAGEMARKER")).IsFalse();
    }

    /// <summary>The /ToUnicode map loses the entries of codes no text uses any more, and keeps the rest.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ToUnicodeLosesRemovedCodes()
    {
        var pdf = new RenderTestPdf(PageObjectSamples.Size, PageObjectSamples.Size);
        var map = pdf.AddStream(string.Empty, Cmap);
        var font = pdf.AddObject($"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /ToUnicode {map} 0 R >>");
        pdf.Resources = $"/Font << /F1 {font} 0 R >>";
        pdf.Content = "BT /F1 20 Tf 20 120 Td (SE) Tj 0 -60 Td (E) Tj ET";
        var area = new PdfRectangle(RedactionSamples.SecretLine.Left, RedactionSamples.SecretLine.Bottom, RedactionSamples.SecretLine.Left + OneLetter, RedactionSamples.SecretLine.Top);
        using var document = PdfDocumentReader.Open(pdf.ToBytes(), null);
        RedactionSamples.Mark(document, area);
        await using var output = new MemoryStream();
        var report = PdfRedactor.ApplyAndSave(document, output, PdfRedactionOptions.Default);
        using var saved = PdfDocumentReader.Open(output.ToArray(), null);
        var fontDictionary = PdfDocumentPages.GetPage(saved, 0).Resources!.GetDictionary(KnownName.Font)!.GetDictionary(saved.Objects.Names.Intern("F1"u8))!;
        var rewritten = Encoding.ASCII.GetString(fontDictionary.GetStream(KnownName.ToUnicode)!.DecodeToArray());

        await Assert.That(report.ToUnicodeEntriesRemoved).IsEqualTo(1);
        await Assert.That(rewritten).DoesNotContain("<53>");
        await Assert.That(rewritten).Contains("<45>");
        await Assert.That(rewritten).Contains("<5A>");
    }

    /// <summary>The document information and metadata are removed when asked.</summary>
    /// <param name="scrub">Whether to scrub.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task MetadataIsScrubbedWhenAsked(bool scrub)
    {
        using var document = PdfDocumentReader.Open(TestPdf.Create(1), null);
        RedactionSamples.Mark(document, RedactionSamples.SecretLine);
        await using var output = new MemoryStream();
        _ = PdfRedactor.ApplyAndSave(document, output, PdfRedactionOptions.Default with { ScrubMetadata = scrub });
        using var saved = PdfDocumentReader.Open(output.ToArray(), null);

        await Assert.That(string.IsNullOrEmpty(PdfDocumentMetadata.GetInfo(saved).Title)).IsEqualTo(scrub);
    }

    /// <summary>Marks are listed with their areas and look, and can be taken off again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MarksCanBeListedAndRemoved()
    {
        using var document = PdfDocumentReader.Open(PageObjectSamples.Page(RedactionSamples.ThreeLines), null);
        var appearance = new PdfRedactionAppearance(Red, Overlay, true, 0, null, PdfRedactionAlignment.Centre);
        var index = PdfRedactions.Add(document, 0, [RedactionSamples.SecretLine, RedactionSamples.AlphaArea], appearance);
        var marks = new List<PdfRedaction>();
        PdfRedactions.GetAll(document, marks);

        await Assert.That(marks.Count).IsEqualTo(1);
        await Assert.That(marks[0].AnnotationIndex).IsEqualTo(index);
        await Assert.That(marks[0].Regions.Length).IsEqualTo(TwoAreas);
        await Assert.That(marks[0].Appearance.FillColor).IsEqualTo(Red);
        await Assert.That(marks[0].Appearance.OverlayText).IsEqualTo(Overlay);
        await Assert.That(marks[0].Appearance.Repeat).IsTrue();
        await Assert.That(marks[0].Appearance.Alignment).IsEqualTo(PdfRedactionAlignment.Centre);
        await Assert.That(PdfRedactions.Remove(document, 0, index)).IsTrue();
        marks.Clear();
        PdfRedactions.GetAll(document, marks);
        await Assert.That(marks.Count).IsEqualTo(0);
    }

    /// <summary>Marked areas show on the page before they are applied.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MarksShowAsATint()
    {
        using var page = new RenderTestPage(PageObjectSamples.Page(RedactionSamples.ThreeLines));
        _ = PdfRedactions.Add(page.Document, 0, [RedactionSamples.SecretLine], PdfRedactionAppearance.Black);
        var image = page.RenderPage(1, 0, PdfRenderFlags.Annotations);

        await Assert.That(image.IsNear(OverlayColumn, OverlayRow, Rgb.White, Tolerance)).IsFalse();
    }

    /// <summary>Builds a page with content and annotations.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="annotations">The /Annots entry.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] Page(string content, string annotations)
    {
        var pdf = new RenderTestPdf(PageObjectSamples.Size, PageObjectSamples.Size) { Content = content, PageEntries = annotations };
        var font = pdf.AddObject("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        pdf.Resources = $"/Font << /F1 {font} 0 R >>";
        return pdf.ToBytes();
    }
}
