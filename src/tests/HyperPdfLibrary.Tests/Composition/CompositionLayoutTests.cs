// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Composition;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Tests.Composition;

/// <summary>Checks how paragraphs, lists, footnotes and sections are laid out on pages, without writing a file.</summary>
public sealed class CompositionLayoutTests
{
    /// <summary>The height of a page that holds a probe paragraph, so nothing breaks.</summary>
    private const float TallPage = 2000F;

    /// <summary>How far a measured position may be from the exact one, in points.</summary>
    private const float Tolerance = 0.6F;

    /// <summary>The text of a paragraph of four lines.</summary>
    private const string FourLines = "one\ntwo\nthree\nfour";

    /// <summary>The text of a paragraph of three lines.</summary>
    private const string ThreeLines = "one\ntwo\nthree";

    /// <summary>The number of lines in the paragraphs of three lines.</summary>
    private const int ThreeLineCount = 3;

    /// <summary>The number of lines in the paragraphs of four lines.</summary>
    private const int FourLineCount = 4;

    /// <summary>The lines a page holds before the half line left over.</summary>
    private const float ThreeLinesOfRoom = 3F;

    /// <summary>Half a line, as a fraction of its height.</summary>
    private const float HalfLine = 0.5F;

    /// <summary>The fewest lines the justified text breaks into.</summary>
    private const int MinimumJustifiedLines = 3;

    /// <summary>The size of a small capital, as a fraction of a full capital.</summary>
    private const float SmallCapScale = 0.8F;

    /// <summary>The page number a section is told to start at.</summary>
    private const int StartNumber = 5;

    /// <summary>The inside margin of the mirrored pages.</summary>
    private const float MirroredInside = 36F;

    /// <summary>The outside margin of the mirrored pages.</summary>
    private const float MirroredOutside = 72F;

    /// <summary>The indent of a list's items.</summary>
    private const float ListIndent = 30F;

    /// <summary>The position of the tab stop with dots before it.</summary>
    private const float TabPosition = 200F;

    /// <summary>Two, for the margins on both sides of a page and for halving.</summary>
    private const float Twice = 2F;

    /// <summary>The space between a border and the text of a boxed paragraph.</summary>
    private const float BoxPadding = 4F;

    /// <summary>The lines of border round a boxed paragraph.</summary>
    private const int BorderLines = 4;

    /// <summary>A quarter of the tall page, for a page with a footnote.</summary>
    private const float QuarterPage = TallPage / 4F;

    /// <summary>The number of pages of the two section test.</summary>
    private const int TwoPages = 2;

    /// <summary>The number of fillers before the heading, one more than the lines that fit.</summary>
    private const int Fillers = 4;

    /// <summary>The lines of room left for the heading and its paragraph.</summary>
    private const float RoomForHeading = 5F;

    /// <summary>The lines of room that hold a paragraph of three lines and then one and a half more.</summary>
    private const float RoomAfterThreeLines = 4.5F;

    /// <summary>The heading level of the heading test.</summary>
    private const int HeadingLevel = 2;

    /// <summary>A long text that wraps over several lines.</summary>
    private const string LongText = "The quick brown fox jumps over the lazy dog and then runs back again over the same hill, "
        + "while the farmer shouts at it from the gate and the sun sets slowly behind the distant mountains of the north.";

    /// <summary>A run of letters wider than the line.</summary>
    private const string WideWord = "Supercalifragilisticexpialidocious-Supercalifragilisticexpialidocious-Supercalifragilisticexpialidocious";

    /// <summary>A four line paragraph that has room for three lines and a half is split two and two, not three and one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WidowControlLeavesTwoLinesOnEachPage()
    {
        var composition = Compose(PageFor(RoomFor(ThreeLinesOfRoom)), Plain(FourLines, true));

        var pages = CompositionKit.Layout(composition).Pages;

        await Assert.That(pages.Count).IsEqualTo(TwoPages);
        await Assert.That(CompositionKit.TextOf(pages[0]).Count).IsEqualTo(TwoPages);
        await Assert.That(CompositionKit.TextOf(pages[1]).Count).IsEqualTo(TwoPages);
    }

