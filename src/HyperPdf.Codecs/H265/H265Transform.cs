// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.Intrinsics;

namespace HyperPdf.Codecs.H265;

/// <summary>
/// The inverse transform of 8.6.4: columns first, then rows, with the intermediate clip and the final rounding shift of 8.6.2.
/// Both stages sum 16 bit values times 8 bit matrix entries over at most 32 terms, so 32 bit sums are exact and four outputs are
/// computed at once.
/// </summary>
internal static class H265Transform
{
    /// <summary>The shift after the first stage.</summary>
    private const int FirstShift = 7;

    /// <summary>The bits that bit depth and the second shift add up to.</summary>
    private const int ShiftTotal = 20;

    /// <summary>The side of the DST block.</summary>
    private const int DstSide = 4;

    /// <summary>The number of 32 bit values in a 128 bit vector, which divides every block side.</summary>
    private const int Lanes = 4;

    /// <summary>The shift that turns a column index into the index of its group of lanes.</summary>
    private const int LaneShift = 2;

    /// <summary>Transforms a block of scaled coefficients to residual samples, in place.</summary>
    /// <param name="block">The coefficients, row after row; they are replaced by the residual.</param>
    /// <param name="log2Size">The base 2 logarithm of the block side, from 2 to 5.</param>
    /// <param name="dst">Whether to use the DST of 4x4 intra luma blocks.</param>
    /// <param name="bitDepth">The bit depth of the component.</param>
    /// <param name="extent">The columns and rows that can hold non-zero coefficients, the last index of each.</param>
    /// <param name="scratch">A work area of at least the block's size.</param>
    internal static void Inverse(Span<int> block, int log2Size, bool dst, int bitDepth, H265BlockExtent extent, Span<int> scratch)
    {
        var side = 1 << log2Size;
        var table = dst
            ? new H265TransformTable(H265TransformMatrix.DstWide(), DstSide, 1)
            : new H265TransformTable(H265TransformMatrix.DctWide(), H265TransformMatrix.Side, H265TransformMatrix.Side >> log2Size);
        Columns(block, scratch, side, table, extent);
        Rows(block, scratch, side, table, extent.LastColumn, ShiftTotal - bitDepth);
    }

    /// <summary>Transforms every column that may hold coefficients, writing the clipped result to the work area.</summary>
    /// <param name="block">The coefficients.</param>
    /// <param name="scratch">Receives the intermediate values, row after row.</param>
    /// <param name="side">The block side.</param>
    /// <param name="table">The matrix.</param>
    /// <param name="extent">The columns and rows that can hold coefficients.</param>
    private static void Columns(ReadOnlySpan<int> block, Span<int> scratch, int side, H265TransformTable table, H265BlockExtent extent)
    {
        var round = Vector128.Create(1 << (FirstShift - 1));
        var low = Vector128.Create((int)short.MinValue);
        var high = Vector128.Create((int)short.MaxValue);
        var groups = (extent.LastColumn >> LaneShift) + 1;
        for (var output = 0; output < side; output++)
        {
            for (var group = 0; group < groups; group++)
            {
                var x = group * Lanes;
                var sum = Vector128<int>.Zero;
                for (var j = 0; j <= extent.LastRow; j++)
                {
                    sum += Vector128.Create(table.Matrix[(j * table.Step * table.RowStride) + output]) * Vector128.Create(block.Slice((j * side) + x, Lanes));
                }

                Vector128.Clamp(Vector128.ShiftRightArithmetic(sum + round, FirstShift), low, high).CopyTo(scratch.Slice((output * side) + x, Lanes));
            }
        }
    }

    /// <summary>Transforms every row of the intermediate values, rounding and writing the residual back to the block.</summary>
    /// <param name="block">Receives the residual.</param>
    /// <param name="scratch">The intermediate values.</param>
    /// <param name="side">The block side.</param>
    /// <param name="table">The matrix.</param>
    /// <param name="lastColumn">The last column that holds values.</param>
    /// <param name="shift">The right shift that scales the result.</param>
    private static void Rows(Span<int> block, ReadOnlySpan<int> scratch, int side, H265TransformTable table, int lastColumn, int shift)
    {
        var round = Vector128.Create(1 << (shift - 1));
        for (var y = 0; y < side; y++)
        {
            for (var output = 0; output < side; output += Lanes)
            {
                var sum = Vector128<int>.Zero;
                for (var j = 0; j <= lastColumn; j++)
                {
                    sum += Vector128.Create(scratch[(y * side) + j]) * Vector128.Create(table.Matrix.Slice((j * table.Step * table.RowStride) + output, Lanes));
                }

                Vector128.ShiftRightArithmetic(sum + round, shift).CopyTo(block.Slice((y * side) + output, Lanes));
            }
        }
    }
}
