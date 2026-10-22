// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Rendering;
using HyperPdf.Engine;
using HyperPdf.Rendering.Skia;
using SkiaSharp;

namespace HyperPdf.App.Tests;

/// <summary>Tests the Skia tile batch with a plain raster canvas, so no UI toolkit is involved.</summary>
public sealed class SkiaTileBatchTests
{
    /// <summary>The tile side in pixels.</summary>
    private const int TileSide = 64;

    /// <summary>The far edge of two tiles laid out corner to corner.</summary>
    private const int FarEdge = TileSide * TileCount;

    /// <summary>The tiles in the bounds test.</summary>
    private const int TileCount = 2;

    /// <summary>Without a graphics context the batch draws every tile from software pixels and the tile stays usable.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RasterTargetDrawsSoftwarePixels()
    {
        using var test = new TestServices();
        using var document = new HyperPdfEngine().Open(test.CreateDocument("skia-batch.pdf", 1), null);
        var tile = Prepared(document);
        using var batch = Create(new TileDraw(tile, new(0, 0, TileSide, TileSide), false));
        using var bitmap = new SKBitmap(TileSide, TileSide);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        batch.Render(new(canvas, null, null, SKColorType.Bgra8888));

        var drawn = Array.Exists(bitmap.Pixels, static pixel => pixel.Alpha != 0);
        using (Assert.Multiple())
        {
            await Assert.That(drawn).IsTrue();
            await Assert.That(tile.IsGpuUnavailable).IsTrue();
        }

        tile.Dispose();
    }

    /// <summary>An unavailable canvas marks the tiles so the next frame draws from software pixels.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnavailableTargetMarksTilesAsGpuUnavailable()
    {
        var tile = new GpuPreparedRenderSurface(TileSide, TileSide, new GpuAvailability(), TestTiles.Software);
        using var batch = Create(new TileDraw(tile, new(0, 0, TileSide, TileSide), false));

        batch.RenderUnavailable();

        await Assert.That(tile.IsGpuUnavailable).IsTrue();
        tile.Dispose();
    }

    /// <summary>A disposed batch ignores a render and releases its tiles once.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DisposedBatchIgnoresRenderAndReleasesOnce()
    {
        var tile = new GpuPreparedRenderSurface(TileSide, TileSide, new GpuAvailability(), TestTiles.Software);
        var batch = Create(new TileDraw(tile, new(0, 0, TileSide, TileSide), false));
        using var bitmap = new SKBitmap(TileSide, TileSide);
        using var canvas = new SKCanvas(bitmap);

        batch.Dispose();
        batch.Dispose();
        batch.Render(new(canvas, null, null, SKColorType.Bgra8888));
        batch.RenderUnavailable();
        tile.Dispose();

        using (Assert.Multiple())
        {
            await Assert.That(tile.IsGpuUnavailable).IsFalse();
            await Assert.That(tile.TryRetainForRender()).IsFalse();
        }
    }

    /// <summary>The batch reports the union of its tile bounds and its count.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BoundsCoverEveryTile()
    {
        var first = new GpuPreparedRenderSurface(TileSide, TileSide, new GpuAvailability(), TestTiles.Software);
        var second = new GpuPreparedRenderSurface(TileSide, TileSide, new GpuAvailability(), TestTiles.Software);
        using var batch = Create(
            new TileDraw(first, new(0, 0, TileSide, TileSide), false),
            new TileDraw(second, new(TileSide, TileSide, FarEdge, FarEdge), true));

        using (Assert.Multiple())
        {
            await Assert.That(batch.Count).IsEqualTo(TileCount);
            await Assert.That(batch.Bounds).IsEqualTo(new(0, 0, FarEdge, FarEdge));
        }

        first.Dispose();
        second.Dispose();
    }

    /// <summary>Creates a batch that retains each tile once.</summary>
    /// <param name="tiles">The tiles.</param>
    /// <returns>The batch.</returns>
    private static SkiaTileBatch Create(params ReadOnlySpan<TileDraw> tiles)
    {
        var array = ArrayPool<TileDraw>.Shared.Rent(tiles.Length);
        for (var i = 0; i < tiles.Length; i++)
        {
            _ = tiles[i].Tile.TryRetain();
            array[i] = tiles[i];
        }

        return new(array, tiles.Length);
    }

    /// <summary>Prepares a tile on an unavailable device, so it draws from software pixels.</summary>
    /// <param name="document">The open PDF.</param>
    /// <returns>The prepared tile.</returns>
    private static GpuPreparedRenderSurface Prepared(IDocument document)
    {
        var availability = new GpuAvailability();
        _ = availability.MarkUnavailable();
        var tile = new GpuPreparedRenderSurface(TileSide, TileSide, availability, TestTiles.Software);
        var client = new RenderClient();
        var tone = PageTone.None;
        var request = new RenderRequest(
            new TileKey(1, 0, TileGrid.ToScaleKey(1), PageRotation.None, tone.Id, 0, 0),
            document,
            new PageRenderInfo(0, 1, PageRotation.None, 0, 0, RenderFlags.Annotations),
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
