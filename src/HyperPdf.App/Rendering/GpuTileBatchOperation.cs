// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using HyperPdf.Skia;
using HyperPdf.Skia.Gpu;
using SkiaSharp;

namespace HyperPdf.App.Rendering;

/// <summary>Composes the prepared tiles of one page with a single Skia lease.</summary>
[DebuggerDisplay("GpuTileBatchOperation: {Count} tiles in {Bounds}")]
internal sealed class GpuTileBatchOperation : ICustomDrawOperation
{
    /// <summary>The operation accepts a render and has not been disposed.</summary>
    private const int Live = 0;

    /// <summary>A render is running.</summary>
    private const int Rendering = 1;

    /// <summary>The operation was disposed and released its tiles.</summary>
    private const int Disposed = 2;

    /// <summary>The operation was disposed during a render, which releases the tiles when it ends.</summary>
    private const int DisposedWhileRendering = 3;

    /// <summary>Sampling for scaled previews and zoom stand-ins.</summary>
    private static readonly SKSamplingOptions SmoothSampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    /// <summary>Sampling for pixel-aligned sharp tiles.</summary>
    private static readonly SKSamplingOptions SharpSampling = new(SKFilterMode.Nearest, SKMipmapMode.None);

    /// <summary>The pooled tile array. Each tile in it is retained once until the operation is disposed.</summary>
    private readonly TileDraw[] _tiles;

    /// <summary>The pooled flags that mark the tiles already drawn through the GPU.</summary>
    private readonly bool[] _drawn;

    /// <summary>The lifetime state: live, rendering or disposed.</summary>
    private int _state;

    /// <summary>Initializes a new instance of the <see cref="GpuTileBatchOperation"/> class.</summary>
    /// <param name="tiles">An array rented from the shared pool whose first <paramref name="count"/> tiles are retained; the operation returns it.</param>
    /// <param name="count">The number of tiles.</param>
    internal GpuTileBatchOperation(TileDraw[] tiles, int count)
    {
        _tiles = tiles;
        Count = count;
        _drawn = ArrayPool<bool>.Shared.Rent(count);
        var bounds = tiles[0].Bounds;
        for (var i = 1; i < count; i++)
        {
            bounds = bounds.Union(tiles[i].Bounds);
        }

        Bounds = bounds;
    }

    /// <inheritdoc/>
    public Rect Bounds { get; }

    /// <summary>Gets the number of tiles in the batch.</summary>
    internal int Count { get; }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HitTest(Point p) => false;

    /// <inheritdoc/>
    public bool Equals(ICustomDrawOperation? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        // A disposed batch has returned its arrays, so it never matches.
        return other is GpuTileBatchOperation batch
            && Count == batch.Count
            && IsActive(Volatile.Read(ref _state))
            && IsActive(Volatile.Read(ref batch._state))
            && _tiles.AsSpan(0, Count).SequenceEqual(batch._tiles.AsSpan(0, batch.Count));
    }

    /// <inheritdoc/>
    public void Render(ImmediateDrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (Interlocked.CompareExchange(ref _state, Rendering, Live) != Live)
        {
            return;
        }

        try
        {
            Array.Clear(_drawn, 0, Count);
            if (!TryDrawGpu(context))
            {
                DrawSoftware(context);
            }
        }
        finally
        {
            if (Interlocked.CompareExchange(ref _state, Live, Rendering) != Rendering)
            {
                ReleaseTiles();
            }
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

    /// <summary>Draws every tile through one lease on the active compositor context.</summary>
    /// <param name="context">The immediate context.</param>
    /// <returns>Whether every tile was drawn.</returns>
    private bool TryDrawGpu(ImmediateDrawingContext context)
    {
        if (context.TryGetFeature<ISkiaSharpApiLeaseFeature>() is not { } feature)
        {
            MarkUndrawnUnavailable();
            return false;
        }

        try
        {
            using var lease = feature.Lease();
            if (lease.GrContext is not { } gpu || lease.ImageRetirement is not { } retirement)
            {
                MarkUndrawnUnavailable();
                return false;
            }

            var complete = true;
            for (var i = 0; i < Count; i++)
            {
                complete &= TryDrawTile(i, lease, gpu, retirement);
            }

            return complete;
        }
        catch (Exception exception) when (exception is InvalidOperationException or ObjectDisposedException)
        {
            MarkUndrawnUnavailable();
            return false;
        }
    }

    /// <summary>Draws one tile and records whether the GPU drew it.</summary>
    /// <param name="index">The tile index.</param>
    /// <param name="lease">The active Skia lease.</param>
    /// <param name="gpu">The graphics context.</param>
    /// <param name="retirement">The image-release queue.</param>
    /// <returns>Whether the GPU drew the tile.</returns>
    private bool TryDrawTile(int index, ISkiaSharpApiLease lease, GRContext gpu, SkiaGpuImageRetirement retirement)
    {
        var (tile, bounds, smooth) = _tiles[index];
        try
        {
            var rect = new SKRect((float)bounds.Left, (float)bounds.Top, (float)bounds.Right, (float)bounds.Bottom);
            _drawn[index] = tile.DrawGpu(lease.SkCanvas, gpu, retirement, lease.SurfaceColorType, rect, smooth ? SmoothSampling : SharpSampling);
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
    /// <param name="context">The immediate context.</param>
    private void DrawSoftware(ImmediateDrawingContext context)
    {
        for (var i = 0; i < Count; i++)
        {
            if (_drawn[i])
            {
                continue;
            }

            var (tile, bounds, _) = _tiles[i];
            var bitmap = tile.SoftwareBitmap;
            if (bitmap is null && tile.IsGpuUnavailable && tile.EnsureSoftwareBitmap())
            {
                bitmap = tile.SoftwareBitmap;
            }

            if (bitmap is not null)
            {
                context.DrawBitmap(bitmap, new(0, 0, tile.Width, tile.Height), bounds);
            }
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
