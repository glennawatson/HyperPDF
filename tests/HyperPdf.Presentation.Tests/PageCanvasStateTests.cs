// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Layout;
using HyperPdf.ViewModels.Tests;

namespace HyperPdf.Presentation.Tests;

/// <summary>Checks the page canvas lays out, scrolls and zooms through a host with no toolkit behind it.</summary>
public sealed class PageCanvasStateTests
{
    /// <summary>The page count of generated documents.</summary>
    private const int Pages = 3;

    /// <summary>The width of the scrolling area.</summary>
    private const double ViewWidth = 800;

    /// <summary>The height of the scrolling area.</summary>
    private const double ViewHeight = 600;

    /// <summary>The width of a row of pages laid out side by side.</summary>
    private const double WideExtent = 2000;

    /// <summary>How far the test moves a horizontal layout.</summary>
    private const double SidewaysStep = 100;

    /// <summary>A step that goes past the end of the row.</summary>
    private const double PastTheEnd = 5000;

    /// <summary>How close two zooms must be to count as the same.</summary>
    private const double Tolerance = 1e-9;

    /// <summary>The zoom multiplier of a wheel test.</summary>
    private const double ZoomFactor = 2;

    /// <summary>How long ago the settled ease started, in stopwatch ticks.</summary>
    private const long LongAgo = 1_000_000_000_000;

    /// <summary>Laying out puts every page in the layout and asks the toolkit to measure again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RebuildLaysOutThePages()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("canvas-layout.pdf", Pages)]);
        var (host, state) = Create(main.SelectedTab!);

        state.RebuildLayout();

