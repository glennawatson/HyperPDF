// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jpx.Encoding;

/// <summary>Transforms, codes and writes tiles using an explicit buffer lifetime.</summary>
internal static class JpxTileEncoding
{
    /// <summary>The components the colour transform covers.</summary>
    private const int TransformComponents = 3;

    /// <summary>Leases the largest tile's buffers from its caller.</summary>
    /// <param name="settings">The coding parameters.</param>
    /// <param name="geometry">The image geometry.</param>
    /// <param name="parameters">The resolved tile parameters.</param>
    /// <param name="quantization">The sub-band quantization.</param>
    /// <param name="pools">The caller's reusable storage.</param>
    /// <returns>The exclusively owned buffer lifetime.</returns>
    internal static JpxTileEncodingState Create(JpxEncodeSettings settings, JpxGeometry geometry, JpxTileParameters parameters, JpxQuantPlan quantization, ScratchPools pools)
    {
        var size = Math.Min(settings.TileWidth, settings.Width) * Math.Min(settings.TileHeight, settings.Height);
        var planes = pools.Get<int[]>().Rent(settings.Components);
        planes.AsSpan().Clear();
        var scratch = Array.Empty<int>();
        try
        {
            for (var component = 0; component < settings.Components; component++)
            {
                planes[component] = pools.Ints.Rent(size);
            }

            scratch = pools.Ints.Rent(size);
            var tile = new JpxTile(settings.Components, pools);
            var code = new JpxTileCode(pools);
            return new(
                pools,
                settings,
                geometry,
                parameters,
                quantization,
                tile,
                code,
                new(pools),
                new(settings, tile, code, pools),
                new(pools),
                new(pools),
                planes,
                scratch);
        }
        catch
        {
            ReleaseBuffers(pools, planes, settings.Components, scratch);
            throw;
        }
    }

    /// <summary>Returns component and wavelet buffers, clearing the component references.</summary>
    /// <param name="pools">The original buffer owner.</param>
    /// <param name="planes">The rented component index.</param>
    /// <param name="components">The used component slots.</param>
    /// <param name="scratch">The wavelet workspace.</param>
    internal static void ReleaseBuffers(ScratchPools pools, int[][] planes, int components, int[] scratch)
    {
        for (var component = 0; component < components; component++)
        {
            if (planes[component] is { } plane)
            {
                pools.Ints.Return(plane);
            }
        }

        pools.Get<int[]>().Return(planes, true);
        if (scratch.Length != 0)
        {
            pools.Ints.Return(scratch);
        }
    }

    /// <summary>Encodes a tile and appends it to the codestream as one tile-part.</summary>
    /// <param name="state">The caller-owned tile lifetime.</param>
    /// <param name="samples">The interleaved image samples.</param>
    /// <param name="tileIndex">The tile index.</param>
    /// <param name="output">The codestream.</param>
    /// <param name="cancellationToken">Cancels between code-blocks.</param>
    /// <returns><see langword="false"/> when a code-block has more bit-planes than its sub-band allows, so the file needs more guard bits.</returns>
    /// <exception cref="InvalidOperationException">The tile is too finely divided to lay out.</exception>
    /// <exception cref="ObjectDisposedException">The tile lifetime has ended.</exception>
    internal static bool Encode(JpxTileEncodingState state, ReadOnlySpan<byte> samples, int tileIndex, ref PooledBuffer output, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(state.IsDisposed, state);
        cancellationToken.ThrowIfCancellationRequested();
        if (!state.Tile.Build(state.Geometry, tileIndex, state.Parameters))
        {
            throw new InvalidOperationException("The tile has too many code-blocks to encode.");
        }

        state.Code.Reset(state.Tile.Blocks.Count);
        Transform(state, samples);
        CodeBlocks(state, cancellationToken);
        if (!state.Packets.Begin())
        {
            return false;
        }

        var start = JpxEncodeHeaders.WriteTileStart(ref output, tileIndex);
        JpxPacketOrder.Build(state.Tile, state.Geometry, state.Settings, state.Keys);
        foreach (var key in state.Keys.AsSpan())
        {
            state.Packets.Write(key, ref output);
        }

        JpxEncodeHeaders.FinishTilePart(ref output, start);
        return true;
    }

    /// <summary>Loads the tile's samples and runs the component transform and the wavelet on them.</summary>
    /// <param name="state">The caller-owned tile lifetime.</param>
    /// <param name="samples">The interleaved image samples.</param>
    private static void Transform(JpxTileEncodingState state, ReadOnlySpan<byte> samples)
    {
        var area = state.Tile.Area;
        var length = area.Width * area.Height;
        for (var c = 0; c < state.Settings.Components; c++)
        {
            if (state.Settings.Reversible)
            {
                JpxTileSamples.LoadIntegers(samples, state.Settings, c, area, state.Planes[c].AsSpan(0, length));
            }
            else
            {
                JpxTileSamples.LoadFloats(samples, state.Settings, c, area, MemoryMarshal.Cast<int, float>(state.Planes[c].AsSpan(0, length)));
            }
        }

        if (state.Settings.UseTransform)
        {
            RunColourTransform(state, length);
        }

        for (var c = 0; c < state.Settings.Components; c++)
        {
            RunWavelet(state, c, length);
        }
    }

