// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using HyperPdf.Engine.Tests.PdfiumReference;
using HyperPdfLibrary.Document;

namespace HyperPdf.Engine.Tests;

/// <summary>
/// Compares text extraction on a Devanagari reference PDF with PDFium. The file is a scan of a Hindi book with an invisible
/// text layer, so its text is whatever the recogniser wrote, and its lines overlap. The engines agree on most pages. The
/// differences recorded here are deliberate or unavoidable: HyperPDF drops a glyph drawn twice in the same place where
/// PDFium keeps both; each engine decides on its own where a gap between glyphs is a space; and where the boxes of an
/// overlapping line sort differently the words come in another order. The letters on the page are the same.
/// The file comes from <c>tests/reference-pdfs/manifest.json</c>; a missing file skips the test, or fails it when
/// <c>HYPERPDF_REQUIRE_REFERENCE_PDFS</c> is <c>1</c>.
/// </summary>
public sealed partial class IndicTextParityTests
{
    /// <summary>The manifest id of the Devanagari reference PDF.</summary>
    private const string IndicId = "commons-apna-aap";

    /// <summary>The fewest pages that read the same in both engines once white space is collapsed (measured 166 of 200).</summary>
    private const int MinimumSamePages = 160;

    /// <summary>The most any page's letters may differ, as a share of the page's letters (measured 1.05 percent on the worst page).</summary>
    private const double MaximumLetterDifference = 0.02;

    /// <summary>The fewest letters a page needs before its difference is judged as a share.</summary>
    private const int MinimumLetters = 1;

    /// <summary>Verifies that most pages read the same and that no page differs in more than its spacing, order and doubled glyphs.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DevanagariPdf_MostPagesMatchPdfiumAndTheRestHoldTheSameLetters()
    {
        using var pair = new EnginePair(ReferencePdfFile.Read(IndicId));
        var same = 0;
        var worst = 0D;
        for (var page = 0; page < pair.Pdfium.PageCount; page++)
        {
            var expected = PdfiumReferenceText.GetTextPage(pair.Pdfium, page).Text;
            var actual = PdfDocumentText.GetTextPage(pair.Library, page).Text;
            same += Collapse(expected) == Collapse(actual) ? 1 : 0;
            worst = Math.Max(worst, LetterDifference(expected, actual));
        }

        TestContext.Current?.Output.WriteLine($"{same} of {pair.Pdfium.PageCount} pages read the same; the largest letter difference is {worst:P2}");
        await Assert.That(same).IsGreaterThanOrEqualTo(MinimumSamePages);
        await Assert.That(worst).IsLessThanOrEqualTo(MaximumLetterDifference);
    }

    /// <summary>Matches a run of white space.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpace();

    /// <summary>Collapses runs of white space to one space and trims the ends.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The collapsed text.</returns>
    private static string Collapse(string text) => WhiteSpace().Replace(text, " ").Trim();

    /// <summary>Measures how many letters two texts do not share, as a share of the letters of the longer one. Order and spacing do not count.</summary>
    /// <param name="first">The first text.</param>
    /// <param name="second">The second text.</param>
    /// <returns>The difference, from 0 for the same letters to 1.</returns>
    private static double LetterDifference(string first, string second)
    {
        var counts = new Dictionary<char, int>();
        var total = 0;
        foreach (var value in first.Where(static c => !char.IsWhiteSpace(c)))
        {
            counts[value] = counts.GetValueOrDefault(value) + 1;
            total++;
        }

        var secondTotal = 0;
        var difference = 0;
        foreach (var value in second.Where(static c => !char.IsWhiteSpace(c)))
        {
            counts[value] = counts.GetValueOrDefault(value) - 1;
            secondTotal++;
        }

        foreach (var count in counts.Values)
        {
            difference += Math.Abs(count);
        }

        return (double)difference / Math.Max(Math.Max(total, secondTotal), MinimumLetters);
    }
}
