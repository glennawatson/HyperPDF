// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Reading;

namespace HyperPdf.Core.Tests.Reading;

/// <summary>Tests for <see cref="ReadingOrder"/> on page layouts built character by character.</summary>
public sealed class ReadingOrderTests
{
    /// <summary>The left column's edge.</summary>
    private const float LeftColumn = 72;

    /// <summary>The right column's edge.</summary>
    private const float RightColumn = 320;

    /// <summary>The first body line's top.</summary>
    private const float FirstLine = 160;

    /// <summary>The distance between body lines.</summary>
    private const float Leading = 12;

    /// <summary>The distance between paragraphs.</summary>
    private const float ParagraphGap = 24;

    /// <summary>The title's top, above the first body line.</summary>
    private const float TitleTop = FirstLine - (ParagraphGap * 2);

    /// <summary>The third body line's top.</summary>
    private const float ThirdLine = FirstLine + (Leading * 2);

    /// <summary>The fourth body line's top.</summary>
    private const float FourthLine = FirstLine + (Leading * 3);

    /// <summary>The sixth body line's top.</summary>
    private const float SixthLine = FirstLine + (Leading * 5);

    /// <summary>The footnote's top.</summary>
    private const float FootnoteTop = FooterTop - (ParagraphGap * 4);

    /// <summary>The pages sampled for running headers.</summary>
    private const int SampledPages = 3;

    /// <summary>The page read in the header test.</summary>
    private const int ReadPage = 2;

    /// <summary>The blocks expected when a header is not repeated.</summary>
    private const int KeptBlocks = 2;

    /// <summary>The runs behind a word mended across lines.</summary>
    private const int MendedRuns = 2;

    /// <summary>A heading's size.</summary>
    private const float HeadingSize = 16;

    /// <summary>A footnote's size.</summary>
    private const float FootnoteSize = 8;

    /// <summary>The top of the footer band.</summary>
    private const float FooterTop = 760;

    /// <summary>The top of the header band.</summary>
    private const float HeaderTop = 30;

    /// <summary>Two columns written line by line across the page are read column by column.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsColumnsInTurn()
    {
        var page = new PageBuilder()
            .Body("Left one starts here and", LeftColumn, FirstLine).Body("Right one starts here and", RightColumn, FirstLine)
            .Body("continues on this line.", LeftColumn, FirstLine + Leading).Body("continues over here.", RightColumn, FirstLine + Leading)
            .Read();

        await Assert.That(Texts(page)).IsEquivalentTo(["Left one starts here and continues on this line.", "Right one starts here and continues over here."]);
    }

    /// <summary>A title across both columns is read first, then each column top to bottom, each paragraph its own block.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsTitleBeforeColumns()
    {
        var page = new PageBuilder()
            .Line("A Study of Calm Reading", LeftColumn, TitleTop, HeadingSize, true)
            .Body("First left paragraph.", LeftColumn, FirstLine)
            .Body("Second left paragraph.", LeftColumn, FirstLine + ParagraphGap)
            .Body("First right paragraph.", RightColumn, FirstLine)
            .Body("Second right paragraph.", RightColumn, FirstLine + ParagraphGap)
            .Read();

        await Assert.That(Texts(page)).IsEquivalentTo(
            ["A Study of Calm Reading", "First left paragraph.", "Second left paragraph.", "First right paragraph.", "Second right paragraph."]);
        await Assert.That(page.Blocks[0].Kind).IsEqualTo(ReadingBlockKind.Heading);
        await Assert.That(page.Blocks[1].Kind).IsEqualTo(ReadingBlockKind.Paragraph);
    }

