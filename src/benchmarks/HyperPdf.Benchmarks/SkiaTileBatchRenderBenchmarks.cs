// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if HYPERPDF_VIEWMODELS_PROJECT
using System.Buffers;
using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Rendering;
using HyperPdf.Rendering.Skia;
using HyperPdf.TestAssets;
using SkiaSharp;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures one frame of a page's tiles through <see cref="SkiaTileBatch"/> on a raster canvas: building the batch, drawing every
/// tile from its software pixels and releasing the tiles. A loop that draws the tiles' images directly is the baseline, so the
/// difference is the cost of the batch. The compositor and the GPU path need a window and a graphics device and are not measured.
/// </summary>
public class SkiaTileBatchRenderBenchmarks
{
    /// <summary>The tile side in pixels.</summary>
    private const int TileSide = 256;

    /// <summary>The tiles drawn on each row of a page.</summary>
    private const int Columns = 6;

    /// <summary>The sampling for pixel-aligned tiles.</summary>
    private static readonly SKSamplingOptions Sharp = new(SKFilterMode.Nearest, SKMipmapMode.None);

    /// <summary>The one page document the tiles render.</summary>
    private BenchmarkPdfDocument _pdf = null!;

    /// <summary>The prepared tiles, each with its software pixels ready.</summary>
    private GpuPreparedRenderSurface[] _tiles = null!;

    /// <summary>The destination of each tile.</summary>
    private SKRect[] _bounds = null!;

    /// <summary>The bitmap the canvas draws into.</summary>
    private SKBitmap _target = null!;

    /// <summary>The canvas.</summary>
    private SKCanvas _canvas = null!;

    /// <summary>Gets or sets the number of visible tiles.</summary>
    [Params(12, 48)]
    public int VisibleTiles { get; set; }

    /// <summary>Makes the software tile a prepared tile draws from.</summary>
    /// <param name="width">The tile width.</param>
    /// <param name="height">The tile height.</param>
    /// <returns>The tile.</returns>
    public static SkiaBitmapRenderSurface SoftwareTile(int width, int height) => new(width, height);

    /// <summary>Creates the tiles with their software pixels and the canvas.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var availability = new GpuAvailability();
        _ = availability.MarkUnavailable();
        _pdf = new(TestPdf.Create(1));
        _tiles = new GpuPreparedRenderSurface[VisibleTiles];
        _bounds = new SKRect[VisibleTiles];
        for (var i = 0; i < VisibleTiles; i++)
        {
            _tiles[i] = Prepared(availability, i);
            _ = _tiles[i].EnsureSoftwareBitmap();
            _bounds[i] = SKRect.Create((i % Columns) * TileSide, (i / Columns) * TileSide, TileSide, TileSide);
        }

        _target = new(Columns * TileSide, ((VisibleTiles + Columns - 1) / Columns) * TileSide);
        _canvas = new(_target);
    }

    /// <summary>Frees the canvas and the tiles.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _canvas.Dispose();
        _target.Dispose();
        foreach (var tile in _tiles)
        {
            tile.Dispose();
        }

        _pdf.Dispose();
    }

    /// <summary>Draws each tile's software image straight onto the canvas, with no batch.</summary>
    /// <returns>The number of tiles drawn.</returns>
    [Benchmark(Baseline = true)]
    public int DrawDirect()
    {
        for (var i = 0; i < VisibleTiles; i++)
        {
            _ = _tiles[i].DrawSoftware(_canvas, _bounds[i], Sharp);
        }

        return VisibleTiles;
    }

    /// <summary>Builds a batch for the page, renders it onto the canvas and releases the tiles.</summary>
    /// <returns>The number of tiles in the batch.</returns>
    [Benchmark]
    public int RenderBatch()
    {
        var array = ArrayPool<TileDraw>.Shared.Rent(VisibleTiles);
        for (var i = 0; i < VisibleTiles; i++)
        {
            _ = _tiles[i].TryRetain();
            array[i] = new(_tiles[i], _bounds[i], false);
        }

        using var batch = new SkiaTileBatch(array, VisibleTiles);
        batch.Render(new(_canvas, null, null, SKColorType.Bgra8888));
        return batch.Count;
    }

    /// <summary>Prepares one tile of the generated page.</summary>
    /// <param name="availability">The device state shared by the tiles.</param>
    /// <param name="index">The tile index, which selects the page offset.</param>
    /// <returns>The prepared tile.</returns>
    private GpuPreparedRenderSurface Prepared(GpuAvailability availability, int index)
    {
        var tile = new GpuPreparedRenderSurface(TileSide, TileSide, availability, SoftwareTile);
        var client = new RenderClient();
        var tone = PageTone.None;
        var request = new RenderRequest(
            new TileKey(1, 0, TileGrid.ToScaleKey(1), PageRotation.None, tone.Id, (short)index, 0),
            _pdf.Document,
            new PageRenderInfo(0, 1, PageRotation.None, index * TileSide, 0, RenderFlags.Annotations),
            TileSide,
            TileSide,
            RenderPriority.Visible,
            client,
            client.Generation,
            tone);
        _ = tile.Prepare(request, CancellationToken.None);
        return tile;
    }
}
#endif
