// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.Tests.Text;

/// <summary>
/// Checks selection rectangles on a line of Latin and Hebrew text. The page text is in logical order, so a selection is a
/// range of the logical text, but the rectangles are where the glyphs are drawn: a right-to-left run sits right to left
/// inside the line, and a selection that takes part of it must cover those glyphs and no others.
/// </summary>
public sealed class TextBidiSelectionTests
{
    /// <summary>
    /// "ab", a space, three Hebrew letters drawn left to right as alef, bet, gimel, a space and "cd". The Hebrew letters are
    /// one right-to-left run, so the page text reads "ab", a space, gimel, bet, alef, a space and "cd".
    /// </summary>
    private const string MixedLine = "BT /F1 10 Tf 10 100 Td (ab \\240\\241\\242 cd) Tj ET";

    /// <summary>
    /// The same line with three Arabic letters drawn left to right as hamza, alef with madda and alef with hamza above. The
    /// page text reads the Arabic letters in reverse: alef with hamza above, alef with madda, then hamza.
    /// </summary>
    private const string MixedArabicLine = "BT /F1 10 Tf 10 100 Td (ab \\300\\301\\302 cd) Tj ET";

    /// <summary>The index of the letter "b".</summary>
    private const int LetterB = 1;

    /// <summary>The index of the first character of the Hebrew run in logical order: gimel, which is drawn last.</summary>
    private const int Gimel = 3;

    /// <summary>The index of bet, the second character of the Hebrew run in logical order.</summary>
    private const int Bet = 4;

    /// <summary>The index of alef, the last character of the Hebrew run in logical order, which is drawn first.</summary>
    private const int Alef = 5;

    /// <summary>The index of the last character of the line.</summary>
    private const int LastCharacter = 8;

    /// <summary>The embedding level of a right-to-left run.</summary>
    private const int RightToLeftLevel = 1;

    /// <summary>The characters of the Hebrew run.</summary>
    private const int HebrewLength = 3;

    /// <summary>The rectangles of a selection that takes the end of the Latin text and the first two Hebrew letters.</summary>
    private const int PartialSelectionRects = 2;

    /// <summary>The rectangles of the whole line: Latin text, the Hebrew run, and Latin text again.</summary>
    private const int WholeLineRects = 3;

    /// <summary>The characters from "b" to bet.</summary>
    private const int ToBet = 4;

    /// <summary>The allowed difference between two coordinates.</summary>
    private const float Tolerance = 0.01F;

    /// <summary>Verifies the page text and the levels: Hebrew letters are in reverse drawing order at level 1, Latin text at level 0.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MixedLine_HasLogicalTextAndEmbeddingLevels()
    {
        var page = TextTestDocument.Extract(MixedLine);

        await Assert.That(page.Text).IsEqualTo("ab \U000005D2\U000005D1\U000005D0 cd");
        await Assert.That(page.GetBidiLevel(0)).IsEqualTo(0);
        await Assert.That(page.GetBidiLevel(Gimel)).IsEqualTo(RightToLeftLevel);
        await Assert.That(page.GetBidiLevel(Bet)).IsEqualTo(RightToLeftLevel);
        await Assert.That(page.GetBidiLevel(Alef)).IsEqualTo(RightToLeftLevel);
        await Assert.That(page.GetBidiLevel(LastCharacter)).IsEqualTo(0);
    }

    /// <summary>Verifies that a selection taking only part of the right-to-left run covers those glyphs and not the alef drawn between it and the Latin text.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PartialRightToLeftRun_CoversOnlyItsOwnGlyphs()
    {
        var page = TextTestDocument.Extract(MixedLine);
        var rects = new List<PdfRectangle>();

        page.GetRects(LetterB, ToBet, rects);

        await Assert.That(rects.Count).IsEqualTo(PartialSelectionRects);
        await Assert.That(rects[1].Left).IsEqualTo(page.GetChar(Bet).Box.Left).Within(Tolerance);
        await Assert.That(rects[1].Right).IsEqualTo(page.GetChar(Gimel).Box.Right).Within(Tolerance);
        await Assert.That(rects[1].Left).IsGreaterThan(page.GetChar(Alef).Box.Right);
    }

    /// <summary>Verifies that the whole right-to-left run is one rectangle covering all three letters.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task WholeRightToLeftRun_IsOneRectangle()
    {
        var page = TextTestDocument.Extract(MixedLine);
        var rects = new List<PdfRectangle>();

        page.GetRects(Gimel, HebrewLength, rects);

        await Assert.That(rects.Count).IsEqualTo(1);
        await Assert.That(rects[0].Left).IsEqualTo(page.GetChar(Alef).Box.Left).Within(Tolerance);
        await Assert.That(rects[0].Right).IsEqualTo(page.GetChar(Gimel).Box.Right).Within(Tolerance);
    }

    /// <summary>Verifies that the whole line gives a rectangle for each direction run, in the order they sit on the page.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task WholeLine_GivesARectangleForEachDirectionRun()
    {
        var page = TextTestDocument.Extract(MixedLine);
        var rects = new List<PdfRectangle>();

        page.GetRects(0, page.CharCount, rects);

        await Assert.That(rects.Count).IsEqualTo(WholeLineRects);
        await Assert.That(rects[0].Right).IsLessThanOrEqualTo(rects[1].Left);
        await Assert.That(rects[1].Right).IsLessThanOrEqualTo(rects[WholeLineRects - 1].Left);
    }

    /// <summary>Verifies that Arabic text drawn left to right reads in logical order, copies as that string, and selects only its own glyphs.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ArabicRun_ReadsInLogicalOrderAndSelectsItsOwnGlyphs()
    {
        var page = TextTestDocument.Extract(MixedArabicLine);
        var rects = new List<PdfRectangle>();

        page.GetRects(LetterB, ToBet, rects);

        await Assert.That(page.Text).IsEqualTo("ab \U00000623\U00000622\U00000621 cd");
        await Assert.That(page.GetText(Gimel, HebrewLength)).IsEqualTo("\U00000623\U00000622\U00000621");
        await Assert.That(page.GetBidiLevel(Gimel)).IsEqualTo(RightToLeftLevel);
        await Assert.That(rects.Count).IsEqualTo(PartialSelectionRects);
        await Assert.That(rects[1].Left).IsGreaterThan(page.GetChar(Alef).Box.Right);
    }

    /// <summary>Verifies that a line of left-to-right text is still one rectangle, as before direction runs were tracked.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LeftToRightLine_IsOneRectangle()
    {
        var page = TextTestDocument.Extract("BT /F1 10 Tf 10 100 Td (abc def) Tj ET");
        var rects = new List<PdfRectangle>();

        page.GetRects(0, page.CharCount, rects);

        await Assert.That(rects.Count).IsEqualTo(1);
        await Assert.That(page.GetBidiLevel(0)).IsEqualTo(0);
    }
}
