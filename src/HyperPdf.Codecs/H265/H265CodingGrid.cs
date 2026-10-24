// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Records what the decoder learns about each 4x4 block of a coding unit in the picture's grids.</summary>
internal static class H265CodingGrid
{
    /// <summary>Records the kind, depth and flags of a coding unit and its edges.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="unit">The coding unit.</param>
    /// <param name="depth">The quadtree depth.</param>
    /// <param name="skip">Whether the unit is skipped.</param>
    internal static void Mark(H265SliceContext context, H265CodingUnit unit, int depth, bool skip)
    {
        var picture = context.Picture;
        var kind = unit.Intra ? H265Picture.Intra : H265Picture.Inter;
        byte flags = 0;
        if (unit.Bypass)
        {
            flags |= H265Picture.BypassFlag;
        }

        if (skip)
        {
            flags |= H265Picture.SkipFlag;
        }

        var blocks = (1 << unit.Log2Size) >> H265Picture.GridLog2;
        var start = picture.GridIndex(unit.X, unit.Y);
        for (var row = 0; row < blocks; row++)
        {
            var offset = start + (row * picture.GridWidth);
            for (var column = 0; column < blocks; column++)
            {
                var index = offset + column;
                picture.Kind[index] = kind;
                picture.CtDepth[index] = (byte)depth;
                picture.IntraMode[index] = H265IntraModes.Dc;
                picture.Flags[index] = (byte)(flags | EdgeFlags(row, column));
                picture.Motion[index] = H265PuMotion.None;
            }
        }
    }

    /// <summary>Records the quantiser of a finished coding unit and remembers it for the quantiser prediction that follows.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="unit">The coding unit.</param>
    internal static void Finish(H265SliceContext context, H265CodingUnit unit)
    {
        var picture = context.Picture;
        var blocks = (1 << unit.Log2Size) >> H265Picture.GridLog2;
        var start = picture.GridIndex(unit.X, unit.Y);
        for (var row = 0; row < blocks; row++)
        {
            picture.QpY.AsSpan(start + (row * picture.GridWidth), blocks).Fill((sbyte)context.QpY);
        }

        context.LastQpY = context.QpY;
    }

    /// <summary>Sets the luma intra prediction mode of a square area of the picture.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="x">The column of the area.</param>
    /// <param name="y">The row of the area.</param>
    /// <param name="size">The side of the area in luma samples.</param>
    /// <param name="mode">The mode.</param>
    internal static void SetIntraMode(H265SliceContext context, int x, int y, int size, int mode)
    {
        var picture = context.Picture;
        var blocks = size >> H265Picture.GridLog2;
        var start = picture.GridIndex(x, y);
        for (var row = 0; row < blocks; row++)
        {
            picture.IntraMode.AsSpan(start + (row * picture.GridWidth), blocks).Fill((byte)mode);
        }
    }

    /// <summary>Sets the flags that mark the left or upper edge of a block as an edge of the given kind.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="x">The column of the block.</param>
    /// <param name="y">The row of the block.</param>
    /// <param name="width">The width of the block in luma samples.</param>
    /// <param name="height">The height of the block in luma samples.</param>
    /// <param name="left">The flag to set along the left edge.</param>
    /// <param name="top">The flag to set along the upper edge.</param>
    internal static void MarkEdges(H265SliceContext context, int x, int y, int width, int height, byte left, byte top)
    {
        var picture = context.Picture;
        var start = picture.GridIndex(x, y);
        var rows = height >> H265Picture.GridLog2;
        var columns = width >> H265Picture.GridLog2;
        for (var row = 0; row < rows; row++)
        {
            picture.Flags[start + (row * picture.GridWidth)] |= left;
        }

        for (var column = 0; column < columns; column++)
        {
            picture.Flags[start + column] |= top;
        }
    }

    /// <summary>Flags the 4x4 blocks of a transform block as holding luma coefficients.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="x">The column of the block.</param>
    /// <param name="y">The row of the block.</param>
    /// <param name="size">The side of the block in luma samples.</param>
    internal static void MarkCoded(H265SliceContext context, int x, int y, int size)
    {
        var picture = context.Picture;
        var blocks = size >> H265Picture.GridLog2;
        var start = picture.GridIndex(x, y);
        for (var row = 0; row < blocks; row++)
        {
            var offset = start + (row * picture.GridWidth);
            for (var column = 0; column < blocks; column++)
            {
                picture.Flags[offset + column] |= H265Picture.CodedFlag;
            }
        }
    }

    /// <summary>Finds the edge flags a block at a position inside its coding unit gets: the coding unit's own boundary counts as both a transform and a prediction block edge.</summary>
    /// <param name="row">The row of the block inside the unit.</param>
    /// <param name="column">The column of the block inside the unit.</param>
    /// <returns>The flags.</returns>
    private static byte EdgeFlags(int row, int column)
    {
        byte flags = 0;
        if (column == 0)
        {
            flags |= H265Picture.TransformEdgeLeft | H265Picture.PredictionEdgeLeft;
        }

        if (row == 0)
        {
            flags |= H265Picture.TransformEdgeTop | H265Picture.PredictionEdgeTop;
        }

        return flags;
    }
}
