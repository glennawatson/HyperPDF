// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures the cache probes of one unchanged-view repaint on a 300 page document with a warm cache. The canvas itself needs a
/// window, so this drives the underlying calls: the baseline probes the distant previews and the adjacent tiles every repaint, and the
/// stamp variant compares the cache totals with the last frame's and skips them.
/// </summary>
public class PageCanvasPrefetchStampBenchmarks
{
    /// <summary>The page count.</summary>
    private const int PageCount = 300;

    /// <summary>The previews probed ahead and behind the drawn pages.</summary>
    private const int ReachPages = 32;

    /// <summary>The first drawn page.</summary>
    private const int FirstPage = 100;

    /// <summary>The document identifier.</summary>
    private const int DocumentId = 1;

    /// <summary>The cache.</summary>
    private readonly TileCache _cache = new(long.MaxValue);

    /// <summary>The cache count at the last frame.</summary>
    private int _count;

    /// <summary>The cache bytes at the last frame.</summary>
    private long _bytes;

    /// <summary>The preview bytes at the last frame.</summary>
    private long _previewBytes;

    /// <summary>Caches a preview of every page.</summary>
    [GlobalSetup]
    public void Setup()
    {
        for (var page = 0; page < PageCount; page++)
        {
            _cache.Add(TileKey.Preview(DocumentId, page, PageRotation.None, 0), new NullSurface());
        }

        _count = _cache.Count;
        _bytes = _cache.CurrentBytes;
        _previewBytes = _cache.PreviewBytes;
    }

    /// <summary>Probes the preview of every page in the reach, as each repaint did before.</summary>
    /// <returns>The previews found.</returns>
    [Benchmark(Baseline = true)]
    public int RepaintUnchangedViewProbingEveryPreview()
    {
        var found = 0;
        for (var page = FirstPage - ReachPages; page < FirstPage + ReachPages; page++)
        {
            if (_cache.TryGet(TileKey.Preview(DocumentId, page, PageRotation.None, 0), out _))
            {
                found++;
            }
        }

        return found;
    }

    /// <summary>Compares the cache totals with the last frame's and skips the probes when nothing changed.</summary>
    /// <returns>The previews found.</returns>
    [Benchmark]
    public int RepaintUnchangedViewComparingCacheTotals()
    {
        if (_count == _cache.Count && _bytes == _cache.CurrentBytes && _previewBytes == _cache.PreviewBytes)
        {
            return 0;
        }

        return RepaintUnchangedViewProbingEveryPreview();
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
