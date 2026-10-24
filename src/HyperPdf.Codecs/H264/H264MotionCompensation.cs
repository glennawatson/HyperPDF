// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs.H264;

/// <summary>Predicts the samples of an inter block from a reference frame (clause 8.4.2.2 of ITU-T H.264).</summary>
internal static class H264MotionCompensation
{
    /// <summary>The largest block, in samples along a side.</summary>
    private const int MaxBlock = 16;

    /// <summary>The extra samples around a block that the six-tap filter reads, in each direction in total.</summary>
    private const int TapSpan = 5;

    /// <summary>The samples before a block that the filter reads.</summary>
    private const int TapBefore = 2;

    /// <summary>The offsets of the six taps from the first.</summary>
    private const int Tap1 = 1;

    /// <summary>The offset of the third tap.</summary>
    private const int Tap2 = 2;

    /// <summary>The offset of the fourth tap.</summary>
    private const int Tap3 = 3;

    /// <summary>The offset of the fifth tap.</summary>
    private const int Tap4 = 4;

    /// <summary>The offset of the sixth tap.</summary>
    private const int Tap5 = 5;

    /// <summary>The weight of the samples next to the centre taps of the six-tap filter.</summary>
    private const int WeightSide = 5;

    /// <summary>The weight of the two centre taps of the six-tap filter.</summary>
    private const int WeightCentre = 20;

    /// <summary>The rounding added before a single filtering step is shifted.</summary>
    private const int HalfRound = 16;

    /// <summary>The shift after a single filtering step.</summary>
    private const int HalfShift = 5;

    /// <summary>The rounding added before the two-step centre sample is shifted.</summary>
    private const int CentreRound = 512;

    /// <summary>The shift after the two-step centre sample.</summary>
    private const int CentreShift = 10;

    /// <summary>The largest sample value.</summary>
    private const int MaxSample = 255;

    /// <summary>The shift of a luma motion vector to whole samples.</summary>
    private const int LumaFractionBits = 2;

    /// <summary>The mask of the fractional part of a luma motion vector.</summary>
    private const int LumaFractionMask = 3;

    /// <summary>The shift of a chroma motion vector to whole samples.</summary>
    private const int ChromaFractionBits = 3;

    /// <summary>The mask of the fractional part of a chroma motion vector.</summary>
    private const int ChromaFractionMask = 7;

    /// <summary>The sum of the weights along one axis of the chroma interpolation.</summary>
    private const int ChromaScale = 8;

    /// <summary>The rounding added before the chroma sum is shifted.</summary>
    private const int ChromaRound = 32;

    /// <summary>The shift after the chroma sum.</summary>
    private const int ChromaShift = 6;

    /// <summary>The size of the window of reference samples read for the largest block.</summary>
    private const int WindowSize = (MaxBlock + TapSpan) * (MaxBlock + TapSpan);

    /// <summary>The samples in the window rows after they are filtered horizontally: the window's rows, each as wide as the largest block.</summary>
    private const int MiddleSize = (MaxBlock + TapSpan) * MaxBlock;

    /// <summary>The shift of the vertical fraction inside the combined fraction code.</summary>
    private const int FractionYShift = 2;

    /// <summary>The entries per quarter-sample position in <see cref="Mix"/>.</summary>
    private const int PairSize = 2;

    /// <summary>The kind of sample that is the whole sample to the right.</summary>
    private const int WholeRight = 1;

    /// <summary>The kind of sample that is the whole sample below.</summary>
    private const int WholeBelow = 2;

    /// <summary>The kind of sample that is the half sample to the right.</summary>
    private const int HalfRight = 3;

    /// <summary>The kind of sample that is the half sample below.</summary>
    private const int HalfBelow = 4;

    /// <summary>The kind of sample that is the half sample at the centre.</summary>
    private const int HalfCentre = 5;

    /// <summary>The kind of sample that is the half sample below the whole sample to the right.</summary>
    private const int HalfBelowRight = 6;

    /// <summary>The kind of sample that is the half sample to the right of the whole sample below.</summary>
    private const int HalfRightBelow = 7;

    /// <summary>Gets the pair of samples whose average each quarter-sample position is (Table 8-12); a pair of equal kinds means the sample itself.</summary>
    private static ReadOnlySpan<byte> Mix =>
    [
        0x00, 0x00, 0x00, 0x03, 0x03, 0x03, 0x01, 0x03, 0x00, 0x04, 0x03, 0x04, 0x03, 0x05, 0x03, 0x06,
        0x04, 0x04, 0x04, 0x05, 0x05, 0x05, 0x05, 0x06, 0x02, 0x04, 0x04, 0x07, 0x05, 0x07, 0x06, 0x07,
    ];

