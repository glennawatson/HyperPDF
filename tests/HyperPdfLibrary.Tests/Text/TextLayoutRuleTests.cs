// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.Tests.Text;

/// <summary>Checks the line grouping, word spacing and line joining rules, with pages built from the half-em test font.</summary>
[NotInParallel]
public sealed class TextLayoutRuleTests
{
    /// <summary>The text of a line split by a word space.</summary>
    private const string SplitWords = "ab cd";

    /// <summary>The text of a line with no space.</summary>
    private const string JoinedWords = "abcd";

    /// <summary>A gap of two points between "ab" (ending at 20 at size 10) and "cd": 0.2 em.</summary>
    private const string GapOfTwoPointsAtTen = "BT /F1 10 Tf 10 100 Td (ab) Tj 12 0 Td (cd) Tj ET";

    /// <summary>The same two point gap at size 20, where it is 0.1 em.</summary>
    private const string GapOfTwoPointsAtTwenty = "BT /F1 20 Tf 10 100 Td (ab) Tj 22 0 Td (cd) Tj ET";

    /// <summary>A TJ adjustment of a tenth of an em, as kerning makes.</summary>
    private const string TenthEmAdjustment = "BT /F1 10 Tf 10 100 Td [(ab) -100 (cd)] TJ ET";

    /// <summary>A TJ adjustment of a third of an em, as a word gap makes.</summary>
    private const string ThirdEmAdjustment = "BT /F1 10 Tf 10 100 Td [(ab) -333 (cd)] TJ ET";

    /// <summary>Two points of character spacing, which are inside the glyph advances and so are not gaps.</summary>
    private const string CharacterSpacing = "BT /F1 10 Tf 2 Tc 10 100 Td (abcd) Tj ET";

    /// <summary>Text turned a quarter, drawn in two pieces with a two point gap along its baseline.</summary>
    private const string RotatedGap = "BT /F1 10 Tf 0 1 -1 0 100 100 Tm (ab) Tj 0 1 -1 0 100 112 Tm (cd) Tj ET";

    /// <summary>Text turned 45 degrees, drawn in two pieces with a two point gap along its baseline.</summary>
    private const string DiagonalGap = "BT /F1 10 Tf 0.70711 0.70711 -0.70711 0.70711 100 100 Tm (ab) Tj 0.70711 0.70711 -0.70711 0.70711 108.4853 108.4853 Tm (cd) Tj ET";

    /// <summary>A superscript raised by three tenths of an em.</summary>
    private const string Superscript = "BT /F1 10 Tf 10 100 Td (x) Tj 3 Ts (2) Tj ET";

    /// <summary>The same words on two baselines one em apart.</summary>
    private const string TwoBaselines = "BT /F1 10 Tf 10 100 Td (ab) Tj 0 -10 Td (cd) Tj ET";

    /// <summary>Text drawn twice a little apart, as fake bold does, with spaces inside.</summary>
    private const string DoubleStruck = "BT /F1 10 Tf 10 100 Td (Hello World) Tj ET BT /F1 10 Tf 10.3 100 Td (Hello World) Tj ET";

    /// <summary>The index of the space generated in "ab cd".</summary>
    private const int GeneratedIndex = 2;

    /// <summary>The count of rectangles for one line drawn in two runs.</summary>
    private const int OneRectangle = 1;

    /// <summary>The count of characters in "ab cd" with its space.</summary>
    private const int SplitWordsLength = 5;

    /// <summary>The tolerance for coordinates.</summary>
    private const float Tolerance = 0.001F;

    /// <summary>A two point gap is a space at size 10 (0.2 em) and none at size 20 (0.1 em): the threshold scales with the font.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SpaceThresholdScalesWithFontSize()
    {
        var small = TextTestDocument.Extract(GapOfTwoPointsAtTen);
        var large = TextTestDocument.Extract(GapOfTwoPointsAtTwenty);

        await Assert.That(small.Text).IsEqualTo(SplitWords);
        await Assert.That(small.GetChar(GeneratedIndex).IsGenerated).IsTrue();
        await Assert.That(large.Text).IsEqualTo(JoinedWords);
    }

