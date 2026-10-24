// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.H264;

/// <summary>Reads the residual blocks of a macroblock coded with CAVLC (clause 7.3.5.3 of ITU-T H.264).</summary>
internal static class H264CavlcResidual
{
    /// <summary>The luma blocks in an 8x8 group, which share a bit of the coded block pattern.</summary>
    private const int BlocksPerGroup = 4;

    /// <summary>The coefficients of an Intra 16x16 DC block or of a luma block.</summary>
    private const int FullBlock = 16;

    /// <summary>The coefficients of an 8x8 block.</summary>
    private const int LargeBlock = 64;

    /// <summary>The nC value of a chroma DC block.</summary>
    private const int ChromaDcNc = -1;

    /// <summary>The coefficients of a chroma DC block.</summary>
    private const int ChromaDcCoefficients = 4;

    /// <summary>The coded block pattern chroma value that has AC coefficients.</summary>
    private const int ChromaWithAc = 2;

    /// <summary>Reads the residual blocks of the macroblock.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="picture">The picture, which keeps the coefficient counts the neighbours need.</param>
    /// <param name="mb">The macroblock, which receives the levels.</param>
    /// <param name="slice">The number of the slice.</param>
    internal static void Read(ref MsbBitReader reader, H264Picture picture, H264CurrentMb mb, int slice)
    {
        Span<int> levels = stackalloc int[FullBlock];
        var zigzag = H264Tables.ZigZag4x4;
        var intra16 = mb.Kind == H264MbKind.Intra16x16;
        if (intra16)
        {
            var nc = H264Availability.LumaNc(picture, mb.X, mb.Y, slice, 0, 0);
            _ = H264Cavlc.Read(ref reader, nc, FullBlock, levels);
            for (var scan = 0; scan < FullBlock; scan++)
            {
                mb.LumaDc[zigzag[scan]] = levels[scan];
            }
        }

        var first = intra16 ? 1 : 0;
        var limit = intra16 ? H264Layout.AcCoefficients : FullBlock;
        for (var block = 0; block < H264Layout.LumaBlocks; block++)
        {
            if ((mb.CbpLuma & (1 << (block / BlocksPerGroup))) == 0)
            {
                continue;
            }

            int column = H264Tables.BlockColumn[block];
            int row = H264Tables.BlockRow[block];
            var nc = H264Availability.LumaNc(picture, mb.X, mb.Y, slice, column, row);
            var count = H264Cavlc.Read(ref reader, nc, limit, levels);
            picture.Counts[(mb.Address * H264Picture.CountsPerMb) + (row * H264Layout.BlocksPerRow) + column] = (byte)count;
            if (mb.Transform8x8)
            {
                // The four 4x4 reads of an 8x8 block are interleaved: read number k holds every fourth coefficient from k.
                var scan8 = H264Tables.ZigZag8x8;
                for (var scan = 0; scan < FullBlock; scan++)
                {
                    mb.Luma[((block / BlocksPerGroup) * LargeBlock) + scan8[(scan * BlocksPerGroup) + (block % BlocksPerGroup)]] = levels[scan];
                }

                continue;
            }

            for (var scan = 0; scan < limit; scan++)
            {
                mb.Luma[(block * H264Layout.BlockSamples) + zigzag[scan + first]] = levels[scan];
            }
        }

        ReadChroma(ref reader, picture, mb, slice, levels);
    }

    /// <summary>Reads the chroma residual: the DC levels of both components, then the AC blocks.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="picture">The picture.</param>
    /// <param name="mb">The macroblock.</param>
    /// <param name="slice">The number of the slice.</param>
    /// <param name="levels">A place to read the levels into.</param>
    private static void ReadChroma(ref MsbBitReader reader, H264Picture picture, H264CurrentMb mb, int slice, scoped Span<int> levels)
    {
        if (mb.CbpChroma == 0)
        {
            return;
        }

        for (var component = 0; component < H264Layout.Components; component++)
        {
            _ = H264Cavlc.Read(ref reader, ChromaDcNc, ChromaDcCoefficients, levels);
            for (var index = 0; index < ChromaDcCoefficients; index++)
            {
                mb.ChromaDc[(component * ChromaDcCoefficients) + index] = levels[index];
            }
        }

        if (mb.CbpChroma < ChromaWithAc)
        {
            return;
        }

        ReadChromaAc(ref reader, picture, mb, slice, levels);
    }

    /// <summary>Reads the chroma AC blocks.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="picture">The picture.</param>
    /// <param name="mb">The macroblock.</param>
    /// <param name="slice">The number of the slice.</param>
    /// <param name="levels">A place to read the levels into.</param>
    private static void ReadChromaAc(ref MsbBitReader reader, H264Picture picture, H264CurrentMb mb, int slice, scoped Span<int> levels)
    {
        var zigzag = H264Tables.ZigZag4x4;
        for (var component = 0; component < H264Layout.Components; component++)
        {
            for (var block = 0; block < H264Layout.ChromaBlocks; block++)
            {
                var nc = H264Availability.ChromaNc(picture, mb.X, mb.Y, slice, component, block % H264Layout.ChromaBlocksPerRow, block / H264Layout.ChromaBlocksPerRow);
                var count = H264Cavlc.Read(ref reader, nc, H264Layout.AcCoefficients, levels);
                var index = (component * H264Layout.ChromaBlocks) + block;
                picture.Counts[(mb.Address * H264Picture.CountsPerMb) + H264Picture.ChromaCounts + index] = (byte)count;
                for (var scan = 0; scan < H264Layout.AcCoefficients; scan++)
                {
                    mb.ChromaAc[(index * H264Layout.BlockSamples) + zigzag[scan + 1]] = levels[scan];
                }
            }
        }
    }
}
