// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace HyperPdf.Rendering.Cpu;

/// <summary>Copies and scales BGRA pixels into a frame buffer without allocating.</summary>
public static class CpuBlit
{
    /// <summary>The bytes per pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The destination pixels the smooth sampler stretches at once where the processor has 128 bit vectors.</summary>
    private const int PixelsPerVector = 4;

    /// <summary>The destination pixels sampled at once where the processor can load eight source pixels from eight places.</summary>
    private const int PixelsPerGather = 8;

    /// <summary>The number that copies a byte into each of the four bytes of a 32 bit lane when multiplied by it.</summary>
    private const int ChannelSpread = 0x01010101;

    /// <summary>The scratch lines the smooth sampler keeps: the source rows above and below the frame line.</summary>
    private const int ScratchLines = 2;

    /// <summary>The number of weight steps for the smooth sampler.</summary>
    private const uint WeightSteps = 256;

    /// <summary>The shift that divides a 16.16 fixed point number back to whole pixels.</summary>
    private const int FixedShift = 16;

    /// <summary>The shift that keeps the top 8 bits of a 16 bit fraction, and that divides by the weight steps.</summary>
    private const int WeightShift = 8;

    /// <summary>The mask for the fraction of a 16.16 fixed point number.</summary>
    private const int FractionMask = 0xFFFF;

    /// <summary>The mask that keeps one byte in each 16 bit lane of a spread pixel.</summary>
    private const ulong LaneMask = 0x00FF00FF00FF00FF;

    /// <summary>The mask that keeps two bytes in each 32 bit half of a half spread pixel.</summary>
    private const ulong PairMask = 0x0000FFFF0000FFFF;

    /// <summary>The shift that moves a byte pair into the upper half of a spread pixel.</summary>
    private const int PairShift = 16;

    /// <summary>Draws a source image over a destination rectangle, scaling it to fit and clipping it to the frame.</summary>
    /// <param name="frame">The frame pixels.</param>
    /// <param name="frameGrid">The frame shape.</param>
    /// <param name="source">The source pixels.</param>
    /// <param name="sourceGrid">The source shape.</param>
    /// <param name="request">Where the source goes.</param>
    public static void Draw(Span<byte> frame, in PixelGrid frameGrid, ReadOnlySpan<byte> source, in PixelGrid sourceGrid, in BlitRequest request)
    {
        if (request.Width <= 0 || request.Height <= 0 || sourceGrid.Width <= 0 || sourceGrid.Height <= 0)
        {
            return;
        }

        var rows = BlitRows.Clip(request, frameGrid);
        if (rows.IsEmpty)
        {
            return;
        }

        if (request.Width == sourceGrid.Width && request.Height == sourceGrid.Height)
        {
            CopyRows(frame, frameGrid, source, sourceGrid, request, rows);
            return;
        }

        if (request.Smooth)
        {
            ScaleRowsSmooth(frame, frameGrid, source, sourceGrid, request, rows);
            return;
        }

        ScaleRowsNearest(frame, frameGrid, source, sourceGrid, request, rows);
    }

    /// <summary>Copies unscaled rows, blending only when a source row is not opaque.</summary>
    /// <param name="frame">The frame pixels.</param>
    /// <param name="frameGrid">The frame shape.</param>
    /// <param name="source">The source pixels.</param>
    /// <param name="sourceGrid">The source shape.</param>
    /// <param name="request">Where the source goes.</param>
    /// <param name="rows">The clipped rows.</param>
    private static void CopyRows(Span<byte> frame, in PixelGrid frameGrid, ReadOnlySpan<byte> source, in PixelGrid sourceGrid, in BlitRequest request, in BlitRows rows)
    {
        var length = (rows.Right - rows.Left) * BytesPerPixel;
        for (var row = rows.Top; row < rows.Bottom; row++)
        {
            var from = source.Slice(((row - request.Y) * sourceGrid.Stride) + ((rows.Left - request.X) * BytesPerPixel), length);
            var to = frame.Slice((row * frameGrid.Stride) + (rows.Left * BytesPerPixel), length);
            if (request.Opaque || CpuBlend.IsOpaque(from))
            {
                from.CopyTo(to);
            }
            else
            {
                CpuBlend.BlendRow(to, from);
            }
        }
    }

