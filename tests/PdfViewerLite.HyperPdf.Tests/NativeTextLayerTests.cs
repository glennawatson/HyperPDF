// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Ocr;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>Words written into an OCR text layer remain searchable within their boxes after saving.</summary>
public sealed class NativeTextLayerTests
{
    /// <summary>How far, in points, a character box may sit outside its word's box.</summary>
    private const float BoundsTolerance = 2;

    /// <summary>The left edge of the words.</summary>
    private const float Column = 40;

    /// <summary>The width of a word's box.</summary>
    private const float WordWidth = 80;

    /// <summary>The height of a word's box.</summary>
    private const float WordHeight = 20;

    /// <summary>The top of the first row.</summary>
    private const float FirstRow = 30;

    /// <summary>The top of the second row.</summary>
    private const float SecondRow = 70;

    /// <summary>The top of the third row.</summary>
    private const float ThirdRow = 110;

    /// <summary>The recogniser's confidence in every test word.</summary>
    private const float Confidence = 90;

    /// <summary>The width of the blank test page.</summary>
    private const int PageWidth = 300;

    /// <summary>The height of the blank test page.</summary>
    private const int PageHeight = 200;

    /// <summary>The first Latin test word.</summary>
    private const string Hello = "Hello";

    /// <summary>The number of Latin test words.</summary>
    private const int LatinWords = 3;

    /// <summary>The number of non-Latin test words.</summary>
    private const int OtherWords = 2;

    /// <summary>The match count expected for each Latin word.</summary>
    private static readonly int[] OnceEach = [1, 1, 1];

    /// <summary>The Latin words: plain ASCII and a Windows Latin accent.</summary>
    private static readonly OcrWord[] Latin =
    [
        new(Hello, new(Column, FirstRow, WordWidth, WordHeight), Confidence),
        new("World", new(Column, SecondRow, WordWidth, WordHeight), Confidence),
        new("café", new(Column, ThirdRow, WordWidth, WordHeight), Confidence),
    ];

    /// <summary>Words only an embedded font can show: Greek and Cyrillic, which the test font covers.</summary>
    private static readonly OcrWord[] Other =
    [
        new("αβ", new(Column, FirstRow, WordWidth, WordHeight), Confidence),
        new("Ж", new(Column, SecondRow, WordWidth, WordHeight), Confidence),
    ];

    /// <summary>Words written natively are found, copy as written, and their characters sit inside their boxes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LatinWordsAreSearchableAndInTheirBoxes()
    {
        using var test = Open(Blank(string.Empty));
        var written = HyperPdfAnnotationTextLayer.AddTextLayer(test.Editor, 0, Latin);
        var unsaved = HyperPdfAnnotationReading.GetHasUnsavedChanges(test.Editor);
        var saved = HyperPdfTestDocument.Save(test.Editor);
        var found = new List<int>();
        var overshoot = 0F;
        using var reopenedFile = new HyperPdfTestDocument(saved);
        var reopened = reopenedFile.Document;
        foreach (var word in Latin)
        {
            var matches = new List<TextMatch>();
            reopened.Find(0, word.Text, default, matches);
            found.Add(matches.Count);
            overshoot = Math.Max(overshoot, Overshoot(reopened, matches[0], word.Bounds));
        }

        var text = reopened.GetText(0, 0, reopened.GetCharacterCount(0));
        await Assert.That(written).IsEqualTo(LatinWords);
        await Assert.That(unsaved).IsTrue();
        await Assert.That(found).IsEquivalentTo(OnceEach);
        await Assert.That(text).Contains(Hello);
        await Assert.That(text).Contains("café");
        await Assert.That(overshoot).IsLessThanOrEqualTo(BoundsTolerance);
    }

    /// <summary>Non-Latin words use an embedded font with a text map, so search and copy return the right Unicode.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NonLatinWordsUseAnEmbeddedFont()
    {
        using var test = Open(Blank(string.Empty));
        var written = HyperPdfAnnotationTextLayer.AddTextLayer(test.Editor, 0, Other);
        var saved = HyperPdfTestDocument.Save(test.Editor);
        using var reopenedFile = new HyperPdfTestDocument(saved);
        var reopened = reopenedFile.Document;
        using var managed = PdfDocumentReader.Open(saved, null);
        var greek = new List<TextMatch>();
        var cyrillic = new List<TextMatch>();
        reopened.Find(0, "αβ", default, greek);
        reopened.Find(0, "Ж", default, cyrillic);
        var fonts = PdfDocumentPages.GetPage(managed, 0).Dictionary.GetDictionary(KnownName.Resources)!.GetDictionary(KnownName.Font)!;
        var font = fonts.GetDictionary(fonts.GetKeyAt(0))!;

        await Assert.That(written).IsEqualTo(OtherWords);
        await Assert.That(greek.Count).IsEqualTo(1);
        await Assert.That(cyrillic.Count).IsEqualTo(1);
        await Assert.That(Overshoot(reopened, greek[0], Other[0].Bounds)).IsLessThanOrEqualTo(BoundsTolerance);
        await Assert.That(Overshoot(reopened, cyrillic[0], Other[1].Bounds)).IsLessThanOrEqualTo(BoundsTolerance);
        await Assert.That(font.IsName(KnownName.Subtype, KnownName.Type0)).IsTrue();
        await Assert.That(font.ContainsKey(KnownName.ToUnicode)).IsTrue();
    }

