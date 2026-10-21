// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using SkiaSharp;

namespace HyperPdf.Rendering.Skia;

/// <summary>Draws the prepared tiles of one page onto a Skia canvas the render head provides, and owns the tiles until it is disposed.</summary>
[DebuggerDisplay("SkiaTileBatch: {Count} tiles in {Bounds}")]
public sealed class SkiaTileBatch : IDisposable, IEquatable<SkiaTileBatch>
{
    /// <summary>The batch accepts a render and has not been disposed.</summary>
    private const int Live = 0;

    /// <summary>A render is running.</summary>
    private const int Rendering = 1;

    /// <summary>The batch was disposed and released its tiles.</summary>
    private const int Disposed = 2;

    /// <summary>The batch was disposed during a render, which releases the tiles when it ends.</summary>
    private const int DisposedWhileRendering = 3;

    /// <summary>Sampling for scaled previews and zoom stand-ins.</summary>
    private static readonly SKSamplingOptions SmoothSampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    /// <summary>Sampling for scaled software previews; a raster canvas rebuilds mipmaps on every draw, which cost 14 times as much in the benchmark.</summary>
    private static readonly SKSamplingOptions SoftwareSmoothSampling = new(SKFilterMode.Linear, SKMipmapMode.None);

    /// <summary>Sampling for pixel-aligned sharp tiles.</summary>
    private static readonly SKSamplingOptions SharpSampling = new(SKFilterMode.Nearest, SKMipmapMode.None);

    /// <summary>The pooled tile array. Each tile in it is retained once until the batch is disposed.</summary>
    private readonly TileDraw[] _tiles;

    /// <summary>The pooled flags that mark the tiles already drawn through the GPU.</summary>
    private readonly bool[] _drawn;

    /// <summary>The lifetime state: live, rendering or disposed.</summary>
    private int _state;

    /// <summary>Initializes a new instance of the <see cref="SkiaTileBatch"/> class.</summary>
    /// <param name="tiles">An array rented from the shared pool whose first <paramref name="count"/> tiles are retained; the batch returns it.</param>
    /// <param name="count">The number of tiles.</param>
    public SkiaTileBatch(TileDraw[] tiles, int count)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        _tiles = tiles;
        Count = count;
        _drawn = ArrayPool<bool>.Shared.Rent(count);
        var bounds = tiles[0].Bounds;
        for (var i = 1; i < count; i++)
        {
            bounds = SKRect.Union(bounds, tiles[i].Bounds);
        }

