// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Cpu;

/// <summary>Copies and scales BGRA pixels into a frame buffer without allocating.</summary>
public static class CpuBlit
{
    /// <summary>The bytes per pixel.</summary>
    private const int BytesPerPixel = 4;

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
            if (CpuBlend.IsOpaque(from))
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
            if (sourceRow == sampledRow)
            {
                // When the image is scaled up, several frame lines show the same source row, so the line above is copied instead of sampled again.
                frame.Slice(((row - 1) * frameGrid.Stride) + (rows.Left * BytesPerPixel), length).CopyTo(line);
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
            if (y0 != upperRow)
            {
                StretchLine(upper, source, sourceGrid, y0, walk);
                upperRow = y0;
            }

            if (y1 != lowerRow)
            {
                StretchLine(lower, source, sourceGrid, y1, walk);
                lowerRow = y1;
            }

            CpuBlend.MixRows(frame.Slice((row * frameGrid.Stride) + (rows.Left * BytesPerPixel), length), upper, lower, (uint)((sy & FractionMask) >> WeightShift));
        }
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
        var position = walk.Start;
        var last = row.Length - 1;
        for (var i = 0; i < target.Length; i++)
        {
            var x0 = Math.Min(position >> FixedShift, last);
            var first = row[x0];
            var second = row[Math.Min(x0 + 1, last)];
            var weight = (uint)((position & FractionMask) >> WeightShift);

            // Flat areas such as paper need no mixing, which is most of a page.
            target[i] = first == second ? first : Pack(Mix(Spread(first), Spread(second), weight));
            position += walk.Step;
        }
    }

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
        var position = start;
        var last = sourceRow.Length - 1;
        for (var i = 0; i < line.Length; i++)
        {
            line[i] = sourceRow[Math.Min(position >> FixedShift, last)];
            position += step;
        }
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
