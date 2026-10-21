// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdf.ViewModels.Tests;

namespace HyperPdf.Presentation.Tests;

/// <summary>Checks the text caret, selecting all and character hit testing through a host with no toolkit behind it.</summary>
public sealed class CanvasCaretTests
{
    /// <summary>The pages in the test document.</summary>
    private const int Pages = 2;

    /// <summary>The text lines on each page.</summary>
    private const int Lines = 20;

    /// <summary>A point well outside the pages.</summary>
    private const double OutsideThePages = -100;

    /// <summary>Half, for the middle of the caret's line.</summary>
    private const double Half = 0.5;

    /// <summary>Caret mode starts the caret on the first character of the first page with text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StartingCaretModePlacesTheCaret()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("caret-start.pdf", TestPdf.CreateTextFilled(Pages, Lines))]);
        var tab = main.SelectedTab!;
        var (_, state) = CanvasUnderTest.Create(tab);

        state.StartCaret(tab);

        await Assert.That(state.Caret).IsEqualTo(At(0, 0));
    }

    /// <summary>The right arrow moves the caret on, and Shift extends the selection from where it was.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ArrowMovesTheCaretAndShiftExtendsTheSelection()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("caret-arrow.pdf", TestPdf.CreateTextFilled(Pages, Lines))]);
        var tab = main.SelectedTab!;
        tab.IsCaretMode = true;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();

        var moved = state.HandleCaretKey(ViewerKey.Right, ViewerModifiers.None);
        var plain = state.Caret;
        var extended = state.HandleCaretKey(ViewerKey.Right, ViewerModifiers.Shift);
        var selected = state.Selection.TryGet(out var start, out var end);

        await Assert.That(moved).IsTrue();
        await Assert.That(plain).IsEqualTo(At(0, 1));
        await Assert.That(extended).IsTrue();
        await Assert.That(selected).IsTrue();
        await Assert.That(start).IsEqualTo(plain);
        await Assert.That(end).IsEqualTo(state.Caret);
    }

    /// <summary>Outside caret mode, or with Control held, the arrows are not caret keys.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ArrowsNeedCaretModeAndNoControl()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("caret-mode.pdf", TestPdf.CreateTextFilled(Pages, Lines))]);
        var tab = main.SelectedTab!;
        var (_, state) = CanvasUnderTest.Create(tab);
        tab.IsCaretMode = false;

        var off = state.HandleCaretKey(ViewerKey.Right, ViewerModifiers.None);
        tab.IsCaretMode = true;
        var control = state.HandleCaretKey(ViewerKey.Right, ViewerModifiers.Control);

        await Assert.That(off).IsFalse();
        await Assert.That(control).IsFalse();
    }

    /// <summary>Selecting all selects the page shown and puts the caret at its end.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SelectAllSelectsThePageAndMovesTheCaretToItsEnd()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("caret-all.pdf", TestPdf.CreateTextFilled(Pages, Lines))]);
        var tab = main.SelectedTab!;
        var (_, state) = CanvasUnderTest.Create(tab);
        var count = tab.TryGetDocument()!.GetCharacterCount(0);

        state.SelectAllOnPage(tab);

        await Assert.That(state.Selection.TryGet(out var start, out var end)).IsTrue();
        await Assert.That(start).IsEqualTo(At(0, 0));
        await Assert.That(end).IsEqualTo(At(0, count - 1));
        await Assert.That(state.Caret).IsEqualTo(end);
    }

    /// <summary>The caret has a line on the canvas once it is placed, and none before.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CaretLineExistsOnlyOncePlaced()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("caret-line.pdf", TestPdf.CreateTextFilled(Pages, Lines))]);
        var tab = main.SelectedTab!;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();

        var before = state.TryGetCaretArea(out _);
        state.StartCaret(tab);
        var after = state.TryGetCaretArea(out var area);

        await Assert.That(before).IsFalse();
        await Assert.That(after).IsTrue();
        await Assert.That(area.Height).IsGreaterThan(0);
    }

    /// <summary>A point on the caret's own line finds the character the caret is on.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HitTestFindsTheCharacterUnderAPoint()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("caret-hit.pdf", TestPdf.CreateTextFilled(Pages, Lines))]);
        var tab = main.SelectedTab!;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();
        state.StartCaret(tab);
        _ = state.TryGetCaretArea(out var area);

        var hit = state.TryHitTestCharacter(new(area.X + 1, area.Y + (area.Height * Half)), out var page, out var character);
        var miss = state.TryHitTestCharacter(new(OutsideThePages, OutsideThePages), out _, out _);

        await Assert.That(hit).IsTrue();
        await Assert.That(page).IsEqualTo(0);
        await Assert.That(character).IsGreaterThanOrEqualTo(0);
        await Assert.That(miss).IsFalse();
    }

    /// <summary>Names a position in the test document.</summary>
    /// <param name="page">The page.</param>
    /// <param name="character">The character on the page.</param>
    /// <returns>The position.</returns>
    private static CharacterPosition At(int page, int character) => new(page, character);
}
