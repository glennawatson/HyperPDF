// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;

namespace HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

/// <summary>
/// Compares two symbol bitmaps as they would be placed by the text region: their bottom left corners together. Rows are
/// packed, 1 for black, with zero padding bits.
/// </summary>
internal static class Jbig2ShapeComparer
{
    /// <summary>The shift that moves a byte's top bit to its bottom.</summary>
    private const int TopBit = 7;

    /// <summary>Counts the pixels black in both bitmaps.</summary>
    /// <param name="first">The first bitmap.</param>
    /// <param name="second">The second bitmap.</param>
    /// <returns>The number of pixels black in both.</returns>
    internal static int Intersection(in Jbig2BitmapView first, in Jbig2BitmapView second)
    {
        var rows = Math.Min(first.Height, second.Height);
        var bytes = Math.Min(first.Stride, second.Stride);
        var count = 0;
        for (var i = 1; i <= rows; i++)
        {
            var a = first.Row(first.Height - i);
            var b = second.Row(second.Height - i);
            for (var x = 0; x < bytes; x++)
            {
                count += BitOperations.PopCount((uint)(a[x] & b[x]));
            }
        }

        return count;
    }

    /// <summary>Determines whether every black pixel of one bitmap has a black pixel of the other within one pixel, in any direction.</summary>
    /// <param name="shape">The bitmap whose pixels must be covered.</param>
    /// <param name="cover">The bitmap that must cover them.</param>
    /// <param name="scratch">At least <c>shape.Stride + 2</c> bytes of working space.</param>
    /// <returns><see langword="true"/> when every black pixel of <paramref name="shape"/> is covered.</returns>
    internal static bool IsCovered(in Jbig2BitmapView shape, in Jbig2BitmapView cover, Span<byte> scratch)
    {
        var offset = shape.Height - cover.Height;
        var union = scratch[..(shape.Stride + 1)];
        for (var y = 0; y < shape.Height; y++)
        {
            var row = shape.Row(y);
            if (row.IndexOfAnyExcept((byte)0) < 0)
            {
                continue;
            }

            UnionRows(cover, y - offset, union);
            if (!RowIsCovered(row, union))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Ors the three rows of a bitmap around a row into a buffer, one byte longer than the row.</summary>
    /// <param name="cover">The bitmap.</param>
    /// <param name="center">The middle row; rows outside the bitmap are blank.</param>
    /// <param name="union">Receives the union; bytes the bitmap does not reach are zero.</param>
    private static void UnionRows(in Jbig2BitmapView cover, int center, Span<byte> union)
    {
        union.Clear();
        for (var y = center - 1; y <= center + 1; y++)
        {
            var row = cover.Row(y);
            var bytes = Math.Min(row.Length, union.Length);
            for (var x = 0; x < bytes; x++)
            {
                union[x] |= row[x];
            }
        }
    }

    /// <summary>Determines whether a row's black pixels all lie within one column of a union row's.</summary>
    /// <param name="row">The row.</param>
    /// <param name="union">The union row, one byte longer than <paramref name="row"/>.</param>
    /// <returns><see langword="true"/> when the row is covered.</returns>
    private static bool RowIsCovered(ReadOnlySpan<byte> row, ReadOnlySpan<byte> union)
    {
        for (var x = 0; x < row.Length; x++)
        {
            var center = (uint)union[x];
            var left = ((center << 1) | ((uint)union[x + 1] >> TopBit)) & byte.MaxValue;
            var right = (center >> 1) | (x > 0 ? ((uint)union[x - 1] << TopBit) & byte.MaxValue : 0);
            if ((row[x] & ~(center | left | right) & byte.MaxValue) != 0)
            {
                return false;
            }
        }

        return true;
    }
}