    /// <summary>Draws scaled rows by picking the nearest source pixel, stepping through the source in 16.16 fixed point.</summary>
    /// <param name="frame">The frame pixels.</param>
    /// <param name="frameGrid">The frame shape.</param>
    /// <param name="source">The source pixels.</param>
    /// <param name="sourceGrid">The source shape.</param>
    /// <param name="request">Where the source goes.</param>
    /// <param name="rows">The clipped rows.</param>
    private static void ScaleRowsNearest(Span<byte> frame, in PixelGrid frameGrid, ReadOnlySpan<byte> source, in PixelGrid sourceGrid, in BlitRequest request, in BlitRows rows)
    {
        var stepX = Step(sourceGrid.Width, request.Width);
        var stepY = Step(sourceGrid.Height, request.Height);
        var start = (rows.Left - request.X) * stepX;
        var length = (rows.Right - rows.Left) * BytesPerPixel;
        var sampledRow = -1;
        for (var row = rows.Top; row < rows.Bottom; row++)
        {
            var sourceRow = Math.Min(((row - request.Y) * stepY) >> FixedShift, sourceGrid.Height - 1);
            var line = frame.Slice((row * frameGrid.Stride) + (rows.Left * BytesPerPixel), length);
            if (sourceRow == sampledRow || IsSameRow(source, sourceGrid, new(sampledRow, sourceRow)))
            {
                // When the image is scaled up, several frame lines show the same source row, and paper gives source rows that are
                // the same as the one before, so the line above is copied instead of sampled again.
                frame.Slice(((row - 1) * frameGrid.Stride) + (rows.Left * BytesPerPixel), length).CopyTo(line);
                sampledRow = sourceRow;
                continue;
            }

            sampledRow = sourceRow;
            SampleLineNearest(
                MemoryMarshal.Cast<byte, uint>(line),
                MemoryMarshal.Cast<byte, uint>(source.Slice(sourceRow * sourceGrid.Stride, sourceGrid.Width * BytesPerPixel)),
                start,
                stepX);
        }
    }

    /// <summary>Draws scaled rows by blending the four nearest source pixels, stepping through the source in 16.16 fixed point.</summary>
    /// <param name="frame">The frame pixels.</param>
    /// <param name="frameGrid">The frame shape.</param>
    /// <param name="source">The source pixels.</param>
    /// <param name="sourceGrid">The source shape.</param>
    /// <param name="request">Where the source goes.</param>
    /// <param name="rows">The clipped rows.</param>
    private static void ScaleRowsSmooth(Span<byte> frame, in PixelGrid frameGrid, ReadOnlySpan<byte> source, in PixelGrid sourceGrid, in BlitRequest request, in BlitRows rows)
    {
        // Each source row is stretched sideways once into a scratch line, and every frame line is a mix of two scratch lines.
        // The scratch lines come from the pool, so steady frames allocate nothing.
        var scratch = ArrayPool<byte>.Shared.Rent((rows.Right - rows.Left) * BytesPerPixel * ScratchLines);
        try
        {
            MixStretchedRows(frame, frameGrid, source, sourceGrid, request, rows, scratch);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(scratch);
        }
    }

    /// <summary>Draws smooth scaled rows from two scratch lines that hold the source rows above and below each frame line, already stretched sideways.</summary>
    /// <param name="frame">The frame pixels.</param>
    /// <param name="frameGrid">The frame shape.</param>
    /// <param name="source">The source pixels.</param>
    /// <param name="sourceGrid">The source shape.</param>
    /// <param name="request">Where the source goes.</param>
    /// <param name="rows">The clipped rows.</param>
    /// <param name="scratch">Room for two frame lines.</param>
    private static void MixStretchedRows(Span<byte> frame, in PixelGrid frameGrid, ReadOnlySpan<byte> source, in PixelGrid sourceGrid, in BlitRequest request, in BlitRows rows, Span<byte> scratch)
    {
        var length = (rows.Right - rows.Left) * BytesPerPixel;
        var walk = new SampleWalk((rows.Left - request.X) * Step(sourceGrid.Width, request.Width), Step(sourceGrid.Width, request.Width));
        var stepY = Step(sourceGrid.Height, request.Height);
        var upperSlot = 0;
        var upperRow = -1;
        var lowerRow = -1;
        var alike = false;
        for (var row = rows.Top; row < rows.Bottom; row++)
        {
            var sy = (row - request.Y) * stepY;
            var y0 = Math.Min(sy >> FixedShift, sourceGrid.Height - 1);
            var y1 = Math.Min(y0 + 1, sourceGrid.Height - 1);
            if (y0 == lowerRow && y0 != upperRow)
            {
                // Moving down one source row: the old lower line becomes the upper line without sampling it again.
                upperSlot = 1 - upperSlot;
                upperRow = lowerRow;
                lowerRow = -1;
            }

            var upper = scratch.Slice(upperSlot * length, length);
            var lower = scratch.Slice((1 - upperSlot) * length, length);
            var stretched = y0 != upperRow || y1 != lowerRow;
            if (y0 != upperRow)
            {
                StretchLine(upper, source, sourceGrid, y0, walk);
                upperRow = y0;
            }

            if (y1 != lowerRow)
            {
                StretchBelow(lower, upper, source, sourceGrid, new(y0, y1), walk);
                lowerRow = y1;
            }

            // Paper, and the space between lines of text, gives two rows that are the same, and a mix of those is a copy.
            alike = stretched ? upper.SequenceEqual(lower) : alike;

            // A weight of nothing copies the upper row, which is what two rows alike come to at any weight.
            var weight = alike ? 0 : (uint)((sy & FractionMask) >> WeightShift);
            CpuBlend.MixRows(frame.Slice((row * frameGrid.Stride) + (rows.Left * BytesPerPixel), length), upper, lower, weight);
        }
    }

