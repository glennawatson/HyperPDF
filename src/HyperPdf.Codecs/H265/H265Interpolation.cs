// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Interpolates fractional sample positions of a reference block to the 14 bit intermediate precision the weighted prediction takes (8.5.3.3.3).</summary>
internal static class H265Interpolation
{
    /// <summary>The intermediate bit depth.</summary>
    private const int IntermediateBits = 14;

    /// <summary>The shift of the second filter stage.</summary>
    private const int SecondShift = 6;

    /// <summary>The largest first stage shift.</summary>
    private const int MaxFirstShift = 4;

    /// <summary>The smallest shift that brings a whole sample to the intermediate precision.</summary>
    private const int MinWholeShift = 2;

    /// <summary>The bit depth of 8 bit video.</summary>
    private const int EightBits = 8;

    /// <summary>Interpolates a block.</summary>
    /// <param name="job">The block.</param>
    /// <param name="buffers">The work areas.</param>
    /// <param name="destination">Receives the block, row after row.</param>
    internal static void Run(in H265InterpolationJob job, H265PredictionBuffers buffers, Span<short> destination)
    {
        var taps = job.Chroma ? H265InterpolationFilters.ChromaTaps : H265InterpolationFilters.LumaTaps;
        var before = (taps >> 1) - 1;
        var width = job.Source.Width;
        var height = job.Source.Height;
        var stride = width + taps - 1;
        var wholeShift = Math.Max(MinWholeShift, IntermediateBits - job.BitDepth);
        var shift1 = Math.Min(MaxFirstShift, job.BitDepth - EightBits);
        Fetch(job, buffers.Reference, before, taps);
        var reference = buffers.Reference.AsSpan();
        if (job.FracX == 0 && job.FracY == 0)
        {
            CopyWhole(reference, stride, before, destination, width, height, wholeShift);
            return;
        }

        var horizontal = job.FracX == 0 ? default : Filter(job, job.FracX);
        var vertical = job.FracY == 0 ? default : Filter(job, job.FracY);
        if (job.FracY == 0)
        {
            FilterRows(reference, new(stride, width, height), before, horizontal, destination, shift1);
        }
        else if (job.FracX == 0)
        {
            FilterColumns(reference[before..], stride, vertical, destination, width, height, shift1);
        }
        else
        {
            var middle = buffers.Horizontal.AsSpan();
            FilterRows(reference, new(stride, width, height + taps - 1), 0, horizontal, middle, shift1);
            FilterColumns(middle, width, vertical, destination, width, height, SecondShift);
        }
    }

    /// <summary>Gets the taps for a fraction.</summary>
    /// <param name="job">The block.</param>
    /// <param name="fraction">The fraction.</param>
    /// <returns>The taps.</returns>
    private static ReadOnlySpan<sbyte> Filter(in H265InterpolationJob job, int fraction) =>
        job.Chroma ? H265InterpolationFilters.Chroma(fraction) : H265InterpolationFilters.Luma(fraction);

    /// <summary>Copies the reference block and its margins into the work area, repeating the edge samples of the plane where the block reaches past them.</summary>
    /// <param name="job">The block.</param>
    /// <param name="target">The work area.</param>
    /// <param name="before">The margin before the block.</param>
    /// <param name="taps">The taps of the filter.</param>
    private static void Fetch(in H265InterpolationJob job, ushort[] target, int before, int taps)
    {
        var width = job.Source.Width + taps - 1;
        var height = job.Source.Height + taps - 1;
        var left = job.Source.X - before;
        var top = job.Source.Y - before;
        var inside = left >= 0 && left + width <= job.PlaneWidth;
        for (var row = 0; row < height; row++)
        {
            var sourceRow = Math.Clamp(top + row, 0, job.PlaneHeight - 1) * job.PlaneWidth;
            var line = target.AsSpan(row * width, width);
            if (inside)
            {
                job.Plane.AsSpan(sourceRow + left, width).CopyTo(line);
                continue;
            }

            for (var column = 0; column < width; column++)
            {
                line[column] = job.Plane[sourceRow + Math.Clamp(left + column, 0, job.PlaneWidth - 1)];
            }
        }
    }

    /// <summary>Copies whole samples scaled to the intermediate precision.</summary>
    /// <param name="reference">The work area.</param>
    /// <param name="stride">The work area's row length.</param>
    /// <param name="before">The margin before the block.</param>
    /// <param name="destination">Receives the samples.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="shift">The left shift.</param>
    private static void CopyWhole(ReadOnlySpan<ushort> reference, int stride, int before, Span<short> destination, int width, int height, int shift)
    {
        for (var y = 0; y < height; y++)
        {
            var source = ((y + before) * stride) + before;
            for (var x = 0; x < width; x++)
            {
                destination[(y * width) + x] = (short)(reference[source + x] << shift);
            }
        }
    }

    /// <summary>Filters along rows.</summary>
    /// <param name="reference">The work area.</param>
    /// <param name="area">The row length of the work area, and the width and number of the rows to filter.</param>
    /// <param name="first">The work area row to start at.</param>
    /// <param name="taps">The filter taps.</param>
    /// <param name="destination">Receives the filtered samples.</param>
    /// <param name="shift">The right shift.</param>
    private static void FilterRows(ReadOnlySpan<ushort> reference, in H265FilterArea area, int first, ReadOnlySpan<sbyte> taps, Span<short> destination, int shift)
    {
        var width = area.Width;
        for (var y = 0; y < area.Height; y++)
        {
            var source = (first + y) * area.Stride;
            for (var x = 0; x < width; x++)
            {
                var sum = 0;
                for (var k = 0; k < taps.Length; k++)
                {
                    sum += taps[k] * reference[source + x + k];
                }

                destination[(y * width) + x] = (short)(sum >> shift);
            }
        }
    }

    /// <summary>Filters along columns of the work area (samples) or of the intermediate rows.</summary>
    /// <param name="source">The samples, starting at the first sample of the first column to filter.</param>
    /// <param name="stride">The row length of the source.</param>
    /// <param name="taps">The filter taps.</param>
    /// <param name="destination">Receives the filtered samples.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="shift">The right shift.</param>
    private static void FilterColumns(ReadOnlySpan<ushort> source, int stride, ReadOnlySpan<sbyte> taps, Span<short> destination, int width, int height, int shift)
    {
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var sum = 0;
                for (var k = 0; k < taps.Length; k++)
                {
                    sum += taps[k] * source[((y + k) * stride) + x];
                }

                destination[(y * width) + x] = (short)(sum >> shift);
            }
        }
    }

    /// <summary>Filters along columns of the intermediate rows produced by the horizontal stage.</summary>
    /// <param name="source">The intermediate rows, starting with the margin above the block.</param>
    /// <param name="stride">The row length of the source.</param>
    /// <param name="taps">The filter taps.</param>
    /// <param name="destination">Receives the filtered samples.</param>
    /// <param name="width">The block width.</param>
    /// <param name="height">The block height.</param>
    /// <param name="shift">The right shift.</param>
    private static void FilterColumns(ReadOnlySpan<short> source, int stride, ReadOnlySpan<sbyte> taps, Span<short> destination, int width, int height, int shift)
    {
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var sum = 0;
                for (var k = 0; k < taps.Length; k++)
                {
                    sum += taps[k] * source[((y + k) * stride) + x];
                }

                destination[(y * width) + x] = (short)(sum >> shift);
            }
        }
    }
}