        await Assert.That(state.Layout.PageCount).IsEqualTo(Pages);
        await Assert.That(host.Measures).IsGreaterThan(0);
        await Assert.That(host.Redraws).IsGreaterThan(0);
    }

    /// <summary>With no scrolling area there is no layout to show.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NoScrollerGivesAnEmptyLayout()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("canvas-empty.pdf", Pages)]);
        var (host, state) = Create(main.SelectedTab!);
        host.HasScroller = false;

        state.RebuildLayout();

        await Assert.That(state.Layout.PageCount).IsEqualTo(0);
    }

    /// <summary>A horizontal layout moves sideways, stopping at the end of the row.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SidewaysMovesAHorizontalLayoutAndStopsAtTheEnd()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("canvas-sideways.pdf", Pages)]);
        var tab = main.SelectedTab!;
        tab.ScrollMode = ScrollMode.Horizontal;
        var (host, state) = Create(tab);
        host.ExtentSize = new(WideExtent, ViewHeight);
        state.RebuildLayout();

        var moved = state.StepSideways(SidewaysStep);
        var first = host.ScrollOffset.X;
        var pastEnd = state.StepSideways(PastTheEnd);

        await Assert.That(moved).IsTrue();
        await Assert.That(first).IsEqualTo(SidewaysStep);
        await Assert.That(pastEnd).IsTrue();
        await Assert.That(host.ScrollOffset.X).IsEqualTo(WideExtent - ViewWidth);
    }

    /// <summary>A vertical layout does not move sideways.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task VerticalLayoutDoesNotMoveSideways()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("canvas-vertical.pdf", Pages)]);
        var (host, state) = Create(main.SelectedTab!);
        state.RebuildLayout();

        await Assert.That(state.StepSideways(SidewaysStep)).IsFalse();
        await Assert.That(host.ScrollOffset.X).IsEqualTo(0);
    }

    /// <summary>The page-by-page keys are left alone while the document scrolls freely.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageByPageKeysNeedPageByPage()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("canvas-keys.pdf", Pages)]);
        var tab = main.SelectedTab!;
        tab.IsPageByPage = false;
        var (_, state) = Create(tab);
        state.RebuildLayout();

        await Assert.That(state.HandlePageByPageKey(ViewerKey.PageDown, ViewerModifiers.None)).IsFalse();
        await Assert.That(state.StepPageByPage(1)).IsFalse();
    }

    /// <summary>A modifier other than Shift hands the key back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageByPageKeysIgnoreOtherModifiers()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("canvas-modifiers.pdf", Pages)]);
        var tab = main.SelectedTab!;
        tab.IsPageByPage = true;
        var (_, state) = Create(tab);
        state.RebuildLayout();

        await Assert.That(state.HandlePageByPageKey(ViewerKey.PageDown, ViewerModifiers.Control)).IsFalse();
    }

    /// <summary>Page by page, Page Down moves the view on to the next page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageDownMovesToTheNextPage()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("canvas-next.pdf", Pages)]);
        var tab = main.SelectedTab!;
        tab.IsPageByPage = true;
        var (host, state) = Create(tab);
        state.RebuildLayout();

        var handled = state.HandlePageByPageKey(ViewerKey.PageDown, ViewerModifiers.None);

        await Assert.That(handled).IsTrue();
        await Assert.That(host.ScrollOffset.Y).IsGreaterThan(0);
    }

    /// <summary>With no window to run frames in, a wheel zoom applies at once.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WheelZoomAppliesAtOnceOutsideAWindow()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("canvas-wheel.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var (host, state) = Create(tab);
        state.RebuildLayout();
        var before = tab.Zoom;

        state.ZoomByWheel(tab, ZoomFactor, new(0, 0));

        await Assert.That(tab.Zoom).IsGreaterThan(before);
        await Assert.That(state.IsZoomEasing).IsFalse();
        await Assert.That(host.EaseChanges).IsEqualTo(0);
    }

    /// <summary>In a window, a wheel zoom starts an ease towards the new zoom and leaves the zoom for the frames to move.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WheelZoomEasesInAWindow()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("canvas-ease.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var (host, state) = Create(tab);
        host.IsInWindow = true;
        state.RebuildLayout();
        var before = tab.Zoom;

        state.ZoomByWheel(tab, ZoomFactor, new(0, 0));

        await Assert.That(state.IsZoomEasing).IsEqualTo(!tab.ReduceMotion);
        await Assert.That(tab.ReduceMotion || Math.Abs(tab.Zoom - before) < Tolerance).IsTrue();
    }

    /// <summary>A wheel zoom with no scrolling area does nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WheelZoomNeedsAScroller()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("canvas-noscroll.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var (host, state) = Create(tab);
        host.HasScroller = false;
        var before = tab.Zoom;

        state.ZoomByWheel(tab, ZoomFactor, new(0, 0));

        await Assert.That(tab.Zoom).IsEqualTo(before);
    }

    /// <summary>An ease whose time is up settles at its target and stops.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EaseSettlesAtItsTarget()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("canvas-settle.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var (_, state) = Create(tab);
        state.RebuildLayout();
        var target = ZoomCalculator.Clamp(tab.Zoom * ZoomFactor);
        var ease = new WheelZoomEase(true, tab.Zoom, target, Stopwatch.GetTimestamp() - LongAgo, new(0, 0));
        state.WheelZoom = ease;

        state.StepZoom(ease);

        await Assert.That(state.IsZoomEasing).IsFalse();
        await Assert.That(tab.Zoom).IsEqualTo(target);
    }

    /// <summary>Makes a canvas state for a tab, in a scrolling area of the test size.</summary>
    /// <param name="tab">The tab.</param>
    /// <returns>The host and the state working through it.</returns>
    private static CanvasUnderTest Create(DocumentTabViewModel tab)
    {
        var view = new CanvasSize(ViewWidth, ViewHeight);
        var host = new FakeCanvasHost { ViewportSize = view, ScrollerSize = view, ExtentSize = view };
        return new(host, new(host) { Tab = tab });
    }

    /// <summary>A canvas state and the host it works through.</summary>
    /// <param name="Host">The host.</param>
    /// <param name="State">The state.</param>
    private readonly record struct CanvasUnderTest(FakeCanvasHost Host, PageCanvasState State);
}
