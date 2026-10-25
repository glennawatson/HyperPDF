// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace HyperPdfLibrary.Graphics.Images;

/// <summary>
/// Resamples a one byte plane to another size. An axis that grows reads the source pixel under each target pixel's centre;
/// an axis that shrinks averages the source pixels the target pixel covers. The source columns each target column covers
/// are worked out once instead of once for every row, and a target row that covers the same source rows as the one before
/// it is copied.
/// </summary>
internal static class PlaneResampler
{
    /// <summary>Two, which doubles an index to work in half pixels.</summary>
    private const int PairFactor = 2;

    /// <summary>The ints kept for each target column: the first source column and one past the last.</summary>
    private const int ColumnPair = 2;

    /// <summary>The most pixels an average may cover for the multiplication that replaces its division to be exact.</summary>
    private const int MaxReciprocalCount = 4_096;

    /// <summary>The bytes one vector adds.</summary>
    private const int VectorBytes = 16;

    /// <summary>The sums one vector of ints holds.</summary>
    private const int IntsPerVector = 4;

    /// <summary>The offset of the third group of four columns in a vector of sixteen.</summary>
    private const int ThirdQuarter = 8;

    /// <summary>The offset of the fourth group of four columns in a vector of sixteen.</summary>
    private const int FourthQuarter = 12;

    /// <summary>The bits of the reciprocal.</summary>
    private const int ReciprocalBits = 32;

