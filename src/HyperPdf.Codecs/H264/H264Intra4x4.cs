// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs.H264;

/// <summary>The nine Intra 4x4 prediction modes of clause 8.3.1.2 of ITU-T H.264.</summary>
internal static class H264Intra4x4
{
    /// <summary>The vertical mode.</summary>
    internal const int Vertical = 0;

    /// <summary>The horizontal mode.</summary>
    internal const int Horizontal = 1;

    /// <summary>The DC mode.</summary>
    internal const int Dc = 2;

    /// <summary>The diagonal down left mode.</summary>
    internal const int DiagonalDownLeft = 3;

    /// <summary>The diagonal down right mode.</summary>
    internal const int DiagonalDownRight = 4;

    /// <summary>The vertical right mode.</summary>
    internal const int VerticalRight = 5;

    /// <summary>The horizontal down mode.</summary>
    internal const int HorizontalDown = 6;

    /// <summary>The vertical left mode.</summary>
    internal const int VerticalLeft = 7;

    /// <summary>The prediction of a block with no neighbours.</summary>
    private const int Mid = 128;

    /// <summary>The index of the corner sample in the edge array.</summary>
    private const int Corner = 4;

    /// <summary>The index in the edge array of the sample above the first column.</summary>
    private const int TopStart = 5;

    /// <summary>The samples in the edge array: four on the left, the corner, eight above.</summary>
    private const int EdgeSize = 13;

    /// <summary>The width of a block.</summary>
    private const int Width = 4;

    /// <summary>The offset of the third of three neighbouring samples.</summary>
    private const int Third = 2;

    /// <summary>The offset of the fourth sample.</summary>
    private const int Fourth = 3;

    /// <summary>The index of the last sample above, including the top right group.</summary>
    private const int LastTop = 7;

    /// <summary>The rounding of a filter with weights summing to 4.</summary>
    private const int Round4 = 2;

    /// <summary>The shift of a filter with weights summing to 4.</summary>
    private const int Shift4 = 2;

    /// <summary>The rounding of the mean of four neighbours.</summary>
    private const int Round8 = 4;

    /// <summary>The shift of the mean of eight neighbours.</summary>
    private const int Shift8 = 3;

    /// <summary>The value of the horizontal up index at which the last left sample is fully used.</summary>
    private const int UpLimit = 5;

