// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Rendering.Cpu;

/// <summary>Copies and scales BGRA pixels into a frame buffer without allocating.</summary>
public static class CpuBlit
{
    /// <summary>The bytes per pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The offset of the alpha byte in a pixel.</summary>
    private const int AlphaOffset = 3;

    /// <summary>The largest channel value.</summary>
    private const int ChannelMax = 255;

    /// <summary>Half a channel step, added before a divide to round it.</summary>
    private const int HalfChannel = 127;

    /// <summary>The number of weight steps for the smooth sampler.</summary>
    private const int WeightSteps = 256;

    /// <summary>The shift that divides a 16.16 fixed point number back to whole pixels.</summary>
    private const int FixedShift = 16;

    /// <summary>The shift that keeps the top 8 bits of a 16 bit fraction.</summary>
    private const int WeightShift = 8;

    /// <summary>The shift that undoes the two 8 bit weights multiplied together.</summary>
    private const int BothWeightsShift = 16;

    /// <summary>The mask for the fraction of a 16.16 fixed point number.</summary>
    private const int FractionMask = 0xFFFF;

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

        ScaleRows(frame, frameGrid, source, sourceGrid, request, rows);
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
            if (IsOpaque(from))
            {
                from.CopyTo(to);
            }
            else
            {
                BlendRow(to, from);
            }
        }
    }

    /// <summary>Draws scaled rows by stepping through the source in 16.16 fixed point.</summary>
    /// <param name="frame">The frame pixels.</param>
    /// <param name="frameGrid">The frame shape.</param>
    /// <param name="source">The source pixels.</param>
    /// <param name="sourceGrid">The source shape.</param>
    /// <param name="request">Where the source goes.</param>
    /// <param name="rows">The clipped rows.</param>
    private static void ScaleRows(Span<byte> frame, in PixelGrid frameGrid, ReadOnlySpan<byte> source, in PixelGrid sourceGrid, in BlitRequest request, in BlitRows rows)
    {
        var stepX = (int)(((long)sourceGrid.Width << FixedShift) / request.Width);
        var stepY = (int)(((long)sourceGrid.Height << FixedShift) / request.Height);
        var start = (rows.Left - request.X) * stepX;
        for (var row = rows.Top; row < rows.Bottom; row++)
        {
            var sy = (row - request.Y) * stepY;
            var line = frame.Slice((row * frameGrid.Stride) + (rows.Left * BytesPerPixel), (rows.Right - rows.Left) * BytesPerPixel);
            if (request.Smooth)
            {
                SampleLineBilinear(line, source, sourceGrid, sy, start, stepX);
            }
            else
            {
                var sourceRow = Math.Min(sy >> FixedShift, sourceGrid.Height - 1);
                SampleLineNearest(line, source.Slice(sourceRow * sourceGrid.Stride, sourceGrid.Stride), start, stepX);
            }
        }
    }

    /// <summary>Tells whether every pixel of a row is fully opaque, so the row can be copied as it is.</summary>
    /// <param name="row">The BGRA row.</param>
    /// <returns><see langword="true"/> when every alpha byte is 255.</returns>
    private static bool IsOpaque(ReadOnlySpan<byte> row)
    {
        for (var i = AlphaOffset; i < row.Length; i += BytesPerPixel)
        {
            if (row[i] != ChannelMax)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Blends a premultiplied source row over a destination row.</summary>
    /// <param name="to">The destination row.</param>
    /// <param name="from">The source row.</param>
    private static void BlendRow(Span<byte> to, ReadOnlySpan<byte> from)
    {
        for (var i = 0; i < from.Length; i += BytesPerPixel)
        {
            var inverse = ChannelMax - from[i + AlphaOffset];
            for (var c = 0; c < BytesPerPixel; c++)
            {
                var blended = from[i + c] + (((to[i + c] * inverse) + HalfChannel) / ChannelMax);
                to[i + c] = (byte)Math.Min(ChannelMax, blended);
            }
        }
    }

    /// <summary>Fills one destination line by picking the nearest source pixel for each.</summary>
    /// <param name="line">The destination line.</param>
    /// <param name="sourceRow">The source row.</param>
    /// <param name="start">The 16.16 source position of the first pixel.</param>
    /// <param name="step">The 16.16 source step per destination pixel.</param>
    private static void SampleLineNearest(Span<byte> line, ReadOnlySpan<byte> sourceRow, int start, int step)
    {
        var position = start;
        var last = (sourceRow.Length / BytesPerPixel) - 1;
        for (var i = 0; i < line.Length; i += BytesPerPixel)
        {
            var at = Math.Min(position >> FixedShift, last) * BytesPerPixel;
            sourceRow.Slice(at, BytesPerPixel).CopyTo(line.Slice(i, BytesPerPixel));
            position += step;
        }
    }

    /// <summary>Fills one destination line by blending the four nearest source pixels for each.</summary>
    /// <param name="line">The destination line.</param>
    /// <param name="source">The source pixels.</param>
    /// <param name="sourceGrid">The source shape.</param>
    /// <param name="sy">The 16.16 source row position.</param>
    /// <param name="start">The 16.16 source column position of the first pixel.</param>
    /// <param name="step">The 16.16 source step per destination pixel.</param>
    private static void SampleLineBilinear(Span<byte> line, ReadOnlySpan<byte> source, in PixelGrid sourceGrid, int sy, int start, int step)
    {
        var y0 = Math.Min(sy >> FixedShift, sourceGrid.Height - 1);
        var y1 = Math.Min(y0 + 1, sourceGrid.Height - 1);
        var fy = (sy & FractionMask) >> WeightShift;
        var row0 = source.Slice(y0 * sourceGrid.Stride, sourceGrid.Stride);
        var row1 = source.Slice(y1 * sourceGrid.Stride, sourceGrid.Stride);
        var position = start;
        for (var i = 0; i < line.Length; i += BytesPerPixel)
        {
            var x0 = Math.Min(position >> FixedShift, sourceGrid.Width - 1) * BytesPerPixel;
            var x1 = Math.Min((position >> FixedShift) + 1, sourceGrid.Width - 1) * BytesPerPixel;
            var fx = (position & FractionMask) >> WeightShift;
            for (var c = 0; c < BytesPerPixel; c++)
            {
                var top = (row0[x0 + c] * (WeightSteps - fx)) + (row0[x1 + c] * fx);
                var bottom = (row1[x0 + c] * (WeightSteps - fx)) + (row1[x1 + c] * fx);
                line[i + c] = (byte)(((top * (WeightSteps - fy)) + (bottom * fy)) >> BothWeightsShift);
            }

            position += step;
        }
    }
}