    /// <summary>Predicts a luma block.</summary>
    /// <param name="reference">The reference frame.</param>
    /// <param name="target">Where the predicted samples go: a plane, its stride and the place of the block's first sample.</param>
    /// <param name="block">The block's position and size in the reference frame: 4, 8 or 16 samples along each side.</param>
    /// <param name="vector">The motion vector in quarter samples.</param>
    [SkipLocalsInit]
    internal static void PredictLuma(H264Picture reference, H264PlaneTarget target, BlockRect block, MotionVector vector)
    {
        var planeHeight = reference.MbHeight * H264Picture.LumaSize;
        var fraction = ((vector.Y & LumaFractionMask) << FractionYShift) | (vector.X & LumaFractionMask);
        var left = block.X + (vector.X >> LumaFractionBits);
        var top = block.Y + (vector.Y >> LumaFractionBits);
        if (fraction == 0 && left >= 0 && top >= 0 && left + block.Width <= reference.LumaStride && top + block.Height <= planeHeight)
        {
            CopyWhole(reference, target, new(left, top, block.Width, block.Height));
            return;
        }

        var windowStride = block.Width + TapSpan;
        Span<int> window = stackalloc int[WindowSize];
        Span<int> middle = stackalloc int[MiddleSize];
        var origin = new BlockRect(left - TapBefore, top - TapBefore, windowStride, block.Height + TapSpan);
        FillWindow(reference.Luma, reference.LumaStride, planeHeight, origin, window);
        if (UsesCentre(fraction))
        {
            FillMiddle(window, windowStride, block, middle);
        }

        for (var row = 0; row < block.Height; row++)
        {
            var offset = ((target.Y + row) * target.Stride) + target.X;
            for (var column = 0; column < block.Width; column++)
            {
                target.Plane[offset + column] = (byte)Sample(window, middle, new(windowStride, block.Width), new(column, row), fraction);
            }
        }
    }

    /// <summary>Predicts a chroma block of one component.</summary>
    /// <param name="reference">The reference plane.</param>
    /// <param name="stride">The samples in a row of the reference plane.</param>
    /// <param name="planeHeight">The rows in the reference plane.</param>
    /// <param name="target">Where the predicted samples go: a plane, its stride and the place of the block's first sample.</param>
    /// <param name="block">The block's position and size in the reference plane.</param>
    /// <param name="vector">The luma motion vector, which is in eighths of a chroma sample.</param>
    internal static void PredictChroma(byte[] reference, int stride, int planeHeight, H264PlaneTarget target, BlockRect block, MotionVector vector)
    {
        var baseX = block.X + (vector.X >> ChromaFractionBits);
        var baseY = block.Y + (vector.Y >> ChromaFractionBits);
        var fractionX = vector.X & ChromaFractionMask;
        var fractionY = vector.Y & ChromaFractionMask;
        var lastRow = planeHeight - 1;
        var lastColumn = stride - 1;
        if (baseX >= 0 && baseY >= 0 && baseX + block.Width <= lastColumn && baseY + block.Height <= lastRow)
        {
            PredictChromaInside(reference, stride, target, new(baseX, baseY, block.Width, block.Height), new(fractionX, fractionY));
            return;
        }

        for (var row = 0; row < block.Height; row++)
        {
            var upper = Math.Clamp(baseY + row, 0, lastRow) * stride;
            var lower = Math.Clamp(baseY + row + 1, 0, lastRow) * stride;
            var offset = ((target.Y + row) * target.Stride) + target.X;
            for (var column = 0; column < block.Width; column++)
            {
                var left = Math.Clamp(baseX + column, 0, lastColumn);
                var right = Math.Clamp(baseX + column + 1, 0, lastColumn);
                var sum = ((ChromaScale - fractionX) * (ChromaScale - fractionY) * reference[upper + left])
                    + (fractionX * (ChromaScale - fractionY) * reference[upper + right])
                    + ((ChromaScale - fractionX) * fractionY * reference[lower + left])
                    + (fractionX * fractionY * reference[lower + right]);
                target.Plane[offset + column] = (byte)((sum + ChromaRound) >> ChromaShift);
            }
        }
    }

