// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;
using HyperPdf.ViewModels.Tests;

namespace HyperPdf.Presentation.Tests;

/// <summary>Checks drawing ink and shapes, polygons and click tools through a host with no toolkit behind it.</summary>
public sealed class CanvasDrawingTests
{
    /// <summary>The page count of generated documents.</summary>
    private const int Pages = 1;

    /// <summary>Where a drag starts, in page points.</summary>
    private const float From = 100F;

    /// <summary>Where a long drag ends, in page points.</summary>
    private const float To = 220F;

    /// <summary>A drag too short to draw a shape, in page points.</summary>
    private const float Stray = 1F;

    /// <summary>An ink stroke of several points is added as one drawing when the pointer is released.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InkStrokeIsAddedOnRelease()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("draw-ink.pdf", Pages)]);
        var tab = main.SelectedTab!;
        tab.Annotations.Tool = AnnotationTool.Draw;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();
        var start = state.ToCanvas(0, new(From, From));
        var end = state.ToCanvas(0, new(To, To));

        state.BeginInk(tab, 0, start);
        var moved = state.ContinueDrawing(end);
        var ended = state.EndDrawing(end);

        await Assert.That(moved).IsTrue();
        await Assert.That(ended).IsTrue();
        await Assert.That(state.Drawing.StrokePage).IsEqualTo(-1);
        await Assert.That(state.Drawing.Stroke.Count).IsEqualTo(0);
        await Assert.That(tab.Annotations.Items.Count).IsEqualTo(1);
        await Assert.That(tab.Annotations.Items[0].Annotation.Kind).IsEqualTo(AnnotationKind.Ink);
    }

    /// <summary>Dragging a rectangle adds it on release, and a stray click adds nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RectangleDragIsAddedButAStrayClickIsNot()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("draw-shape.pdf", Pages)]);
        var tab = main.SelectedTab!;
        tab.Annotations.Tool = AnnotationTool.Rectangle;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();
        var start = state.ToCanvas(0, new(From, From));

        var began = state.BeginShape(tab, 0, start);
        _ = state.EndDrawing(state.ToCanvas(0, new(From + Stray, From)));
        var afterStray = tab.Annotations.Items.Count;
        _ = state.BeginShape(tab, 0, start);
        _ = state.ContinueDrawing(state.ToCanvas(0, new(To, To)));
        _ = state.EndDrawing(state.ToCanvas(0, new(To, To)));

        await Assert.That(began).IsTrue();
        await Assert.That(afterStray).IsEqualTo(0);
        await Assert.That(tab.Annotations.Items.Count).IsEqualTo(1);
        await Assert.That(state.Drawing.ActiveShape).IsNull();
    }

    /// <summary>A tool that draws no shape does not start one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SelectToolStartsNoShape()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("draw-none.pdf", Pages)]);
        var tab = main.SelectedTab!;
        tab.Annotations.Tool = AnnotationTool.Select;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();

        await Assert.That(state.BeginShape(tab, 0, state.ToCanvas(0, new(From, From)))).IsFalse();
        await Assert.That(state.ContinueDrawing(state.ToCanvas(0, new(To, To)))).IsFalse();
        await Assert.That(state.EndDrawing(state.ToCanvas(0, new(To, To)))).IsFalse();
    }

    /// <summary>Polygon corners are kept until Escape starts again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PolygonCornersAreKeptUntilCancelled()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("draw-polygon.pdf", Pages)]);
        var tab = main.SelectedTab!;
        tab.Annotations.Tool = AnnotationTool.Polygon;
        var (host, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();
        var redraws = host.Redraws;

        state.PressPolygonCorner(tab, 0, state.ToCanvas(0, new(From, From)), false);
        var kept = state.Drawing.Polygon.Corners.Count;
        var used = state.HandlePolygonKey(ViewerKey.Escape);

        await Assert.That(kept).IsEqualTo(1);
        await Assert.That(used).IsTrue();
        await Assert.That(state.Drawing.Polygon.IsActive).IsFalse();
        await Assert.That(host.Redraws).IsGreaterThan(redraws);
    }

    /// <summary>A click tool remembers its page until the release.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClickToolRemembersItsPressUntilRelease()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("draw-click.pdf", Pages)]);
        var tab = main.SelectedTab!;
        tab.Annotations.Tool = AnnotationTool.Stamp;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();

        state.BeginClick(0, true);
        var ended = state.EndDrawing(state.ToCanvas(0, new(From, From)));

        await Assert.That(ended).IsTrue();
        await Assert.That(state.Drawing.ClickPage).IsEqualTo(-1);
    }

    /// <summary>With no mark being placed, moving the placement does nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NothingToMoveWhenNothingIsBeingPlaced()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("draw-place.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();

        await Assert.That(state.MovePlacement(tab, 1, 1)).IsFalse();
    }

    /// <summary>The hovered region is cleared when the pointer is not over a page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HoveredRegionIsNothingOffThePage()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("draw-hover.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var (host, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();
        var redraws = host.Redraws;

        state.HoverRegion(false, 0, new(From, From));

        await Assert.That(state.Drawing.HoveredRegion).IsNull();
        await Assert.That(host.Redraws).IsEqualTo(redraws);
    }
}