    /// <summary>Predicts a 4x4 block into the picture.</summary>
    /// <param name="plane">The luma plane.</param>
    /// <param name="offset">The index of the block's first sample.</param>
    /// <param name="stride">The bytes in a row.</param>
    /// <param name="mode">The prediction mode.</param>
    /// <param name="neighbours">Which neighbours may be used.</param>
    internal static void Predict(byte[] plane, int offset, int stride, int mode, H264Neighbours neighbours)
    {
        Span<int> edge = stackalloc int[EdgeSize];
        LoadEdge(plane, offset, stride, neighbours, edge);
        Span<int> block = stackalloc int[Width * Width];
        switch (mode)
        {
            case Vertical:
            {
                for (var index = 0; index < block.Length; index++)
                {
                    block[index] = edge[TopStart + (index % Width)];
                }

                break;
            }

            case Horizontal:
            {
                for (var index = 0; index < block.Length; index++)
                {
                    block[index] = edge[Corner - 1 - (index / Width)];
                }

                break;
            }

            case Dc:
            {
                block.Fill(DcValue(edge, neighbours));
                break;
            }

            default:
            {
                Directional(block, edge, mode);
                break;
            }
        }

        for (var y = 0; y < Width; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                plane[offset + (y * stride) + x] = (byte)block[(y * Width) + x];
            }
        }
    }

    /// <summary>Loads the neighbouring samples, replacing missing top right samples with the last top sample.</summary>
    /// <param name="plane">The luma plane.</param>
    /// <param name="offset">The index of the block's first sample.</param>
    /// <param name="stride">The bytes in a row.</param>
    /// <param name="neighbours">Which neighbours may be used.</param>
    /// <param name="edge">Receives the samples: the left column from the bottom up, the corner, then the row above.</param>
    private static void LoadEdge(byte[] plane, int offset, int stride, H264Neighbours neighbours, Span<int> edge)
    {
        if (neighbours.Left)
        {
            for (var y = 0; y < Width; y++)
            {
                edge[Corner - 1 - y] = plane[offset + (y * stride) - 1];
            }
        }

        if (neighbours.TopLeft)
        {
            edge[Corner] = plane[offset - stride - 1];
        }

        if (!neighbours.Top)
        {
            return;
        }

        for (var x = 0; x < Width; x++)
        {
            edge[TopStart + x] = plane[offset - stride + x];
        }

        for (var x = Width; x <= LastTop; x++)
        {
            edge[TopStart + x] = neighbours.TopRight ? plane[offset - stride + x] : edge[TopStart + Fourth];
        }
    }

    /// <summary>Works out the DC prediction.</summary>
    /// <param name="edge">The edge samples.</param>
    /// <param name="neighbours">Which neighbours may be used.</param>
    /// <returns>The value every sample of the block takes.</returns>
    private static int DcValue(ReadOnlySpan<int> edge, H264Neighbours neighbours)
    {
        var top = edge[TopStart] + edge[TopStart + 1] + edge[TopStart + Third] + edge[TopStart + Fourth];
        var left = edge[0] + edge[1] + edge[Third] + edge[Fourth];
        if (neighbours.Top && neighbours.Left)
        {
            return (top + left + Round8) >> Shift8;
        }

        if (neighbours.Left)
        {
            return (left + Round4) >> Shift4;
        }

        return neighbours.Top ? (top + Round4) >> Shift4 : Mid;
    }

    /// <summary>Predicts with one of the directional modes.</summary>
    /// <param name="block">Receives the prediction.</param>
    /// <param name="edge">The edge samples.</param>
    /// <param name="mode">The mode, from 3 to 8.</param>
    private static void Directional(Span<int> block, ReadOnlySpan<int> edge, int mode)
    {
        for (var y = 0; y < Width; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                block[(y * Width) + x] = mode switch
                {
                    DiagonalDownLeft => DownLeft(edge, x, y),
                    DiagonalDownRight => DownRight(edge, x, y),
                    VerticalRight => VerticalRightValue(edge, x, y),
                    HorizontalDown => HorizontalDownValue(edge, x, y),
                    VerticalLeft => VerticalLeftValue(edge, x, y),
                    _ => HorizontalUpValue(edge, x, y),
                };
            }
        }
    }

    /// <summary>Gets the top sample p[x,-1].</summary>
    /// <param name="edge">The edge samples.</param>
    /// <param name="x">The column, from -1 to 7.</param>
    /// <returns>The sample; for -1, the corner.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Top(ReadOnlySpan<int> edge, int x) => edge[Corner + 1 + x];

    /// <summary>Gets the left sample p[-1,y].</summary>
    /// <param name="edge">The edge samples.</param>
    /// <param name="y">The row, from -1 to 3.</param>
    /// <returns>The sample; for -1, the corner.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Left(ReadOnlySpan<int> edge, int y) => edge[Corner - 1 - y];

    /// <summary>Averages three samples with weights 1, 2, 1.</summary>
    /// <param name="a">The first.</param>
    /// <param name="b">The middle one.</param>
    /// <param name="c">The last.</param>
    /// <returns>The filtered value.</returns>
    private static int Smooth3(int a, int b, int c) => (a + (b << 1) + c + Round4) >> Shift4;

    /// <summary>Averages two samples.</summary>
    /// <param name="a">The first.</param>
    /// <param name="b">The second.</param>
    /// <returns>The mean, rounded up.</returns>
    private static int Smooth2(int a, int b) => (a + b + 1) >> 1;

    /// <summary>Predicts one sample in the diagonal down left mode.</summary>
    /// <param name="edge">The edge samples.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns>The value.</returns>
    private static int DownLeft(ReadOnlySpan<int> edge, int x, int y) =>
        x == Fourth && y == Fourth
            ? (Top(edge, LastTop - 1) + (Top(edge, LastTop) * Fourth) + Round4) >> Shift4
            : Smooth3(Top(edge, x + y), Top(edge, x + y + 1), Top(edge, x + y + Third));

    /// <summary>Predicts one sample in the diagonal down right mode.</summary>
    /// <param name="edge">The edge samples.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns>The value.</returns>
    private static int DownRight(ReadOnlySpan<int> edge, int x, int y)
    {
        if (x > y)
        {
            return Smooth3(Top(edge, x - y - Third), Top(edge, x - y - 1), Top(edge, x - y));
        }

        return x < y
            ? Smooth3(Left(edge, y - x - Third), Left(edge, y - x - 1), Left(edge, y - x))
            : Smooth3(Top(edge, 0), Top(edge, -1), Left(edge, 0));
    }

    /// <summary>Predicts one sample in the vertical right mode.</summary>
    /// <param name="edge">The edge samples.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns>The value.</returns>
    private static int VerticalRightValue(ReadOnlySpan<int> edge, int x, int y)
    {
        var z = (x << 1) - y;
        var column = x - (y >> 1);
        if (z >= 0)
        {
            return (z & 1) == 0
                ? Smooth2(Top(edge, column - 1), Top(edge, column))
                : Smooth3(Top(edge, column - Third), Top(edge, column - 1), Top(edge, column));
        }

        return z == -1
            ? Smooth3(Left(edge, 0), Left(edge, -1), Top(edge, 0))
            : Smooth3(Left(edge, y - 1), Left(edge, y - Third), Left(edge, y - Fourth));
    }

    /// <summary>Predicts one sample in the horizontal down mode.</summary>
    /// <param name="edge">The edge samples.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns>The value.</returns>
    private static int HorizontalDownValue(ReadOnlySpan<int> edge, int x, int y)
    {
        var z = (y << 1) - x;
        var row = y - (x >> 1);
        if (z >= 0)
        {
            return (z & 1) == 0
                ? Smooth2(Left(edge, row - 1), Left(edge, row))
                : Smooth3(Left(edge, row - Third), Left(edge, row - 1), Left(edge, row));
        }

        return z == -1
            ? Smooth3(Left(edge, 0), Left(edge, -1), Top(edge, 0))
            : Smooth3(Top(edge, x - 1), Top(edge, x - Third), Top(edge, x - Fourth));
    }

    /// <summary>Predicts one sample in the vertical left mode.</summary>
    /// <param name="edge">The edge samples.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns>The value.</returns>
    private static int VerticalLeftValue(ReadOnlySpan<int> edge, int x, int y)
    {
        var column = x + (y >> 1);
        return (y & 1) == 0
            ? Smooth2(Top(edge, column), Top(edge, column + 1))
            : Smooth3(Top(edge, column), Top(edge, column + 1), Top(edge, column + Third));
    }

    /// <summary>Predicts one sample in the horizontal up mode.</summary>
    /// <param name="edge">The edge samples.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns>The value.</returns>
    private static int HorizontalUpValue(ReadOnlySpan<int> edge, int x, int y)
    {
        var z = x + (y << 1);
        var row = y + (x >> 1);
        return z >= UpLimit ? UpTail(edge, z) : UpBody(edge, z, row);
    }

    /// <summary>Predicts a sample of the horizontal up mode that lies past the end of the left column.</summary>
    /// <param name="edge">The edge samples.</param>
    /// <param name="z">The zHU value of the sample.</param>
    /// <returns>The value.</returns>
    private static int UpTail(ReadOnlySpan<int> edge, int z) =>
        z == UpLimit ? (Left(edge, Third) + (Left(edge, Fourth) * Fourth) + Round4) >> Shift4 : Left(edge, Fourth);

    /// <summary>Predicts a sample of the horizontal up mode that lies along the left column.</summary>
    /// <param name="edge">The edge samples.</param>
    /// <param name="z">The zHU value of the sample.</param>
    /// <param name="row">The first left sample the sample is filtered from.</param>
    /// <returns>The value.</returns>
    private static int UpBody(ReadOnlySpan<int> edge, int z, int row) =>
        (z & 1) == 0
            ? Smooth2(Left(edge, row), Left(edge, row + 1))
            : Smooth3(Left(edge, row), Left(edge, row + 1), Left(edge, row + Third));
}