    /// <summary>
    /// Fills the line of the source row below a frame line. A source row that is the same as the row above it, as paper is,
    /// stretches to the same line, so the line above is copied and the row is not sampled again.
    /// </summary>
    /// <param name="lower">The line to fill.</param>
    /// <param name="upper">The line of the row above, already stretched.</param>
    /// <param name="source">The source pixels.</param>
    /// <param name="sourceGrid">The source shape.</param>
    /// <param name="pair">The source rows above and below.</param>
    /// <param name="walk">Where sampling starts and how far it steps.</param>
    private static void StretchBelow(Span<byte> lower, ReadOnlySpan<byte> upper, ReadOnlySpan<byte> source, in PixelGrid sourceGrid, in SourceRowPair pair, in SampleWalk walk)
    {
        if (IsSameRow(source, sourceGrid, pair))
        {
            upper.CopyTo(lower);
            return;
        }

        StretchLine(lower, source, sourceGrid, pair.Below, walk);
    }

    /// <summary>Tells whether two source rows hold the same pixels.</summary>
    /// <param name="source">The source pixels.</param>
    /// <param name="sourceGrid">The source shape.</param>
    /// <param name="pair">The two rows. A row before the first, which nothing has sampled yet, is the same as no row.</param>
    /// <returns><see langword="true"/> when the rows are the same.</returns>
    private static bool IsSameRow(ReadOnlySpan<byte> source, in PixelGrid sourceGrid, in SourceRowPair pair)
    {
        var (above, below) = pair;
        if (above < 0)
        {
            return false;
        }

        var width = sourceGrid.Width * BytesPerPixel;
        return source.Slice(above * sourceGrid.Stride, width).SequenceEqual(source.Slice(below * sourceGrid.Stride, width));
    }

    /// <summary>Stretches one source row sideways into a line, blending the two nearest source pixels for each.</summary>
    /// <param name="line">The destination line.</param>
    /// <param name="source">The source pixels.</param>
    /// <param name="sourceGrid">The source shape.</param>
    /// <param name="sourceRow">The source row.</param>
    /// <param name="walk">Where the line starts in the source and how it steps.</param>
    private static void StretchLine(Span<byte> line, ReadOnlySpan<byte> source, in PixelGrid sourceGrid, int sourceRow, in SampleWalk walk)
    {
        var target = MemoryMarshal.Cast<byte, uint>(line);
        var row = MemoryMarshal.Cast<byte, uint>(source.Slice(sourceRow * sourceGrid.Stride, sourceGrid.Width * BytesPerPixel));
        var (position, step) = walk;
        var last = row.Length - 1;
        var i = 0;
        if (Avx2.IsSupported)
        {
            i = GatherSmooth(target, row, position, step);
            position += step * i;
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; i <= target.Length - PixelsPerVector; i += PixelsPerVector)
            {
                StretchFour(target.Slice(i, PixelsPerVector), row, position, step);
                position += step * PixelsPerVector;
            }
        }

