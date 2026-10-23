// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures the library work behind one scrolled frame of the page canvas: finding the visible pages, working out each
/// page's tile window, checking the tiles cover it and looking every tile up.
/// </summary>
public class ScrollFrameBenchmarks
{
    /// <summary>The page count.</summary>
    private const int PageCount = 500;

    /// <summary>The viewport width.</summary>
    private const double ViewportWidth = 1200;

    /// <summary>The viewport height.</summary>
    private const double ViewportHeight = 900;

    /// <summary>The layout scale, 150% zoom in device pixels per point.</summary>
    private const double Scale = 2;

    /// <summary>The gap between pages.</summary>
    private const double Spacing = 12;

    /// <summary>The content margin.</summary>
    private const double Margin = 16;

    /// <summary>The distance scrolled per frame, a fast wheel flick at 60 frames a second.</summary>
    private const double ScrollStep = 37;

    /// <summary>The A4 short edge in points.</summary>
    private const float ShortEdge = 595;

    /// <summary>The A4 long edge in points.</summary>
    private const float LongEdge = 842;

    /// <summary>The document identifier.</summary>
    private const int DocumentId = 1;

    /// <summary>The cache.</summary>
    private readonly TileCache _cache = new(long.MaxValue);

    /// <summary>The page sizes.</summary>
    private readonly PageSize[] _sizes = CreateSizes();

    /// <summary>The layout.</summary>
    private DocumentLayout _layout = DocumentLayout.Empty;

    /// <summary>The tile scale.</summary>
    private float _scale;

    /// <summary>The current scroll offset.</summary>
    private double _offset;

    /// <summary>Lays out the document and caches every tile, as after the reader has seen each page.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _layout = DocumentLayout.Create(_sizes, new(PageRotation.None, PageLayoutMode.Single, Scale, Spacing, Margin, ViewportWidth));
        _scale = (float)Scale;
        var scaleKey = TileGrid.ToScaleKey(_scale);
        for (var page = 0; page < PageCount; page++)
        {
            TileGrid.GetPagePixelSize(_sizes[page], PageRotation.None, _scale, out var width, out var height);
            TileGrid.GetTileCounts(width, height, out var columns, out var rows);
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    _cache.Add(new(DocumentId, page, scaleKey, PageRotation.None, 0, (short)column, (short)row), new NullSurface());
                }
            }
        }
    }

    /// <summary>Scrolls one step and does a frame's tile work for the visible pages.</summary>
    /// <returns>The tiles found.</returns>
    [Benchmark]
    public int ScrollFrame()
    {
        _offset += ScrollStep;
        if (_offset > _layout.ExtentHeight - ViewportHeight)
        {
            _offset = 0;
        }

        var top = _offset;
        var bottom = _offset + ViewportHeight;
        var scaleKey = TileGrid.ToScaleKey(_scale);
        _layout.GetVisiblePages(top, bottom, out var first, out var last);
        var found = 0;
        for (var page = first; page <= last && page >= 0; page++)
        {
            var bounds = _layout.GetPageBounds(page);
            TileGrid.GetPagePixelSize(_sizes[page], PageRotation.None, _scale, out var width, out var height);
            var window = TileGrid.GetTileWindow(width, height, 0, Math.Max(0, top - bounds.Y), width, Math.Min(bounds.Height, bottom - bounds.Y));
            var key = new TileKey(DocumentId, page, scaleKey, PageRotation.None, 0, 0, 0);
            if (!_cache.Covers(key, window))
            {
                continue;
            }

            for (var row = window.FirstRow; row <= window.LastRow; row++)
            {
                for (var column = window.FirstColumn; column <= window.LastColumn; column++)
                {
                    if (_cache.TryGet(key with { Column = (short)column, Row = (short)row }, out _))
                    {
                        found++;
                    }
                }
            }
        }

        return found;
    }

    /// <summary>Creates A4 portrait page sizes.</summary>
    /// <returns>The sizes.</returns>
    private static PageSize[] CreateSizes()
    {
        var sizes = new PageSize[PageCount];
        Array.Fill(sizes, new(ShortEdge, LongEdge));
        return sizes;
    }

    /// <summary>A surface with no pixels.</summary>
    private sealed class NullSurface : IRenderSurface
    {
        /// <inheritdoc/>
        public int Width => 1;

        /// <inheritdoc/>
        public int Height => 1;

        /// <inheritdoc/>
        public long ByteSize => 1;

        /// <inheritdoc/>
        public bool Write<TState>(in TState state, SurfaceWriter<TState> writer) => false;

        /// <inheritdoc/>
        public void Dispose()
        {
        }
    }
}
