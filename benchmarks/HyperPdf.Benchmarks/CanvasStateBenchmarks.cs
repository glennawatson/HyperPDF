// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Layout;
using HyperPdf.Presentation.Canvas;
using HyperPdf.Presentation.Input;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures the scrolling the page canvas does on a pointer move, wheel turn or key press, through the toolkit-neutral
/// <see cref="PageCanvasState"/> and an <see cref="ICanvasHost"/> with no toolkit. The layout is built from page sizes alone, so no tab or window is needed.
/// </summary>
public class CanvasStateBenchmarks
{
    /// <summary>The pages in the layout.</summary>
    private const int PageCount = 300;

    /// <summary>The page width in points.</summary>
    private const float PageWidth = 612F;

    /// <summary>The page height in points.</summary>
    private const float PageHeight = 792F;

    /// <summary>Canvas units per point.</summary>
    private const double Scale = 1.5;

    /// <summary>The gap between pages.</summary>
    private const double Spacing = 12;

    /// <summary>The margin around the pages.</summary>
    private const double Margin = 16;

    /// <summary>The width of the viewport.</summary>
    private const double ViewWidth = 1200;

    /// <summary>The height of the viewport.</summary>
    private const double ViewHeight = 900;

    /// <summary>The pan start the pointer moves from.</summary>
    private const double PanFrom = 400;

    /// <summary>How far the pointer moves in a pan step.</summary>
    private const double PanStep = 7;

    /// <summary>How far an auto-scroll step goes.</summary>
    private const double GlideStep = 3;

    /// <summary>How many pointer moves one pan benchmark call makes.</summary>
    private const int Moves = 100;

    /// <summary>The scroll offset the page-by-page steps start from.</summary>
    private const double StartOffset = 5000;

    /// <summary>The state under test, free scrolling.</summary>
    private PageCanvasState _free = null!;

    /// <summary>The state under test, page by page.</summary>
    private PageCanvasState _paged = null!;

    /// <summary>The state under test, a horizontal row.</summary>
    private PageCanvasState _row = null!;

    /// <summary>The host of the free state.</summary>
    private BenchmarkHost _freeHost = null!;

    /// <summary>The host of the paged state.</summary>
    private BenchmarkHost _pagedHost = null!;

    /// <summary>The host of the horizontal state.</summary>
    private BenchmarkHost _rowHost = null!;

    /// <summary>Builds the three layouts.</summary>
    [GlobalSetup]
    public void Setup()
    {
        PageSize[] sizes = [.. Enumerable.Repeat(new PageSize(PageWidth, PageHeight), PageCount)];
        (_free, _freeHost) = Create(sizes, Options(false, ScrollMode.Vertical));
        (_paged, _pagedHost) = Create(sizes, Options(true, ScrollMode.Vertical));
        (_row, _rowHost) = Create(sizes, Options(false, ScrollMode.Horizontal));
    }

    /// <summary>Moves the pages with the pointer for a hundred moves.</summary>
    /// <returns>The last vertical offset.</returns>
    [Benchmark]
    public double PanMoves()
    {
        _free.BeginPan(new(PanFrom, PanFrom));
        for (var move = 1; move <= Moves; move++)
        {
            _ = _free.ContinuePan(new(PanFrom + (move * PanStep), PanFrom + (move * PanStep)));
        }

        _free.Tools.Panning = false;
        return _freeHost.Offset.Y;
    }

    /// <summary>Steps the auto-scroll a hundred times.</summary>
    /// <returns>The vertical offset.</returns>
    [Benchmark]
    public double AutoScrollSteps()
    {
        _freeHost.Offset = default;
        for (var step = 0; step < Moves; step++)
        {
            _ = _free.AutoScrollBy(GlideStep);
        }

        return _freeHost.Offset.Y;
    }

