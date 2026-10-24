// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs.H264;

/// <summary>The nine Intra 8x8 prediction modes of clause 8.3.2 of ITU-T H.264, using the filtered neighbouring samples.</summary>
internal static class H264Intra8x8
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

    /// <summary>The samples along a side of the block.</summary>
    private const int Size = 8;

    /// <summary>The samples above the block that are used, counting those above and to the right.</summary>
    private const int TopCount = 16;

    /// <summary>The index of the last sample above.</summary>
    private const int LastTop = TopCount - 1;

    /// <summary>The index of the last sample on the left.</summary>
    private const int LastLeft = Size - 1;

    /// <summary>The prediction of a block with no neighbours.</summary>
    private const int Mid = 128;

    /// <summary>The rounding of the three tap smoothing.</summary>
    private const int SmoothRound = 2;

    /// <summary>The shift of the three tap smoothing.</summary>
    private const int SmoothShift = 2;

    /// <summary>The weight of the sample next to the end in the two tap smoothing that repeats the edge.</summary>
    private const int EdgeWeight = 3;

    /// <summary>The shift of the DC mean of eight samples.</summary>
    private const int OneSideShift = 3;

    /// <summary>The shift of the DC mean of sixteen samples.</summary>
    private const int BothSidesShift = 4;

    /// <summary>The horizontal up position beyond which every sample repeats the last left sample.</summary>
    private const int HorizontalUpLimit = 13;

    /// <summary>Predicts an 8x8 block into the picture.</summary>
    /// <param name="plane">The luma plane.</param>
    /// <param name="offset">The index of the block's first sample.</param>
    /// <param name="stride">The bytes in a row.</param>
    /// <param name="mode">The prediction mode, 0 to 8.</param>
    /// <param name="neighbours">Which neighbours may be used.</param>
    internal static void Predict(byte[] plane, int offset, int stride, int mode, H264Neighbours neighbours)
    {
        Span<int> top = stackalloc int[TopCount];
        Span<int> left = stackalloc int[Size];
        var corner = Filter(plane, offset, stride, neighbours, top, left);
        var edges = new H264Edges(top, left, corner);
        var dc = mode == Dc ? DcValue(top, left, neighbours) : 0;
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                plane[offset + (y * stride) + x] = (byte)(mode == Dc ? dc : Sample(mode, x, y, edges));
            }
        }
    }

    /// <summary>Loads and filters the neighbouring samples (clause 8.3.2.2.1).</summary>
    /// <param name="plane">The plane.</param>
    /// <param name="offset">The index of the block's first sample.</param>
    /// <param name="stride">The bytes in a row.</param>
    /// <param name="neighbours">Which neighbours may be used.</param>
    /// <param name="top">Receives the 16 filtered samples above.</param>
    /// <param name="left">Receives the 8 filtered samples on the left.</param>
    /// <returns>The filtered sample above and to the left.</returns>
    private static int Filter(byte[] plane, int offset, int stride, H264Neighbours neighbours, Span<int> top, Span<int> left)
    {
        Span<int> rawTop = stackalloc int[TopCount];
        Span<int> rawLeft = stackalloc int[Size];
        var corner = neighbours.TopLeft ? plane[offset - stride - 1] : 0;
        if (neighbours.Top)
        {
            for (var x = 0; x < TopCount; x++)
            {
                rawTop[x] = x < Size || neighbours.TopRight ? plane[offset - stride + x] : rawTop[LastLeft];
            }

            Smooth(rawTop, top, neighbours.TopLeft ? corner : rawTop[0]);
        }

        if (neighbours.Left)
        {
            for (var y = 0; y < Size; y++)
            {
                rawLeft[y] = plane[offset + (y * stride) - 1];
            }

            Smooth(rawLeft, left, neighbours.TopLeft ? corner : rawLeft[0]);
        }

        return FilterCorner(neighbours, corner, rawTop[0], rawLeft[0]);
    }

    /// <summary>Smooths a row or column of samples with a 1-2-1 filter, repeating the sample at each end where nothing lies beyond it.</summary>
    /// <param name="raw">The samples.</param>
    /// <param name="filtered">Receives the smoothed samples.</param>
    /// <param name="before">The sample before the first one; the first sample itself when none exists.</param>
    private static void Smooth(ReadOnlySpan<int> raw, Span<int> filtered, int before)
    {
        filtered[0] = Smooth3(before, raw[0], raw[1]);
        for (var i = 1; i < raw.Length - 1; i++)
        {
            filtered[i] = Smooth3(raw[i - 1], raw[i], raw[i + 1]);
        }

        var last = raw.Length - 1;
        filtered[last] = Smooth3(raw[last - 1], raw[last], raw[last]);
    }

    /// <summary>Filters the sample above and to the left of the block.</summary>
    /// <param name="neighbours">Which neighbours may be used.</param>
    /// <param name="corner">The sample.</param>
    /// <param name="firstTop">The first sample above.</param>
    /// <param name="firstLeft">The first sample on the left.</param>
    /// <returns>The filtered sample.</returns>
    private static int FilterCorner(H264Neighbours neighbours, int corner, int firstTop, int firstLeft)
    {
        if (!neighbours.TopLeft)
        {
            return 0;
        }

        if (neighbours.Top && neighbours.Left)
        {
            return Smooth3(firstTop, corner, firstLeft);
        }

        if (neighbours.Top)
        {
            return Smooth3(corner, corner, firstTop);
        }

        return neighbours.Left ? Smooth3(corner, corner, firstLeft) : corner;
    }

    /// <summary>Works out one predicted sample other than DC.</summary>
    /// <param name="mode">The prediction mode.</param>
    /// <param name="x">The sample's column in the block.</param>
    /// <param name="y">The sample's row in the block.</param>
    /// <param name="edges">The filtered neighbouring samples.</param>
    /// <returns>The sample.</returns>
    private static int Sample(int mode, int x, int y, H264Edges edges) => mode switch
    {
        Vertical => edges.Top[x],
        Horizontal => edges.Left[y],
        DiagonalDownLeft => DownLeft(x, y, edges),
        DiagonalDownRight => DownRight(x, y, edges),
        VerticalRight => VerticalRightSample(x, y, edges),
        HorizontalDown => HorizontalDownSample(x, y, edges),
        VerticalLeft => VerticalLeftSample(x, y, edges),
        _ => HorizontalUpSample(x, y, edges),
    };

    /// <summary>Works out the DC prediction.</summary>
    /// <param name="top">The filtered samples above.</param>
    /// <param name="left">The filtered samples on the left.</param>
    /// <param name="neighbours">Which neighbours may be used.</param>
    /// <returns>The value every sample takes.</returns>
    private static int DcValue(ReadOnlySpan<int> top, ReadOnlySpan<int> left, H264Neighbours neighbours)
    {
        var sumTop = 0;
        var sumLeft = 0;
        for (var i = 0; i < Size; i++)
        {
            sumTop += neighbours.Top ? top[i] : 0;
            sumLeft += neighbours.Left ? left[i] : 0;
        }

        if (neighbours.Top && neighbours.Left)
        {
            return (sumTop + sumLeft + Size) >> BothSidesShift;
        }

        if (neighbours.Left)
        {
            return (sumLeft + (Size >> 1)) >> OneSideShift;
        }

        return neighbours.Top ? (sumTop + (Size >> 1)) >> OneSideShift : Mid;
    }

    /// <summary>Smooths three samples: one quarter of the first, a half of the second and a quarter of the third, rounded.</summary>
    /// <param name="a">The first sample.</param>
    /// <param name="b">The middle sample.</param>
    /// <param name="c">The last sample.</param>
    /// <returns>The smoothed value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Smooth3(int a, int b, int c) => (a + (b << 1) + c + SmoothRound) >> SmoothShift;

    /// <summary>Averages two samples with rounding up.</summary>
    /// <param name="a">One sample.</param>
    /// <param name="b">The other sample.</param>
    /// <returns>The average.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Mean(int a, int b) => (a + b + 1) >> 1;

    /// <summary>Works out a sample of the diagonal down left mode.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <param name="edges">The filtered neighbouring samples.</param>
    /// <returns>The sample.</returns>
    private static int DownLeft(int x, int y, H264Edges edges)
    {
        var centre = x + y + 1;
        var corner = (edges.Top[LastTop - 1] + (EdgeWeight * edges.Top[LastTop]) + SmoothRound) >> SmoothShift;
        return x == LastLeft && y == LastLeft ? corner : Smooth3(edges.Top[centre - 1], edges.Top[centre], edges.Top[centre + 1]);
    }

    /// <summary>Works out a sample of the diagonal down right mode.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <param name="edges">The filtered neighbouring samples.</param>
    /// <returns>The sample.</returns>
    private static int DownRight(int x, int y, H264Edges edges)
    {
        if (x > y)
        {
            var centre = x - y - 1;
            return Smooth3(edges.TopAt(centre - 1), edges.TopAt(centre), edges.TopAt(centre + 1));
        }

        if (x < y)
        {
            var centre = y - x - 1;
            return Smooth3(edges.LeftAt(centre - 1), edges.LeftAt(centre), edges.LeftAt(centre + 1));
        }

        return Smooth3(edges.Top[0], edges.Corner, edges.Left[0]);
    }

    /// <summary>Works out a sample of the vertical right mode.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <param name="edges">The filtered neighbouring samples.</param>
    /// <returns>The sample.</returns>
    private static int VerticalRightSample(int x, int y, H264Edges edges)
    {
        var zone = (x << 1) - y;
        var index = x - (y >> 1);
        if (zone >= 0 && (zone & 1) == 0)
        {
            return Mean(edges.TopAt(index - 1), edges.TopAt(index));
        }

        if (zone >= 0)
        {
            return Smooth3(edges.TopAt(index - 1 - 1), edges.TopAt(index - 1), edges.TopAt(index));
        }

        if (zone == -1)
        {
            return Smooth3(edges.Left[0], edges.Corner, edges.Top[0]);
        }

        var centre = y - (x << 1) - 1 - 1;
        return Smooth3(edges.LeftAt(centre + 1), edges.LeftAt(centre), edges.LeftAt(centre - 1));
    }

    /// <summary>Works out a sample of the horizontal down mode.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <param name="edges">The filtered neighbouring samples.</param>
    /// <returns>The sample.</returns>
    private static int HorizontalDownSample(int x, int y, H264Edges edges)
    {
        var zone = (y << 1) - x;
        var index = y - (x >> 1);
        if (zone >= 0 && (zone & 1) == 0)
        {
            return Mean(edges.LeftAt(index - 1), edges.LeftAt(index));
        }

        if (zone >= 0)
        {
            return Smooth3(edges.LeftAt(index - 1 - 1), edges.LeftAt(index - 1), edges.LeftAt(index));
        }

        if (zone == -1)
        {
            return Smooth3(edges.Left[0], edges.Corner, edges.Top[0]);
        }

        var centre = x - (y << 1) - 1 - 1;
        return Smooth3(edges.TopAt(centre + 1), edges.TopAt(centre), edges.TopAt(centre - 1));
    }

    /// <summary>Works out a sample of the vertical left mode.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <param name="edges">The filtered neighbouring samples.</param>
    /// <returns>The sample.</returns>
    private static int VerticalLeftSample(int x, int y, H264Edges edges)
    {
        var index = x + (y >> 1);
        return (y & 1) == 0
            ? Mean(edges.Top[index], edges.Top[index + 1])
            : Smooth3(edges.Top[index], edges.Top[index + 1], edges.Top[index + 1 + 1]);
    }

    /// <summary>Works out a sample of the horizontal up mode.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <param name="edges">The filtered neighbouring samples.</param>
    /// <returns>The sample.</returns>
    private static int HorizontalUpSample(int x, int y, H264Edges edges)
    {
        var zone = x + (y << 1);
        var index = y + (x >> 1);
        if (zone > HorizontalUpLimit)
        {
            return edges.Left[LastLeft];
        }

        if (zone == HorizontalUpLimit)
        {
            return (edges.Left[LastLeft - 1] + (EdgeWeight * edges.Left[LastLeft]) + SmoothRound) >> SmoothShift;
        }

        return (zone & 1) == 0
            ? Mean(edges.Left[index], edges.Left[index + 1])
            : Smooth3(edges.Left[index], edges.Left[index + 1], edges.Left[index + 1 + 1]);
    }
}
