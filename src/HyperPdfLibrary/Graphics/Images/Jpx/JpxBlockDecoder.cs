// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>Decodes code-block segments and stores their coefficients in tile components.</summary>
internal static class JpxBlockDecoder
{
    /// <summary>The highest bit-plane the 32-bit coefficients can hold at twice scale.</summary>
    internal const int MaxPlanes = 31;

    /// <summary>The bit-planes coded with the arithmetic coder before the bypass mode switches to raw passes.</summary>
    internal const int BypassStartPlanes = 4;

    /// <summary>The cleanup pass number within a bit-plane.</summary>
    internal const int CleanupPass = 2;

    /// <summary>The passes of a bit-plane.</summary>
    internal const int PassesPerPlane = 3;

    /// <summary>The scale the coefficients are rebuilt at.</summary>
    internal const int Scale = 2;

    /// <summary>Decodes one code-block into its tile-component buffer.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="tile">The tile, which holds the code-block's segments and chunks.</param>
    /// <param name="blockIndex">The code-block index.</param>
    /// <param name="data">The tile's packet data.</param>
    /// <param name="target">Where the coefficients go.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Decode(JpxBlockState state, JpxTile tile, int blockIndex, byte[] data, in JpxBlockTarget target, CancellationToken cancellationToken) =>
        Decode(state, tile, blockIndex, data, target, tile.Bands[tile.Blocks[blockIndex].Band], cancellationToken);

    /// <summary>
    /// Decodes one code-block into a buffer that holds some of its sub-band, such as a strip of rows. The block lands at
    /// <c>band.BufferX + block.X0 - band.X0</c> across and <c>band.BufferY + block.Y0 - band.Y0</c> down in the buffer.
    /// </summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="tile">The tile, which holds the code-block's segments and chunks.</param>
    /// <param name="blockIndex">The code-block index.</param>
    /// <param name="data">The tile's packet data.</param>
    /// <param name="target">Where the coefficients go.</param>
    /// <param name="band">The sub-band, with the buffer position of its first sample set for <paramref name="target"/>.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    internal static void Decode(JpxBlockState state, JpxTile tile, int blockIndex, byte[] data, in JpxBlockTarget target, in JpxBandLayout band, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var block = tile.Blocks[blockIndex];
        var planes = target.RoiShift + block.BitPlanes;
        if (block.DataLength == 0 || block.FirstSegment < 0 || planes >= MaxPlanes || planes < 1 || block.Area.IsEmpty)
        {
            return;
        }

        if (block.UsesHighThroughput(target.Style))
        {
            JpxHtBlockDecoder.DecodeHighThroughput(state, tile, block, band, data, target);
            return;
        }

        Prepare(state, block.Area.Width, block.Area.Height, band.Orientation, target.Style);
        var source = Gather(state, tile, block, data, out var offset);
        RunSegments(state, tile, block, source, offset, target);
        if (target.RoiShift > 0)
        {
            ApplyRoi(state, target.RoiShift);
        }

