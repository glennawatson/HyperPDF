// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Layout;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels.Tests;

namespace HyperPdf.Presentation.Tests;

/// <summary>Checks what a pointer press, move and release do on the page canvas, with no toolkit to supply the events.</summary>
public sealed class CanvasPointerTests
{
    /// <summary>The page count of generated documents.</summary>
    private const int Pages = 2;

    /// <summary>The text lines on the text test page.</summary>
    private const int TextLines = 20;

    /// <summary>Where a drag starts, in page points.</summary>
    private const float From = 100F;

    /// <summary>Where a drag ends, in page points.</summary>
    private const float To = 220F;

    /// <summary>How far the pointer moves in the pan test.</summary>
    private const double PanDistance = 30;

    /// <summary>The scroll offset the pan test starts from.</summary>
    private const double StartOffset = 80;

    /// <summary>The width of the view in the drag scroll test.</summary>
    private const double ViewWidth = 800;

    /// <summary>The height of the view in the drag scroll test.</summary>
    private const double ViewHeight = 300;

    /// <summary>How far past the edge of the view the pointer is held.</summary>
    private const double PastTheEdge = 150;

    /// <summary>Half, for the middle of the caret's line.</summary>
    private const double Half = 0.5;

    /// <summary>How far across the text the selection test drags.</summary>
    private const double DragAcross = 40;

    /// <summary>A press that something else has already used does nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HandledPressIsLeftAlone()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("pointer-handled.pdf", Pages)]);
        var (_, state) = CanvasUnderTest.Create(main.SelectedTab!);
        state.RebuildLayout();

        var effects = state.Press(Press(state.ToCanvas(0, new(From, From)), ViewerButtons.Left, true));

