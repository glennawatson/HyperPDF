// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Layout;

namespace HyperPdf.Benchmarks;

/// <summary>Measures laying out and querying a long document.</summary>
public class LayoutBenchmarks
{
    /// <summary>The page count.</summary>
    private const int PageCount = 5000;

    /// <summary>The viewport width.</summary>
    private const double Viewport = 1200;

    /// <summary>The page in the middle of the document.</summary>
    private const int MiddlePage = PageCount / 2;

    /// <summary>The viewport height.</summary>
    private const double ViewportHeight = 900;

    /// <summary>Every nth page is landscape.</summary>
    private const int LandscapeInterval = 10;

    /// <summary>The A4 short edge in points.</summary>
    private const float ShortEdge = 595;

    /// <summary>The A4 long edge in points.</summary>
    private const float LongEdge = 842;

    /// <summary>The layout scale.</summary>
    private const double Scale = 1.5;

    /// <summary>The gap between pages.</summary>
    private const double Spacing = 12;

    /// <summary>The content margin.</summary>
    private const double Margin = 16;

    /// <summary>Half, for the middle of the document.</summary>
    private const double Half = 0.5;

    /// <summary>The page sizes.</summary>
    private readonly PageSize[] _sizes = CreateSizes();

    /// <summary>A prepared layout.</summary>
    private DocumentLayout _layout = DocumentLayout.Empty;

    /// <summary>Builds the layout used by the query benchmarks.</summary>
    [GlobalSetup]
    public void Setup() => _layout = Create();

    /// <summary>Lays out every page.</summary>
    /// <returns>The layout.</returns>
    [Benchmark]
    public DocumentLayout CreateLayout() => Create();

    /// <summary>Lays out every page one per viewport-sized slot, for page by page viewing.</summary>
    /// <returns>The layout.</returns>
    [Benchmark]
    public DocumentLayout CreatePageByPageLayout() =>
        DocumentLayout.Create(_sizes, new(PageRotation.None, PageLayoutMode.Single, Scale, Spacing, Margin, Viewport) { PageByPage = true, ViewportHeight = ViewportHeight });

    /// <summary>Starts presenting: fits a page to the screen, then lays out one page per screen.</summary>
    /// <returns>The layout.</returns>
    [Benchmark]
    public DocumentLayout EnterPresentation()
    {
        var zoom = ZoomCalculator.GetFitZoom(_sizes, new(PageRotation.None, PageLayoutMode.Single, ZoomMode.FitPage, Viewport, ViewportHeight, Spacing, Margin));
        return DocumentLayout.Create(
            _sizes,
            new(PageRotation.None, PageLayoutMode.Single, zoom * ZoomCalculator.PixelsPerPoint, Spacing, Margin, Viewport) { PageByPage = true, ViewportHeight = ViewportHeight });
    }

    /// <summary>Lays out every page in two page spreads, scrolling continuously.</summary>
    /// <returns>The layout.</returns>
    [Benchmark]
    public DocumentLayout CreateTwoPageLayout() =>
        DocumentLayout.Create(_sizes, new(PageRotation.None, PageLayoutMode.Dual, Scale, Spacing, Margin, Viewport));

    /// <summary>Fits two page spreads to the width and lays them out one spread per viewport, as Two Pages with Page by Page does.</summary>
    /// <returns>The layout.</returns>
    [Benchmark]
    public DocumentLayout EnterTwoPagesOneAtATime()
    {
        var zoom = ZoomCalculator.GetFitZoom(_sizes, new(PageRotation.None, PageLayoutMode.Dual, ZoomMode.FitWidth, Viewport, ViewportHeight, Spacing, Margin));
        return DocumentLayout.Create(
            _sizes,
            new(PageRotation.None, PageLayoutMode.Dual, zoom * ZoomCalculator.PixelsPerPoint, Spacing, Margin, Viewport) { PageByPage = true, ViewportHeight = ViewportHeight });
    }

    /// <summary>Fits the widest spread, with the cover alone, to the viewport.</summary>
    /// <returns>The zoom.</returns>
    [Benchmark]
    public double FitCoverSpreads() =>
        ZoomCalculator.GetFitZoom(_sizes, new(PageRotation.None, PageLayoutMode.DualCover, ZoomMode.FitPage, Viewport, ViewportHeight, Spacing, Margin));

    /// <summary>Finds the current page in the middle of the viewport, keeping the page asked for, as every scroll report does.</summary>
    /// <returns>The page.</returns>
    [Benchmark]
    public int FindCurrentPage() => _layout.GetCurrentPage(_layout.ExtentHeight * Half, MiddlePage);

    /// <summary>Finds the next spread from the right hand page, as the Next page command does.</summary>
    /// <returns>The page.</returns>
    [Benchmark]
    public int FindNextSpread() => PageRows.GetNextRowPage(MiddlePage + 1, PageCount, PageLayoutMode.DualCover);

    /// <summary>Finds the row a page sits in, as page by page scrolling does for every step.</summary>
    /// <returns>The row top.</returns>
    [Benchmark]
    public double FindRowExtent()
    {
        _layout.GetRowExtent(MiddlePage, out var top, out _);
        return top;
    }

    /// <summary>Finds the pages visible in a viewport halfway down the document.</summary>
    /// <returns>The first visible page.</returns>
    [Benchmark]
    public int FindVisiblePages()
    {
        var top = _layout.ExtentHeight * Half;
        _layout.GetVisiblePages(top, top + Viewport, out var first, out _);
        return first;
    }

    /// <summary>Creates mixed portrait and landscape pages.</summary>
    /// <returns>The sizes.</returns>
    private static PageSize[] CreateSizes()
    {
        var sizes = new PageSize[PageCount];
        for (var i = 0; i < sizes.Length; i++)
        {
            sizes[i] = i % LandscapeInterval == 0 ? new(LongEdge, ShortEdge) : new(ShortEdge, LongEdge);
        }

        return sizes;
    }

    /// <summary>Lays out the document.</summary>
    /// <returns>The layout.</returns>
    private DocumentLayout Create() => DocumentLayout.Create(_sizes, new(PageRotation.None, PageLayoutMode.Single, Scale, Spacing, Margin, Viewport));
}
