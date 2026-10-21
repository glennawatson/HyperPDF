// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>Measures the tile cache work of one repaint: probing the visible, prefetched and preview tiles of a full cache.</summary>
public class TileCacheFrameLookupBenchmarks
{
    /// <summary>The number of resident tiles.</summary>
    private const int Resident = 300;

    /// <summary>The number of keys probed per frame.</summary>
    private const int Probes = 150;

    /// <summary>The distance between probed tile indexes, so the probes cover every other resident tile.</summary>
    private const int Stride = 2;

    /// <summary>Tiles per page.</summary>
    private const int TilesPerPage = 16;

    /// <summary>Tile columns per page.</summary>
    private const int Columns = 4;

    /// <summary>The scale key used for every tile.</summary>
    private const int ScaleKey = 2048;

    /// <summary>The cache.</summary>
    private readonly TileCache _cache = new(long.MaxValue);

    /// <summary>The keys probed each frame.</summary>
    private readonly TileKey[] _probes = new TileKey[Probes];

    /// <summary>Fills the cache and builds the probe keys.</summary>
    [GlobalSetup]
    public void Setup()
    {
        for (var i = 0; i < Resident; i++)
        {
            _cache.Add(Key(i), new NullSurface());
        }

        for (var i = 0; i < Probes; i++)
        {
            _probes[i] = Key(i * Stride);
        }
    }

    /// <summary>Releases the cached surfaces.</summary>
    [GlobalCleanup]
    public void Cleanup() => _cache.Dispose();

    /// <summary>Probes 150 keys of a cache holding 300 tiles with TryGet.</summary>
    /// <returns>The number found.</returns>
    [Benchmark]
    public int TryGetProbes150Of300()
    {
        var found = 0;
        foreach (var key in _probes)
        {
            if (_cache.TryGet(key, out _))
            {
                found++;
            }
        }

        return found;
    }

    /// <summary>Checks a four by four tile window with Covers.</summary>
    /// <returns><see langword="true"/> when the window is covered.</returns>
    [Benchmark]
    public bool CoversSixteenTileWindow() => _cache.Covers(Key(0), new(0, Columns - 1, 0, (TilesPerPage / Columns) - 1));

    /// <summary>Creates a key.</summary>
    /// <param name="index">The tile index.</param>
    /// <returns>The key.</returns>
    private static TileKey Key(int index) => new(1, index / TilesPerPage, ScaleKey, PageRotation.None, 0, (short)(index % Columns), (short)(index % TilesPerPage / Columns));

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
