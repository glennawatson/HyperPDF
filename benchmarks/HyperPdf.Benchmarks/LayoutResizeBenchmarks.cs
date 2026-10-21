// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Layout;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures what the page canvas does when the window is resized: the fit zoom and then the layout. A change that lays out
/// again, caches the page measurements or skips the layout is compiled in when the Core project has it, so one benchmark
/// definition runs against the baseline and the change.
/// </summary>
public class LayoutResizeBenchmarks
{
    /// <summary>Every nth page is landscape.</summary>
    private const int LandscapeInterval = 10;

    /// <summary>The A4 short edge in points.</summary>
    private const float ShortEdge = 595;

    /// <summary>The A4 long edge in points.</summary>
    private const float LongEdge = 842;

    /// <summary>The viewport width before a resize.</summary>
    private const double Width = 1200;

    /// <summary>The viewport height before a resize.</summary>
    private const double Height = 900;

    /// <summary>One device pixel, the size of each resize step.</summary>
    private const double Step = 1;

    /// <summary>The gap between pages.</summary>
    private const double Spacing = 12;

    /// <summary>The content margin.</summary>
    private const double Margin = 16;

#if HYPERPDF_FIT_EXTENTS_CACHE
    /// <summary>The page measurements the fit zoom comes from.</summary>
    private readonly PageFitExtentsCache _extents = new();

#endif
    /// <summary>The page sizes.</summary>
    private PageSize[] _sizes = [];

    /// <summary>The layout the last rebuild produced, which the next one starts from.</summary>
    private DocumentLayout _layout = DocumentLayout.Empty;

    /// <summary>The viewport width of the next rebuild.</summary>
    private double _width = Width;

    /// <summary>The viewport height of the next rebuild.</summary>
    private double _height = Height;

    /// <summary>Gets or sets the number of pages.</summary>
    [Params(100, 5000, 50_000)]
    public int PageCount { get; set; }

    /// <summary>Gets or sets the zoom mode of the rebuilds.</summary>
    [Params(ZoomMode.FitWidth, ZoomMode.Free)]
    public ZoomMode Zoom { get; set; }

    /// <summary>Builds the pages and the first layout.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _sizes = new PageSize[PageCount];
        for (var i = 0; i < _sizes.Length; i++)
        {
            _sizes[i] = i % LandscapeInterval == 0 ? new(LongEdge, ShortEdge) : new(ShortEdge, LongEdge);
        }

        _width = Width;
        _height = Height;
        _layout = Rebuild();
    }

    /// <summary>Lays the pages out again after the window width changed by a step.</summary>
    /// <returns>The layout.</returns>
    [Benchmark]
    public DocumentLayout RebuildLayoutAfterWidthChange()
    {
        _width = _width > Width ? Width : Width + Step;
        _layout = Rebuild();
        return _layout;
    }

    /// <summary>Lays the pages out again after the window height changed by a step.</summary>
    /// <returns>The layout.</returns>
    [Benchmark]
    public DocumentLayout RebuildLayoutAfterHeightChange()
    {
        _height = _height > Height ? Height : Height + Step;
        _layout = Rebuild();
        return _layout;
    }

    /// <summary>Finds the fit width zoom for the current window.</summary>
    /// <returns>The zoom.</returns>
    [Benchmark]
    public double GetFitZoom() => FitZoom();

    /// <summary>Finds the fit zoom and lays the pages out, as the canvas does for a resize.</summary>
    /// <returns>The layout.</returns>
    private DocumentLayout Rebuild()
    {
        var zoom = Zoom == ZoomMode.Free ? 1.0 : FitZoom();
        var options = new LayoutOptions(PageRotation.None, PageLayoutMode.Single, zoom * ZoomCalculator.PixelsPerPoint, Spacing, Margin, _width) { ViewportHeight = _height };
#if HYPERPDF_FIT_EXTENTS_CACHE
        return _layout.Matches(options) ? _layout : DocumentLayout.Create(_sizes, options, _layout);
#else
        return DocumentLayout.Create(_sizes, options);
#endif
    }

    /// <summary>Gets the fit width zoom.</summary>
    /// <returns>The zoom.</returns>
    private double FitZoom()
    {
        var parameters = new FitParameters(PageRotation.None, PageLayoutMode.Single, ZoomMode.FitWidth, _width, _height, Spacing, Margin);
#if HYPERPDF_FIT_EXTENTS_CACHE
        return ZoomCalculator.GetFitZoom(_extents.Get(_sizes, PageRotation.None, PageLayoutMode.Single), parameters);
#else
        return ZoomCalculator.GetFitZoom(_sizes, parameters);
#endif
    }
}
