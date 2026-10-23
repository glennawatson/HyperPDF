// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Accessibility;

namespace HyperPdf.Core.Tests.Accessibility;

/// <summary>Checks how a text range moves and resizes by a unit, which the Windows and macOS bridges rely on.</summary>
public sealed class TextRangesTests
{
    /// <summary>Some page text with two lines and a contraction.</summary>
    private const string Page = "The quick fox. It can't stop!\nNew line here.";

    /// <summary>The number of words after the first, counting the line feed as a word of its own.</summary>
    private const int WordsAfterTheFirst = 9;

    /// <summary>A distance in words that runs past the end of the text.</summary>
    private const int ManyWords = 100;

    /// <summary>Where the second word starts and the first word ends.</summary>
    private const int SecondWordStart = 4;

    /// <summary>Where the second word ends.</summary>
    private const int SecondWordEnd = 10;

    /// <summary>An offset inside the first line.</summary>
    private const int InFirstLine = 3;

    /// <summary>An offset inside a word, away from its start.</summary>
    private const int InsideAWord = 5;

    /// <summary>Two words.</summary>
    private const int TwoWords = 2;

    /// <summary>The offset of the character after "a" in a string with an emoji, where the emoji takes two code units.</summary>
    private const int EmojiStart = 1;

    /// <summary>The offset just after the emoji.</summary>
    private const int EmojiEnd = 3;

    /// <summary>A text with an emoji, which is a surrogate pair.</summary>
    private const string WithEmoji = "a\U0001F600b";

    /// <summary>The first word with its space.</summary>
    private static readonly TextSpan FirstWord = new(0, SecondWordStart);

    /// <summary>The second word with its space.</summary>
    private static readonly TextSpan SecondWord = new(SecondWordStart, SecondWordEnd);

    /// <summary>All of the page.</summary>
    private static readonly TextSpan WholePage = new(0, Page.Length);

    /// <summary>Verifies that a range moves forward by a whole word.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Move_ForwardByAWord()
    {
        var move = TextRanges.Move(Page, FirstWord, TextRangeUnit.Word, 1);

        await Assert.That(Page[move.Range.Start..move.Range.End]).IsEqualTo("quick ");
        await Assert.That(move.Moved).IsEqualTo(1);
    }

    /// <summary>Verifies that a range moves back by a whole word.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Move_BackByAWord()
    {
        var move = TextRanges.Move(Page, SecondWord, TextRangeUnit.Word, -1);

        await Assert.That(Page[move.Range.Start..move.Range.End]).IsEqualTo("The ");
        await Assert.That(move.Moved).IsEqualTo(-1);
    }

    /// <summary>Verifies that a move past the end stops on the last word and reports how far it got.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Move_PastTheEnd_StopsOnTheLastWord()
    {
        var move = TextRanges.Move(Page, FirstWord, TextRangeUnit.Word, ManyWords);

        await Assert.That(Page[move.Range.Start..move.Range.End]).IsEqualTo("here.");
        await Assert.That(move.Moved).IsEqualTo(WordsAfterTheFirst);
    }

    /// <summary>Verifies that a move back past the start stops on the first word.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Move_PastTheStart_StopsOnTheFirstWord()
    {
        var move = TextRanges.Move(Page, SecondWord, TextRangeUnit.Word, -ManyWords);

        await Assert.That(Page[move.Range.Start..move.Range.End]).IsEqualTo("The ");
        await Assert.That(move.Moved).IsEqualTo(-1);
    }

    /// <summary>Verifies that a document is one unit that cannot be moved.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Move_ByDocument_DoesNotMove()
    {
        var move = TextRanges.Move(Page, FirstWord, TextRangeUnit.Document, 1);

        await Assert.That(move.Range).IsEqualTo(WholePage);
        await Assert.That(move.Moved).IsEqualTo(0);
    }

    /// <summary>Verifies that moving through no text goes nowhere.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Move_ThroughNoText_GoesNowhere()
    {
        var move = TextRanges.Move(string.Empty, default, TextRangeUnit.Word, 1);

        await Assert.That(move.Range).IsEqualTo(default);
        await Assert.That(move.Moved).IsEqualTo(0);
    }

    /// <summary>Verifies that expanding to a line holds the whole line with its line feed.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Expand_ToALine_HoldsTheLineFeed()
    {
        var line = TextRanges.Expand(Page, InFirstLine, TextRangeUnit.Line);

        await Assert.That(Page[line.Start..line.End]).IsEqualTo("The quick fox. It can't stop!\n");
    }

    /// <summary>Verifies that expanding to a document or a page holds all of the text.</summary>
    /// <param name="unit">The unit.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(TextRangeUnit.Document)]
    [Arguments(TextRangeUnit.Page)]
    public async Task Expand_ToADocumentOrPage_HoldsAllTheText(TextRangeUnit unit) =>
        await Assert.That(TextRanges.Expand(Page, InsideAWord, unit)).IsEqualTo(WholePage);

    /// <summary>Verifies that a character made of a surrogate pair is kept whole.</summary>
    /// <param name="offset">An offset in the pair.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(1)]
    [Arguments(2)]
    public async Task Expand_ToACharacter_KeepsASurrogatePairWhole(int offset) =>
        await Assert.That(TextRanges.Expand(WithEmoji, offset, TextRangeUnit.Character)).IsEqualTo(new(EmojiStart, EmojiEnd));

    /// <summary>Verifies that moving the end forward grows the range.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MoveEndpoint_EndForward_GrowsTheRange()
    {
        var move = TextRanges.MoveEndpoint(Page, FirstWord, true, TextRangeUnit.Word, 1);

        await Assert.That(move.Range).IsEqualTo(new(0, SecondWordEnd));
        await Assert.That(move.Moved).IsEqualTo(1);
    }

    /// <summary>Verifies that moving the start past the end collapses the range to the start.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MoveEndpoint_StartPastTheEnd_Collapses()
    {
        var move = TextRanges.MoveEndpoint(Page, FirstWord, false, TextRangeUnit.Word, TwoWords);

        await Assert.That(move.Range).IsEqualTo(new(SecondWordEnd, SecondWordEnd));
        await Assert.That(move.Moved).IsEqualTo(TwoWords);
    }

    /// <summary>Verifies that moving the end back past the start collapses the range to the end.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MoveEndpoint_EndBeforeTheStart_Collapses()
    {
        var move = TextRanges.MoveEndpoint(Page, SecondWord, true, TextRangeUnit.Word, -TwoWords);

        await Assert.That(move.Range).IsEqualTo(default);
        await Assert.That(move.Moved).IsEqualTo(-TwoWords);
    }

    /// <summary>Verifies that moving the end by a document goes to the end of the text.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MoveEndpoint_ByDocument_GoesToTheEnd()
    {
        var move = TextRanges.MoveEndpoint(Page, FirstWord, true, TextRangeUnit.Document, 1);

        await Assert.That(move.Range).IsEqualTo(WholePage);
        await Assert.That(move.Moved).IsEqualTo(1);
    }
}