        Bounds = bounds;
    }

    /// <summary>Gets the area the tiles cover.</summary>
    public SKRect Bounds { get; }

    /// <summary>Gets the number of tiles in the batch.</summary>
    public int Count { get; }

    /// <inheritdoc/>
    public bool Equals(SkiaTileBatch? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        // A disposed batch has returned its arrays, so it never matches.
        return other is not null
            && Count == other.Count
            && IsActive(Volatile.Read(ref _state))
            && IsActive(Volatile.Read(ref other._state))
            && _tiles.AsSpan(0, Count).SequenceEqual(other._tiles.AsSpan(0, other.Count));
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as SkiaTileBatch);

    /// <inheritdoc/>
    public override int GetHashCode() => Count;

    /// <summary>Draws every tile onto the target: through the GPU where it can, and from software pixels for the rest.</summary>
    /// <param name="target">The canvas and graphics context to draw with.</param>
    public void Render(in SkiaTileTarget target)
    {
        if (!TryBeginRender())
        {
            return;
        }

        try
        {
            DrawTiles(target);
        }
        finally
        {
            EndRender();
        }
    }

    /// <summary>Records that the head could not give a canvas, so every tile falls back to software pixels from the next frame.</summary>
    public void RenderUnavailable()
    {
        if (!TryBeginRender())
        {
            return;
        }

        try
        {
            MarkUndrawnUnavailable();
        }
        finally
        {
            EndRender();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        while (true)
        {
            switch (Volatile.Read(ref _state))
            {
                case Live:
                {
                    if (Interlocked.CompareExchange(ref _state, Disposed, Live) == Live)
                    {
                        ReleaseTiles();
                        return;
                    }

                    break;
                }

                case Rendering:
                {
                    if (Interlocked.CompareExchange(ref _state, DisposedWhileRendering, Rendering) == Rendering)
                    {
                        return;
                    }

                    break;
                }

                default:
                {
                    return;
                }
            }
        }
    }

    /// <summary>Gets whether a state still owns its arrays.</summary>
    /// <param name="state">The lifetime state.</param>
    /// <returns><see langword="true"/> while the batch has not been disposed.</returns>
    private static bool IsActive(int state) => state is Live or Rendering;

    /// <summary>Moves a live batch into rendering and clears what was drawn last time.</summary>
    /// <returns>Whether the render may go ahead.</returns>
    private bool TryBeginRender()
    {
        if (Interlocked.CompareExchange(ref _state, Rendering, Live) != Live)
        {
            return false;
        }

        Array.Clear(_drawn, 0, Count);
        return true;
    }

    /// <summary>Ends a render and releases the tiles when the batch was disposed during it.</summary>
    private void EndRender()
    {
        if (Interlocked.CompareExchange(ref _state, Live, Rendering) != Rendering)
        {
            ReleaseTiles();
        }
    }

    /// <summary>Draws every tile through the GPU where the target has one, and from software pixels for the rest.</summary>
    /// <param name="target">The canvas and graphics context.</param>
    private void DrawTiles(in SkiaTileTarget target)
    {
        try
        {
            if (target.Gpu is { } gpu && target.Retirement is { } retirement)
            {
                for (var i = 0; i < Count; i++)
                {
                    _ = TryDrawTile(i, target, gpu, retirement);
                }
            }
            else
            {
                MarkUndrawnUnavailable();
            }

            DrawSoftware(target.Canvas);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException)
        {
            MarkUndrawnUnavailable();
        }
    }

    /// <summary>Draws one tile and records whether the GPU drew it.</summary>
    /// <param name="index">The tile index.</param>
    /// <param name="target">The canvas and color type.</param>
    /// <param name="gpu">The graphics context.</param>
    /// <param name="retirement">The image-release queue.</param>
    /// <returns>Whether the GPU drew the tile.</returns>
    private bool TryDrawTile(int index, in SkiaTileTarget target, GRContext gpu, SkiaGpuImageRetirement retirement)
    {
        var (tile, bounds, smooth) = _tiles[index];
        try
        {
            _drawn[index] = tile.DrawGpu(target.Canvas, gpu, retirement, target.ColorType, bounds, smooth ? SmoothSampling : SharpSampling);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException)
        {
            tile.MarkGpuUnavailable();
        }

        return _drawn[index];
    }

    /// <summary>Marks the hub of every tile the GPU did not draw as unable to present through the GPU.</summary>
    private void MarkUndrawnUnavailable()
    {
        for (var i = 0; i < Count; i++)
        {
            if (!_drawn[i])
            {
                _tiles[i].Tile.MarkGpuUnavailable();
            }
        }
    }

    /// <summary>Draws software pixels for tiles the GPU did not draw, preparing a cached tile on its first recovery draw.</summary>
    /// <param name="canvas">The canvas.</param>
    private void DrawSoftware(SKCanvas canvas)
    {
        for (var i = 0; i < Count; i++)
        {
            if (_drawn[i])
            {
                continue;
            }

            var (tile, bounds, smooth) = _tiles[i];
            if (tile.SoftwareSurface is null && tile.IsGpuUnavailable)
            {
                _ = tile.EnsureSoftwareBitmap();
            }

            _drawn[i] = tile.DrawSoftware(canvas, bounds, smooth ? SoftwareSmoothSampling : SharpSampling);
        }
    }

    /// <summary>Releases each tile once and returns the pooled arrays.</summary>
    private void ReleaseTiles()
    {
        for (var i = 0; i < Count; i++)
        {
            _tiles[i].Tile.Release();
        }

        ArrayPool<TileDraw>.Shared.Return(_tiles, clearArray: true);
        ArrayPool<bool>.Shared.Return(_drawn);
    }
}
