// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>Averages packed JBIG2 pixels into gray display samples.</summary>
internal static class Jbig2Downsampler
{
    /// <summary>The bits in one source byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The largest supported source block side.</summary>
    private const int TwoBytes = 16;

    /// <summary>The white sample value.</summary>
    private const int White = 255;

    /// <summary>Converts each square of binary source pixels into one averaged gray sample.</summary>
    /// <param name="rows">The packed source rows, with one meaning white.</param>
    /// <param name="sourceWidth">The source width.</param>
    /// <param name="sourceHeight">The source height.</param>
    /// <param name="rowBytes">The source row length.</param>
    /// <param name="levels">The number of power-of-two halvings.</param>
    /// <param name="samples">The destination, sized for the reduced width and height.</param>
    internal static void Average(ReadOnlySpan<byte> rows, int sourceWidth, int sourceHeight, int rowBytes, int levels, Span<byte> samples)
    {
        var side = 1 << levels;
        var width = (sourceWidth + side - 1) / side;
        var height = (sourceHeight + side - 1) / side;
        for (var y = 0; y < height; y++)
        {
            var y0 = y * side;
            var y1 = Math.Min(y0 + side, sourceHeight);
            for (var x = 0; x < width; x++)
            {
                var x0 = x * side;
                var x1 = Math.Min(x0 + side, sourceWidth);
                var white = CountWhite(rows, rowBytes, x0, x1, y0, y1, side);
                samples[(y * width) + x] = (byte)((white * White) / ((x1 - x0) * (y1 - y0)));
            }
        }
    }

    /// <summary>Counts white source bits in one display sample's source block.</summary>
    /// <param name="rows">The packed source rows.</param>
    /// <param name="rowBytes">The packed row length.</param>
    /// <param name="x0">The first column.</param>
    /// <param name="x1">One past the last column.</param>
    /// <param name="y0">The first row.</param>
    /// <param name="y1">One past the last row.</param>
    /// <param name="side">The full source block side.</param>
    /// <returns>The number of white bits.</returns>
    private static int CountWhite(ReadOnlySpan<byte> rows, int rowBytes, int x0, int x1, int y0, int y1, int side)
    {
        var white = 0;
        var startByte = x0 >> Jbig2Bits.ByteShift;
        if (x1 - x0 == side && side <= ByteBits)
        {
            var mask = ((1 << side) - 1) << (ByteBits - side - (x0 & Jbig2Bits.BitMask));
            for (var y = y0; y < y1; y++)
            {
                white += BitOperations.PopCount((uint)(rows[(y * rowBytes) + startByte] & mask));
            }

            return white;
        }

        if (x1 - x0 == TwoBytes)
        {
            for (var y = y0; y < y1; y++)
            {
                var offset = (y * rowBytes) + startByte;
                white += BitOperations.PopCount((uint)rows[offset]) + BitOperations.PopCount((uint)rows[offset + 1]);
            }

            return white;
        }

        for (var y = y0; y < y1; y++)
        {
            var row = rows.Slice(y * rowBytes, rowBytes);
            for (var x = x0; x < x1; x++)
            {
                white += Jbig2Bits.Get(row, x, x1);
            }
        }

        return white;
    }
}
