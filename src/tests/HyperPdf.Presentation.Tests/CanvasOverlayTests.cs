// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels.Tests;

namespace HyperPdf.Presentation.Tests;

/// <summary>Checks what the canvas plans to draw over the pages, through a painter with no toolkit behind it.</summary>
public sealed class CanvasOverlayTests
{
    /// <summary>The page count of generated documents.</summary>
    private const int Pages = 1;

    /// <summary>Where a drag starts, in page points.</summary>
    private const float From = 100F;

    /// <summary>Where a drag ends, in page points.</summary>
    private const float To = 220F;

    /// <summary>The width of the viewport.</summary>
    private const double ViewWidth = 800;

    /// <summary>The height of the viewport.</summary>
    private const double ViewHeight = 600;

    /// <summary>The corners placed in the polygon test.</summary>
    private const int TwoCorners = 2;

    /// <summary>The sides drawn for them: one between the corners and one to the pointer, and one from the first corner to itself.</summary>
    private const int ThreeSides = 3;

    /// <summary>The lines of the stroke in the stroke test.</summary>
    private const int TwoLines = 2;

    /// <summary>Where the note goes on the page.</summary>
    private const float NoteAt = 300F;

    /// <summary>The lines of text on the text test page.</summary>
    private const int TextLines = 20;

    /// <summary>A rectangle being dragged is drawn as a rectangle outline, an ellipse as an ellipse and a line as a line.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShapePreviewFollowsTheShapeKind()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("overlay-shape.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();
        var rectangle = new FakeOverlayPainter();
        var ellipse = new FakeOverlayPainter();
        var line = new FakeOverlayPainter();

        tab.Annotations.Tool = AnnotationTool.Rectangle;
        _ = state.BeginShape(tab, 0, state.ToCanvas(0, new(From, From)));
        state.Drawing.ActiveShape = state.Drawing.ActiveShape!.Value with { End = new(To, To) };
        state.DrawAnnotationOverlay(rectangle, tab, View());
        state.Drawing.ActiveShape = state.Drawing.ActiveShape!.Value with { Kind = HyperPdf.Core.Annotations.AnnotationKind.Ellipse };
        state.DrawAnnotationOverlay(ellipse, tab, View());
        state.Drawing.ActiveShape = state.Drawing.ActiveShape!.Value with { Kind = HyperPdf.Core.Annotations.AnnotationKind.Line };
        state.DrawAnnotationOverlay(line, tab, View());

        await Assert.That(rectangle.Rectangles).IsEqualTo(1);
        await Assert.That(ellipse.Ellipses).IsEqualTo(1);
        await Assert.That(line.Lines).IsEqualTo(1);
    }

    /// <summary>The corners of a polygon are drawn as dots joined by lines, with one more line to the pointer.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PolygonPreviewDrawsCornersAndSides()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("overlay-polygon.pdf", Pages)]);
        var tab = main.SelectedTab!;
        tab.Annotations.Tool = AnnotationTool.Polygon;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();
        var painter = new FakeOverlayPainter();
        state.PressPolygonCorner(tab, 0, state.ToCanvas(0, new(From, From)), false);
        state.PressPolygonCorner(tab, 0, state.ToCanvas(0, new(To, From)), false);

        state.DrawAnnotationOverlay(painter, tab, View());

        await Assert.That(painter.Dots).IsEqualTo(TwoCorners);
        await Assert.That(painter.Lines).IsEqualTo(ThreeSides);
    }

    /// <summary>A freehand stroke is drawn a line at a time, as the highlighter when that tool is chosen.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StrokeIsDrawnAsInkOrTheHighlighter()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("overlay-stroke.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();
        var ink = new FakeOverlayPainter();
        var highlighter = new FakeOverlayPainter();
        tab.Annotations.Tool = AnnotationTool.Draw;
        state.BeginInk(tab, 0, state.ToCanvas(0, new(From, From)));
        _ = state.ContinueDrawing(state.ToCanvas(0, new(To, To)));
        _ = state.ContinueDrawing(state.ToCanvas(0, new(To, From)));

        state.DrawAnnotationOverlay(ink, tab, View());
        tab.Annotations.Tool = AnnotationTool.FreehandHighlight;
        state.DrawAnnotationOverlay(highlighter, tab, View());

        await Assert.That(ink.StrokeLines).IsEqualTo(TwoLines);
        await Assert.That(ink.SawHighlighter).IsFalse();
        await Assert.That(highlighter.SawHighlighter).IsTrue();
    }

    /// <summary>The area being dragged is outlined only where the toolkit has a colour for it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AreaIsOutlinedOnlyWithAHitColour()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("overlay-area.pdf", Pages)]);
        var (_, state) = CanvasUnderTest.Create(main.SelectedTab!);
        state.RebuildLayout();
        state.Tools.AreaDragging = true;
        var painted = new FakeOverlayPainter();
        var bare = new FakeOverlayPainter { HasHit = false };

        state.DrawArea(painted);
        state.DrawArea(bare);

        await Assert.That(painted.Rectangles).IsEqualTo(1);
        await Assert.That(bare.Rectangles).IsEqualTo(0);
    }

    /// <summary>The caret is filled in caret mode once it has a place, and only where the toolkit has an ink for it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CaretIsFilledInCaretMode()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("overlay-caret.pdf", TestPdf.CreateTextFilled(Pages, TextLines))]);
        var tab = main.SelectedTab!;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();
        tab.IsCaretMode = true;
        state.StartCaret(tab);
        var painted = new FakeOverlayPainter();
        var bare = new FakeOverlayPainter { HasCaretInk = false };
        var off = new FakeOverlayPainter();

        state.DrawCaret(painted, tab);
        state.DrawCaret(bare, tab);
        tab.IsCaretMode = false;
        state.DrawCaret(off, tab);

        await Assert.That(painted.Fills).IsEqualTo(1);
        await Assert.That(bare.Fills).IsEqualTo(0);
        await Assert.That(off.Fills).IsEqualTo(0);
    }

    /// <summary>A drag in progress is outlined as the annotation moves.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EditInProgressIsOutlined()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("overlay-edit.pdf", Pages)]);
        var tab = main.SelectedTab!;
        using var prompt = tab.Annotations.PromptInteraction.RegisterHandler(static context => context.SetOutput("Outline me"));
        await tab.Annotations.AddNoteAsync(0, new(NoteAt, NoteAt));
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();
        var note = tab.Annotations.Items[0].Annotation;
        state.Tools.ActiveEdit = new(note, new(note.Bounds.Left, note.Bounds.Top), default, note.Bounds, false);
        var painter = new FakeOverlayPainter();

        state.DrawAnnotationOverlay(painter, tab, View());

        await Assert.That(painter.Rectangles).IsGreaterThanOrEqualTo(1);
    }

    /// <summary>With nothing under way, nothing is drawn over the pages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NothingUnderWayDrawsNothing()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("overlay-none.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();
        var painter = new FakeOverlayPainter();

        state.DrawAnnotationOverlay(painter, tab, View());
        state.DrawRegions(painter, tab, View());
        state.DrawMeasurement(painter, tab);

        await Assert.That(painter.Lines + painter.Rectangles + painter.Ellipses + painter.Fills + painter.Dots + painter.Badges + painter.Marks + painter.Labels + painter.StrokeLines).IsEqualTo(0);
    }

    /// <summary>Makes the part of the canvas in view.</summary>
    /// <returns>The viewport.</returns>
    private static LayoutRect View() => new(0, 0, ViewWidth, ViewHeight);
}
