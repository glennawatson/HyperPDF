// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>Measures the tile cache lookups made for every visible tile on every frame.</summary>
public class TileCacheBenchmarks
{
    /// <summary>The number of cached tiles.</summary>
    private const int Tiles = 256;

    /// <summary>Tiles per page.</summary>
    private const int TilesPerPage = 16;

    /// <summary>Tile columns per page.</summary>
    private const int Columns = 4;

    /// <summary>The scale key used for every tile.</summary>
    private const int ScaleKey = 2048;

    /// <summary>The cache.</summary>
    private readonly TileCache _cache = new(long.MaxValue);

    /// <summary>Fills the cache.</summary>
    [GlobalSetup]
    public void Setup()
    {
        for (var i = 0; i < Tiles; i++)
        {
            _cache.Add(Key(i), new NullSurface());
        }
    }

    /// <summary>Looks up every tile once.</summary>
    /// <returns>The number found.</returns>
    [Benchmark]
    public int LookupAll()
    {
        var found = 0;
        for (var i = 0; i < Tiles; i++)
        {
            if (_cache.TryGet(Key(i), out _))
            {
                found++;
            }
        }

        return found;
    }

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
