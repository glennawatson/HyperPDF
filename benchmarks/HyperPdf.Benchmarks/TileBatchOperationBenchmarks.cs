// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using Avalonia;
using BenchmarkDotNet.Attributes;
using HyperPdf.App.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures building, comparing and releasing the scene operations for a page's visible GPU tiles: one operation per tile
/// against one operation per page. The compositor call itself needs a GPU and is not part of this measurement.
/// </summary>
public class TileBatchOperationBenchmarks
{
    /// <summary>The tile side in pixels.</summary>
    private const int TileSide = 512;

    /// <summary>The tiles drawn on each row of a page.</summary>
    private const int Columns = 6;

    /// <summary>The prepared tiles, never drawn.</summary>
    private GpuPreparedRenderSurface[] _tiles = null!;

    /// <summary>The destination of each tile.</summary>
    private Rect[] _bounds = null!;

    /// <summary>A page-sized operation to compare against.</summary>
    private GpuTileBatchOperation _previous = null!;

    /// <summary>Gets or sets the number of visible tiles.</summary>
    [Params(12, 48)]
    public int VisibleTiles { get; set; }

    /// <summary>Creates the tiles and an operation for the equality comparison.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _tiles = new GpuPreparedRenderSurface[VisibleTiles];
        _bounds = new Rect[VisibleTiles];
        for (var i = 0; i < VisibleTiles; i++)
        {
            _tiles[i] = new(TileSide, TileSide, new(), SoftwareTile);
            _bounds[i] = new((i % Columns) * TileSide, (i / Columns) * TileSide, TileSide, TileSide);
        }

        _previous = BuildPageOperation();
    }

    /// <summary>Releases the operation and the tiles.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _previous.Dispose();
        foreach (var tile in _tiles)
        {
            tile.Dispose();
        }
    }

    /// <summary>Builds and releases one operation per tile, as each frame did before.</summary>
    /// <returns>The number of tiles queued.</returns>
    [Benchmark(Baseline = true)]
    public int BuildAndReleaseOneOperationPerTile()
    {
        for (var i = 0; i < VisibleTiles; i++)
        {
            using var operation = Build(i, 1);
        }

        return VisibleTiles;
    }

    /// <summary>Builds and releases one operation for all the tiles of the page.</summary>
    /// <returns>The number of tiles queued.</returns>
    [Benchmark]
    public int BuildAndReleaseOneOperationPerPage()
    {
        using var operation = BuildPageOperation();
        return operation.Count;
    }

    /// <summary>Compares the page operation with the one from the previous frame, which lets the scene keep an unchanged page.</summary>
    /// <returns>Whether the operations are equal.</returns>
    [Benchmark]
    public bool CompareUnchangedPageOperation()
    {
        using var current = BuildPageOperation();
        return current.Equals(_previous);
    }

    /// <summary>Makes the software tile a prepared tile falls back to.</summary>
    /// <param name="width">The tile width.</param>
    /// <param name="height">The tile height.</param>
    /// <returns>The tile.</returns>
    private static AvaloniaRenderSurface SoftwareTile(int width, int height) => new(width, height);

    /// <summary>Builds an operation over a run of tiles, retaining each once.</summary>
    /// <param name="start">The first tile.</param>
    /// <param name="count">The number of tiles.</param>
    /// <returns>The operation.</returns>
    private GpuTileBatchOperation Build(int start, int count)
    {
        var array = ArrayPool<TileDraw>.Shared.Rent(count);
        for (var i = 0; i < count; i++)
        {
            _ = _tiles[start + i].TryRetain();
            array[i] = new(_tiles[start + i], _bounds[start + i], false);
        }

        return new(array, count);
    }

    /// <summary>Builds the operation for every visible tile.</summary>
    /// <returns>The operation.</returns>
    private GpuTileBatchOperation BuildPageOperation() => Build(0, VisibleTiles);
}
