// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>Works out the context increments of CABAC syntax elements from the neighbouring macroblocks of an intra slice (clause 9.3.3.1.1).</summary>
internal static class H264CabacNeighbours
{
    /// <summary>The chroma component shift of the DC flag bits: bit 0 is luma, bit 1 Cb and bit 2 Cr.</summary>
    internal const int ChromaDcFlagFirstBit = 1;

    /// <summary>The DC flags of a macroblock of raw samples.</summary>
    internal const byte AllDcFlags = 0x7;

    /// <summary>The weight of the macroblock above in a context increment; the macroblock to the left weighs 1.</summary>
    private const int TopWeight = 2;

    /// <summary>The context increment the second bin of the chroma coded block pattern adds.</summary>
    private const int SecondBinOffset = 4;

    /// <summary>The luma bits of a coded block pattern.</summary>
    private const int LumaMask = 0xF;

    /// <summary>The shift of the chroma value in a stored coded block pattern.</summary>
    private const int ChromaShift = 4;

    /// <summary>The chroma value that says AC levels are coded.</summary>
    private const int ChromaWithAc = 2;

    /// <summary>The 8x8 luma blocks in a row of a macroblock.</summary>
    private const int GroupsPerRow = 2;

    /// <summary>Finds the address of the macroblock to the left, or -1 when it is outside the slice.</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="mb">The macroblock being decoded.</param>
    /// <param name="slice">The number of the slice.</param>
    /// <returns>The address, or -1.</returns>
    internal static int Left(H264Picture picture, H264CurrentMb mb, int slice) =>
        mb.X > 0 && picture.SliceOfMb[mb.Address - 1] == slice ? mb.Address - 1 : -1;

    /// <summary>Finds the address of the macroblock above, or -1 when it is outside the slice.</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="mb">The macroblock being decoded.</param>
    /// <param name="slice">The number of the slice.</param>
    /// <returns>The address, or -1.</returns>
    internal static int Top(H264Picture picture, H264CurrentMb mb, int slice) =>
        mb.Y > 0 && picture.SliceOfMb[mb.Address - picture.MbWidth] == slice ? mb.Address - picture.MbWidth : -1;

    /// <summary>Gets the context increment of the first bin of mb_type in an I slice: the number of neighbours that are not I_NxN.</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="left">The address of the left macroblock, or -1.</param>
    /// <param name="top">The address of the macroblock above, or -1.</param>
    /// <returns>0 to 2.</returns>
    internal static int MbTypeInc(H264Picture picture, int left, int top) =>
        (left >= 0 && picture.Kinds[left] != H264MbKind.Intra4x4 ? 1 : 0) + (top >= 0 && picture.Kinds[top] != H264MbKind.Intra4x4 ? 1 : 0);

    /// <summary>Gets the context increment of transform_size_8x8_flag: the number of neighbours that use the 8x8 transform.</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="left">The address of the left macroblock, or -1.</param>
    /// <param name="top">The address of the macroblock above, or -1.</param>
    /// <returns>0 to 2.</returns>
    internal static int Transform8x8Inc(H264Picture picture, int left, int top) =>
        (left >= 0 && picture.Transform8x8[left] ? 1 : 0) + (top >= 0 && picture.Transform8x8[top] ? 1 : 0);

    /// <summary>Gets the context increment of intra_chroma_pred_mode: the number of neighbours with a chroma mode other than 0.</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="left">The address of the left macroblock, or -1.</param>
    /// <param name="top">The address of the macroblock above, or -1.</param>
    /// <returns>0 to 2.</returns>
    internal static int ChromaModeInc(H264Picture picture, int left, int top) =>
        (left >= 0 && picture.ChromaModes[left] != 0 ? 1 : 0) + (top >= 0 && picture.ChromaModes[top] != 0 ? 1 : 0);

    /// <summary>Gets the context increment of a luma bit of the coded block pattern.</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="mb">The macroblock being decoded, whose earlier luma bits are set.</param>
    /// <param name="group">The 8x8 block, 0 to 3.</param>
    /// <param name="left">The address of the left macroblock, or -1.</param>
    /// <param name="top">The address of the macroblock above, or -1.</param>
    /// <returns>0 to 3.</returns>
    internal static int CbpLumaInc(H264Picture picture, H264CurrentMb mb, int group, int left, int top)
    {
        var column = group % GroupsPerRow;
        var row = group / GroupsPerRow;
        var a = column > 0 ? BitClear(mb.CbpLuma, group - 1) : ForeignBitClear(picture, left, group + 1);
        var b = row > 0 ? BitClear(mb.CbpLuma, group - GroupsPerRow) : ForeignBitClear(picture, top, group + GroupsPerRow);
        return a + (TopWeight * b);
    }

    /// <summary>Gets the context increment of a bin of the chroma part of the coded block pattern.</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="left">The address of the left macroblock, or -1.</param>
    /// <param name="top">The address of the macroblock above, or -1.</param>
    /// <param name="second">Whether this is the second bin, which asks whether AC levels are coded.</param>
    /// <returns>0 to 7.</returns>
    internal static int CbpChromaInc(H264Picture picture, int left, int top, bool second)
    {
        var a = ChromaCondition(picture, left, second);
        var b = ChromaCondition(picture, top, second);
        return a + (TopWeight * b) + (second ? SecondBinOffset : 0);
    }

