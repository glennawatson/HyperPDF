// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.H265;

/// <summary>The settings of pulse code modulation coding units, whose samples are stored without prediction or transform.</summary>
/// <param name="Enabled">Whether such units may occur.</param>
/// <param name="BitDepthLuma">The bits of a stored luma sample.</param>
/// <param name="BitDepthChroma">The bits of a stored chroma sample.</param>
/// <param name="MinCbLog2">The base 2 logarithm of the smallest coding block that may be coded this way.</param>
/// <param name="MaxCbLog2">The base 2 logarithm of the largest coding block that may be coded this way.</param>
/// <param name="LoopFilterDisabled">Whether the loop filters leave these samples alone.</param>
internal readonly record struct H265Pcm(bool Enabled, int BitDepthLuma, int BitDepthChroma, int MinCbLog2, int MaxCbLog2, bool LoopFilterDisabled)
{
    /// <summary>The bits to add to a bit position, and clear after, to reach the next byte boundary.</summary>
    private const long ByteRound = 7;

    /// <summary>Tells whether a coding unit may hold pulse code modulation samples.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="unit">The coding unit.</param>
    /// <returns><see langword="true"/> when pcm_flag is present.</returns>
    internal static bool IsAllowed(H265SliceContext context, H265CodingUnit unit)
    {
        var pcm = context.Sps.Pcm;
        return pcm.Enabled && unit.PartMode == H265PartMode.Part2Nx2N && unit.Log2Size >= pcm.MinCbLog2 && unit.Log2Size <= pcm.MaxCbLog2;
    }

    /// <summary>Reads the samples of a pulse code modulation coding unit and restarts the arithmetic decoder after them.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The slice context.</param>
    /// <param name="unit">The coding unit.</param>
    internal static void Read(ref CabacEngine engine, H265SliceContext context, H265CodingUnit unit)
    {
        var sps = context.Sps;
        var pcm = sps.Pcm;
        var picture = context.Picture;
        var start = (engine.BitPosition + ByteRound) & ~ByteRound;
        var reader = new MsbBitReader(engine.Data);
        reader.Seek(start);
        var size = 1 << unit.Log2Size;
        ReadPlane(ref reader, picture.Luma, picture.Width, new(unit.X, unit.Y, size, size), pcm.BitDepthLuma, sps.BitDepthLuma);
        if (context.HasChroma)
        {
            BlockRect block = new(unit.X / sps.SubWidthC, unit.Y / sps.SubHeightC, size / sps.SubWidthC, size / sps.SubHeightC);
            ReadPlane(ref reader, picture.Cb, picture.ChromaWidth, block, pcm.BitDepthChroma, sps.BitDepthChroma);
            ReadPlane(ref reader, picture.Cr, picture.ChromaWidth, block, pcm.BitDepthChroma, sps.BitDepthChroma);
        }

        engine.Restart(reader.BitPosition);
        MarkPcm(context, unit);
    }

    /// <summary>Reads the samples of one colour plane of the unit.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="plane">The plane.</param>
    /// <param name="stride">The samples in a row of the plane.</param>
    /// <param name="block">The area of the plane the unit covers.</param>
    /// <param name="codedDepth">The bits of a stored sample.</param>
    /// <param name="bitDepth">The bit depth of the plane.</param>
    private static void ReadPlane(ref MsbBitReader reader, ushort[] plane, int stride, BlockRect block, int codedDepth, int bitDepth)
    {
        var shift = bitDepth - codedDepth;
        for (var row = 0; row < block.Height; row++)
        {
            var offset = ((block.Y + row) * stride) + block.X;
            for (var column = 0; column < block.Width; column++)
            {
                plane[offset + column] = (ushort)(reader.Read(codedDepth) << shift);
            }
        }
    }

    /// <summary>Flags the blocks of the unit as pulse code modulation blocks, and as left alone by the loop filters when the sequence says so.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="unit">The coding unit.</param>
    private static void MarkPcm(H265SliceContext context, H265CodingUnit unit)
    {
        var picture = context.Picture;
        var blocks = (1 << unit.Log2Size) >> H265Picture.GridLog2;
        var start = picture.GridIndex(unit.X, unit.Y);
        var flag = (byte)(H265Picture.PcmFlag | (context.Sps.Pcm.LoopFilterDisabled ? H265Picture.BypassFlag : 0));
        for (var row = 0; row < blocks; row++)
        {
            var offset = start + (row * picture.GridWidth);
            for (var column = 0; column < blocks; column++)
            {
                picture.Flags[offset + column] |= flag;
            }
        }
    }
}
