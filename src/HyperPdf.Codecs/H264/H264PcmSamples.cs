// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.H264;

/// <summary>Reads the samples of a macroblock that sends them as they are (I_PCM).</summary>
internal static class H264PcmSamples
{
    /// <summary>The count a block of raw samples is given, so the neighbours that follow see it as full.</summary>
    private const int FullBlock = 16;

    /// <summary>Reads the samples of a macroblock and records the macroblock as raw.</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="mb">The macroblock.</param>
    /// <param name="reader">The bits, positioned before the alignment bits.</param>
    internal static void Read(H264Picture picture, H264CurrentMb mb, ref MsbBitReader reader)
    {
        reader.Align();
        var x = mb.X * H264Layout.MbLuma;
        var y = mb.Y * H264Layout.MbLuma;
        var stride = picture.LumaStride;
        for (var row = 0; row < H264Layout.MbLuma; row++)
        {
            for (var column = 0; column < H264Layout.MbLuma; column++)
            {
                picture.Luma[((y + row) * stride) + x + column] = (byte)reader.Read(H264Layout.SampleBits);
            }
        }

        ReadChroma(picture, mb, ref reader, picture.Cb);
        ReadChroma(picture, mb, ref reader, picture.Cr);
        var address = mb.Address;
        picture.Kinds[address] = H264MbKind.Pcm;
        picture.Qp[address] = 0;
        Array.Fill(picture.IntraModes, (byte)H264IntraModes.DcMode, address * H264Layout.LumaBlocks, H264Layout.LumaBlocks);
        Array.Fill(picture.Counts, (byte)FullBlock, address * H264Picture.CountsPerMb, H264Picture.CountsPerMb);
    }

    /// <summary>Reads the raw samples of one chroma component of a macroblock.</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="mb">The macroblock.</param>
    /// <param name="reader">The bits.</param>
    /// <param name="plane">The chroma plane.</param>
    private static void ReadChroma(H264Picture picture, H264CurrentMb mb, ref MsbBitReader reader, byte[] plane)
    {
        var stride = picture.ChromaStride;
        for (var row = 0; row < H264Layout.MbChroma; row++)
        {
            var start = (((mb.Y * H264Layout.MbChroma) + row) * stride) + (mb.X * H264Layout.MbChroma);
            for (var column = 0; column < H264Layout.MbChroma; column++)
            {
                plane[start + column] = (byte)reader.Read(H264Layout.SampleBits);
            }
        }
    }
}
