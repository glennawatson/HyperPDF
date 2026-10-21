// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;
using HyperPdf.ViewModels.Tests;

namespace HyperPdf.Presentation.Tests;

/// <summary>Checks what a frame of the page canvas draws and asks for, through a painter with no toolkit behind it.</summary>
public sealed class CanvasRenderTests
{
    /// <summary>The page count of generated documents.</summary>
    private const int Pages = 3;

    /// <summary>The width of the viewport.</summary>
    private const double ViewWidth = 800;

    /// <summary>The height of the viewport.</summary>
    private const double ViewHeight = 600;

    /// <summary>A viewport shifted to a different place.</summary>
    private const double Shifted = 50;

    /// <summary>The display scale of the test frames.</summary>
    private const double Scaling = 1;

    /// <summary>A frame draws the paper of the pages in view, flushes after each page and asks for what is not cached.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FirstFrameDrawsPagesAndAsksForTiles()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("render-first.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();
        var painter = new FakeTilePainter();
        var frame = Frame(tab, new(0, 0, ViewWidth, ViewHeight), state);

        state.DrawPages(painter, frame);

        await Assert.That(painter.Begins).IsEqualTo(1);
        await Assert.That(painter.Ends).IsEqualTo(1);
        await Assert.That(painter.Papers).IsGreaterThan(0);
        await Assert.That(painter.Overlays).IsEqualTo(painter.Papers);
        await Assert.That(painter.Flushes).IsGreaterThanOrEqualTo(painter.Papers);
        await Assert.That(state.Render.RequestsThisFrame).IsGreaterThan(0);
        await Assert.That(state.Render.FrameComplete).IsFalse();
    }

    /// <summary>The generation is kept while the view is the same, and a new one starts when the view changes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GenerationFollowsTheView()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("render-generation.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var document = tab.TryGetDocument()!;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();
        var viewport = new LayoutRect(0, 0, ViewWidth, ViewHeight);

        var first = state.GetRenderGeneration(tab, document, viewport, Scaling);
        var same = state.GetRenderGeneration(tab, document, viewport, Scaling);
        var moved = state.GetRenderGeneration(tab, document, viewport with { Y = Shifted }, Scaling);

        await Assert.That(same).IsEqualTo(first);
        await Assert.That(moved).IsNotEqualTo(first);
    }

    /// <summary>Forgetting the request starts a new generation even for the same view.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ForgettingTheRequestStartsANewGeneration()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("render-forget.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var document = tab.TryGetDocument()!;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();
        var viewport = new LayoutRect(0, 0, ViewWidth, ViewHeight);
        var first = state.GetRenderGeneration(tab, document, viewport, Scaling);

        state.Render.ForgetRequest();
        var again = state.GetRenderGeneration(tab, document, viewport, Scaling);

        await Assert.That(again).IsNotEqualTo(first);
    }

    /// <summary>Scrolling down remembers the direction, and a tiny move does not change it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ScrollDirectionIsRememberedAcrossFrames()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("render-direction.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();
        var painter = new FakeTilePainter();

        state.DrawPages(painter, Frame(tab, new(0, Shifted, ViewWidth, ViewHeight), state));
        var down = state.Render.ScrollingDown;
        state.DrawPages(painter, Frame(tab, new(0, 0, ViewWidth, ViewHeight), state));
        var up = state.Render.ScrollingDown;

        await Assert.That(down).IsTrue();
        await Assert.That(up).IsFalse();
    }

    /// <summary>Makes the frame of a viewport.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="viewport">The part of the canvas in view.</param>
    /// <param name="state">The canvas state.</param>
    /// <returns>The frame.</returns>
    private static FrameContext Frame(DocumentTabViewModel tab, in LayoutRect viewport, PageCanvasState state)
    {
        var document = tab.TryGetDocument()!;
        return new(tab, document, tab.RenderHub, viewport, Scaling, state.GetRenderGeneration(tab, document, viewport, Scaling));
    }
}
