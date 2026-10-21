// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using HyperPdf.App.Rendering;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Rendering;
using HyperPdf.Engine;

namespace HyperPdf.App.Tests;

/// <summary>Checks that a page's GPU tiles compose as one operation with exact tile lifetimes.</summary>
public sealed class TileBatchPresentationTests
{
    /// <summary>The tile side in pixels.</summary>
    private const int TileSide = 64;

    /// <summary>The number of tiles in the pixel comparison.</summary>
    private const int TileCount = 3;

    /// <summary>The window side in pixels.</summary>
    private const int WindowSide = 256;

    /// <summary>The distance between tile origins in the comparison.</summary>
    private const int TileStep = 70;

    /// <summary>Two batches of the same tiles at the same places are equal, so an unchanged scene can be skipped.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BatchesOfTheSameTilesAndBoundsAreEqual()
    {
        var first = new GpuPreparedRenderSurface(TileSide, TileSide, new GpuAvailability());
        var second = new GpuPreparedRenderSurface(TileSide, TileSide, new GpuAvailability());
        var origin = new Rect(0, 0, TileSide, TileSide);
        var beside = new Rect(TileStep, 0, TileSide, TileSide);
        using var one = Create(new TileDraw(first, origin, false), new TileDraw(second, beside, false));
        using var same = Create(new TileDraw(first, origin, false), new TileDraw(second, beside, false));
        using var moved = Create(new TileDraw(first, origin, false), new TileDraw(second, beside.Translate(new(0, 1)), false));
        using var smooth = Create(new TileDraw(first, origin, true), new TileDraw(second, beside, false));
        using var swapped = Create(new TileDraw(second, origin, false), new TileDraw(first, beside, false));
        using var shorter = Create(new TileDraw(first, origin, false));

        using (Assert.Multiple())
        {
            await Assert.That(one.Equals(same)).IsTrue();
            await Assert.That(one.Equals(moved)).IsFalse();
            await Assert.That(one.Equals(smooth)).IsFalse();
            await Assert.That(one.Equals(swapped)).IsFalse();
            await Assert.That(one.Equals(shorter)).IsFalse();
            await Assert.That(one.Equals(null)).IsFalse();
        }

        first.Dispose();
        second.Dispose();
    }

    /// <summary>A disposed batch never matches, because it has already returned its arrays.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DisposedBatchIsNotEqual()
    {
        var tile = new GpuPreparedRenderSurface(TileSide, TileSide, new GpuAvailability());
        var bounds = new Rect(0, 0, TileSide, TileSide);
        var disposed = Create(new TileDraw(tile, bounds, false));
        using var live = Create(new TileDraw(tile, bounds, false));
        disposed.Dispose();

        using (Assert.Multiple())
        {
            await Assert.That(live.Equals(disposed)).IsFalse();
            await Assert.That(disposed.Equals(live)).IsFalse();
        }

        tile.Dispose();
    }

    /// <summary>Each tile is released exactly once, even when the cache evicted it while the batch was queued.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EvictedTilesAreReleasedExactlyOnceWhenTheBatchIsDisposed()
    {
        var kept = new GpuPreparedRenderSurface(TileSide, TileSide, new GpuAvailability());
        var evicted = new GpuPreparedRenderSurface(TileSide, TileSide, new GpuAvailability());
        var operation = Create(new TileDraw(kept, new(0, 0, TileSide, TileSide), false), new TileDraw(evicted, new(TileStep, 0, TileSide, TileSide), false));

        evicted.Dispose();
        var evictedStillHeld = evicted.TryRetainForRender();
        if (evictedStillHeld)
        {
            evicted.Release();
        }

        kept.Dispose();
        operation.Dispose();
        operation.Dispose();

        // A missing release leaves the count above zero and a double release takes it below zero; both make TryRetainForRender succeed.
        using (Assert.Multiple())
        {
            await Assert.That(evictedStillHeld).IsTrue();
            await Assert.That(kept.TryRetainForRender()).IsFalse();
            await Assert.That(evicted.TryRetainForRender()).IsFalse();
        }
    }

