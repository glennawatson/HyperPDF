// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>Measures dropping the tiles of one page from a cache holding 5,000 entries, as happens after an annotation.</summary>
public class TileCacheRemovePageBenchmarks
{
    /// <summary>The number of resident entries.</summary>
    private const int Resident = 5000;

    /// <summary>Tiles per page.</summary>
    private const int TilesPerPage = 16;

    /// <summary>Tile columns per page.</summary>
    private const int Columns = 4;

    /// <summary>The scale key used for every tile.</summary>
    private const int ScaleKey = 2048;

    /// <summary>The document identifier of the resident tiles.</summary>
    private const int DocumentId = 7;

    /// <summary>A page that is not in the cache, so a removal walks every entry and removes none.</summary>
    private const int MissingPage = -2;

    /// <summary>The cache.</summary>
    private readonly TileCache _cache = new(long.MaxValue);

    /// <summary>Fills the cache.</summary>
    [GlobalSetup]
    public void Setup()
    {
        for (var i = 0; i < Resident; i++)
        {
            _cache.Add(
                new(DocumentId, i / TilesPerPage, ScaleKey, PageRotation.None, 0, (short)(i % Columns), (short)(i % TilesPerPage / Columns)),
                new NullSurface());
        }
    }

    /// <summary>Releases the cached surfaces.</summary>
    [GlobalCleanup]
    public void Cleanup() => _cache.Dispose();

    /// <summary>Removes a missing page for the document and its alternate identifier with two calls, the shape DocumentTabViewModel uses today.</summary>
    /// <returns>The cache count.</returns>
    [Benchmark(Baseline = true)]
    public int RemovePageTwoCallsOf5000Entries()
    {
        _cache.RemovePage(DocumentId, MissingPage);
        _cache.RemovePage(~DocumentId, MissingPage);
        return _cache.Count;
    }

#if HYPERPDF_REMOVE_PAGE_PAIR
    /// <summary>Removes a missing page for the document and its alternate identifier with the single-walk overload.</summary>
    /// <returns>The cache count.</returns>
    [Benchmark]
    public int RemovePageOneCallOf5000Entries()
    {
        _cache.RemovePage(DocumentId, ~DocumentId, MissingPage);
        return _cache.Count;
    }
#endif

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