    /// <summary>Predicts a chroma block whose reference samples, and the one beyond each, all lie inside the plane, so no position needs limiting.</summary>
    /// <param name="reference">The reference plane.</param>
    /// <param name="stride">The samples in a row of the reference plane.</param>
    /// <param name="target">Where the predicted samples go.</param>
    /// <param name="source">The block's place in the reference plane after the whole-sample part of the vector.</param>
    /// <param name="fraction">The eighth-sample part of the vector: the column fraction as the column and the row fraction as the row.</param>
    private static void PredictChromaInside(byte[] reference, int stride, H264PlaneTarget target, BlockRect source, SamplePlace fraction)
    {
        var (fractionX, fractionY) = fraction;
        var topLeft = (ChromaScale - fractionX) * (ChromaScale - fractionY);
        var topRight = fractionX * (ChromaScale - fractionY);
        var bottomLeft = (ChromaScale - fractionX) * fractionY;
        var bottomRight = fractionX * fractionY;
        for (var row = 0; row < source.Height; row++)
        {
            var upper = reference.AsSpan(((source.Y + row) * stride) + source.X, source.Width + 1);
            var lower = reference.AsSpan(((source.Y + row + 1) * stride) + source.X, source.Width + 1);
            var destination = target.Plane.AsSpan(((target.Y + row) * target.Stride) + target.X, source.Width);
            for (var column = 0; column < destination.Length; column++)
            {
                var sum = (topLeft * upper[column]) + (topRight * upper[column + 1]) + (bottomLeft * lower[column]) + (bottomRight * lower[column + 1]);
                destination[column] = (byte)((sum + ChromaRound) >> ChromaShift);
            }
        }
    }

    /// <summary>Averages two samples with rounding up.</summary>
    /// <param name="a">One sample.</param>
    /// <param name="b">The other sample.</param>
    /// <returns>The average.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Average(int a, int b) => (a + b + 1) >> 1;

    /// <summary>Limits a value to the range of a sample.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The value clamped to 0 to 255.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Clip(int value) => Math.Clamp(value, 0, MaxSample);

    /// <summary>Copies reference samples around a block into a window, repeating the edge samples for places outside the frame.</summary>
    /// <param name="plane">The reference plane.</param>
    /// <param name="stride">The samples in a row; also the width of the plane.</param>
    /// <param name="planeHeight">The rows in the plane.</param>
    /// <param name="area">The window's first column and row in the plane, and its size.</param>
    /// <param name="window">Receives the samples.</param>
    private static void FillWindow(byte[] plane, int stride, int planeHeight, BlockRect area, Span<int> window)
    {
        var inside = area.X >= 0 && area.X + area.Width <= stride;
        for (var row = 0; row < area.Height; row++)
        {
            var source = Math.Clamp(area.Y + row, 0, planeHeight - 1) * stride;
            var target = row * area.Width;
            for (var column = 0; column < area.Width; column++)
            {
                var x = inside ? area.X + column : Math.Clamp(area.X + column, 0, stride - 1);
                window[target + column] = plane[source + x];
            }
        }
    }

    /// <summary>Works out one predicted luma sample from the window (Table 8-12).</summary>
    /// <param name="window">The reference samples around the block.</param>
    /// <param name="middle">The rows of the window filtered horizontally, when the position needs them.</param>
    /// <param name="shape">The window's width and the block's width.</param>
    /// <param name="at">The sample's column and row in the block.</param>
    /// <param name="fraction">The quarter-sample position: the vertical part times four plus the horizontal part.</param>
    /// <returns>The sample.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Sample(ReadOnlySpan<int> window, ReadOnlySpan<int> middle, WindowShape shape, SamplePlace at, int fraction)
    {
        var first = Mix[fraction * PairSize];
        var second = Mix[(fraction * PairSize) + 1];
        var value = Kind(window, middle, shape, at, first);
        return first == second ? value : Average(value, Kind(window, middle, shape, at, second));
    }

    /// <summary>Gets one of the samples that the quarter-sample positions are made of.</summary>
    /// <param name="window">The window.</param>
    /// <param name="middle">The rows of the window filtered horizontally.</param>
    /// <param name="shape">The window's width and the block's width.</param>
    /// <param name="at">The sample's column and row in the block.</param>
    /// <param name="kind">Which sample.</param>
    /// <returns>The sample value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Kind(ReadOnlySpan<int> window, ReadOnlySpan<int> middle, WindowShape shape, SamplePlace at, int kind)
    {
        var stride = shape.Stride;
        var (column, row) = at;
        var here = ((row + TapBefore) * stride) + column + TapBefore;
        return kind switch
        {
            WholeRight => window[here + 1],
            WholeBelow => window[here + stride],
            HalfRight => HalfX(window, stride, column, row),
            HalfBelow => HalfY(window, stride, column, row),
            HalfCentre => Centre(middle, shape.BlockWidth, column, row),
            HalfBelowRight => HalfY(window, stride, column + 1, row),
            HalfRightBelow => HalfX(window, stride, column, row + 1),
            _ => window[here],
        };
    }

    /// <summary>Finds whether a quarter-sample position is made from the centre half sample.</summary>
    /// <param name="fraction">The quarter-sample position.</param>
    /// <returns><see langword="true"/> when the filtered rows are needed.</returns>
    private static bool UsesCentre(int fraction) => Mix[fraction * PairSize] == HalfCentre || Mix[(fraction * PairSize) + 1] == HalfCentre;