    /// <summary>Without widow control the page is filled and one line is left over.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WithoutWidowControlThePageIsFilled()
    {
        var composition = Compose(PageFor(RoomFor(ThreeLinesOfRoom)), Plain(FourLines, false));

        var pages = CompositionKit.Layout(composition).Pages;

        await Assert.That(CompositionKit.TextOf(pages[0]).Count).IsEqualTo(ThreeLineCount);
        await Assert.That(CompositionKit.TextOf(pages[1]).Count).IsEqualTo(1);
    }

    /// <summary>A paragraph that would start the foot of a page with a single line moves to the next page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OrphanControlMovesAParagraphToTheNextPage()
    {
        var composition = Compose(PageFor(RoomFor(RoomAfterThreeLines - HalfLine)), Plain(ThreeLines, true), Plain(FourLines, true));

        var pages = CompositionKit.Layout(composition).Pages;

        await Assert.That(pages.Count).IsEqualTo(TwoPages);
        await Assert.That(CompositionKit.TextOf(pages[0]).Count).IsEqualTo(ThreeLineCount);
        await Assert.That(CompositionKit.TextOf(pages[1]).Count).IsEqualTo(FourLineCount);
    }

    /// <summary>A heading that would be alone at the foot of a page goes to the next page with its paragraph.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AHeadingStaysWithItsParagraph()
    {
        var blocks = new List<PdfBlock>();
        for (var i = 0; i < Fillers; i++)
        {
            blocks.Add(PlainLine("filler"));
        }

        blocks.Add(PdfParagraph.Heading(HeadingLevel, "Heading"));
        blocks.Add(Plain(ThreeLines, false));

        var pages = CompositionKit.Layout(Compose(PageFor(RoomFor(RoomForHeading)), [.. blocks])).Pages;
        var headingPage = pages.FindIndex(static p => CompositionKit.TextOf(p).Exists(static t => t.Text == "Heading"));
        var bodyPage = pages.FindIndex(static p => CompositionKit.TextOf(p).Exists(static t => t.Text == "one"));

        await Assert.That(headingPage).IsEqualTo(bodyPage);
        await Assert.That(headingPage).IsGreaterThan(0);
    }

    /// <summary>A page break starts a new page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task APageBreakStartsANewPage()
    {
        var composition = Compose(CompositionKit.CreateSetup(TallPage), PdfParagraph.Of("before"), PdfPageBreak.Instance, PdfParagraph.Of("after"));

        var pages = CompositionKit.Layout(composition).Pages;

        await Assert.That(pages.Count).IsEqualTo(TwoPages);
        await Assert.That(CompositionKit.TextOf(pages[1])[0].Text).IsEqualTo("after");
    }

    /// <summary>Every line of a justified paragraph but the last ends at the right margin.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task JustifiedLinesFillTheWidth()
    {
        var paragraph = new PdfParagraph([new PdfRun(LongText)]) { Format = new() { Alignment = PdfTextAlignment.Justify } };

        var page = CompositionKit.Layout(Compose(CompositionKit.CreateSetup(TallPage), paragraph)).Pages[0];
        var lines = GroupLines(CompositionKit.TextOf(page));

        await Assert.That(lines.Count).IsGreaterThanOrEqualTo(MinimumJustifiedLines);
        for (var i = 0; i < lines.Count - 1; i++)
        {
            await Assert.That(RightEdge(lines[i])).IsEqualTo(CompositionKit.PageWidth - CompositionKit.Margin).Within(Tolerance);
        }

        await Assert.That(RightEdge(lines[^1])).IsLessThan(CompositionKit.PageWidth - CompositionKit.Margin - Tolerance);
    }

