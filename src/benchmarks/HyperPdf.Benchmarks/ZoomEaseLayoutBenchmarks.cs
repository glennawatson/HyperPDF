// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Layout;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures the layout part of one wheel zoom easing frame: the pages laid out again at a slightly different scale.
/// A change that starts from the previous layout is compiled in when the Core project has it.
/// </summary>
public class ZoomEaseLayoutBenchmarks
{
    /// <summary>Every nth page is landscape.</summary>
    private const int LandscapeInterval = 10;

    /// <summary>The A4 short edge in points.</summary>
    private const float ShortEdge = 595;

    /// <summary>The A4 long edge in points.</summary>
    private const float LongEdge = 842;

    /// <summary>The viewport width.</summary>
    private const double Width = 1200;

    /// <summary>The viewport height.</summary>
    private const double Height = 900;

    /// <summary>The scale of one frame.</summary>
    private const double Scale = 1.5;

    /// <summary>The scale of the next frame.</summary>
    private const double NextScale = 1.52;

    /// <summary>The gap between pages.</summary>
    private const double Spacing = 12;

    /// <summary>The content margin.</summary>
    private const double Margin = 16;

    /// <summary>The page sizes.</summary>
    private PageSize[] _sizes = [];

    /// <summary>The layout the last frame produced.</summary>
    private DocumentLayout _layout = DocumentLayout.Empty;

    /// <summary>The scale of the next frame.</summary>
    private double _scale = Scale;

    /// <summary>Gets or sets the number of pages.</summary>
    [Params(1000, 10_000)]
    public int PageCount { get; set; }

    /// <summary>Builds the pages and the first layout.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _sizes = new PageSize[PageCount];
        for (var i = 0; i < _sizes.Length; i++)
        {
            _sizes[i] = i % LandscapeInterval == 0 ? new(LongEdge, ShortEdge) : new(ShortEdge, LongEdge);
        }

        _scale = Scale;
        _layout = DocumentLayout.Create(_sizes, CreateOptions());
    }

    /// <summary>Lays the pages out at the scale of the next easing frame.</summary>
    /// <returns>The layout.</returns>
    [Benchmark]
    public DocumentLayout LayOutZoomEaseFrame()
    {
        _scale = _scale > Scale ? Scale : NextScale;
#if HYPERPDF_FIT_EXTENTS_CACHE
        _layout = DocumentLayout.Create(_sizes, CreateOptions(), _layout);
#else
        _layout = DocumentLayout.Create(_sizes, CreateOptions());
#endif
        return _layout;
    }

    /// <summary>Creates the options for the current scale.</summary>
    /// <returns>The options.</returns>
    private LayoutOptions CreateOptions() =>
        new(PageRotation.None, PageLayoutMode.Single, _scale, Spacing, Margin, Width) { ViewportHeight = Height };
}