    /// <summary>Resamples a plane.</summary>
    /// <param name="source">The source plane.</param>
    /// <param name="sourceWidth">The source width.</param>
    /// <param name="sourceHeight">The source height.</param>
    /// <param name="target">Receives the resampled plane.</param>
    /// <param name="width">The target width.</param>
    /// <param name="height">The target height.</param>
    internal static void Resample(ReadOnlySpan<byte> source, int sourceWidth, int sourceHeight, Span<byte> target, int width, int height)
    {
        if (sourceWidth == width && sourceHeight == height)
        {
            source[..(width * height)].CopyTo(target);
            return;
        }

        var columns = ArrayPool<int>.Shared.Rent(width * ColumnPair);
        var sums = ArrayPool<int>.Shared.Rent(sourceWidth);
        try
        {
            for (var x = 0; x < width; x++)
            {
                var low = RangeStart(x, sourceWidth, width);
                columns[x * ColumnPair] = low;
                columns[(x * ColumnPair) + 1] = RangeEnd(x, sourceWidth, width, low);
            }

            var previousLow = -1;
            var previousHigh = -1;
            for (var y = 0; y < height; y++)
            {
                var rowLow = RangeStart(y, sourceHeight, height);
                var rowHigh = RangeEnd(y, sourceHeight, height, rowLow);
                var row = target.Slice(y * width, width);
                if (rowLow == previousLow && rowHigh == previousHigh)
                {
                    target.Slice((y - 1) * width, width).CopyTo(row);
                    continue;
                }

                previousLow = rowLow;
                previousHigh = rowHigh;
                ResampleRow(source, sourceWidth, rowLow, rowHigh, columns, sums, row);
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(columns);
            ArrayPool<int>.Shared.Return(sums);
        }
    }

    /// <summary>Adds the source rows a target row covers into one sum for each column, a vector at a time.</summary>
    /// <param name="source">The source plane.</param>
    /// <param name="stride">The source width.</param>
    /// <param name="rowLow">The first source row.</param>
    /// <param name="rowHigh">One past the last source row.</param>
    /// <param name="sums">Receives the sums, one for each column.</param>
    private static void AddRows(ReadOnlySpan<byte> source, int stride, int rowLow, int rowHigh, Span<int> sums)
    {
        sums = sums[..stride];
        sums.Clear();
        for (var r = rowLow; r < rowHigh; r++)
        {
            var line = source.Slice(r * stride, stride);
            var column = 0;
            if (Vector128.IsHardwareAccelerated)
            {
                ref var sumStart = ref MemoryMarshal.GetReference(sums);
                ref var lineStart = ref MemoryMarshal.GetReference(line);
                for (; column + VectorBytes <= stride; column += VectorBytes)
                {
                    var bytes = Vector128.LoadUnsafe(ref lineStart, (nuint)column);
                    var low = Vector128.WidenLower(bytes);
                    var high = Vector128.WidenUpper(bytes);
                    AddInts(ref sumStart, column, Vector128.WidenLower(low));
                    AddInts(ref sumStart, column + IntsPerVector, Vector128.WidenUpper(low));
                    AddInts(ref sumStart, column + ThirdQuarter, Vector128.WidenLower(high));
                    AddInts(ref sumStart, column + FourthQuarter, Vector128.WidenUpper(high));
                }
            }

            for (; column < stride; column++)
            {
                sums[column] += line[column];
            }
        }
    }

    /// <summary>Adds four widened bytes to four sums.</summary>
    /// <param name="sums">The start of the sums.</param>
    /// <param name="index">The index of the first sum.</param>
    /// <param name="values">The bytes, widened.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddInts(ref int sums, int index, Vector128<uint> values) =>
        (Vector128.LoadUnsafe(ref sums, (nuint)index) + values.AsInt32()).StoreUnsafe(ref sums, (nuint)index);

    /// <summary>Gets the first source index a target index covers.</summary>
    /// <param name="index">The target index.</param>
    /// <param name="sourceSize">The source size.</param>
    /// <param name="targetSize">The target size.</param>
    /// <returns>The first source index.</returns>
    private static int RangeStart(int index, int sourceSize, int targetSize) => targetSize < sourceSize
        ? (int)((long)index * sourceSize / targetSize)
        : (int)(((PairFactor * index) + 1L) * sourceSize / (PairFactor * (long)targetSize));

    /// <summary>Gets one past the last source index a target index covers.</summary>
    /// <param name="index">The target index.</param>
    /// <param name="sourceSize">The source size.</param>
    /// <param name="targetSize">The target size.</param>
    /// <param name="start">The first source index.</param>
    /// <returns>The end of the covered range, at least one pixel after <paramref name="start"/>.</returns>
    private static int RangeEnd(int index, int sourceSize, int targetSize, int start) => targetSize < sourceSize
        ? Math.Max(start + 1, (int)((long)(index + 1) * sourceSize / targetSize))
        : start + 1;

    /// <summary>Resamples one target row.</summary>
    /// <param name="source">The source plane.</param>
    /// <param name="stride">The source width.</param>
    /// <param name="rowLow">The first source row the target row covers.</param>
    /// <param name="rowHigh">One past the last source row.</param>
    /// <param name="columns">The first source column and one past the last, for each target column.</param>
    /// <param name="sums">Scratch for the column sums, one int for each source column.</param>
    /// <param name="row">Receives the target row.</param>
    private static void ResampleRow(ReadOnlySpan<byte> source, int stride, int rowLow, int rowHigh, int[] columns, Span<int> sums, Span<byte> row)
    {
        var rows = rowHigh - rowLow;
        var cached = default(ReciprocalCache);
        if (rows > 1 && sums.Length >= stride)
        {
            AddRows(source, stride, rowLow, rowHigh, sums);
            for (var x = 0; x < row.Length; x++)
            {
                var low = columns[x * ColumnPair];
                var high = columns[(x * ColumnPair) + 1];
                var sum = 0L;
                for (var column = low; column < high; column++)
                {
                    sum += sums[column];
                }

                row[x] = Divide(sum, (high - low) * rows, ref cached);
            }

            return;
        }

        for (var x = 0; x < row.Length; x++)
        {
            var low = columns[x * ColumnPair];
            var high = columns[(x * ColumnPair) + 1];
            if (rows == 1 && high - low == 1)
            {
                row[x] = source[(rowLow * stride) + low];
                continue;
            }

            var sum = 0L;
            for (var r = rowLow; r < rowHigh; r++)
            {
                sum += Sum(source.Slice((r * stride) + low, high - low));
            }

            row[x] = Divide(sum, (high - low) * rows, ref cached);
        }
    }

    /// <summary>Adds up bytes.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>Their sum.</returns>
    private static long Sum(ReadOnlySpan<byte> bytes)
    {
        var sum = 0L;
        foreach (var value in bytes)
        {
            sum += value;
        }

        return sum;
    }

    /// <summary>Divides a sum by a count, rounding to nearest.</summary>
    /// <param name="sum">The sum.</param>
    /// <param name="count">The count, above zero.</param>
    /// <returns>The rounded quotient.</returns>
    private static byte Divide(long sum, int count) => (byte)((sum + (count / PairFactor)) / count);

    /// <summary>Divides a sum by a count with the count's reciprocal, which is worked out only when the count changes.</summary>
    /// <param name="sum">The sum.</param>
    /// <param name="count">The count, above zero.</param>
    /// <param name="cached">The count the reciprocal was worked out for, and that reciprocal.</param>
    /// <returns>The rounded quotient.</returns>
    private static byte Divide(long sum, int count, ref ReciprocalCache cached)
    {
        if (count > MaxReciprocalCount)
        {
            return Divide(sum, count);
        }

        if (cached.Count != count)
        {
            cached = new(count, Reciprocal(count));
        }

        // For a count below 4096 and a sum of bytes, multiplying by the rounded-up reciprocal gives exactly the integer quotient.
        return (byte)(((ulong)(sum + (count / PairFactor)) * cached.Value) >> ReciprocalBits);
    }

    /// <summary>Gets the reciprocal of a count as a 32 bit fraction, rounded up.</summary>
    /// <param name="count">The count, above zero.</param>
    /// <returns>The reciprocal.</returns>
    private static ulong Reciprocal(int count) => (((1UL << ReciprocalBits) + (ulong)count) - 1) / (ulong)count;

    /// <summary>The reciprocal of a count.</summary>
    /// <param name="Count">The count.</param>
    /// <param name="Value">The reciprocal as a 32 bit fraction, rounded up.</param>
    private readonly record struct ReciprocalCache(int Count, ulong Value);
}