    /// <summary>Kerning of a tenth of an em keeps a word whole, and a third of an em separates words.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AdjustmentsSplitWordsOnlyWhenWideEnough()
    {
        var kerned = TextTestDocument.Extract(TenthEmAdjustment);
        var spaced = TextTestDocument.Extract(ThirdEmAdjustment);

        await Assert.That(kerned.Text).IsEqualTo(JoinedWords);
        await Assert.That(spaced.Text).IsEqualTo(SplitWords);
    }

    /// <summary>Character spacing widens each advance, so it is not mistaken for a gap between words.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CharacterSpacingDoesNotMakeSpaces()
    {
        var page = TextTestDocument.Extract(CharacterSpacing);

        await Assert.That(page.Text).IsEqualTo(JoinedWords);
    }

    /// <summary>Text turned a quarter or 45 degrees measures its gaps along its own baseline.</summary>
    /// <param name="content">The page content.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(RotatedGap)]
    [Arguments(DiagonalGap)]
    public async Task RotatedTextSpacesAlongItsBaseline(string content)
    {
        var page = TextTestDocument.Extract(content);

        await Assert.That(page.Text).IsEqualTo(SplitWords);
        await Assert.That(page.CharCount).IsEqualTo(SplitWordsLength);
    }

    /// <summary>A raised glyph stays on its line, so no line break comes between it and the text before it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SuperscriptStaysOnTheLine()
    {
        var page = TextTestDocument.Extract(Superscript);

        await Assert.That(page.Text).IsEqualTo("x2");
    }

    /// <summary>Text a full em above or below breaks the line.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BaselinesAnEmApartAreSeparateLines()
    {
        var page = TextTestDocument.Extract(TwoBaselines);

        await Assert.That(page.Text).IsEqualTo("ab\r\ncd");
    }

    /// <summary>A whole line drawn twice at a small offset is kept once, with its spaces.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LineDrawnTwiceIsKeptOnce()
    {
        var page = TextTestDocument.Extract(DoubleStruck);

        await Assert.That(page.Text).IsEqualTo("Hello World");
    }

    /// <summary>A line drawn in two runs gets one selection rectangle, which bridges the generated space.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OneLineGivesOneRectangle()
    {
        var page = TextTestDocument.Extract(GapOfTwoPointsAtTen);
        var rects = new List<HyperPdfLibrary.Objects.PdfRectangle>();
        page.GetRects(0, page.CharCount, rects);

        await Assert.That(rects.Count).IsEqualTo(OneRectangle);
        await Assert.That(rects[0].Left).IsEqualTo(page.GetChar(0).Box.Left).Within(Tolerance);
        await Assert.That(rects[0].Right).IsEqualTo(page.GetChar(page.CharCount - 1).Box.Right).Within(Tolerance);
    }

    /// <summary>A hyphen before a lower-case letter on the next line joins the word; a capital or a dash alone does not.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="expected">The page text.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("BT /F1 10 Tf 10 100 Td (well-) Tj 0 -20 Td (known) Tj ET", "wellknown")]
    [Arguments("BT /F1 10 Tf 10 100 Td (well-) Tj 0 -20 Td (Known) Tj ET", "well-\r\nKnown")]
    [Arguments("BT /F1 10 Tf 10 100 Td (-) Tj 0 -20 Td (known) Tj ET", "-\r\nknown")]
    [Arguments("BT /F1 10 Tf 10 100 Td (a 1-) Tj 0 -20 Td (known) Tj ET", "a 1-\r\nknown")]
    public async Task HyphenationFollowsTheLineEndRule(string content, string expected)
    {
        var page = TextTestDocument.Extract(content);

        await Assert.That(page.Text).IsEqualTo(expected);
    }

    /// <summary>An empty /ActualText hides the glyphs it covers.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EmptyActualTextHidesGlyphs()
    {
        var pdf = TextTestDocument.Create("BT /F1 10 Tf 10 100 Td (ab) Tj /Span /P1 BDC (xx) Tj EMC (cd) Tj ET");
        pdf.Resources += " /Properties << /P1 << /ActualText () >> >>";
        var page = TextTestDocument.Extract(pdf);

        await Assert.That(page.Text).IsEqualTo(JoinedWords);
    }
}
