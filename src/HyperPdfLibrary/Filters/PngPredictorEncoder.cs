// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Filters;

/// <summary>
/// Applies the PNG row filters that a Flate stream's <c>/Predictor</c> 15 describes (ISO 32000-2 7.4.4.4): each row gets
/// a tag byte followed by the row filtered with None, Sub, Up, Average or Paeth. For every row the filter whose output has
/// the smallest sum of absolute signed byte values is kept, which is the usual measure of how well the row compresses.
/// </summary>
internal static class PngPredictorEncoder
{
    /// <summary>The number of row filters.</summary>
    private const int FilterCount = 5;

    /// <summary>The tag of the None filter.</summary>
    private const byte None = 0;

    /// <summary>The tag of the Sub filter.</summary>
    private const byte Sub = 1;

    /// <summary>The tag of the Up filter.</summary>
    private const byte Up = 2;

    /// <summary>The tag of the Average filter.</summary>
    private const byte Average = 3;

    /// <summary>The tag of the Paeth filter.</summary>
    private const byte Paeth = 4;

    /// <summary>The value that maps a byte to its signed magnitude.</summary>
    private const int SignedLimit = 128;

    /// <summary>The number of values in a byte.</summary>
    private const int ByteValues = 256;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>Filters every row of an image.</summary>
    /// <param name="samples">The unfiltered rows, packed with no padding between them.</param>
    /// <param name="rowBytes">The bytes in a row.</param>
    /// <param name="pixelBytes">The bytes in a pixel, at least one; the distance to the pixel on the left.</param>
    /// <param name="output">Receives a tag byte and the filtered bytes for each row.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rowBytes"/> or <paramref name="pixelBytes"/> is not positive.</exception>
    /// <exception cref="ArgumentException">The samples are not a whole number of rows.</exception>
    internal static void Encode(ReadOnlySpan<byte> samples, int rowBytes, int pixelBytes, ref PooledBuffer output)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rowBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelBytes);
        if (samples.Length % rowBytes != 0)
        {
            throw new ArgumentException("The samples are not a whole number of rows.", nameof(samples));
        }

        var candidates = ArrayPool<byte>.Shared.Rent(rowBytes * FilterCount);
        try
        {
            var previous = ReadOnlySpan<byte>.Empty;
            for (var offset = 0; offset < samples.Length; offset += rowBytes)
            {
                var row = samples.Slice(offset, rowBytes);
                var best = FilterRow(row, previous, pixelBytes, candidates.AsSpan(0, rowBytes * FilterCount));
                output.WriteByte(best);
                output.Write(candidates.AsSpan(best * rowBytes, rowBytes));
                previous = row;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(candidates);
        }
    }

    /// <summary>Gets the bytes in a pixel for the predictor, rounded up to a whole byte.</summary>
    /// <param name="colors">The components per pixel.</param>
    /// <param name="bitsPerComponent">The bits per component.</param>
    /// <returns>The bytes, at least one.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetPixelBytes(int colors, int bitsPerComponent) => Math.Max(1, ((colors * bitsPerComponent) + ByteBits - 1) / ByteBits);

    /// <summary>Filters one row all five ways and picks the best.</summary>
    /// <param name="row">The row.</param>
    /// <param name="previous">The row above, or empty for the first row.</param>
    /// <param name="pixelBytes">The bytes in a pixel.</param>
    /// <param name="candidates">Receives the five filtered rows one after another.</param>
    /// <returns>The tag of the filter with the lowest cost.</returns>
    private static byte FilterRow(ReadOnlySpan<byte> row, ReadOnlySpan<byte> previous, int pixelBytes, Span<byte> candidates)
    {
        var length = row.Length;
        row.CopyTo(candidates[(None * length)..]);
        FilterSub(row, pixelBytes, candidates.Slice(Sub * length, length));
        FilterUp(row, previous, candidates.Slice(Up * length, length));
        FilterAverage(row, previous, pixelBytes, candidates.Slice(Average * length, length));
        FilterPaeth(row, previous, pixelBytes, candidates.Slice(Paeth * length, length));
        var best = None;
        var bestCost = long.MaxValue;
        for (byte tag = 0; tag < FilterCount; tag++)
        {
            var cost = Cost(candidates.Slice(tag * length, length));
            best = cost < bestCost ? tag : best;
            bestCost = Math.Min(cost, bestCost);
        }

        return best;
    }

    /// <summary>Adds up how far each byte is from zero when read as a signed value.</summary>
    /// <param name="filtered">The filtered row.</param>
    /// <returns>The sum.</returns>
    private static long Cost(ReadOnlySpan<byte> filtered)
    {
        long sum = 0;
        foreach (var value in filtered)
        {
            sum += value < SignedLimit ? value : ByteValues - value;
        }

        return sum;
    }

    /// <summary>Filters with Sub: each byte minus the one a pixel to its left.</summary>
    /// <param name="row">The row.</param>
    /// <param name="pixelBytes">The bytes in a pixel.</param>
    /// <param name="target">Receives the filtered row.</param>
    private static void FilterSub(ReadOnlySpan<byte> row, int pixelBytes, Span<byte> target)
    {
        for (var i = 0; i < row.Length; i++)
        {
            target[i] = (byte)(row[i] - Left(row, i, pixelBytes));
        }
    }

    /// <summary>Filters with Up: each byte minus the one above.</summary>
    /// <param name="row">The row.</param>
    /// <param name="previous">The row above, or empty.</param>
    /// <param name="target">Receives the filtered row.</param>
    private static void FilterUp(ReadOnlySpan<byte> row, ReadOnlySpan<byte> previous, Span<byte> target)
    {
        for (var i = 0; i < row.Length; i++)
        {
            target[i] = (byte)(row[i] - Above(previous, i));
        }
    }

    /// <summary>Filters with Average: each byte minus the floor of the mean of the left and upper bytes.</summary>
    /// <param name="row">The row.</param>
    /// <param name="previous">The row above, or empty.</param>
    /// <param name="pixelBytes">The bytes in a pixel.</param>
    /// <param name="target">Receives the filtered row.</param>
    private static void FilterAverage(ReadOnlySpan<byte> row, ReadOnlySpan<byte> previous, int pixelBytes, Span<byte> target)
    {
        for (var i = 0; i < row.Length; i++)
        {
            target[i] = (byte)(row[i] - ((Left(row, i, pixelBytes) + Above(previous, i)) >> 1));
        }
    }

    /// <summary>Filters with Paeth: each byte minus the neighbour the Paeth rule picks.</summary>
    /// <param name="row">The row.</param>
    /// <param name="previous">The row above, or empty.</param>
    /// <param name="pixelBytes">The bytes in a pixel.</param>
    /// <param name="target">Receives the filtered row.</param>
    private static void FilterPaeth(ReadOnlySpan<byte> row, ReadOnlySpan<byte> previous, int pixelBytes, Span<byte> target)
    {
        for (var i = 0; i < row.Length; i++)
        {
            var upLeft = i >= pixelBytes ? Above(previous, i - pixelBytes) : 0;
            target[i] = (byte)(row[i] - PaethPredict(Left(row, i, pixelBytes), Above(previous, i), upLeft));
        }
    }

    /// <summary>Gets the byte a pixel to the left.</summary>
    /// <param name="row">The row.</param>
    /// <param name="index">The byte's index.</param>
    /// <param name="pixelBytes">The bytes in a pixel.</param>
    /// <returns>The byte, or zero at the start of the row.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Left(ReadOnlySpan<byte> row, int index, int pixelBytes) => index >= pixelBytes ? row[index - pixelBytes] : 0;

    /// <summary>Gets the byte above.</summary>
    /// <param name="previous">The row above, or empty.</param>
    /// <param name="index">The byte's index.</param>
    /// <returns>The byte, or zero in the first row.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Above(ReadOnlySpan<byte> previous, int index) => previous.IsEmpty ? 0 : previous[index];

    /// <summary>Chooses the Paeth predictor (the PNG specification's rule, which ISO 32000-2 7.4.4.4 refers to).</summary>
    /// <param name="left">The byte to the left.</param>
    /// <param name="up">The byte above.</param>
    /// <param name="upLeft">The byte above and to the left.</param>
    /// <returns>The prediction.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int PaethPredict(int left, int up, int upLeft)
    {
        var estimate = left + up - upLeft;
        var toLeft = Math.Abs(estimate - left);
        var toUp = Math.Abs(estimate - up);
        var toUpLeft = Math.Abs(estimate - upLeft);
        if (toLeft <= toUp && toLeft <= toUpLeft)
        {
            return left;
        }

        return toUp <= toUpLeft ? up : upLeft;
    }
}
