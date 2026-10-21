// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;
using HyperPdf.Core.Tests.Fakes;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks the selected text, its order and the rectangles kept for drawing it.</summary>
public sealed class TextSelectionTests
{
    /// <summary>The size of the test pages.</summary>
    private const float PageSide = 100F;

    /// <summary>The text of each test page.</summary>
    private const string PageText = "abcd";

    /// <summary>The second character of a test page.</summary>
    private const int SecondCharacter = 1;

    /// <summary>The third character of a test page.</summary>
    private const int ThirdCharacter = 2;

    /// <summary>The last character of a test page.</summary>
    private const int LastCharacter = 3;

    /// <summary>The second page.</summary>
    private const int SecondPage = 1;

    /// <summary>Nothing is selected until a selection starts, and starting on one character selects nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NothingIsSelectedAtFirst()
    {
        var selection = new TextSelection();
        var empty = selection.TryGet(out _, out _);
        selection.Begin(At(0, SecondCharacter));
        var single = selection.TryGet(out _, out _);

        await Assert.That(empty).IsFalse();
        await Assert.That(single).IsFalse();
    }

    /// <summary>A selection dragged backwards reads from its first character to its last.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BackwardsSelectionIsPutInOrder()
    {
        var selection = new TextSelection();
        selection.Begin(At(SecondPage, SecondCharacter));
        _ = selection.MoveFocus(At(0, LastCharacter));

        var found = selection.TryGet(out var start, out var end);

        await Assert.That(found).IsTrue();
        await Assert.That(start).IsEqualTo(At(0, LastCharacter));
        await Assert.That(end).IsEqualTo(At(SecondPage, SecondCharacter));
    }

    /// <summary>Moving the end to where it already is changes nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MovingTheFocusToWhereItIsDoesNothing()
    {
        var selection = new TextSelection();
        selection.Begin(At(0, SecondCharacter));

        await Assert.That(selection.MoveFocus(At(0, SecondCharacter))).IsFalse();
        await Assert.That(selection.MoveFocus(At(0, ThirdCharacter))).IsTrue();
    }

    /// <summary>The text of a selection across pages is joined by line breaks.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextRunsAcrossPages()
    {
        using var document = Create();
        var selection = new TextSelection();
        selection.Begin(At(0, ThirdCharacter));
        _ = selection.MoveFocus(At(SecondPage, SecondCharacter));

        var text = selection.GetText(document);

        await Assert.That(text).IsEqualTo($"cd{Environment.NewLine}ab");
    }

    /// <summary>With no document or no selection there is no text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NoDocumentOrSelectionGivesNoText()
    {
        using var document = Create();
        var selection = new TextSelection();

        await Assert.That(selection.GetText(document)).IsEqualTo(string.Empty);
        selection.Begin(At(0, 0));
        _ = selection.MoveFocus(At(0, SecondCharacter));
        await Assert.That(selection.GetText(null)).IsEqualTo(string.Empty);
    }

    /// <summary>Selecting a page selects every character on it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SelectingAPageSelectsAllItsText()
    {
        using var document = Create();
        var selection = new TextSelection();

        selection.SelectPage(0, document.GetCharacterCount(0));

        await Assert.That(selection.GetText(document)).IsEqualTo(PageText);
    }

    /// <summary>The rectangles of a page are worked out once and then kept.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RectanglesAreKeptUntilTheFocusMovesOverThePage()
    {
        using var document = Create();
        var selection = new TextSelection();
        selection.Begin(At(0, 0));
        _ = selection.MoveFocus(At(0, LastCharacter));

        var first = selection.GetRects(document, 0);
        var again = selection.GetRects(document, 0);
        _ = selection.MoveFocus(At(0, SecondCharacter));
        var after = selection.GetRects(document, 0);

        await Assert.That(first.Count).IsGreaterThan(0);
        await Assert.That(ReferenceEquals(first, again)).IsTrue();
        await Assert.That(after.Count).IsGreaterThan(0);
    }

    /// <summary>Moving the caret without extending drops the selection, and extending starts it where the caret was.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CaretMovesExtendOrDropTheSelection()
    {
        var selection = new TextSelection();
        var caret = At(0, SecondCharacter);

        selection.MoveCaret(caret, At(0, ThirdCharacter), true);
        var extended = selection.TryGet(out var start, out var end);
        selection.MoveCaret(At(0, ThirdCharacter), At(0, LastCharacter), false);
        var dropped = selection.TryGet(out _, out _);

        await Assert.That(extended).IsTrue();
        await Assert.That(start).IsEqualTo(caret);
        await Assert.That(end).IsEqualTo(At(0, ThirdCharacter));
        await Assert.That(dropped).IsFalse();
    }

    /// <summary>Clearing forgets the selection.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClearForgetsTheSelection()
    {
        var selection = new TextSelection();
        selection.Begin(At(0, 0));
        _ = selection.MoveFocus(At(0, SecondCharacter));

        selection.Clear();

        await Assert.That(selection.TryGet(out _, out _)).IsFalse();
        await Assert.That(selection.Anchor.Page).IsEqualTo(-1);
    }

    /// <summary>Names a position in the test document.</summary>
    /// <param name="page">The page.</param>
    /// <param name="character">The character on the page.</param>
    /// <returns>The position.</returns>
    private static CharacterPosition At(int page, int character) => new(page, character);

    /// <summary>Makes two pages of text.</summary>
    /// <returns>The document.</returns>
    private static FakeDocument Create() => new("selection.pdf", new PageSize(PageSide, PageSide), new PageSize(PageSide, PageSide)) { PageTextFor = static _ => PageText };
}