    /// <summary>Right aligned lines end at the right margin and centred lines are in the middle.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AlignmentPlacesLines()
    {
        var right = new PdfParagraph([new PdfRun("right")]) { Format = new() { Alignment = PdfTextAlignment.Right } };
        var centre = new PdfParagraph([new PdfRun("centre")]) { Format = new() { Alignment = PdfTextAlignment.Center } };

        var page = CompositionKit.Layout(Compose(CompositionKit.CreateSetup(TallPage), right, centre)).Pages[0];
        var texts = CompositionKit.TextOf(page);

        await Assert.That(texts[0].X + Width(texts[0])).IsEqualTo(CompositionKit.PageWidth - CompositionKit.Margin).Within(Tolerance);
        await Assert.That(texts[1].X + (Width(texts[1]) / Twice)).IsEqualTo(CompositionKit.PageWidth / Twice).Within(Tolerance);
    }

    /// <summary>A right tab stop with a dot leader puts the text after the tab at the stop and fills the gap with dots.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RightTabsAlignWithALeader()
    {
        var stops = new PdfTabStop[] { new(TabPosition, PdfTabAlignment.Right, PdfTabLeader.Dots) };
        var paragraph = new PdfParagraph([new PdfRun("Chapter"), PdfTab.Instance, new PdfRun("12")]) { Format = new() { TabStops = stops } };

        var page = CompositionKit.Layout(Compose(CompositionKit.CreateSetup(TallPage), paragraph)).Pages[0];
        var texts = CompositionKit.TextOf(page);
        var number = texts.First(static t => t.Text == "12");

        await Assert.That(number.X + Width(number)).IsEqualTo(CompositionKit.Margin + TabPosition).Within(Tolerance);
        await Assert.That(texts.Exists(static t => t.Text.StartsWith(". .", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>A footnote is drawn below the body text on the page that has its number.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AFootnoteSitsAtTheFootOfItsPage()
    {
        var paragraph = PdfParagraph.Of(new PdfRun("body text"), PdfFootnote.Of("the note"));

        var page = CompositionKit.Layout(Compose(CompositionKit.CreateSetup(QuarterPage), paragraph)).Pages[0];
        var texts = CompositionKit.TextOf(page);
        var body = texts.First(static t => t.Text.StartsWith("body", StringComparison.Ordinal));
        var note = texts.First(static t => t.Text.Contains("the note", StringComparison.Ordinal));

        await Assert.That(note.Y).IsLessThan(body.Y);
        await Assert.That(page.Notes.Count).IsEqualTo(1);
        await Assert.That(page.Commands.OfType<StrokeCommand>().Count()).IsEqualTo(1);
    }

    /// <summary>A line whose footnote does not fit moves to the next page with its number.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AFootnoteThatDoesNotFitMovesItsLine()
    {
        var inlines = new PdfInline[] { new PdfRun("one\ntwo\n"), new PdfRun("three"), PdfFootnote.Of("note") };
        var paragraph = new PdfParagraph(inlines) { Format = new() { WidowControl = false } };

        var pages = CompositionKit.Layout(Compose(PageFor(RoomFor(ThreeLinesOfRoom)), paragraph)).Pages;

        await Assert.That(pages.Count).IsEqualTo(TwoPages);
        await Assert.That(pages[0].Notes.Count).IsEqualTo(0);
        await Assert.That(pages[1].Notes.Count).IsEqualTo(1);
    }

    /// <summary>A list's label hangs to the left of its text, and a wrapped line lines up with the text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ListLabelsHangLeftOfTheText()
    {
        var list = new PdfList(PdfListKind.Numbered, [PdfListItem.Of(LongText)]) { Indent = ListIndent };

        var page = CompositionKit.Layout(Compose(CompositionKit.CreateSetup(TallPage), list)).Pages[0];
        var texts = CompositionKit.TextOf(page);
        var label = texts.First(static t => t.Text == "1.");
        var lines = GroupLines(texts.FindAll(static t => t.Text != "1."));

        await Assert.That(label.X).IsEqualTo(CompositionKit.Margin).Within(Tolerance);
        await Assert.That(lines.Count).IsGreaterThan(1);
        await Assert.That(lines[0][0].X).IsEqualTo(CompositionKit.Margin + ListIndent).Within(Tolerance);
        await Assert.That(lines[1][0].X).IsEqualTo(CompositionKit.Margin + ListIndent).Within(Tolerance);
    }

    /// <summary>A section starts a page, and its first page can restart the number.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SectionsStartPagesAndRestartNumbers()
    {
        var creator = CompositionKit.CreateCreator(false);
        var styles = CompositionKit.CreateStyles(creator);
        var restarted = CompositionKit.CreateSetup(TallPage) with { StartPageNumber = StartNumber };
        var first = new PdfSection([PdfParagraph.Of("first")]) { Setup = CompositionKit.CreateSetup(TallPage) };
        var second = new PdfSection([PdfParagraph.Of("second")]) { Setup = restarted };
        var composition = new PdfComposition(styles, [first, second]);

        var pages = CompositionKit.Layout(composition).Pages;

        await Assert.That(pages.Count).IsEqualTo(TwoPages);
        await Assert.That(pages[0].DisplayNumber).IsEqualTo(1);
        await Assert.That(pages[1].DisplayNumber).IsEqualTo(StartNumber);
        await Assert.That(pages[1].Section.Number).IsEqualTo(TwoPages);
    }

    /// <summary>Mirrored margins swap the inside and outside margins on even pages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MirroredMarginsSwapSides()
    {
        var setup = CompositionKit.CreateSetup(TallPage) with { MirrorMargins = true, MarginLeft = MirroredInside, MarginRight = MirroredOutside };

        var pages = CompositionKit.Layout(Compose(setup, PdfParagraph.Of("odd"), PdfPageBreak.Instance, PdfParagraph.Of("even"))).Pages;

        await Assert.That(CompositionKit.TextOf(pages[0])[0].X).IsEqualTo(MirroredInside).Within(Tolerance);
        await Assert.That(CompositionKit.TextOf(pages[1])[0].X).IsEqualTo(MirroredOutside).Within(Tolerance);
    }

    /// <summary>A word wider than a line is broken, and no text is lost.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnOverlongWordIsBroken()
    {
        var page = CompositionKit.Layout(Compose(CompositionKit.CreateSetup(TallPage), PdfParagraph.Of(WideWord))).Pages[0];
        var texts = CompositionKit.TextOf(page);

        await Assert.That(texts.Count).IsGreaterThan(1);
        await Assert.That(string.Concat(texts.Select(static t => t.Text))).IsEqualTo(WideWord);
        foreach (var text in texts)
        {
            await Assert.That(text.X + Width(text)).IsLessThanOrEqualTo(CompositionKit.PageWidth - CompositionKit.Margin + Tolerance);
        }
    }

    /// <summary>Small capitals draw the lower case letters as capitals at a smaller size.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SmallCapitalsShrinkLowerCaseLetters()
    {
        var page = CompositionKit.Layout(Compose(CompositionKit.CreateSetup(TallPage), PdfParagraph.Of(PdfRun.SmallCapitals("Hello")))).Pages[0];
        var texts = CompositionKit.TextOf(page);

        await Assert.That(texts.Count).IsEqualTo(TwoPages);
        await Assert.That(texts[0].Text).IsEqualTo("H");
        await Assert.That(texts[1].Text).IsEqualTo("ELLO");
        await Assert.That(texts[1].Size).IsEqualTo(texts[0].Size * SmallCapScale).Within(Tolerance);
    }

    /// <summary>Superscript text is smaller and raised above the baseline.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SuperscriptIsRaisedAndSmaller()
    {
        var paragraph = PdfParagraph.Of(new PdfRun("x"), PdfRun.Superscript("2"));

        var texts = CompositionKit.TextOf(CompositionKit.Layout(Compose(CompositionKit.CreateSetup(TallPage), paragraph)).Pages[0]);

        await Assert.That(texts[1].Size).IsLessThan(texts[0].Size);
        await Assert.That(texts[1].Y).IsGreaterThan(texts[0].Y);
    }

    /// <summary>A paragraph with a border and shading draws the shading first and a line on each side.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BordersAndShadingAreDrawn()
    {
        var border = PdfBorders.Box(PdfBorderSide.Solid(1F, PdfCanvasColor.Black));
        var format = new PdfParagraphFormat { Shading = PdfCanvasColor.Gray(HalfLine), Borders = border, Padding = PdfInsets.Uniform(BoxPadding) };
        var paragraph = new PdfParagraph([new PdfRun("boxed")]) { Format = format };

        var page = CompositionKit.Layout(Compose(CompositionKit.CreateSetup(TallPage), paragraph)).Pages[0];

        await Assert.That(page.Commands[0]).IsTypeOf<FillCommand>();
        await Assert.That(page.Commands.OfType<StrokeCommand>().Count()).IsEqualTo(BorderLines);
    }

    /// <summary>Makes a composition of one section.</summary>
    /// <param name="setup">The page setup.</param>
    /// <param name="blocks">The blocks.</param>
    /// <returns>The composition.</returns>
    private static PdfComposition Compose(PdfPageSetup setup, params PdfBlock[] blocks)
    {
        var creator = CompositionKit.CreateCreator(false);
        return new(CompositionKit.CreateStyles(creator), [new PdfSection(blocks) { Setup = setup }]);
    }

    /// <summary>Makes a paragraph of plain text whose lines are given by line breaks, with no space after it.</summary>
    /// <param name="text">The text.</param>
    /// <param name="widowControl">Whether widow and orphan control is on.</param>
    /// <returns>The paragraph.</returns>
    private static PdfParagraph Plain(string text, bool widowControl) =>
        new([new PdfRun(text)]) { Format = new() { SpaceAfter = 0F, WidowControl = widowControl } };

    /// <summary>Makes a paragraph of one line with no space after it.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The paragraph.</returns>
    private static PdfParagraph PlainLine(string text) => new([new PdfRun(text)]) { Format = new() { SpaceAfter = 0F } };

    /// <summary>Works out the height of a text area that holds a number of lines.</summary>
    /// <param name="lines">The number of lines, with a half line to spare.</param>
    /// <returns>The height in points.</returns>
    private static float RoomFor(float lines) => (lines + HalfLine) * MeasureLineHeight();

    /// <summary>Makes a page setup whose text area is a height.</summary>
    /// <param name="textHeight">The height of the text area.</param>
    /// <returns>The setup.</returns>
    private static PdfPageSetup PageFor(float textHeight) => CompositionKit.CreateSetup((Twice * CompositionKit.Margin) + textHeight);

    /// <summary>Measures the height of a line of body text by laying out two lines.</summary>
    /// <returns>The height in points.</returns>
    private static float MeasureLineHeight()
    {
        var page = CompositionKit.Layout(Compose(CompositionKit.CreateSetup(TallPage), Plain("a\nb", false))).Pages[0];
        var texts = CompositionKit.TextOf(page);
        return texts[0].Y - texts[1].Y;
    }

    /// <summary>Gets the width of a text command.</summary>
    /// <param name="text">The command.</param>
    /// <returns>The width in points.</returns>
    private static float Width(TextCommand text) => text.Font.Measure(text.Text, text.Size);

    /// <summary>Gets where the rightmost text of a line ends.</summary>
    /// <param name="line">The line's text commands.</param>
    /// <returns>The x.</returns>
    private static float RightEdge(List<TextCommand> line) => line.Max(static t => t.X + Width(t));

    /// <summary>Groups text commands into lines by their baseline.</summary>
    /// <param name="texts">The commands, in drawing order.</param>
    /// <returns>The lines, top first, each with its commands left to right.</returns>
    private static List<List<TextCommand>> GroupLines(List<TextCommand> texts)
    {
        var lines = new List<List<TextCommand>>();
        foreach (var text in texts)
        {
            if (lines.Count > 0 && MathF.Abs(lines[^1][0].Y - text.Y) < Tolerance)
            {
                lines[^1].Add(text);
            }
            else
            {
                lines.Add([text]);
            }
        }

        return lines;
    }
}