    /// <summary>Turns the page a hundred times, as Page Down does page by page.</summary>
    /// <returns>The vertical offset.</returns>
    [Benchmark]
    public double PageByPageKeys()
    {
        _pagedHost.Offset = new(0, StartOffset);
        for (var step = 0; step < Moves; step++)
        {
            _ = _paged.HandlePageByPageKey(ViewerKey.PageDown, ViewerModifiers.None);
        }

        return _pagedHost.Offset.Y;
    }

    /// <summary>Moves a horizontal row sideways a hundred times.</summary>
    /// <returns>The horizontal offset.</returns>
    [Benchmark]
    public double SidewaysSteps()
    {
        _rowHost.Offset = default;
        for (var step = 0; step < Moves; step++)
        {
            _ = _row.StepSideways(GlideStep);
        }

        return _rowHost.Offset.X;
    }

    /// <summary>Finds the pages in view a hundred times, as every repaint does.</summary>
    /// <returns>The last first page.</returns>
    [Benchmark]
    public int VisiblePages()
    {
        var first = 0;
        for (var step = 0; step < Moves; step++)
        {
            _free.GetPagesBetween(0, ViewWidth, StartOffset + step, StartOffset + step + ViewHeight, out first, out _);
        }

        return first;
    }

    /// <summary>Makes the layout options of a benchmark.</summary>
    /// <param name="pageByPage">Whether pages are shown one row at a time.</param>
    /// <param name="scroll">The scroll direction.</param>
    /// <returns>The options.</returns>
    private static LayoutOptions Options(bool pageByPage, ScrollMode scroll) =>
        new(PageRotation.None, PageLayoutMode.Single, Scale, Spacing, Margin, ViewWidth) { PageByPage = pageByPage, ViewportHeight = ViewHeight, Scroll = scroll };

    /// <summary>Makes a canvas state over a layout and the host it works through.</summary>
    /// <param name="sizes">The page sizes.</param>
    /// <param name="options">The layout options.</param>
    /// <returns>The state and its host.</returns>
    private static StateAndHost Create(PageSize[] sizes, in LayoutOptions options)
    {
        var layout = DocumentLayout.Create(sizes, options);
        var host = new BenchmarkHost { Viewport = new(ViewWidth, ViewHeight), Extent = new(layout.ExtentWidth, layout.ExtentHeight) };
        var state = new PageCanvasState(host);
        state.UseSizes(sizes);
        state.UseLayout(layout, sizes);
        return new(state, host);
    }

    /// <summary>A canvas state and the host it works through.</summary>
    /// <param name="State">The state.</param>
    /// <param name="Host">The host.</param>
    private readonly record struct StateAndHost(PageCanvasState State, BenchmarkHost Host);

    /// <summary>A scrolling area with no toolkit behind it.</summary>
    private sealed class BenchmarkHost : ICanvasHost
    {
        /// <summary>Gets or sets the scroll offset.</summary>
        public CanvasPoint Offset { get; set; }

        /// <summary>Gets or sets the viewport size.</summary>
        public CanvasSize Viewport { get; set; }

        /// <summary>Gets or sets the extent size.</summary>
        public CanvasSize Extent { get; set; }

        /// <inheritdoc/>
        public bool HasScroller => true;

        /// <inheritdoc/>
        public bool IsInWindow => false;

        /// <inheritdoc/>
        public CanvasPoint ScrollOffset
        {
            get => Offset;
            set => Offset = value;
        }

        /// <inheritdoc/>
        public CanvasSize ViewportSize => Viewport;

        /// <inheritdoc/>
        public CanvasSize ExtentSize => Extent;

        /// <inheritdoc/>
        public CanvasSize ScrollerSize => Viewport;

        /// <inheritdoc/>
        public void InvalidateMeasure()
        {
        }

        /// <inheritdoc/>
        public void InvalidateVisual()
        {
        }

        /// <inheritdoc/>
        public void UpdateLayout()
        {
        }

        /// <inheritdoc/>
        public void PageScaleChanged(double before, double after)
        {
        }

        /// <inheritdoc/>
        public void WheelZoomChanged(WheelZoomEase before, WheelZoomEase after)
        {
        }
    }
}
