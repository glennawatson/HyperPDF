// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdf.ViewModels.Tests;

namespace HyperPdf.Presentation.Tests;

/// <summary>Checks what a key does on the page canvas, with no toolkit to supply the events.</summary>
public sealed class CanvasKeyTests
{
    /// <summary>The page count of generated documents.</summary>
    private const int Pages = 3;

    /// <summary>The text lines on the text test page.</summary>
    private const int TextLines = 20;

    /// <summary>Ctrl+A selects the page, and Ctrl+C asks for that text to be copied.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SelectAllThenCopyAsksForTheText()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("key-copy.pdf", TestPdf.CreateTextFilled(Pages, TextLines))]);
        var tab = main.SelectedTab!;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();

        var selected = state.PressKey(ViewerKey.A, ViewerModifiers.Control).Handled;
        var copied = state.PressKey(ViewerKey.C, ViewerModifiers.Control);

        await Assert.That(selected).IsTrue();
        await Assert.That(copied.Handled).IsTrue();
        await Assert.That(copied.CopyText).IsNotNull();
        await Assert.That(copied.CopyText!.Length).IsGreaterThan(0);
    }

    /// <summary>End goes to the last page and Home to the first.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HomeAndEndGoToTheEdgePages()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("key-edges.pdf", Pages)]);
        var tab = main.SelectedTab!;
        tab.IsCaretMode = false;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();

        var end = state.PressKey(ViewerKey.End, ViewerModifiers.None).Handled;
        var last = tab.Destination.PageIndex;
        var home = state.PressKey(ViewerKey.Home, ViewerModifiers.None).Handled;

        await Assert.That(end).IsTrue();
        await Assert.That(last).IsEqualTo(Pages - 1);
        await Assert.That(home).IsTrue();
        await Assert.That(tab.Destination.PageIndex).IsEqualTo(0);
    }

    /// <summary>Holding Space asks for the hand, and letting go gives the tool's pointer back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SpaceHoldShowsTheHandAndLettingGoGivesThePointerBack()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("key-space.pdf", Pages)]);
        var tab = main.SelectedTab!;
        tab.IsCaretMode = false;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();

        var held = state.PressKey(ViewerKey.Space, ViewerModifiers.None);
        var cursor = held.Cursor;
        var handled = held.Handled;
        var heldFlag = state.Tools.SpaceHeld;
        var released = state.ReleaseKey(ViewerKey.Space, ViewerModifiers.None);

        await Assert.That(handled).IsTrue();
        await Assert.That(cursor).IsEqualTo(ViewerCursor.Hand);
        await Assert.That(heldFlag).IsTrue();
        await Assert.That(released.Handled).IsTrue();
        await Assert.That(released.Cursor).IsEqualTo(ViewerCursor.Inherit);
        await Assert.That(state.Tools.SpaceHeld).IsFalse();
    }

    /// <summary>Letting go of a key that is not Space, or of Space that was not held, does nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OtherReleasesDoNothing()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("key-release.pdf", Pages)]);
        var (_, state) = CanvasUnderTest.Create(main.SelectedTab!);
        state.RebuildLayout();

        await Assert.That(state.ReleaseKey(ViewerKey.Enter, ViewerModifiers.None).Handled).IsFalse();
        await Assert.That(state.ReleaseKey(ViewerKey.Space, ViewerModifiers.None).Handled).IsFalse();
    }

    /// <summary>Escape goes back to selecting text from another page tool, and does nothing when it already is.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EscapeGoesBackToSelectingText()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("key-escape.pdf", Pages)]);
        var tab = main.SelectedTab!;
        tab.IsCaretMode = false;
        tab.PageTools.PageTool = PageTool.Hand;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();

        var first = state.PressKey(ViewerKey.Escape, ViewerModifiers.None);
        var cursor = first.Cursor;
        var handled = first.Handled;
        var again = state.PressKey(ViewerKey.Escape, ViewerModifiers.None).Handled;

        await Assert.That(handled).IsTrue();
        await Assert.That(cursor).IsEqualTo(ViewerCursor.Inherit);
        await Assert.That(tab.PageTools.PageTool).IsEqualTo(PageTool.SelectText);
        await Assert.That(again).IsFalse();
    }

    /// <summary>A key nothing uses is not handled.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnusedKeysAreNotHandled()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("key-unused.pdf", Pages)]);
        var (_, state) = CanvasUnderTest.Create(main.SelectedTab!);
        state.RebuildLayout();

        await Assert.That(state.PressKey(ViewerKey.Other, ViewerModifiers.None).Handled).IsFalse();
    }
}
