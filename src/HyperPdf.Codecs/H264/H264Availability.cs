// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>Works out which neighbouring macroblocks can be used, and the coefficient counts that choose CAVLC tables.</summary>
internal static class H264Availability
{
    /// <summary>The blocks in a row of a macroblock's luma blocks.</summary>
    private const int BlocksPerRow = 4;

    /// <summary>The blocks in a row of a macroblock's chroma blocks.</summary>
    private const int ChromaPerRow = 2;

    /// <summary>The count of a macroblock whose samples are sent as they are.</summary>
    private const int PcmCount = 16;

    /// <summary>Finds which neighbouring macroblocks can predict the samples of a macroblock.</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="x">The macroblock's column.</param>
    /// <param name="y">The macroblock's row.</param>
    /// <param name="slice">The number of the slice being decoded.</param>
    /// <param name="constrainedIntra">Whether intra prediction may use only intra neighbours.</param>
    /// <returns>The neighbours that can be used.</returns>
    internal static H264Neighbours ForIntra(H264Picture picture, int x, int y, int slice, bool constrainedIntra)
    {
        var width = picture.MbWidth;
        var address = (y * width) + x;
        var left = x > 0 && Usable(picture, address - 1, slice, constrainedIntra);
        var top = y > 0 && Usable(picture, address - width, slice, constrainedIntra);
        var topRight = y > 0 && x < width - 1 && Usable(picture, address - width + 1, slice, constrainedIntra);
        var topLeft = y > 0 && x > 0 && Usable(picture, address - width - 1, slice, constrainedIntra);
        return new(left, top, topRight, topLeft);
    }

    /// <summary>Finds the nC value for a luma block: the mean of the coefficient counts of the blocks to its left and above.</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="x">The macroblock's column.</param>
    /// <param name="y">The macroblock's row.</param>
    /// <param name="slice">The number of the slice being decoded.</param>
    /// <param name="blockX">The block's column in the macroblock, 0 to 3.</param>
    /// <param name="blockY">The block's row in the macroblock, 0 to 3.</param>
    /// <returns>The nC value.</returns>
    internal static int LumaNc(H264Picture picture, int x, int y, int slice, int blockX, int blockY)
    {
        var width = picture.MbWidth;
        var address = (y * width) + x;
        var a = blockX > 0
            ? picture.Counts[(address * H264Picture.CountsPerMb) + (blockY * BlocksPerRow) + blockX - 1]
            : NeighbourCount(picture, x > 0, address - 1, slice, (blockY * BlocksPerRow) + BlocksPerRow - 1);
        var b = blockY > 0
            ? picture.Counts[(address * H264Picture.CountsPerMb) + ((blockY - 1) * BlocksPerRow) + blockX]
            : NeighbourCount(picture, y > 0, address - width, slice, ((BlocksPerRow - 1) * BlocksPerRow) + blockX);
        return Combine(a, b);
    }

    /// <summary>Finds the nC value for a chroma AC block.</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="x">The macroblock's column.</param>
    /// <param name="y">The macroblock's row.</param>
    /// <param name="slice">The number of the slice being decoded.</param>
    /// <param name="component">0 for Cb, 1 for Cr.</param>
    /// <param name="blockX">The block's column in the macroblock, 0 or 1.</param>
    /// <param name="blockY">The block's row in the macroblock, 0 or 1.</param>
    /// <returns>The nC value.</returns>
    internal static int ChromaNc(H264Picture picture, int x, int y, int slice, int component, int blockX, int blockY)
    {
        var width = picture.MbWidth;
        var address = (y * width) + x;
        var first = H264Picture.ChromaCounts + (component * ChromaPerRow * ChromaPerRow);
        var a = blockX > 0
            ? picture.Counts[(address * H264Picture.CountsPerMb) + first + (blockY * ChromaPerRow) + blockX - 1]
            : NeighbourCount(picture, x > 0, address - 1, slice, first + (blockY * ChromaPerRow) + ChromaPerRow - 1);
        var b = blockY > 0
            ? picture.Counts[(address * H264Picture.CountsPerMb) + first + ((blockY - 1) * ChromaPerRow) + blockX]
            : NeighbourCount(picture, y > 0, address - width, slice, first + ((ChromaPerRow - 1) * ChromaPerRow) + blockX);
        return Combine(a, b);
    }

    /// <summary>Tells whether a neighbouring macroblock may predict samples.</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="address">The neighbour's address.</param>
    /// <param name="slice">The number of the slice being decoded.</param>
    /// <param name="constrainedIntra">Whether intra prediction may use only intra neighbours.</param>
    /// <returns><see langword="true"/> when the neighbour is in the same slice and, if needed, intra coded.</returns>
    private static bool Usable(H264Picture picture, int address, int slice, bool constrainedIntra) =>
        picture.SliceOfMb[address] == slice && (!constrainedIntra || picture.Kinds[address] != H264MbKind.Inter);

    /// <summary>Gets the coefficient count of a block in a neighbouring macroblock, or -1 when the macroblock is not available.</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="exists">Whether the macroblock is inside the picture.</param>
    /// <param name="address">The macroblock's address.</param>
    /// <param name="slice">The number of the slice being decoded.</param>
    /// <param name="index">The block's index among the macroblock's counts.</param>
    /// <returns>The count; 16 for a macroblock of raw samples.</returns>
    private static int NeighbourCount(H264Picture picture, bool exists, int address, int slice, int index)
    {
        if (!exists || picture.SliceOfMb[address] != slice)
        {
            return -1;
        }

        return picture.Kinds[address] == H264MbKind.Pcm ? PcmCount : picture.Counts[(address * H264Picture.CountsPerMb) + index];
    }

    /// <summary>Combines the counts of the left and top blocks into nC.</summary>
    /// <param name="a">The left block's count, or -1.</param>
    /// <param name="b">The top block's count, or -1.</param>
    /// <returns>The nC value.</returns>
    private static int Combine(int a, int b)
    {
        if (a >= 0 && b >= 0)
        {
            return (a + b + 1) >> 1;
        }

        return a >= 0 ? a : Math.Max(b, 0);
    }
}
