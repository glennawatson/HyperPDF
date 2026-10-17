// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jpx.Encoding;

/// <summary>Owns exclusively leased tile buffers until an encode ends.</summary>
/// <param name="Pools">The caller's reusable storage.</param>
/// <param name="Settings">The coding parameters.</param>
/// <param name="Geometry">The image geometry.</param>
/// <param name="Parameters">The resolved tile parameters.</param>
/// <param name="Quantization">The sub-band quantization.</param>
/// <param name="Tile">The tile layout.</param>
/// <param name="Code">The coded blocks.</param>
/// <param name="Blocks">The block coder.</param>
/// <param name="Packets">The packet writer.</param>
/// <param name="Keys">The packet order.</param>
/// <param name="BandQuantization">The quantization indices.</param>
/// <param name="Planes">The rented component planes.</param>
/// <param name="Scratch">The wavelet workspace.</param>
internal sealed record JpxTileEncodingState(
    ScratchPools Pools,
    JpxEncodeSettings Settings,
    JpxGeometry Geometry,
    JpxTileParameters Parameters,
    JpxQuantPlan Quantization,
    JpxTile Tile,
    JpxTileCode Code,
    JpxBlockEncoder Blocks,
    JpxPacketWriter Packets,
    JpxList<JpxPacketKey> Keys,
    JpxList<int> BandQuantization,
    int[][] Planes,
    int[] Scratch) : IDisposable
{
    /// <summary>Prevents duplicate buffer returns.</summary>
    private int _disposed;

    /// <summary>Gets whether this lifetime has ended.</summary>
    internal bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        JpxTileEncoding.ReleaseBuffers(Pools, Planes, Settings.Components, Scratch);
        Tile.Dispose();
        Code.Dispose();
        Blocks.Dispose();
        Packets.Dispose();
        Keys.Dispose();
        BandQuantization.Dispose();
    }
}