    /// <summary>On a rotated page the characters still sit inside the boxes the viewer shows.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RotatedPagesKeepWordsInTheirBoxes()
    {
        using var test = Open(Blank("/Rotate 90"));
        _ = HyperPdfAnnotationTextLayer.AddTextLayer(test.Editor, 0, Latin);
        var saved = HyperPdfTestDocument.Save(test.Editor);
        using var reopenedFile = new HyperPdfTestDocument(saved);
        var reopened = reopenedFile.Document;
        var matches = new List<TextMatch>();
        reopened.Find(0, Hello, default, matches);

        await Assert.That(matches.Count).IsEqualTo(1);
        await Assert.That(Overshoot(reopened, matches[0], Latin[0].Bounds)).IsLessThanOrEqualTo(BoundsTolerance);
    }

    /// <summary>Existing page text and content survive, and an empty list or a bad page changes nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsExistingTextAndIgnoresNothingToWrite()
    {
        using var test = new HyperPdfTestDocument(1);
        var writer = test.Editor;
        var none = HyperPdfAnnotationTextLayer.AddTextLayer(writer, 0, []);
        var badPage = HyperPdfAnnotationTextLayer.AddTextLayer(writer, 1, Latin);
        var unchanged = HyperPdfAnnotationReading.GetHasUnsavedChanges(test.Editor);
        _ = HyperPdfAnnotationTextLayer.AddTextLayer(writer, 0, Latin);
        var saved = HyperPdfTestDocument.Save(test.Editor);
        using var reopenedFile = new HyperPdfTestDocument(saved);
        var reopened = reopenedFile.Document;
        var existing = new List<TextMatch>();
        var layer = new List<TextMatch>();
        reopened.Find(0, "quick", default, existing);
        reopened.Find(0, Hello, default, layer);

        await Assert.That(none).IsEqualTo(0);
        await Assert.That(badPage).IsEqualTo(0);
        await Assert.That(unchanged).IsFalse();
        await Assert.That(existing.Count).IsEqualTo(1);
        await Assert.That(layer.Count).IsEqualTo(1);
    }

    /// <summary>Opens bytes with HyperPDF for native editing, with the test fonts installed.</summary>
    /// <param name="bytes">The PDF.</param>
    /// <returns>The document.</returns>
    private static HyperPdfTestDocument Open(byte[] bytes)
    {
        var test = new HyperPdfTestDocument(bytes);
        HyperPdfAnnotationReading.SetFontCatalog(test.Editor, TestFont.Catalog);
        return test;
    }

    /// <summary>Builds a one page document with no content, like a scan before recognition.</summary>
    /// <param name="pageEntries">Extra page dictionary entries.</param>
    /// <returns>The file.</returns>
    private static byte[] Blank(string pageEntries) => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        string.Create(CultureInfo.InvariantCulture, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth} {PageHeight}] /Resources << >> {pageEntries} >>"));

    /// <summary>Gets how far a found word sticks out of the box it was written for.</summary>
    /// <param name="document">The document.</param>
    /// <param name="match">The match.</param>
    /// <param name="box">The word's box.</param>
    /// <returns>The largest overshoot on any side, in points; 0 when inside.</returns>
    private static float Overshoot(HyperPdfDocument document, TextMatch match, PageRect box)
    {
        var rectangles = new List<PageRect>();
        document.GetTextBounds(0, match.Start, match.Length, rectangles);
        var worst = 0F;
        foreach (var rectangle in rectangles)
        {
            worst = Math.Max(worst, Math.Max(box.Left - rectangle.Left, Math.Max(rectangle.Right - box.Right, Math.Max(box.Top - rectangle.Top, rectangle.Bottom - box.Bottom))));
        }

        return worst;
    }
}
