// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs.H264;

/// <summary>Predicts motion vectors from the neighbouring blocks (clause 8.4.1.3 of ITU-T H.264). Positions are in 4x4 blocks from the macroblock's top left corner.</summary>
[DebuggerDisplay("H264MotionPrediction: mask {DecodedMask}")]
internal sealed class H264MotionPrediction
{
    /// <summary>The shape of a partition that has no directional rule.</summary>
    internal const int Plain = 0;

    /// <summary>The upper partition of a 16x8 macroblock.</summary>
    internal const int Upper16x8 = 1;

    /// <summary>The lower partition of a 16x8 macroblock.</summary>
    internal const int Lower16x8 = 2;

    /// <summary>The left partition of an 8x16 macroblock.</summary>
    internal const int Left8x16 = 3;

    /// <summary>The right partition of an 8x16 macroblock.</summary>
    internal const int Right8x16 = 4;

    /// <summary>The 4x4 blocks along a macroblock side.</summary>
    private const int Side = 4;

    /// <summary>The mask that wraps a block position into the neighbouring macroblock.</summary>
    private const int SideMask = 3;

    /// <summary>The 4x4 blocks along an 8x8 partition.</summary>
    private const int Half = 2;

    /// <summary>The picture being decoded.</summary>
    private readonly H264Picture _picture;

    /// <summary>The macroblock being decoded.</summary>
    private readonly H264CurrentMb _mb;

    /// <summary>The number of the slice.</summary>
    private readonly int _slice;

    /// <summary>The motion of the reference list the vectors are predicted for.</summary>
    private readonly H264MotionField _field;

    /// <summary>Initializes a new instance of the <see cref="H264MotionPrediction"/> class.</summary>
    /// <param name="picture">The picture being decoded.</param>
    /// <param name="mb">The macroblock being decoded, which the caller moves from one macroblock to the next.</param>
    /// <param name="slice">The number of the slice.</param>
    /// <param name="field">The motion of the reference list the vectors are predicted for.</param>
    internal H264MotionPrediction(H264Picture picture, H264CurrentMb mb, int slice, H264MotionField field)
    {
        _picture = picture;
        _mb = mb;
        _slice = slice;
        _field = field;
    }

    /// <summary>Gets which 4x4 blocks of the macroblock have motion already, one bit per block in raster order.</summary>
    internal int DecodedMask { get; private set; }

    /// <summary>Gets the neighbour that is not available.</summary>
    private static H264MotionNeighbour Missing { get; } = new(false, -1, 0, 0);

    /// <summary>Starts a macroblock: none of its blocks has motion yet.</summary>
    internal void Begin() => DecodedMask = 0;

    /// <summary>Records that the blocks of a partition have motion, so the partitions after it can predict from them.</summary>
    /// <param name="partition">The partition.</param>
    internal void MarkDecoded(H264Partition partition)
    {
        for (var row = partition.Row; row < partition.Row + partition.Height; row++)
        {
            for (var column = partition.Column; column < partition.Column + partition.Width; column++)
            {
                DecodedMask |= 1 << ((row * Side) + column);
            }
        }
    }

    /// <summary>Finds the macroblock that holds the block at a position, whether or not the block has been decoded yet.</summary>
    /// <param name="column">The block's column relative to the current macroblock, from -1 to 3.</param>
    /// <param name="row">The block's row relative to the current macroblock, from -1 to 3.</param>
    /// <returns>The macroblock's address (the current macroblock for a block inside it), or -1 when the block is outside the picture or the slice.</returns>
    internal int MacroblockAt(int column, int row)
    {
        var shiftX = ColumnShift(column);
        var shiftY = row < 0 ? -1 : 0;
        return shiftX == 0 && shiftY == 0 ? _mb.Address : OutsideAddress(shiftX, shiftY);
    }

    /// <summary>Gets the reference indices of the neighbours a whole macroblock has to its left, above, and above and to the right (or above and to the left), for spatial direct prediction.</summary>
    /// <returns>The three reference indices, each -1 when the neighbour is missing or does not use the list.</returns>
    internal H264RefTriple NeighbourRefs()
    {
        var a = Neighbour(-1, 0);
        var b = Neighbour(0, -1);
        var c = Neighbour(Side, -1);
        if (!c.Available)
        {
            c = Neighbour(-1, -1);
        }

        return new(a.RefIdx, b.RefIdx, c.RefIdx);
    }

    /// <summary>Predicts the motion vector of a partition.</summary>
    /// <param name="partition">The partition.</param>
    /// <param name="refIdx">The partition's reference index.</param>
    /// <returns>The predicted vector.</returns>
    internal MotionVector Predict(H264Partition partition, int refIdx)
    {
        var a = Neighbour(partition.Column - 1, partition.Row);
        var b = Neighbour(partition.Column, partition.Row - 1);
        var c = Neighbour(partition.Column + partition.Width, partition.Row - 1);
        if (!c.Available)
        {
            c = Neighbour(partition.Column - 1, partition.Row - 1);
        }

        if (!b.Available && !c.Available && a.Available)
        {
            b = a;
            c = a;
        }

        var directional = Directional(a, b, c, refIdx, partition.Shape);
        return directional ?? Combine(a, b, c, refIdx);
    }

    /// <summary>Predicts the motion vector of a P_Skip macroblock (clause 8.4.1.1).</summary>
    /// <returns>The vector.</returns>
    internal MotionVector Skip()
    {
        var a = Neighbour(-1, 0);
        var b = Neighbour(0, -1);
        var zero = !a.Available || !b.Available || IsZeroOnFirst(a) || IsZeroOnFirst(b);
        return zero ? default : Predict(new(0, 0, Side, Side, Plain), 0);
    }