    /// <summary>A batch draws the same pixels as software tiles when the graphics device is lost, and releases its tiles after the scene is gone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LostDeviceBatchDrawsTheSamePixelsAsSoftwareTiles()
    {
        using var test = new TestServices();
        using var document = new HyperPdfEngine().Open(test.CreateDocument("batch-pixels.pdf", 1), null);
        var lost = new GpuAvailability();
        var batched = new List<GpuPreparedRenderSurface>();
        var software = new List<GpuPreparedRenderSurface>();
        var softwareAvailability = new GpuAvailability();
        _ = softwareAvailability.MarkUnavailable();
        for (var i = 0; i < TileCount; i++)
        {
            batched.Add(Prepared(document, lost, i));
            software.Add(Prepared(document, softwareAvailability, i));
        }

        var batchedPixels = await RenderAsync(new(batched));
        var softwarePixels = await RenderAsync(new(software));

        using (Assert.Multiple())
        {
            await Assert.That(batched.TrueForAll(static tile => tile.SoftwareBitmap is not null)).IsTrue();
            await Assert.That(batchedPixels.Length).IsEqualTo(softwarePixels.Length);
            await Assert.That(batchedPixels.AsSpan().SequenceEqual(softwarePixels)).IsTrue();
        }

        foreach (var tile in batched.Concat(software))
        {
            tile.Dispose();
        }
    }

    /// <summary>Creates a batch that retains each tile once, as the page canvas does.</summary>
    /// <param name="tiles">The tiles, their destinations and sampling.</param>
    /// <returns>The operation.</returns>
    internal static GpuTileBatchOperation Create(params ReadOnlySpan<TileDraw> tiles)
    {
        var array = ArrayPool<TileDraw>.Shared.Rent(tiles.Length);
        for (var i = 0; i < tiles.Length; i++)
        {
            _ = tiles[i].Tile.TryRetain();
            array[i] = tiles[i];
        }

        return new(array, tiles.Length);
    }

    /// <summary>Prepares one tile of the generated page.</summary>
    /// <param name="document">The open PDF.</param>
    /// <param name="availability">The device state shared by the tile.</param>
    /// <param name="index">The tile index, which selects the page offset.</param>
    /// <returns>The prepared tile.</returns>
    private static GpuPreparedRenderSurface Prepared(IDocument document, GpuAvailability availability, int index)
    {
        var tile = new GpuPreparedRenderSurface(TileSide, TileSide, availability);
        var client = new RenderClient();
        var tone = PageTone.None;
        var request = new RenderRequest(
            new TileKey(1, 0, TileGrid.ToScaleKey(1), PageRotation.None, tone.Id, (short)index, 0),
            document,
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

    /// <summary>Shows a host in a window and copies the rendered frame's bytes.</summary>
    /// <param name="host">The host.</param>
    /// <returns>The frame bytes.</returns>
    private static async Task<byte[]> RenderAsync(TileHost host)
    {
        var window = new Window { Width = WindowSide, Height = WindowSide, Content = host };
        window.Show();
        try
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            using var frame = window.CaptureRenderedFrame();
            await Assert.That(frame).IsNotNull();
            using var locked = frame!.Lock();
            var bytes = new byte[locked.RowBytes * locked.Size.Height];
            Marshal.Copy(locked.Address, bytes, 0, bytes.Length);
            return bytes;
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A control that draws tiles through a batch.</summary>
    private sealed class TileHost : Control
    {
        /// <summary>The tiles to draw.</summary>
        private readonly List<GpuPreparedRenderSurface> _tiles;

        /// <summary>The batch under test.</summary>
        private readonly TileDrawBatch _batch = new();

        /// <summary>Initializes a new instance of the <see cref="TileHost"/> class.</summary>
        /// <param name="tiles">The tiles to draw.</param>
        internal TileHost(List<GpuPreparedRenderSurface> tiles) => _tiles = tiles;

        /// <inheritdoc/>
        public override void Render(DrawingContext context)
        {
            for (var i = 0; i < _tiles.Count; i++)
            {
                _batch.Draw(context, _tiles[i], new(i * TileStep, 0, TileSide, TileSide), smooth: false);
            }

            _batch.Flush(context);
        }
    }
}
