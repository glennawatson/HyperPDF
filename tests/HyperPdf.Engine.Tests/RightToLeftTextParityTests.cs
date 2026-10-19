// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Engine.Tests.PdfiumReference;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Text;

namespace HyperPdf.Engine.Tests;

/// <summary>
/// Compares text extraction and search on a right-to-left reference PDF with PDFium. The file comes from
/// <c>tests/reference-pdfs/manifest.json</c>; a missing file skips the test, or fails it when
/// <c>HYPERPDF_REQUIRE_REFERENCE_PDFS</c> is <c>1</c>. The page holds the Arabic word habibi twice with the Latin word
/// "habibi" between them. PDFium is clearly wrong on it: it gives the Arabic letters in visual order, so every Arabic word
/// reads backwards, and it adds five stray characters made from glyph names. HyperPDF gives logical order, as pdf.js does,
/// so the differences below are deliberate and recorded here.
/// </summary>
public sealed class RightToLeftTextParityTests
{
    /// <summary>The manifest id of the Arabic reference PDF.</summary>
    private const string ArabicId = "pypdf-arabic";

    /// <summary>The Latin word between the two Arabic words.</summary>
    private const string LatinWord = "habibi";

    /// <summary>The first character of the Arabic block.</summary>
    private const char ArabicStart = '\U00000600';

    /// <summary>The last character of the Arabic block.</summary>
    private const char ArabicEnd = '\U000006FF';

    /// <summary>The Arabic word, in logical order: hah, fatha, beh, yeh, beh, yeh.</summary>
    private const string ArabicWord = "\U0000062D\U0000064E\U00000628\U0000064A\U00000628\U0000064A";

    /// <summary>The Arabic word without its fatha.</summary>
    private const string ArabicWordWithoutMark = "\U0000062D\U00000628\U0000064A\U00000628\U0000064A";

    /// <summary>The number of times the Arabic word is on the page.</summary>
    private const int ArabicWordCount = 2;

    /// <summary>Verifies the page text: HyperPDF reads in logical order and has the same Latin text and Arabic letters as PDFium, whose Arabic is reversed.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ArabicPdf_TextIsInLogicalOrderWhereTextOfPdfiumIsReversed()
    {
        using var pair = new EnginePair(ReferencePdfFile.Read(ArabicId));
        var pdfium = PdfiumReferenceText.GetTextPage(pair.Pdfium, 0).Text;
        var hyperPdf = PdfDocumentText.GetTextPage(pair.Library, 0).Text;

        await Assert.That(hyperPdf).IsEqualTo($"{ArabicWord} {LatinWord} {ArabicWord} ");
        await Assert.That(pdfium).Contains(LatinWord);
        await Assert.That(ArabicLettersOf(hyperPdf)).IsEqualTo(Reverse(ArabicLettersOf(pdfium))).Because("PDFium gives the same Arabic letters in visual order");
    }

    /// <summary>Verifies the Latin word is found once by both engines, and that only HyperPDF finds the Arabic word, twice, because PDFium holds it reversed.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ArabicPdf_SearchFindsTheLogicalWordWherePdfiumCannot()
    {
        using var pair = new EnginePair(ReferencePdfFile.Read(ArabicId));
        var text = PdfDocumentText.GetTextPage(pair.Library, 0);

        await Assert.That(PdfiumReferenceText.Find(pair.Pdfium, 0, LatinWord, PdfiumSearchFlags.None).Count).IsEqualTo(1);
        await Assert.That(Count(text, LatinWord, PdfTextSearchOptions.None)).IsEqualTo(1);
        await Assert.That(PdfiumReferenceText.Find(pair.Pdfium, 0, ArabicWord, PdfiumSearchFlags.None).Count).IsEqualTo(0);
        await Assert.That(Count(text, ArabicWord, PdfTextSearchOptions.None)).IsEqualTo(ArabicWordCount);
    }

    /// <summary>Verifies that the Arabic word is found without its fatha when accents are ignored, and not otherwise.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ArabicPdf_WordWithoutItsMarkIsFoundWhenAccentsAreIgnored()
    {
        using var pair = new EnginePair(ReferencePdfFile.Read(ArabicId));
        var text = PdfDocumentText.GetTextPage(pair.Library, 0);

        await Assert.That(Count(text, ArabicWordWithoutMark, PdfTextSearchOptions.None)).IsEqualTo(0);
        await Assert.That(Count(text, ArabicWordWithoutMark, PdfTextSearchOptions.IgnoreDiacritics)).IsEqualTo(ArabicWordCount);
    }

    /// <summary>Keeps only the Arabic block characters of a text.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The Arabic characters, in the order they are in the text.</returns>
    private static string ArabicLettersOf(string text) => string.Concat(text.Where(static c => c is >= ArabicStart and <= ArabicEnd));

    /// <summary>Reverses a string.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text with its characters in the opposite order.</returns>
    private static string Reverse(string text) => string.Concat(text.Reverse());

    /// <summary>Counts the matches of a query.</summary>
    /// <param name="page">The page.</param>
    /// <param name="query">The query.</param>
    /// <param name="options">The options.</param>
    /// <returns>The number of matches.</returns>
    private static int Count(PdfTextPage page, string query, PdfTextSearchOptions options)
    {
        var matches = new List<PdfTextMatch>();
        page.Find(query, options, matches);
        return matches.Count;
    }
}
