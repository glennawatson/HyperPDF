// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Derives the quantisers of the colour components and scales coefficient levels (8.6.1 and 8.6.3).</summary>
internal static class H265Dequantizer
{
    /// <summary>The largest chroma quantiser index.</summary>
    private const int MaxChromaQpIndex = 57;

    /// <summary>The largest chroma quantiser of formats other than 4:2:0.</summary>
    private const int MaxChromaQp = 51;

    /// <summary>The first index of the chroma quantiser table that maps to a different value.</summary>
    private const int ChromaTableFirst = 30;

    /// <summary>The last index of the chroma quantiser table.</summary>
    private const int ChromaTableLast = 43;

    /// <summary>The difference between the index and the quantiser above the table.</summary>
    private const int ChromaTableTail = 6;

    /// <summary>The number of quantiser steps in a doubling of the step size.</summary>
    private const int QpPeriod = 6;

    /// <summary>The bit depth that needs no quantiser offset.</summary>
    private const int BaseBitDepth = 8;

    /// <summary>The scale a level is multiplied by when no scaling list applies.</summary>
    private const int FlatScale = 16;

    /// <summary>The bits that scaling shifts out beyond the block size term.</summary>
    private const int ScaleShiftBase = 5;

    /// <summary>The log2 of the smallest transform block side.</summary>
    private const int MinLog2Size = 2;

    /// <summary>The largest side of a transform skip block that still uses the scaling list.</summary>
    private const int MaxListedSkipSide = 4;

    /// <summary>The scaling list matrix numbers an inter block's components start at.</summary>
    private const int InterMatrixBase = 3;

    /// <summary>Gets the scale of each step of the quantiser within its period.</summary>
    private static ReadOnlySpan<byte> LevelScale => [0x28, 0x2D, 0x33, 0x39, 0x40, 0x48];

    /// <summary>Gets the chroma quantiser for the indices 30 to 43 (Table 8-10).</summary>
    private static ReadOnlySpan<byte> ChromaTable => [0x1D, 0x1E, 0x1F, 0x20, 0x21, 0x21, 0x22, 0x22, 0x23, 0x23, 0x24, 0x24, 0x25, 0x25];

    /// <summary>Gets the quantiser a colour component's transform block is scaled with.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="component">The colour component.</param>
    /// <returns>The quantiser including the bit depth offset.</returns>
    internal static int QuantiserOf(H265SliceContext context, int component)
    {
        var sps = context.Sps;
        if (component == 0)
        {
            return context.QpY + (QpPeriod * (sps.BitDepthLuma - BaseBitDepth));
        }

        var offset = QpPeriod * (sps.BitDepthChroma - BaseBitDepth);
        var picture = context.Pps.Quant;
        var header = context.Header.Quant;
        var qpi = component == 1
            ? context.QpY + picture.CbQpOffset + header.CbQpOffset + context.CuQpOffsetCb
            : context.QpY + picture.CrQpOffset + header.CrQpOffset + context.CuQpOffsetCr;
        qpi = Math.Clamp(qpi, -offset, MaxChromaQpIndex);
        return MapChroma(sps.ChromaArrayType, qpi) + offset;
    }

    /// <summary>Maps a chroma quantiser index to a quantiser (Table 8-10).</summary>
    /// <param name="chromaArrayType">The chroma format.</param>
    /// <param name="qpi">The index.</param>
    /// <returns>The quantiser before the bit depth offset.</returns>
    internal static int MapChroma(int chromaArrayType, int qpi)
    {
        if (chromaArrayType != ChromaSampling.Yuv420)
        {
            return Math.Min(qpi, MaxChromaQp);
        }

        if (qpi < ChromaTableFirst)
        {
            return qpi;
        }

        return qpi <= ChromaTableLast ? ChromaTable[qpi - ChromaTableFirst] : qpi - ChromaTableTail;
    }

    /// <summary>Scales the coefficient levels of a block in place.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="levels">The coefficients, row after row.</param>
    /// <param name="block">Describes the block.</param>
    /// <param name="transformSkip">Whether the block skips the transform, which switches off the scaling list for large blocks.</param>
    /// <param name="extent">The part of the block that holds coefficients.</param>
    internal static void Scale(H265SliceContext context, Span<int> levels, in H265ResidualBlock block, bool transformSkip, H265BlockExtent extent)
    {
        var sps = context.Sps;
        var qp = QuantiserOf(context, block.CIdx);
        var bitDepth = block.CIdx == 0 ? sps.BitDepthLuma : sps.BitDepthChroma;
        var shift = bitDepth + block.Log2Size - ScaleShiftBase;
        var round = 1L << (shift - 1);
        var scale = (long)LevelScale[qp % QpPeriod] << (qp / QpPeriod);
        var side = 1 << block.Log2Size;
        var lists = context.Pps.Scaling ?? sps.Scaling;
        if (lists is null || (transformSkip && side > MaxListedSkipSide))
        {
            ScaleFlat(levels, side, extent, scale * FlatScale, round, shift);
            return;
        }

        var factors = lists.Factors(block.Log2Size - MinLog2Size, (block.Intra ? 0 : InterMatrixBase) + block.CIdx);
        for (var y = 0; y <= extent.LastRow; y++)
        {
            for (var x = 0; x <= extent.LastColumn; x++)
            {
                var index = (y * side) + x;
                levels[index] = Clamp(((levels[index] * scale * factors[index]) + round) >> shift);
            }
        }
    }

    /// <summary>Scales a block whose scaling factor is the same for every coefficient.</summary>
    /// <param name="levels">The coefficients.</param>
    /// <param name="side">The block side.</param>
    /// <param name="extent">The part of the block that holds coefficients.</param>
    /// <param name="scale">The scale including the flat factor.</param>
    /// <param name="round">The rounding term.</param>
    /// <param name="shift">The right shift.</param>
    private static void ScaleFlat(Span<int> levels, int side, H265BlockExtent extent, long scale, long round, int shift)
    {
        for (var y = 0; y <= extent.LastRow; y++)
        {
            for (var x = 0; x <= extent.LastColumn; x++)
            {
                var index = (y * side) + x;
                if (levels[index] != 0)
                {
                    levels[index] = Clamp(((levels[index] * scale) + round) >> shift);
                }
            }
        }
    }

    /// <summary>Clips a scaled coefficient to 16 bits.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The clipped value.</returns>
    private static int Clamp(long value) => (int)Math.Clamp(value, short.MinValue, short.MaxValue);
}