        await Assert.That(effects.Handled).IsTrue();
        await Assert.That(effects.Capture).IsFalse();
        await Assert.That(effects.Focus).IsFalse();
    }

    /// <summary>The middle button moves the pages: the press keeps the pointer, a move scrolls, and the release lets go.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MiddleButtonMovesThePages()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("pointer-pan.pdf", Pages)]);
        var (host, state) = CanvasUnderTest.Create(main.SelectedTab!);
        state.RebuildLayout();
        host.ScrollOffset = new(0, StartOffset);
        var start = new CanvasPoint(PanDistance, PanDistance);

        var pressed = state.Press(Press(start, ViewerButtons.Middle, false));
        var focus = pressed.Focus;
        var capture = pressed.Capture;
        var cursor = pressed.Cursor;
        var handled = pressed.Handled;
        _ = state.Move(new(new(start.X, start.Y + PanDistance), new(start.X, start.Y + PanDistance), false));
        var scrolledTo = host.ScrollOffset.Y;
        var released = state.Release(new(start.X, start.Y + PanDistance), false);

        await Assert.That(handled).IsTrue();
        await Assert.That(focus).IsTrue();
        await Assert.That(capture).IsTrue();
        await Assert.That(cursor).IsEqualTo(ViewerCursor.Move);
        await Assert.That(scrolledTo).IsEqualTo(StartOffset - PanDistance);
        await Assert.That(released.ReleaseCapture).IsTrue();
        await Assert.That(state.Tools.Panning).IsFalse();
    }

    /// <summary>The right button asks for the context menu.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RightButtonAsksForTheMenu()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("pointer-menu.pdf", Pages)]);
        var (_, state) = CanvasUnderTest.Create(main.SelectedTab!);
        state.RebuildLayout();

        var effects = state.Press(Press(state.ToCanvas(0, new(From, From)), ViewerButtons.Right, false));

        await Assert.That(effects.ShowContextMenu).IsTrue();
        await Assert.That(effects.Handled).IsTrue();
    }

    /// <summary>A left press on text starts a selection that a move extends and a release ends.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PressingOnTextSelectsIt()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("pointer-text.pdf", TestPdf.CreateTextFilled(Pages, TextLines))]);
        var tab = main.SelectedTab!;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();
        state.StartCaret(tab);
        _ = state.TryGetCaretArea(out var area);
        var onText = new CanvasPoint(area.X + 1, area.Y + (area.Height * Half));
        var further = new CanvasPoint(onText.X + DragAcross, onText.Y);

        var pressed = state.Press(Press(onText, ViewerButtons.Left, false));
        var capture = pressed.Capture;
        var selecting = state.Tools.Selecting;
        _ = state.Move(new(further, further, true));
        var selected = state.Selection.TryGet(out _, out _);
        var released = state.Release(further, false);

        await Assert.That(capture).IsTrue();
        await Assert.That(selecting).IsTrue();
        await Assert.That(selected).IsTrue();
        await Assert.That(released.ReleaseCapture).IsTrue();
        await Assert.That(state.Tools.Selecting).IsFalse();
    }

    /// <summary>Holding the pointer below the view during a drag scrolls the pages and carries the selection on to later text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HoldingThePointerBelowTheViewScrollsAndExtendsTheSelection()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("pointer-drag-scroll.pdf", TestPdf.CreateTextFilled(Pages, TextLines))]);
        var tab = main.SelectedTab!;
        var (host, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();
        host.ViewportSize = new(ViewWidth, ViewHeight);
        host.ExtentSize = new(ViewWidth, state.Layout.ExtentHeight);
        state.StartCaret(tab);
        _ = state.TryGetCaretArea(out var area);
        var onText = new CanvasPoint(area.X + 1, area.Y + (area.Height * Half));
        var below = new CanvasPoint(onText.X, ViewHeight + PastTheEdge);

        _ = state.Press(Press(onText, ViewerButtons.Left, false));
        var dragging = host.Dragging;
        _ = state.Move(new(below, below, true));
        var firstFocus = state.Selection.Focus;
        state.Tools.SelectionDragLast = Stopwatch.GetTimestamp() - Stopwatch.Frequency;
        var moved = state.StepSelectionDrag();
        var laterFocus = state.Selection.Focus;
        _ = state.Release(below, false);

        await Assert.That(dragging).IsTrue();
        await Assert.That(moved).IsTrue();
        await Assert.That(host.ScrollOffset.Y).IsGreaterThan(0);
        await Assert.That(laterFocus.Page > firstFocus.Page || (laterFocus.Page == firstFocus.Page && laterFocus.Char > firstFocus.Char)).IsTrue();
        await Assert.That(host.Dragging).IsFalse();
    }

    /// <summary>Hovering text shows the text pointer, and hovering nothing shows the arrow.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HoverShowsThePointerForWhatIsUnder()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("pointer-hover.pdf", TestPdf.CreateTextFilled(Pages, TextLines))]);
        var tab = main.SelectedTab!;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();
        state.StartCaret(tab);
        _ = state.TryGetCaretArea(out var area);
        var onText = new CanvasPoint(area.X + 1, area.Y + (area.Height * Half));
        var overText = state.Move(new(onText, onText, false)).Cursor;
        var nowhere = new CanvasPoint(-1, -1);

        var offPage = state.Move(new(nowhere, nowhere, false)).Cursor;

        await Assert.That(overText).IsEqualTo(ViewerCursor.Text);
        await Assert.That(offPage).IsNotEqualTo(ViewerCursor.Text);
    }

    /// <summary>The zoom area tool drags an area, and releasing zooms to it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ZoomAreaToolDragsAnAreaAndZoomsToIt()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("pointer-zoom.pdf", Pages)]);
        var tab = main.SelectedTab!;
        tab.PageTools.PageTool = PageTool.ZoomArea;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();
        var start = state.ToCanvas(0, new(From, From));
        var end = state.ToCanvas(0, new(To, To));
        var before = tab.Zoom;

        var captured = state.Press(Press(start, ViewerButtons.Left, false)).Capture;
        var dragging = state.Tools.AreaDragging;
        _ = state.Move(new(end, end, true));
        var released = state.Release(end, false);

        await Assert.That(captured).IsTrue();
        await Assert.That(dragging).IsTrue();
        await Assert.That(released.Snapshot).IsNull();
        await Assert.That(state.Tools.AreaDragging).IsFalse();
        await Assert.That(tab.Zoom).IsNotEqualTo(before);
    }

    /// <summary>The snapshot tool asks for a picture of the area when the pointer is released.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SnapshotToolAsksForAPicture()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("pointer-snapshot.pdf", Pages)]);
        var tab = main.SelectedTab!;
        tab.PageTools.PageTool = PageTool.Snapshot;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();
        var start = state.ToCanvas(0, new(From, From));
        var end = state.ToCanvas(0, new(To, To));

        _ = state.Press(Press(start, ViewerButtons.Left, false));
        _ = state.Move(new(end, end, true));
        var released = state.Release(end, false);

        await Assert.That(released.Snapshot).IsNotNull();
    }

    /// <summary>A rectangle drawn with the pointer is added when it is released.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DrawingARectangleWithThePointerAddsIt()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("pointer-rectangle.pdf", Pages)]);
        var tab = main.SelectedTab!;
        tab.Annotations.Tool = AnnotationTool.Rectangle;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();
        var start = state.ToCanvas(0, new(From, From));
        var end = state.ToCanvas(0, new(To, To));

        var pressed = state.Press(Press(start, ViewerButtons.Left, false));
        var capture = pressed.Capture;
        _ = state.Move(new(end, end, true));
        _ = state.Release(end, false);

        await Assert.That(capture).IsTrue();
        await Assert.That(tab.Annotations.Items.Count).IsEqualTo(1);
    }

    /// <summary>A release with no drag in progress still lets go of the pointer.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReleaseLetsGoOfThePointer()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("pointer-release.pdf", Pages)]);
        var (_, state) = CanvasUnderTest.Create(main.SelectedTab!);
        state.RebuildLayout();

        var effects = state.Release(new(From, From), false);

        await Assert.That(effects.ReleaseCapture).IsTrue();
        await Assert.That(state.Release(new(From, From), true).ReleaseCapture).IsFalse();
    }

    /// <summary>Makes a press at a point with a button.</summary>
    /// <param name="at">Where the pointer is on the canvas.</param>
    /// <param name="buttons">The buttons down.</param>
    /// <param name="handled">Whether something else has used the press.</param>
    /// <returns>The press.</returns>
    private static ViewerPress Press(CanvasPoint at, ViewerButtons buttons, bool handled) =>
        new(at, at, buttons, ViewerModifiers.None, 1, handled);
}