        Store(state, block, band, target);
    }

    /// <summary>Sizes and clears the state for a code-block.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="width">The code-block width.</param>
    /// <param name="height">The code-block height.</param>
    /// <param name="orientation">The sub-band orientation.</param>
    /// <param name="style">The mode switches.</param>
    internal static void Prepare(JpxBlockState state, int width, int height, int orientation, JpxBlockStyle style)
    {
        state.Width = width;
        state.Height = height;
        state.Stripes = (height + JpxBlockLayout.LastRow) / JpxBlockLayout.StripeRows;
        state.StripeStride = (width + JpxBlockLayout.Border) * JpxBlockLayout.StripeRows;
        state.Length = (state.Stripes + JpxBlockLayout.Border) * state.StripeStride;
        JpxBlockLayout.Ensure(ref state.Values, state.Length, state.ScratchPools);
        JpxBlockLayout.Ensure(ref state.Flags, state.Length, state.ScratchPools);
        JpxBlockLayout.Ensure(ref state.Neighbours, state.Length, state.ScratchPools);
        state.Values.AsSpan(0, state.Length).Clear();
        state.Flags.AsSpan(0, state.Length).Clear();
        state.Neighbours.AsSpan(0, state.Length).Clear();
        state.ZeroTable = orientation * JpxContexts.Neighbourhoods;
        state.Causal = (style & JpxBlockStyle.VerticallyCausal) != 0;
        ResetContexts(state);
    }

    /// <summary>Sets every context to its initial state (table D.7).</summary>
    /// <param name="state">The decoder's mutable state.</param>
    internal static void ResetContexts(JpxBlockState state)
    {
        state.Contexts.AsSpan().Clear();
        state.Contexts[0] = JpxContexts.ZeroState;
        state.Contexts[JpxContexts.RunLength] = JpxContexts.RunLengthState;
        state.Contexts[JpxContexts.Uniform] = JpxContexts.UniformState;
    }

    /// <summary>Gets the code-block's data as one run, joining its chunks when there is more than one.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="tile">The tile.</param>
    /// <param name="block">The code-block.</param>
    /// <param name="data">The tile's packet data.</param>
    /// <param name="offset">Receives the offset of the run.</param>
    /// <returns>The array holding the run.</returns>
    internal static byte[] Gather(JpxBlockState state, JpxTile tile, JpxCodeBlock block, byte[] data, out int offset)
    {
        var first = tile.Chunks[block.FirstChunk];
        if (first.Next < 0)
        {
            offset = first.Offset;
            return data;
        }

        JpxBlockLayout.Ensure(ref state.Joined, block.DataLength, state.ScratchPools);
        var written = 0;
        for (var chunk = block.FirstChunk; chunk >= 0; chunk = tile.Chunks[chunk].Next)
        {
            var piece = tile.Chunks[chunk];
            data.AsSpan(piece.Offset, piece.Length).CopyTo(state.Joined.AsSpan(written));
            written += piece.Length;
        }

        offset = 0;
        return state.Joined;
    }

    /// <summary>Runs the coding passes of every segment, each segment with a fresh arithmetic or raw decoder.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="tile">The tile.</param>
    /// <param name="block">The code-block.</param>
    /// <param name="source">The coded data.</param>
    /// <param name="offset">The offset of the first segment.</param>
    /// <param name="target">The decoding target, for the mode switches and region-of-interest shift.</param>
    internal static void RunSegments(JpxBlockState state, JpxTile tile, JpxCodeBlock block, byte[] source, int offset, in JpxBlockTarget target)
    {
        var coder = default(JpxMqDecoder);
        var firstPlane = target.RoiShift + block.BitPlanes;
        var cursor = new PassCursor(firstPlane, CleanupPass);
        var end = offset + block.DataLength;
        var bypass = (target.Style & JpxBlockStyle.Bypass) != 0;
        for (var segment = block.FirstSegment; segment >= 0 && cursor.Plane >= 1; segment = tile.Segments[segment].Next)
        {
            var current = tile.Segments[segment];
            var length = Math.Min(current.Length, end - offset);

            // The raw passes start four bit-planes below the first coded one, which counts the region-of-interest shift (D.6).
            var raw = bypass && cursor.Plane <= firstPlane - BypassStartPlanes && cursor.Pass < CleanupPass;
            if (raw)
            {
                coder.StartRaw(source, offset, length);
            }
            else
            {
                coder.Start(source, offset, length);
            }

            offset += length;
            cursor = RunPasses(state, ref coder, current.Passes, raw, target.Style, cursor);
        }
    }

    /// <summary>Removes the region-of-interest up-shift from the coefficients that carry it (equation D-1).</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="shift">The shift.</param>
    internal static void ApplyRoi(JpxBlockState state, int shift)
    {
        // The values are at twice scale, so a coefficient of magnitude 2^shift or more (one that carries the shift)
        // is at least 2^(shift + 1) here; background coefficients stay below that.
        var threshold = 1 << (shift + 1);
        foreach (ref var value in state.Values.AsSpan(0, state.Length))
        {
            var magnitude = Math.Abs(value);
            if (magnitude < threshold)
            {
                continue;
            }

            magnitude >>= shift;
            value = value < 0 ? -magnitude : magnitude;
        }
    }

    /// <summary>Copies the coefficients into the tile-component, halved or scaled by half the step.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="block">The code-block.</param>
    /// <param name="band">The sub-band.</param>
    /// <param name="target">The tile-component.</param>
    internal static void Store(JpxBlockState state, JpxCodeBlock block, in JpxBandLayout band, in JpxBlockTarget target)
    {
        var x0 = band.BufferX + block.Area.X0 - band.Area.X0;
        var y0 = band.BufferY + block.Area.Y0 - band.Area.Y0;
        var floats = MemoryMarshal.Cast<int, float>(target.Buffer.AsSpan());
        for (var y = 0; y < state.Height; y++)
        {
            var source = (((y / JpxBlockLayout.StripeRows) + 1) * state.StripeStride) + JpxBlockLayout.StripeRows + (y % JpxBlockLayout.StripeRows);
            var row = ((y0 + y) * target.Stride) + x0;
            if (target.Reversible)
            {
                StoreReversible(state, target.Buffer.AsSpan(row, state.Width), source);
            }
            else
            {
                StoreIrreversible(state, floats.Slice(row, state.Width), source, band.StepSize);
            }
        }
    }

    /// <summary>Stores one row of 5/3 coefficients, halving with truncation toward zero.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="row">The destination row.</param>
    /// <param name="source">The first coefficient in the state buffer.</param>
    internal static void StoreReversible(JpxBlockState state, Span<int> row, int source)
    {
        for (var x = 0; x < row.Length; x++)
        {
            row[x] = state.Values[source + (x * JpxBlockLayout.StripeRows)] / Scale;
        }
    }

    /// <summary>Stores one row of 9/7 coefficients scaled by half the step.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="row">The destination row.</param>
    /// <param name="source">The first coefficient in the state buffer.</param>
    /// <param name="step">Half the dequantization step.</param>
    internal static void StoreIrreversible(JpxBlockState state, Span<float> row, int source, float step)
    {
        for (var x = 0; x < row.Length; x++)
        {
            row[x] = state.Values[source + (x * JpxBlockLayout.StripeRows)] * step;
        }
    }

    /// <summary>Runs a segment's coding passes.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="coder">The segment's decoder.</param>
    /// <param name="passes">The passes in the segment.</param>
    /// <param name="raw">Whether the segment is raw bypass data.</param>
    /// <param name="style">The mode switches.</param>
    /// <param name="cursor">The bit-plane and pass to start at.</param>
    /// <returns>The bit-plane and pass after the segment.</returns>
    private static PassCursor RunPasses(JpxBlockState state, ref JpxMqDecoder coder, int passes, bool raw, JpxBlockStyle style, PassCursor cursor)
    {
        var (plane, pass) = cursor;
        for (var i = 0; i < passes && plane >= 1; i++)
        {
            RunPass(state, ref coder, pass, plane, raw, style);
            if (!raw && (style & JpxBlockStyle.Reset) != 0)
            {
                ResetContexts(state);
            }

            pass++;
            if (pass != PassesPerPlane)
            {
                continue;
            }

            pass = 0;
            plane--;
        }

        return new(plane, pass);
    }

    /// <summary>Runs one coding pass.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="coder">The decoder.</param>
    /// <param name="pass">The pass type: 0 significance, 1 refinement, 2 cleanup.</param>
    /// <param name="plane">The bit-plane plus one.</param>
    /// <param name="raw">Whether the pass is raw bypass data.</param>
    /// <param name="style">The mode switches.</param>
    private static void RunPass(JpxBlockState state, ref JpxMqDecoder coder, int pass, int plane, bool raw, JpxBlockStyle style)
    {
        switch (pass)
        {
            case 0:
                {
                    JpxBlockPasses.Significance(state, ref coder, plane, raw);
                    break;
                }

            case 1:
                {
                    JpxBlockPasses.Refinement(state, ref coder, plane, raw);
                    break;
                }

            default:
                {
                    JpxBlockPasses.Cleanup(state, ref coder, plane, (style & JpxBlockStyle.SegmentationSymbols) != 0);
                    break;
                }
        }
    }

    /// <summary>The position of the decoder in the pass sequence.</summary>
    /// <param name="Plane">The bit-plane plus one.</param>
    /// <param name="Pass">The pass within the bit-plane: 0 significance, 1 refinement, 2 cleanup.</param>
    private readonly record struct PassCursor(int Plane, int Pass);
}