    /// <summary>Tells whether a neighbour uses the first reference with no motion.</summary>
    /// <param name="neighbour">The neighbour.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool IsZeroOnFirst(H264MotionNeighbour neighbour) => neighbour.RefIdx == 0 && neighbour.X == 0 && neighbour.Y == 0;

    /// <summary>Applies the rules that pick one neighbour's vector for 16x8 and 8x16 partitions.</summary>
    /// <param name="a">The neighbour to the left.</param>
    /// <param name="b">The neighbour above.</param>
    /// <param name="c">The neighbour above and to the right, or above and to the left.</param>
    /// <param name="refIdx">The partition's reference index.</param>
    /// <param name="shape">The partition's shape.</param>
    /// <returns>The vector, or <see langword="null"/> when no rule applies.</returns>
    private static MotionVector? Directional(H264MotionNeighbour a, H264MotionNeighbour b, H264MotionNeighbour c, int refIdx, int shape)
    {
        var chosen = shape switch
        {
            Upper16x8 => b,
            Lower16x8 or Left8x16 => a,
            Right8x16 => c,
            _ => Missing,
        };
        return shape != Plain && chosen.RefIdx == refIdx ? new MotionVector(chosen.X, chosen.Y) : null;
    }

    /// <summary>Takes the one neighbour that uses the same reference, or the median of the three vectors.</summary>
    /// <param name="a">The neighbour to the left.</param>
    /// <param name="b">The neighbour above.</param>
    /// <param name="c">The neighbour above and to the right, or above and to the left.</param>
    /// <param name="refIdx">The partition's reference index.</param>
    /// <returns>The vector.</returns>
    private static MotionVector Combine(H264MotionNeighbour a, H264MotionNeighbour b, H264MotionNeighbour c, int refIdx)
    {
        var matches = (a.RefIdx == refIdx ? 1 : 0) + (b.RefIdx == refIdx ? 1 : 0) + (c.RefIdx == refIdx ? 1 : 0);
        if (matches != 1)
        {
            return new(Median(a.X, b.X, c.X), Median(a.Y, b.Y, c.Y));
        }

        var only = c;
        if (a.RefIdx == refIdx)
        {
            only = a;
        }
        else if (b.RefIdx == refIdx)
        {
            only = b;
        }

        return new(only.X, only.Y);
    }

    /// <summary>Finds which macroblock a block column falls in relative to the current one.</summary>
    /// <param name="column">The block column, from -1 to 4.</param>
    /// <returns>-1 for the macroblock to the left, 1 for the one to the right, otherwise 0.</returns>
    private static int ColumnShift(int column)
    {
        if (column < 0)
        {
            return -1;
        }

        return column >= Side ? 1 : 0;
    }

    /// <summary>Takes the middle of three values.</summary>
    /// <param name="a">The first value.</param>
    /// <param name="b">The second value.</param>
    /// <param name="c">The third value.</param>
    /// <returns>The median.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Median(int a, int b, int c) => Math.Max(Math.Min(a, b), Math.Min(Math.Max(a, b), c));

    /// <summary>Looks at the block at a position relative to the current macroblock.</summary>
    /// <param name="column">The block's column, from -1 to 4.</param>
    /// <param name="row">The block's row, from -1 to 3.</param>
    /// <returns>What the block holds, or a neighbour marked unavailable.</returns>
    private H264MotionNeighbour Neighbour(int column, int row)
    {
        var address = AddressOf(column, row);
        if (address < 0)
        {
            return Missing;
        }

        var blockColumn = (column + Side) & SideMask;
        var blockRow = (row + Side) & SideMask;
        var group = ((blockRow >> 1) * Half) + (blockColumn >> 1);
        var index = (address * H264Picture.BlocksPerMb) + (blockRow * Side) + blockColumn;
        return new(true, _field.RefIdx[(address * H264Picture.GroupsPerMb) + group], _field.MvX[index], _field.MvY[index]);
    }

    /// <summary>Finds the macroblock that holds the block at a position, if it is available.</summary>
    /// <param name="column">The block's column relative to the current macroblock.</param>
    /// <param name="row">The block's row relative to the current macroblock.</param>
    /// <returns>The macroblock's address, or -1 when the block is outside the picture or the slice, or not decoded yet.</returns>
    private int AddressOf(int column, int row)
    {
        var shiftX = ColumnShift(column);
        var shiftY = row < 0 ? -1 : 0;
        if (shiftX == 0 && shiftY == 0)
        {
            return ((DecodedMask >> ((row * Side) + column)) & 1) != 0 ? _mb.Address : -1;
        }

        return OutsideAddress(shiftX, shiftY);
    }

    /// <summary>Finds a neighbouring macroblock if it is available.</summary>
    /// <param name="shiftX">The column of the neighbour relative to the current macroblock: -1, 0 or 1.</param>
    /// <param name="shiftY">The row of the neighbour relative to the current macroblock: -1 or 0.</param>
    /// <returns>The macroblock's address, or -1 when it is outside the picture or the slice, or not decoded yet.</returns>
    private int OutsideAddress(int shiftX, int shiftY)
    {
        var x = _mb.X + shiftX;
        var y = _mb.Y + shiftY;

        // The macroblock to the right on the same row comes later in decoding order.
        if ((shiftX > 0 && shiftY == 0) || x < 0 || x >= _picture.MbWidth || y < 0)
        {
            return -1;
        }

        var address = (y * _picture.MbWidth) + x;
        return _picture.SliceOfMb[address] == _slice ? address : -1;
    }
}
