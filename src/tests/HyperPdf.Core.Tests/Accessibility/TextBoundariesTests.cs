// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Accessibility;

namespace HyperPdf.Core.Tests.Accessibility;

/// <summary>Checks how a screen reader moves through page text by character, word, sentence and line.</summary>
public sealed class TextBoundariesTests
{
    /// <summary>Some page text with two lines, two sentences and a contraction.</summary>
    private const string Page = "The quick fox. It can't stop!\nNew line here.";

    /// <summary>A word the tests look for in the page.</summary>
    private const string Quick = "quick";

    /// <summary>How far into a word the tests place an offset.</summary>
    private const int IntoWord = 2;

    /// <summary>Verifies that a word holds the spaces after it, from wherever in the word the offset is.</summary>
    /// <param name="offset">An offset inside the word "quick".</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(4)]
    [Arguments(7)]
    [Arguments(9)]
    public async Task Word_HoldsTheSpacesAfterIt(int offset)
    {
        var span = TextBoundaries.At(Page, offset, TextGranularity.Word);

        await Assert.That(Page[span.Start..span.End]).IsEqualTo("quick ");
    }

    /// <summary>Verifies that moving to the next word gets past a line feed instead of stopping at the end of the line.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Word_AfterTheLastWordOfALine_IsTheLineFeedThenTheNextLine()
    {
        var lastWord = TextBoundaries.At(Page, Page.IndexOf("stop", StringComparison.Ordinal), TextGranularity.Word);
        var feed = TextBoundaries.After(Page, lastWord.Start, TextGranularity.Word);
        var next = TextBoundaries.After(Page, feed.Start, TextGranularity.Word);

        await Assert.That(Page[feed.Start..feed.End]).IsEqualTo("\n");
        await Assert.That(Page[next.Start..next.End]).IsEqualTo("New ");
    }

    /// <summary>Verifies that the space after a word belongs to that word.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Word_AtASpace_IsTheWordBeforeIt()
    {
        var span = TextBoundaries.At(Page, Page.IndexOf(' ', StringComparison.Ordinal), TextGranularity.Word);

        await Assert.That(Page[span.Start..span.End]).IsEqualTo("The ");
    }

    /// <summary>Verifies that an apostrophe between letters stays inside the word.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Word_KeepsAnApostropheBetweenLetters()
    {
        var offset = Page.IndexOf("can't", StringComparison.Ordinal) + IntoWord;
        var span = TextBoundaries.At(Page, offset, TextGranularity.Word);

        await Assert.That(Page[span.Start..span.End]).IsEqualTo("can't ");
    }

    /// <summary>Verifies that a sentence runs to the start of the next one.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Sentence_RunsToTheStartOfTheNext()
    {
        var span = TextBoundaries.At(Page, Page.IndexOf(Quick, StringComparison.Ordinal), TextGranularity.Sentence);

        await Assert.That(Page[span.Start..span.End]).IsEqualTo("The quick fox. ");
    }

    /// <summary>Verifies that a sentence stops at a line feed.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Sentence_StopsAtALineFeed()
    {
        var span = TextBoundaries.At(Page, Page.IndexOf("stop", StringComparison.Ordinal), TextGranularity.Sentence);

        await Assert.That(Page[span.Start..span.End]).IsEqualTo("It can't stop!\n");
    }

    /// <summary>Verifies that a line includes its line feed.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Line_IncludesItsLineFeed()
    {
        var span = TextBoundaries.At(Page, Page.IndexOf(Quick, StringComparison.Ordinal), TextGranularity.Line);

        await Assert.That(Page[span.Start..span.End]).IsEqualTo("The quick fox. It can't stop!\n");
    }

    /// <summary>Verifies that the last line has no line feed to include.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Line_LastLineRunsToTheEnd()
    {
        var span = TextBoundaries.At(Page, Page.Length - IntoWord, TextGranularity.Line);

        await Assert.That(Page[span.Start..span.End]).IsEqualTo("New line here.");
    }

    /// <summary>Verifies that the stretch before and after step by whole units.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BeforeAndAfter_StepByWholeWords()
    {
        var offset = Page.IndexOf(Quick, StringComparison.Ordinal);
        var before = TextBoundaries.Before(Page, offset, TextGranularity.Word);
        var after = TextBoundaries.After(Page, offset, TextGranularity.Word);

        await Assert.That(Page[before.Start..before.End]).IsEqualTo("The ");
        await Assert.That(Page[after.Start..after.End]).IsEqualTo("fox. ");
    }

    /// <summary>Verifies that nothing is before the first stretch or after the last.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BeforeTheFirstAndAfterTheLast_AreEmpty()
    {
        await Assert.That(TextBoundaries.Before(Page, 0, TextGranularity.Word).IsEmpty).IsTrue();
        await Assert.That(TextBoundaries.After(Page, Page.Length - 1, TextGranularity.Line).IsEmpty).IsTrue();
    }

    /// <summary>Verifies that an empty text and an offset at the end give an empty stretch.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmptyTextAndTheEnd_GiveAnEmptyStretch()
    {
        await Assert.That(TextBoundaries.At(string.Empty, 0, TextGranularity.Word).IsEmpty).IsTrue();
        await Assert.That(TextBoundaries.At(Page, Page.Length, TextGranularity.Character).IsEmpty).IsTrue();
    }

    /// <summary>Verifies that a character stretch is one character.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Character_IsOneCharacter()
    {
        var span = TextBoundaries.At(Page, 1, TextGranularity.Character);

        await Assert.That(Page[span.Start..span.End]).IsEqualTo("h");
    }
}