        for (; i < target.Length; i++)
        {
            var x0 = Math.Min(position >> FixedShift, last);
            var first = row[x0];
            var second = row[Math.Min(x0 + 1, last)];
            var weight = (uint)((position & FractionMask) >> WeightShift);

            // Flat areas such as paper need no mixing, which is most of a page.
            target[i] = first == second ? first : Pack(Mix(Spread(first), Spread(second), weight));
            position += step;
        }
    }

    /// <summary>Stretches four destination pixels at once, giving the same values as the pixel by pixel loop.</summary>
    /// <param name="target">The four destination pixels.</param>
    /// <param name="row">The source row.</param>
    /// <param name="position">The 16.16 source position of the first of the four.</param>
    /// <param name="step">The 16.16 source step per destination pixel.</param>
    private static void StretchFour(Span<uint> target, ReadOnlySpan<uint> row, int position, int step)
    {
        var last = row.Length - 1;
        var second = position + step;
        var third = second + step;
        var fourth = third + step;
        var x0 = Math.Min(position >> FixedShift, last);
        var x1 = Math.Min(second >> FixedShift, last);
        var x2 = Math.Min(third >> FixedShift, last);
        var x3 = Math.Min(fourth >> FixedShift, last);
        var left = Vector128.Create(row[x0], row[x1], row[x2], row[x3]);
        var right = Vector128.Create(row[Math.Min(x0 + 1, last)], row[Math.Min(x1 + 1, last)], row[Math.Min(x2 + 1, last)], row[Math.Min(x3 + 1, last)]);

        // Flat areas such as paper need no mixing, which is most of a page.
        if (left == right)
        {
            left.CopyTo(target);
            return;
        }

        var w0 = WeightOf(position);
        var w1 = WeightOf(second);
        var w2 = WeightOf(third);
        var w3 = WeightOf(fourth);
        var low = MixLanes(Vector128.WidenLower(left.AsByte()), Vector128.WidenLower(right.AsByte()), Vector128.Create(w0, w0, w0, w0, w1, w1, w1, w1));
        var high = MixLanes(Vector128.WidenUpper(left.AsByte()), Vector128.WidenUpper(right.AsByte()), Vector128.Create(w2, w2, w2, w2, w3, w3, w3, w3));
        Vector128.Narrow(low, high).AsUInt32().CopyTo(target);
    }

    /// <summary>Gets the weight, out of 256, of the pixel after a 16.16 source position.</summary>
    /// <param name="position">The source position.</param>
    /// <returns>The weight.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort WeightOf(int position) => (ushort)((position & FractionMask) >> WeightShift);

    /// <summary>Mixes the channels of two pixels by a weight out of 256 for each channel, eight channels at once.</summary>
    /// <param name="first">The channels of the pixels before the position, one in each 16 bit lane.</param>
    /// <param name="second">The channels of the pixels after it.</param>
    /// <param name="weight">The weight of the second pixel for each lane.</param>
    /// <returns>The mixed channels, each in the low byte of its lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ushort> MixLanes(Vector128<ushort> first, Vector128<ushort> second, Vector128<ushort> weight) =>
        ((first * (Vector128.Create((ushort)WeightSteps) - weight)) + (second * weight)) >>> WeightShift;

    /// <summary>Finds the 16.16 source step for one destination pixel.</summary>
    /// <param name="sourceLength">The source length in pixels.</param>
    /// <param name="targetLength">The destination length in pixels.</param>
    /// <returns>The step.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Step(int sourceLength, int targetLength) => (int)(((long)sourceLength << FixedShift) / targetLength);

    /// <summary>Fills one destination line by picking the nearest source pixel for each.</summary>
    /// <param name="line">The destination line.</param>
    /// <param name="sourceRow">The source row.</param>
    /// <param name="start">The 16.16 source position of the first pixel.</param>
    /// <param name="step">The 16.16 source step per destination pixel.</param>
    private static void SampleLineNearest(Span<uint> line, ReadOnlySpan<uint> sourceRow, int start, int step)
    {
        var i = Avx2.IsSupported ? GatherNearest(line, sourceRow, start, step) : 0;
        var position = start + (step * i);
        var last = sourceRow.Length - 1;
        for (; i < line.Length; i++)
        {
            line[i] = sourceRow[Math.Min(position >> FixedShift, last)];
            position += step;
        }
    }

    /// <summary>Picks the nearest source pixel for eight destination pixels at once, for as many whole eights as the line has.</summary>
    /// <param name="line">The destination pixels.</param>
    /// <param name="sourceRow">The source row.</param>
    /// <param name="start">The 16.16 source position of the first destination pixel.</param>
    /// <param name="step">The 16.16 source step per destination pixel.</param>
    /// <returns>The destination pixels written.</returns>
    private static unsafe int GatherNearest(Span<uint> line, ReadOnlySpan<uint> sourceRow, int start, int step)
    {
        var i = 0;
        var last = Vector256.Create(sourceRow.Length - 1);
        var stride = Vector256.Create(step * PixelsPerGather);
        var positions = Vector256.Create(start) + (Vector256.Create(step) * Vector256<int>.Indices);
        fixed (uint* from = sourceRow)
        {
            fixed (uint* to = line)
            {
                for (; i <= line.Length - PixelsPerGather; i += PixelsPerGather)
                {
                    Avx2.GatherVector256(from, Vector256.Min(positions >> FixedShift, last), BytesPerPixel).Store(to + i);
                    positions += stride;
                }
            }
        }

        return i;
    }

    /// <summary>
    /// Stretches eight destination pixels at once, for as many whole eights as the line has, giving the same values as the pixel
    /// by pixel loop.
    /// </summary>
    /// <param name="target">The destination pixels.</param>
    /// <param name="row">The source row.</param>
    /// <param name="start">The 16.16 source position of the first destination pixel.</param>
    /// <param name="step">The 16.16 source step per destination pixel.</param>
    /// <returns>The destination pixels written.</returns>
    private static unsafe int GatherSmooth(Span<uint> target, ReadOnlySpan<uint> row, int start, int step)
    {
        var i = 0;
        var last = Vector256.Create(row.Length - 1);
        var stride = Vector256.Create(step * PixelsPerGather);
        var positions = Vector256.Create(start) + (Vector256.Create(step) * Vector256<int>.Indices);
        fixed (uint* from = row)
        {
            fixed (uint* to = target)
            {
                for (; i <= target.Length - PixelsPerGather; i += PixelsPerGather)
                {
                    var columns = Vector256.Min(positions >> FixedShift, last);
                    var left = Avx2.GatherVector256(from, columns, BytesPerPixel);
                    var right = Avx2.GatherVector256(from, Vector256.Min(columns + Vector256<int>.One, last), BytesPerPixel);

                    // Flat areas such as paper need no mixing, which is most of a page.
                    (left == right ? left : MixGathered(left, right, positions)).Store(to + i);
                    positions += stride;
                }
            }
        }

        return i;
    }

    /// <summary>Mixes eight pairs of neighbouring source pixels by the fraction of each source position.</summary>
    /// <param name="left">The pixels before the positions.</param>
    /// <param name="right">The pixels after them.</param>
    /// <param name="positions">The 16.16 source positions.</param>
    /// <returns>The mixed pixels.</returns>
    private static Vector256<uint> MixGathered(Vector256<uint> left, Vector256<uint> right, Vector256<int> positions)
    {
        // The weight of each pixel, out of 256, is copied into each of its four bytes, so it widens into one weight a channel.
        var weights = (((positions & Vector256.Create(FractionMask)) >> WeightShift) * Vector256.Create(ChannelSpread)).AsByte();
        var whole = Vector256.Create((ushort)WeightSteps);
        var lowWeight = Vector256.WidenLower(weights);
        var highWeight = Vector256.WidenUpper(weights);
        var low = ((Vector256.WidenLower(left.AsByte()) * (whole - lowWeight)) + (Vector256.WidenLower(right.AsByte()) * lowWeight)) >>> WeightShift;
        var high = ((Vector256.WidenUpper(left.AsByte()) * (whole - highWeight)) + (Vector256.WidenUpper(right.AsByte()) * highWeight)) >>> WeightShift;
        return Vector256.Narrow(low, high).AsUInt32();
    }

    /// <summary>Spreads the four bytes of a pixel into four 16 bit lanes, so one multiply weights every channel.</summary>
    /// <param name="pixel">The pixel.</param>
    /// <returns>The spread pixel.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Spread(uint pixel)
    {
        var pairs = (pixel | ((ulong)pixel << PairShift)) & PairMask;
        return (pairs | (pairs << WeightShift)) & LaneMask;
    }

    /// <summary>Packs four 16 bit lanes back into the four bytes of a pixel.</summary>
    /// <param name="lanes">The spread pixel.</param>
    /// <returns>The pixel.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Pack(ulong lanes)
    {
        var pairs = (lanes | (lanes >> WeightShift)) & PairMask;
        return (uint)(pairs | (pairs >> PairShift));
    }

    /// <summary>Mixes two spread pixels by a weight out of 256.</summary>
    /// <param name="first">The pixel the weight moves away from.</param>
    /// <param name="second">The pixel the weight moves toward.</param>
    /// <param name="weight">The weight, from 0 to 255.</param>
    /// <returns>The mixed spread pixel.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Mix(ulong first, ulong second, uint weight) =>
        (((first * (WeightSteps - weight)) + (second * weight)) >> WeightShift) & LaneMask;
}