    /// <summary>Page numbers and repeated running headers are left out; other margin text is kept.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LeavesOutPageNumbersAndRunningHeaders()
    {
        static PageBuilder Page(int number) => new PageBuilder()
            .Body("Journal of Calm Design, Volume 3", LeftColumn, HeaderTop)
            .Body($"Body text on page {number}.", LeftColumn, FirstLine)
            .Body($"Page {number} of 9", LeftColumn, FooterTop);
        List<List<string>> samples = [];
        for (var i = 1; i <= SampledPages; i++)
        {
            var signatures = new List<string>();
            ReadingOrder.CollectMarginSignatures(PageBuilder.Letter, Page(i).Characters, signatures);
            samples.Add(signatures);
        }

        var repeated = ReadingOrder.FindRepeatedMargins(samples);
        var page = Page(ReadPage).Read(repeated);

        await Assert.That(Texts(page)).IsEquivalentTo(["Body text on page 2."]);
        await Assert.That(new PageBuilder().Body("Only page header", LeftColumn, HeaderTop).Body("Text.", LeftColumn, FirstLine).Read().Blocks.Count).IsEqualTo(KeptBlocks);
    }

    /// <summary>Bulleted and numbered lines start their own list items; captions are marked.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MarksListsAndCaptions()
    {
        var page = new PageBuilder()
            .Body("Shopping:", LeftColumn, FirstLine)
            .Body("• Apples and pears", LeftColumn, FirstLine + Leading)
            .Body("• Bread", LeftColumn, ThirdLine)
            .Body("2. Second step", LeftColumn, FourthLine)
            .Body("Figure 3: Reading speed by layout.", LeftColumn, SixthLine)
            .Read();

        await Assert.That(page.Blocks.Select(static b => b.Kind)).IsEquivalentTo(
            [ReadingBlockKind.Paragraph, ReadingBlockKind.ListItem, ReadingBlockKind.ListItem, ReadingBlockKind.ListItem, ReadingBlockKind.Caption]);
    }

    /// <summary>Small numbered text at the foot of the page is a footnote, read after the main text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsFootnotesLast()
    {
        var page = new PageBuilder()
            .Line("1 A footnote explaining the claim.", LeftColumn, FootnoteTop, FootnoteSize, false)
            .Body("The main text makes a claim.", LeftColumn, FirstLine)
            .Body("It carries on here.", LeftColumn, FirstLine + ParagraphGap)
            .Read();

        await Assert.That(page.Blocks[^1].Kind).IsEqualTo(ReadingBlockKind.Footnote);
        await Assert.That(page.Blocks[^1].Text).IsEqualTo("1 A footnote explaining the claim.");
    }

    /// <summary>Words hyphenated at a line end are mended, and every character maps back to the page for highlighting.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MendsHyphensAndMapsCharacters()
    {
        var builder = new PageBuilder()
            .Body("The docu-", LeftColumn, FirstLine)
            .Body("ment is calm.", LeftColumn, FirstLine + Leading);
        var page = builder.Read();
        var text = ReadingDocument.Flatten(page, out var map);
        var start = text.IndexOf("document", StringComparison.Ordinal);
        List<ReadingCharacterRun> runs = [];
        ReadingDocument.GetRuns(map, start, "document".Length, runs);

        await Assert.That(page.Blocks[0].Text).IsEqualTo("The document is calm.");
        await Assert.That(runs.Count).IsEqualTo(MendedRuns);
        await Assert.That(builder.Characters[runs[0].Start].Value).IsEqualTo('d');
        await Assert.That(builder.Characters[runs[1].Start].Value).IsEqualTo('m');
    }

    /// <summary>Page numbers are recognised in their usual forms.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RecognisesPageNumbers()
    {
        await Assert.That(ReadingOrder.IsPageNumber("12")).IsTrue();
        await Assert.That(ReadingOrder.IsPageNumber("Page 3 of 9")).IsTrue();
        await Assert.That(ReadingOrder.IsPageNumber("- 4 -")).IsTrue();
        await Assert.That(ReadingOrder.IsPageNumber("xii")).IsTrue();
        await Assert.That(ReadingOrder.IsPageNumber("12 Angry Men")).IsFalse();
        await Assert.That(ReadingOrder.IsPageNumber("Introduction")).IsFalse();
    }

    /// <summary>Gets the blocks' texts.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The texts.</returns>
    private static string[] Texts(ReadingPage page) => [.. page.Blocks.Select(static b => b.Text)];
}
