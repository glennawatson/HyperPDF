// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HyperPdf.Ocr.Lstm;

/// <summary>Resizes 8 bit greyscale pictures: each output pixel is the average of the part of the source it covers, or a blend of the nearest pixels when the picture grows.</summary>
public static class GreyScaler
{
    /// <summary>The most a value can be.</summary>
    private const float MaxValue = 255F;

    /// <summary>Half, used to find the middle of a pixel.</summary>
    private const double Half = 0.5;

    /// <summary>Resizes a picture.</summary>
    /// <param name="source">The pixels, row after row.</param>
    /// <param name="width">The source width.</param>
    /// <param name="height">The source height.</param>
    /// <param name="destination">Receives the resized pixels, <paramref name="newWidth"/> times <paramref name="newHeight"/> long.</param>
    /// <param name="newWidth">The new width.</param>
    /// <param name="newHeight">The new height.</param>
    /// <param name="scratch">The working memory.</param>
    public static void Scale(ReadOnlySpan<byte> source, int width, int height, Span<byte> destination, int newWidth, int newHeight, RecognitionScratch scratch)
    {
        ArgumentNullException.ThrowIfNull(scratch);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(newWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(newHeight);
        ArgumentOutOfRangeException.ThrowIfLessThan(source.Length, width * height);
        ArgumentOutOfRangeException.ThrowIfLessThan(destination.Length, newWidth * newHeight);

        // Rows first, into floats, so each column is averaged once however wide the picture is.
        var rows = scratch.RentFloats(newHeight * width);
        ScaleRows(source, width, height, rows, newHeight);
        for (var y = 0; y < newHeight; y++)
        {
            ScaleLine(rows.AsSpan(y * width, width), destination.Slice(y * newWidth, newWidth));
        }

        scratch.Return(rows);
    }

    /// <summary>Gets the position, in source pixels, of the middle of an output pixel.</summary>
    /// <param name="start">The position of the output pixel's start.</param>
    /// <param name="ratio">The number of source pixels in an output pixel.</param>
    /// <returns>The position, counted so that the middle of the first source pixel is 0.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double MiddleOf(double start, double ratio) => start + ((ratio - 1.0) * Half);

    /// <summary>Averages source rows into a smaller or larger number of rows.</summary>
    /// <param name="source">The pixels.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The source height.</param>
    /// <param name="rows">Receives the rows as floats.</param>
    /// <param name="newHeight">The new height.</param>
    private static void ScaleRows(ReadOnlySpan<byte> source, int width, int height, float[] rows, int newHeight)
    {
        var ratio = (double)height / newHeight;
        for (var y = 0; y < newHeight; y++)
        {
            var line = rows.AsSpan(y * width, width);
            var from = y * ratio;
            if (ratio >= 1.0)
            {
                AccumulateRange(source, width, from, from + ratio, line);
            }
            else
            {
                Interpolate(source, width, height, MiddleOf(from, ratio), line);
            }
        }
    }

    /// <summary>Averages the source rows covering a range into one row.</summary>
    /// <param name="source">The pixels.</param>
    /// <param name="width">The width.</param>
    /// <param name="from">The start of the range, in source rows.</param>
    /// <param name="to">The end of the range.</param>
    /// <param name="line">Receives the average.</param>
    private static void AccumulateRange(ReadOnlySpan<byte> source, int width, double from, double to, Span<float> line)
    {
        line.Clear();
        var last = Math.Min((int)Math.Ceiling(to) - 1, (source.Length / width) - 1);
        for (var row = (int)from; row <= last; row++)
        {
            var weight = (float)(Math.Min(to, row + 1) - Math.Max(from, row));
            AddScaled(source.Slice(row * width, width), weight, line);
        }

        Multiply(line, (float)(1.0 / (to - from)));
    }

    /// <summary>Blends the two source rows around a position into one row.</summary>
    /// <param name="source">The pixels.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The source height.</param>
    /// <param name="position">The position in source rows.</param>
    /// <param name="line">Receives the blend.</param>
    private static void Interpolate(ReadOnlySpan<byte> source, int width, int height, double position, Span<float> line)
    {
        var clamped = Math.Clamp(position, 0, height - 1);
        var low = (int)clamped;
        var high = Math.Min(low + 1, height - 1);
        var weight = (float)(clamped - low);
        line.Clear();
        AddScaled(source.Slice(low * width, width), 1F - weight, line);
        AddScaled(source.Slice(high * width, width), weight, line);
    }

    /// <summary>Adds a row multiplied by a weight to a line.</summary>
    /// <param name="row">The pixels of the row.</param>
    /// <param name="weight">The weight.</param>
    /// <param name="line">The line, added to.</param>
    private static void AddScaled(ReadOnlySpan<byte> row, float weight, Span<float> line)
    {
        for (var i = 0; i < line.Length; i++)
        {
            line[i] += row[i] * weight;
        }
    }

    /// <summary>Multiplies a line by a factor.</summary>
    /// <param name="line">The line.</param>
    /// <param name="factor">The factor.</param>
    private static void Multiply(Span<float> line, float factor)
    {
        var width = Vector<float>.Count;
        var i = 0;
        if (line.Length >= width)
        {
            var vectors = MemoryMarshal.Cast<float, Vector<float>>(line);
            for (var v = 0; v < vectors.Length; v++)
            {
                vectors[v] *= factor;
            }

            i = vectors.Length * width;
        }

        for (; i < line.Length; i++)
        {
            line[i] *= factor;
        }
    }

    /// <summary>Resizes one line of floats into bytes along its length.</summary>
    /// <param name="line">The line.</param>
    /// <param name="destination">Receives the resized line.</param>
    private static void ScaleLine(ReadOnlySpan<float> line, Span<byte> destination)
    {
        var ratio = (double)line.Length / destination.Length;
        for (var x = 0; x < destination.Length; x++)
        {
            var from = x * ratio;
            var value = ratio >= 1.0 ? Average(line, from, from + ratio) : Blend(line, MiddleOf(from, ratio));
            destination[x] = (byte)Math.Clamp(MathF.Round(value), 0F, MaxValue);
        }
    }

    /// <summary>Averages the part of a line a range covers.</summary>
    /// <param name="line">The line.</param>
    /// <param name="from">The start of the range.</param>
    /// <param name="to">The end of the range.</param>
    /// <returns>The average.</returns>
    private static float Average(ReadOnlySpan<float> line, double from, double to)
    {
        var total = 0.0;
        var last = Math.Min((int)Math.Ceiling(to) - 1, line.Length - 1);
        for (var i = (int)from; i <= last; i++)
        {
            total += line[i] * (Math.Min(to, i + 1) - Math.Max(from, i));
        }

        return (float)(total / (to - from));
    }

    /// <summary>Blends the two pixels around a position.</summary>
    /// <param name="line">The line.</param>
    /// <param name="position">The position.</param>
    /// <returns>The blend.</returns>
    private static float Blend(ReadOnlySpan<float> line, double position)
    {
        var clamped = Math.Clamp(position, 0, line.Length - 1);
        var low = (int)clamped;
        var high = Math.Min(low + 1, line.Length - 1);
        var weight = (float)(clamped - low);
        return (line[low] * (1F - weight)) + (line[high] * weight);
    }
}
