// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>Predicts the Intra 4x4 prediction mode of a block from the modes of the blocks to its left and above (clause 8.3.1.1).</summary>
internal static class H264IntraModes
{
    /// <summary>The prediction mode a block takes when no neighbour gives one.</summary>
    internal const int DcMode = 2;

    /// <summary>The 8x8 blocks in a macroblock.</summary>
    internal const int Blocks8x8 = 4;

    /// <summary>The 4x4 blocks in an 8x8 block.</summary>
    private const int BlocksPerGroup = 4;

    /// <summary>The 4x4 blocks along the side of an 8x8 block.</summary>
    private const int GroupSide = 2;

    /// <summary>Records the prediction mode of a block so the blocks that follow, and later macroblocks, can predict from it.</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="mb">The macroblock being decoded.</param>
    /// <param name="block">The block, in coding order: a 4x4 block, or an 8x8 block when the macroblock uses the 8x8 transform.</param>
    /// <param name="mode">The mode.</param>
    internal static void Store(H264Picture picture, H264CurrentMb mb, int block, int mode)
    {
        mb.Modes[block] = mode;
        var origin = mb.Address * H264Layout.LumaBlocks;
        if (!mb.Transform8x8)
        {
            picture.IntraModes[origin + (H264Tables.BlockRow[block] * H264Layout.BlocksPerRow) + H264Tables.BlockColumn[block]] = (byte)mode;
            return;
        }

        var firstRow = (block / GroupSide) * GroupSide;
        var firstColumn = (block % GroupSide) * GroupSide;
        for (var row = 0; row < GroupSide; row++)
        {
            for (var column = 0; column < GroupSide; column++)
            {
                picture.IntraModes[origin + ((firstRow + row) * H264Layout.BlocksPerRow) + firstColumn + column] = (byte)mode;
            }
        }
    }

    /// <summary>Works out the prediction mode a block is expected to have from its neighbours.</summary>
    /// <param name="picture">The picture, which holds the modes of the blocks decoded so far.</param>
    /// <param name="mb">The macroblock being decoded.</param>
    /// <param name="coded">The block, in coding order: a 4x4 block, or an 8x8 block when the macroblock uses the 8x8 transform.</param>
    /// <returns>The predicted mode.</returns>
    internal static int Predict(H264Picture picture, H264CurrentMb mb, int coded)
    {
        var block = mb.Transform8x8 ? coded * BlocksPerGroup : coded;
        int column = H264Tables.BlockColumn[block];
        int row = H264Tables.BlockRow[block];
        var left = column != 0
            ? new H264BlockRef(mb.Address, row, column - 1, true)
            : new H264BlockRef(mb.Address - 1, row, H264Layout.LastBlock, mb.Neighbours.Left);
        var top = row != 0
            ? new H264BlockRef(mb.Address, row - 1, column, true)
            : new H264BlockRef(mb.Address - picture.MbWidth, H264Layout.LastBlock, column, mb.Neighbours.Top);
        var leftMode = ModeOf(picture, mb, left);
        var topMode = ModeOf(picture, mb, top);
        return leftMode < 0 || topMode < 0 ? DcMode : Math.Min(leftMode, topMode);
    }

    /// <summary>Gets the mode of a neighbouring 4x4 block.</summary>
    /// <param name="picture">The picture.</param>
    /// <param name="mb">The macroblock being decoded.</param>
    /// <param name="block">The block.</param>
    /// <returns>The mode, or -1 when the block is not available so that DC is predicted.</returns>
    private static int ModeOf(H264Picture picture, H264CurrentMb mb, H264BlockRef block)
    {
        if (!block.Available)
        {
            return -1;
        }

        var notIntra4x4 = block.Address != mb.Address && picture.Kinds[block.Address] != H264MbKind.Intra4x4;
        return notIntra4x4 ? DcMode : picture.IntraModes[(block.Address * H264Layout.LumaBlocks) + (block.Row * H264Layout.BlocksPerRow) + block.Column];
    }
}