    /// <summary>Filters every row of the window horizontally, so the centre half samples need only a vertical pass.</summary>
    /// <param name="window">The window.</param>
    /// <param name="stride">The window's width.</param>
    /// <param name="block">The block, whose size says how many columns and rows are filtered.</param>
    /// <param name="middle">Receives the filtered rows, each as wide as the block.</param>
    private static void FillMiddle(ReadOnlySpan<int> window, int stride, BlockRect block, Span<int> middle)
    {
        for (var windowRow = 0; windowRow < block.Height + TapSpan; windowRow++)
        {
            for (var column = 0; column < block.Width; column++)
            {
                middle[(windowRow * block.Width) + column] = RowTaps(window, stride, column, windowRow);
            }
        }
    }

    /// <summary>Copies a block of whole samples that lies inside the reference plane.</summary>
    /// <param name="reference">The reference frame.</param>
    /// <param name="target">Where the samples go.</param>
    /// <param name="source">The block's place in the reference plane.</param>
    private static void CopyWhole(H264Picture reference, H264PlaneTarget target, BlockRect source)
    {
        for (var row = 0; row < source.Height; row++)
        {
            reference.Luma.AsSpan(((source.Y + row) * reference.LumaStride) + source.X, source.Width)
                .CopyTo(target.Plane.AsSpan(((target.Y + row) * target.Stride) + target.X, source.Width));
        }
    }

    /// <summary>Filters six samples along a row with the six-tap filter, without rounding.</summary>
    /// <param name="window">The window.</param>
    /// <param name="stride">The window's width.</param>
    /// <param name="column">The block column of the first of the six taps.</param>
    /// <param name="windowRow">The window row.</param>
    /// <returns>The filtered value, scaled by 32.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RowTaps(ReadOnlySpan<int> window, int stride, int column, int windowRow)
    {
        var start = (windowRow * stride) + column;
        return window[start] - (WeightSide * window[start + Tap1]) + (WeightCentre * window[start + Tap2]) + (WeightCentre * window[start + Tap3])
            - (WeightSide * window[start + Tap4]) + window[start + Tap5];
    }

    /// <summary>Filters six samples down a column with the six-tap filter, without rounding.</summary>
    /// <param name="window">The window.</param>
    /// <param name="stride">The window's width.</param>
    /// <param name="windowColumn">The window column.</param>
    /// <param name="row">The window row of the first of the six taps.</param>
    /// <returns>The filtered value, scaled by 32.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ColumnTaps(ReadOnlySpan<int> window, int stride, int windowColumn, int row)
    {
        var start = (row * stride) + windowColumn;
        return window[start] - (WeightSide * window[start + (Tap1 * stride)]) + (WeightCentre * window[start + (Tap2 * stride)])
            + (WeightCentre * window[start + (Tap3 * stride)]) - (WeightSide * window[start + (Tap4 * stride)]) + window[start + (Tap5 * stride)];
    }

    /// <summary>Gets the half-sample position to the right of a sample.</summary>
    /// <param name="window">The window.</param>
    /// <param name="stride">The window's width.</param>
    /// <param name="column">The block column.</param>
    /// <param name="row">The block row.</param>
    /// <returns>The sample value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int HalfX(ReadOnlySpan<int> window, int stride, int column, int row) =>
        Clip((RowTaps(window, stride, column, row + TapBefore) + HalfRound) >> HalfShift);

    /// <summary>Gets the half-sample position below a sample.</summary>
    /// <param name="window">The window.</param>
    /// <param name="stride">The window's width.</param>
    /// <param name="column">The block column.</param>
    /// <param name="row">The block row.</param>
    /// <returns>The sample value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int HalfY(ReadOnlySpan<int> window, int stride, int column, int row) =>
        Clip((ColumnTaps(window, stride, column + TapBefore, row) + HalfRound) >> HalfShift);

    /// <summary>Gets the half-sample position at the centre of four samples, filtered in both directions.</summary>
    /// <param name="middle">The window rows filtered horizontally, each as wide as the block.</param>
    /// <param name="width">The block's width.</param>
    /// <param name="column">The block column.</param>
    /// <param name="row">The block row.</param>
    /// <returns>The sample value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Centre(ReadOnlySpan<int> middle, int width, int column, int row)
    {
        var start = (row * width) + column;
        var total = middle[start]
            - (WeightSide * middle[start + (Tap1 * width)])
            + (WeightCentre * middle[start + (Tap2 * width)])
            + (WeightCentre * middle[start + (Tap3 * width)])
            - (WeightSide * middle[start + (Tap4 * width)])
            + middle[start + (Tap5 * width)];
        return Clip((total + CentreRound) >> CentreShift);
    }
}