    /// <summary>Runs the component transform on the first three components.</summary>
    /// <param name="state">The caller-owned tile lifetime.</param>
    /// <param name="length">The samples in a tile-component.</param>
    private static void RunColourTransform(JpxTileEncodingState state, int length)
    {
        if (state.Settings.Reversible)
        {
            JpxTileSamples.ForwardReversible(state.Planes[0].AsSpan(0, length), state.Planes[1].AsSpan(0, length), state.Planes[TransformComponents - 1].AsSpan(0, length));
            return;
        }

        JpxTileSamples.ForwardIrreversible(
            MemoryMarshal.Cast<int, float>(state.Planes[0].AsSpan(0, length)),
            MemoryMarshal.Cast<int, float>(state.Planes[1].AsSpan(0, length)),
            MemoryMarshal.Cast<int, float>(state.Planes[TransformComponents - 1].AsSpan(0, length)));
    }

    /// <summary>Runs the forward wavelet on one component.</summary>
    /// <param name="state">The caller-owned tile lifetime.</param>
    /// <param name="component">The component.</param>
    /// <param name="length">The samples in a tile-component.</param>
    private static void RunWavelet(JpxTileEncodingState state, int component, int length)
    {
        var plane = state.Planes[component].AsSpan(0, length);
        var scratch = state.Scratch.AsSpan(0, length);
        if (state.Settings.Reversible)
        {
            JpxForwardWavelet53.Forward(state.Tile, state.Tile.Components[component], plane, scratch);
        }
        else
        {
            JpxForwardWavelet97.Forward(state.Tile, state.Tile.Components[component], MemoryMarshal.Cast<int, float>(plane), MemoryMarshal.Cast<int, float>(scratch));
        }
    }

    /// <summary>Codes every code-block of the tile.</summary>
    /// <param name="state">The caller-owned tile lifetime.</param>
    /// <param name="cancellationToken">Cancels between code-blocks.</param>
    private static void CodeBlocks(JpxTileEncodingState state, CancellationToken cancellationToken)
    {
        FindBandQuantization(state);
        for (var i = 0; i < state.Tile.Blocks.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var block = state.Tile.Blocks[i];
            if (block.Area.IsEmpty)
            {
                continue;
            }

            var band = state.Tile.Bands[block.Band];
            var window = new JpxBlockWindow(
                band.BufferX + block.Area.X0 - band.Area.X0,
                band.BufferY + block.Area.Y0 - band.Area.Y0,
                block.Area.Width,
                block.Area.Height,
                band.Orientation,
                state.Settings.Style);
            state.Blocks.Encode(LoadBlock(state, block.Band, window), state.Settings.Style, state.Code, i);
        }
    }

    /// <summary>Loads a code-block's coefficients, quantizing them for a lossy file.</summary>
    /// <param name="state">The caller-owned tile lifetime.</param>
    /// <param name="bandIndex">The index of the block's sub-band in the tile.</param>
    /// <param name="window">Where the block is in its tile-component.</param>
    /// <returns>The magnitude bit-planes of the block.</returns>
    private static int LoadBlock(JpxTileEncodingState state, int bandIndex, in JpxBlockWindow window)
    {
        var component = state.Tile.Bands[bandIndex].Component;
        var area = state.Tile.Components[component].Area;
        var plane = state.Planes[component].AsSpan(0, area.Width * area.Height);
        return state.Settings.Reversible
            ? state.Blocks.Load(plane, area.Width, window)
            : state.Blocks.Load(MemoryMarshal.Cast<int, float>(plane), area.Width, window, 1F / state.Quantization.GetStep(state.BandQuantization[bandIndex]));
    }

    /// <summary>Finds the quantization index of every sub-band of the tile.</summary>
    /// <param name="state">The caller-owned tile lifetime.</param>
    private static void FindBandQuantization(JpxTileEncodingState state)
    {
        state.BandQuantization.Clear();
        _ = state.BandQuantization.AddDefault(state.Tile.Bands.Count);
        foreach (var component in state.Tile.Components)
        {
            for (var r = 0; r <= component.Levels; r++)
            {
                var layout = state.Tile.Resolutions[component.FirstResolution + r];
                for (var b = 0; b < layout.BandCount; b++)
                {
                    state.BandQuantization[layout.FirstBand + b] = JpxQuantPlan.BandIndex(r, b + 1);
                }
            }
        }
    }
}
