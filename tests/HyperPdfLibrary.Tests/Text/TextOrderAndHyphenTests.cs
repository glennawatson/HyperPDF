// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.Tests.Text;

/// <summary>Checks the extraction options that choose what a hyphen at a line end does and which order the page is read in.</summary>
public sealed class TextOrderAndHyphenTests
{
    /// <summary>The first word, a hyphen, and the second word on the next line.</summary>
    private const string SplitWord = "BT /F1 10 Tf 10 100 Td (well-) Tj 0 -20 Td (known) Tj ET";

    /// <summary>
    /// Two columns drawn one after the other: the left column's two lines, then the right column's two lines. Content order
    /// reads each line on its own; layout order reads across.
    /// </summary>
    private const string Columns = "BT /F1 10 Tf 10 100 Td (L1) Tj 0 -20 Td (L2) Tj ET BT /F1 10 Tf 150 100 Td (R1) Tj 0 -20 Td (R2) Tj ET";

    /// <summary>The same word drawn twice a little apart, as fake bold does.</summary>
    private const string DoubleStruck = "BT /F1 10 Tf 10 100 Td (bold) Tj ET BT /F1 10 Tf 10.3 100 Td (bold) Tj ET";

    /// <summary>Three Hebrew letters drawn left to right: alef, bet, gimel.</summary>
    private const string Hebrew = "BT /F1 10 Tf 10 100 Td (\\240\\241\\242) Tj ET";

    /// <summary>Verifies that a split word is joined by default, and that keeping the hyphen leaves it and the line break.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Hyphen_IsJoinedByDefaultAndKeptOnRequest()
    {
        var joined = Extract(SplitWord, new(false, false, PdfTextHyphenMode.Join, PdfTextOrder.Content));
        var kept = Extract(SplitWord, new(false, false, PdfTextHyphenMode.Keep, PdfTextOrder.Content));

        await Assert.That(joined.Text).IsEqualTo("wellknown");
        await Assert.That(kept.Text).IsEqualTo("well-\r\nknown");
    }

    /// <summary>Verifies that layout order reads across the columns, where content order reads the lines as they were drawn.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LayoutOrder_ReadsAcrossTheColumns()
    {
        var content = Extract(Columns, new(false, false, PdfTextHyphenMode.Join, PdfTextOrder.Content));
        var layout = Extract(Columns, new(false, false, PdfTextHyphenMode.Join, PdfTextOrder.Layout));

        await Assert.That(content.Text).IsEqualTo("L1\r\nL2\r\nR1\r\nR2");
        await Assert.That(layout.Text).IsEqualTo("L1 R1\r\nL2 R2");
    }

    /// <summary>Verifies that layout order leaves a page with rotated text in the order it is drawn.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LayoutOrder_KeepsContentOrderForRotatedText()
    {
        const string rotated = "BT /F1 10 Tf 10 100 Td (b) Tj ET BT /F1 10 Tf 0 1 -1 0 150 50 Tm (a) Tj ET BT /F1 10 Tf 10 120 Td (c) Tj ET";
        var content = Extract(rotated, new(false, false, PdfTextHyphenMode.Join, PdfTextOrder.Content));
        var layout = Extract(rotated, new(false, false, PdfTextHyphenMode.Join, PdfTextOrder.Layout));

        await Assert.That(layout.Text).IsEqualTo(content.Text);
    }

    /// <summary>Verifies that two runs along the same diagonal baseline are one line, and a run off that baseline starts another.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DiagonalRuns_AlongOneBaselineAreOneLine()
    {
        const string diagonal =
            "BT /F1 10 Tf 0.7071 0.7071 -0.7071 0.7071 50 50 Tm (ab) Tj ET "
            + "BT /F1 10 Tf 0.7071 0.7071 -0.7071 0.7071 64.14 64.14 Tm (cd) Tj ET "
            + "BT /F1 10 Tf 0.7071 0.7071 -0.7071 0.7071 40 60 Tm (ef) Tj ET";
        var page = Extract(diagonal, new(false, false, PdfTextHyphenMode.Join, PdfTextOrder.Content));

        await Assert.That(page.Text).IsEqualTo("ab cd\r\nef");
    }

    /// <summary>Verifies that raw order keeps a word drawn twice, which the other orders keep once.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RawOrder_KeepsAWordDrawnTwice()
    {
        var content = Extract(DoubleStruck, new(false, false, PdfTextHyphenMode.Join, PdfTextOrder.Content));
        var raw = Extract(DoubleStruck, new(false, false, PdfTextHyphenMode.Join, PdfTextOrder.Raw));

        await Assert.That(content.Text).IsEqualTo("bold");
        await Assert.That(raw.Text.Replace(" ", string.Empty, StringComparison.Ordinal)).IsEqualTo("boldbold");
    }

    /// <summary>Verifies that raw order leaves right-to-left text in the order it is drawn, and the other orders put it in logical order.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RawOrder_LeavesRightToLeftTextInDrawingOrder()
    {
        var content = Extract(Hebrew, new(false, false, PdfTextHyphenMode.Join, PdfTextOrder.Content));
        var raw = Extract(Hebrew, new(false, false, PdfTextHyphenMode.Join, PdfTextOrder.Raw));

        await Assert.That(content.Text).IsEqualTo("\U000005D2\U000005D1\U000005D0");
        await Assert.That(raw.Text).IsEqualTo("\U000005D0\U000005D1\U000005D2");
    }

    /// <summary>Verifies that the old two-argument options keep their defaults.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TwoArgumentOptions_UseContentOrderAndJoinedHyphens()
    {
        var options = new PdfTextExtractionOptions(true, false);

        await Assert.That(options.HyphenMode).IsEqualTo(PdfTextHyphenMode.Join);
        await Assert.That(options.Order).IsEqualTo(PdfTextOrder.Content);
    }

    /// <summary>Extracts a page with options.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="options">The options.</param>
    /// <returns>The text page.</returns>
    private static PdfTextPage Extract(string content, PdfTextExtractionOptions options) =>
        PdfDocumentText.GetTextPage(TextTestDocument.Open(TextTestDocument.Create(content).ToBytes()), 0, options);
}
