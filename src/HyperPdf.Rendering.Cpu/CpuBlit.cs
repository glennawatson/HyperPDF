// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Cpu;

/// <summary>Copies and scales BGRA pixels into a frame buffer without allocating.</summary>
public static class CpuBlit
{
    /// <summary>The bytes per pixel.</summary>
    private const int BytesPerPixel = 4;

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
        var stepX = Step(sourceGrid.Width, request.Width);
        var stepY = Step(sourceGrid.Height, request.Height);
        var start = (rows.Left - request.X) * stepX;
        var length = (rows.Right - rows.Left) * BytesPerPixel;
        var rowBytes = sourceGrid.Width * BytesPerPixel;
        for (var row = rows.Top; row < rows.Bottom; row++)
        {
            var sy = (row - request.Y) * stepY;
            var y0 = Math.Min(sy >> FixedShift, sourceGrid.Height - 1);
            var y1 = Math.Min(y0 + 1, sourceGrid.Height - 1);
            SampleLineBilinear(
                MemoryMarshal.Cast<byte, uint>(frame.Slice((row * frameGrid.Stride) + (rows.Left * BytesPerPixel), length)),
                MemoryMarshal.Cast<byte, uint>(source.Slice(y0 * sourceGrid.Stride, rowBytes)),
                MemoryMarshal.Cast<byte, uint>(source.Slice(y1 * sourceGrid.Stride, rowBytes)),
                new(start, stepX, (uint)((sy & FractionMask) >> WeightShift)));
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

    /// <summary>Fills one destination line by blending the four nearest source pixels for each.</summary>
    /// <param name="line">The destination line.</param>
    /// <param name="row0">The upper source row.</param>
    /// <param name="row1">The lower source row.</param>
    /// <param name="walk">Where the line starts in the source and how it steps.</param>
    private static void SampleLineBilinear(Span<uint> line, ReadOnlySpan<uint> row0, ReadOnlySpan<uint> row1, in SampleWalk walk)
    {
        var position = walk.Start;
        var last = row0.Length - 1;
        for (var i = 0; i < line.Length; i++)
        {
            var x0 = Math.Min(position >> FixedShift, last);
            var x1 = Math.Min(x0 + 1, last);
            var fx = (uint)((position & FractionMask) >> WeightShift);
            var top = Mix(Spread(row0[x0]), Spread(row0[x1]), fx);
            var bottom = Mix(Spread(row1[x0]), Spread(row1[x1]), fx);
            line[i] = Pack(Mix(top, bottom, walk.WeightY));
            position += walk.Step;
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
