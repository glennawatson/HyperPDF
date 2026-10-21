// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using Avalonia;
using Avalonia.Media;
using HyperPdf.Core.Rendering;
using HyperPdf.Rendering.Skia;

namespace HyperPdf.App.Rendering;

/// <summary>Collects the GPU tiles drawn in sequence so they reach Avalonia as one operation per flush, in draw order.</summary>
internal sealed class TileDrawBatch
{
    /// <summary>The first array size rented for a batch.</summary>
    private const int InitialCapacity = 16;

    /// <summary>The factor the pooled array grows by when it is full.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The pooled array holding retained tiles, or null when none are pending.</summary>
    private TileDraw[]? _pending;

    /// <summary>The number of pending tiles.</summary>
    private int _count;

    /// <summary>Queues a tile, which is drawn when the batch is flushed, through the GPU or from its software pixels.</summary>
    /// <param name="context">The recorded drawing context.</param>
    /// <param name="surface">The cached tile.</param>
    /// <param name="destination">The destination bounds.</param>
    /// <param name="smooth">Whether the tile is being scaled as a preview or stand-in.</param>
    internal void Draw(DrawingContext context, IRenderSurface surface, in Rect destination, bool smooth)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(surface);
        if (surface is GpuPreparedRenderSurface gpu && gpu.TryRetain())
        {
            Append(new(gpu, new((float)destination.Left, (float)destination.Top, (float)destination.Right, (float)destination.Bottom), smooth));
        }
    }

    /// <summary>Hands the queued GPU tiles to the scene as one operation.</summary>
    /// <param name="context">The recorded drawing context.</param>
    internal void Flush(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (_pending is not { } tiles)
        {
            return;
        }

        var operation = new GpuTileBatchOperation(new(tiles, _count));
        _pending = null;
        _count = 0;
        try
        {
            context.Custom(operation);
        }
        catch
        {
            operation.Dispose();
            throw;
        }
    }

    /// <summary>Adds a retained tile, growing the pooled array when it is full.</summary>
    /// <param name="draw">The retained tile.</param>
    private void Append(in TileDraw draw)
    {
        var tiles = _pending ?? ArrayPool<TileDraw>.Shared.Rent(InitialCapacity);
        if (_count == tiles.Length)
        {
            var larger = ArrayPool<TileDraw>.Shared.Rent(tiles.Length * GrowthFactor);
            tiles.AsSpan(0, _count).CopyTo(larger);
            ArrayPool<TileDraw>.Shared.Return(tiles, clearArray: true);
            tiles = larger;
        }

        _pending = tiles;
        tiles[_count] = draw;
        _count++;
    }
}