    /// <summary>Gets the context increment of coded_block_flag for a DC block.</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="left">The address of the left macroblock, or -1.</param>
    /// <param name="top">The address of the macroblock above, or -1.</param>
    /// <param name="bit">The bit of the DC flags: 0 luma, 1 Cb, 2 Cr.</param>
    /// <param name="intra">Whether the macroblock being decoded is intra coded, which makes a missing neighbour count as coded.</param>
    /// <returns>0 to 3.</returns>
    internal static int DcFlagInc(H264Picture picture, int left, int top, int bit, bool intra) =>
        DcFlagOf(picture, left, bit, intra) + (TopWeight * DcFlagOf(picture, top, bit, intra));

    /// <summary>Gets the context increment of coded_block_flag for a 4x4 block.</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="mb">The macroblock being decoded.</param>
    /// <param name="slice">The number of the slice.</param>
    /// <param name="first">The index of the block group's first count among the macroblock's counts.</param>
    /// <param name="perRow">The blocks in a row of the group, 4 for luma and 2 for chroma.</param>
    /// <param name="column">The block's column.</param>
    /// <param name="row">The block's row.</param>
    /// <returns>0 to 3.</returns>
    internal static int BlockFlagInc(H264Picture picture, H264CurrentMb mb, int slice, int first, int perRow, int column, int row)
    {
        var address = mb.Address;
        var intra = mb.Kind != H264MbKind.Inter;
        var a = column > 0
            ? picture.Counts[(address * H264Picture.CountsPerMb) + first + (row * perRow) + column - 1]
            : CountOfNeighbour(picture, Left(picture, mb, slice), first + (row * perRow) + perRow - 1, intra);
        var b = row > 0
            ? picture.Counts[(address * H264Picture.CountsPerMb) + first + ((row - 1) * perRow) + column]
            : CountOfNeighbour(picture, Top(picture, mb, slice), first + ((perRow - 1) * perRow) + column, intra);
        return (a != 0 ? 1 : 0) + (b != 0 ? TopWeight : 0);
    }

    /// <summary>Gets 1 when a bit of the luma coded block pattern is 0.</summary>
    /// <param name="pattern">The luma bits.</param>
    /// <param name="group">The 8x8 block.</param>
    /// <returns>1 or 0.</returns>
    private static int BitClear(int pattern, int group) => ((pattern >> group) & 1) == 0 ? 1 : 0;

    /// <summary>Gets 1 when a neighbouring macroblock has a luma coded block pattern bit of 0 (a macroblock of raw samples and a missing one give 0).</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="address">The neighbour's address, or -1.</param>
    /// <param name="group">The 8x8 block in the neighbour.</param>
    /// <returns>1 or 0.</returns>
    private static int ForeignBitClear(H264Picture picture, int address, int group) =>
        address < 0 || picture.Kinds[address] == H264MbKind.Pcm ? 0 : BitClear(picture.Cbp[address] & LumaMask, group);

    /// <summary>Tells whether a neighbour counts towards a bin of the chroma coded block pattern.</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="address">The neighbour's address, or -1.</param>
    /// <param name="second">Whether this is the second bin.</param>
    /// <returns>1 or 0.</returns>
    private static int ChromaCondition(H264Picture picture, int address, bool second)
    {
        if (address < 0)
        {
            return 0;
        }

        if (picture.Kinds[address] == H264MbKind.Pcm)
        {
            return 1;
        }

        var chroma = picture.Cbp[address] >> ChromaShift;
        var counts = second ? chroma == ChromaWithAc : chroma != 0;
        return counts ? 1 : 0;
    }

    /// <summary>Gets a DC flag of a neighbour; a missing neighbour counts as 1 because the macroblock being decoded is intra.</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="address">The neighbour's address, or -1.</param>
    /// <param name="bit">The bit of the flags.</param>
    /// <param name="intra">Whether the macroblock being decoded is intra coded.</param>
    /// <returns>1 or 0.</returns>
    private static int DcFlagOf(H264Picture picture, int address, int bit, bool intra)
    {
        if (address >= 0)
        {
            return (picture.DcFlags[address] >> bit) & 1;
        }

        return intra ? 1 : 0;
    }

    /// <summary>Gets the coefficient count of a block in a neighbouring macroblock; a missing macroblock gives 1 for an intra macroblock, so it counts as coded, and 0 for an inter one.</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="address">The neighbour's address, or -1.</param>
    /// <param name="index">The block's index among the macroblock's counts.</param>
    /// <param name="intra">Whether the macroblock being decoded is intra coded.</param>
    /// <returns>The count.</returns>
    private static int CountOfNeighbour(H264Picture picture, int address, int index, bool intra)
    {
        if (address >= 0)
        {
            return picture.Counts[(address * H264Picture.CountsPerMb) + index];
        }

        return intra ? 1 : 0;
    }
}
