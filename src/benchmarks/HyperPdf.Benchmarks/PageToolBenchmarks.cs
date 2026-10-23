// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Navigation;
using HyperPdf.Core.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures the sums behind moving around the pages: zooming to a dragged area, sizing a snapshot and one auto-scroll
/// frame, smooth and stepped. Auto-scroll runs every frame, so its step must not allocate.
/// </summary>
public class PageToolBenchmarks
{
    /// <summary>The view's width.</summary>
    private const double ViewportWidth = 1200;

    /// <summary>The view's height.</summary>
    private const double ViewportHeight = 900;

    /// <summary>The dragged area's width.</summary>
    private const double AreaWidth = 320;

    /// <summary>The dragged area's height.</summary>
    private const double AreaHeight = 180;

    /// <summary>The area's left edge on the page.</summary>
    private const double AreaLeft = 140;

    /// <summary>The area's top edge on the page.</summary>
    private const double AreaTop = 420;

    /// <summary>The middle of the area, for centring.</summary>
    private const double AreaCentre = 2400;

    /// <summary>The content's height.</summary>
    private const double Extent = 60_000;

    /// <summary>The current zoom.</summary>
    private const double Zoom = 1.25;

    /// <summary>A high density screen's scaling.</summary>
    private const double Scaling = 2;

    /// <summary>One frame at 60 frames per second.</summary>
    private const double Frame = 1D / 60D;

    /// <summary>The auto-scroll sums.</summary>
    private readonly AutoScroller _scroller = new();

    /// <summary>Works out the zoom and offset that fill the view with a dragged area.</summary>
    /// <returns>The offset, so the work is kept.</returns>
    [Benchmark]
    public double ZoomToArea()
    {
        var zoom = AreaZoom.GetZoom(Zoom, AreaWidth, AreaHeight, ViewportWidth, ViewportHeight);
        return AreaZoom.GetCentredOffset(AreaCentre * zoom / Zoom, ViewportHeight, Extent * zoom / Zoom);
    }

    /// <summary>Works out the pixels of a snapshot of a dragged area.</summary>
    /// <returns>The region.</returns>
    [Benchmark]
    public SnapshotRegion SizeSnapshot() => SnapshotRegion.Create(AreaLeft, AreaTop, AreaWidth, AreaHeight, Zoom * ZoomCalculator.PixelsPerPoint, Scaling);

    /// <summary>Moves smooth auto-scroll on one frame.</summary>
    /// <returns>The distance.</returns>
    [Benchmark]
    public double SmoothAutoScrollFrame() => _scroller.Advance(Frame, AutoScroller.DefaultSpeed, stepByLine: false);

    /// <summary>Moves stepped auto-scroll, for reduced motion, on one frame.</summary>
    /// <returns>The distance.</returns>
    [Benchmark]
    public double SteppedAutoScrollFrame() => _scroller.Advance(Frame, AutoScroller.DefaultSpeed, stepByLine: true);
}
