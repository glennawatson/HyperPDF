// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using HyperPdf.Core.Ocr;

namespace HyperPdf.Core.Forms.Detection;

/// <summary>
/// Scans greyscale rows for dark pixels: the runs along a row, and counts per column across rows. Vector instructions
/// test 16 or 32 pixels at once where the processor has them; the scalar loops give the same answers elsewhere.
/// </summary>
internal static class DarkPixels
{
    /// <summary>The bits of the mask one 128 bit compare gives.</summary>
    private const int HalfMaskBits = 16;

    /// <summary>The pixels two 128 bit compares cover.</summary>
    private const int PairBytes = 32;

    /// <summary>Converts BGRA pixels to luminance.</summary>
    /// <param name="bgra">The pixels, four bytes each.</param>
    /// <param name="luma">Receives one byte per pixel.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ToLuminance(ReadOnlySpan<byte> bgra, Span<byte> luma) => OcrRunner.ToGrey(bgra, luma);

    /// <summary>Appends the runs of dark pixels in a row at least a given length.</summary>
    /// <param name="row">The row's luminance.</param>
    /// <param name="threshold">The level below which a pixel is dark.</param>
    /// <param name="minLength">The shortest run kept.</param>
    /// <param name="y">The row's index.</param>
    /// <param name="output">Receives the runs.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void FindRuns(ReadOnlySpan<byte> row, byte threshold, int minLength, int y, List<DarkRun> output) =>
        FindRuns(row, threshold, minLength, y, output, true);

    /// <summary>Appends the runs of dark pixels in a row at least a given length.</summary>
    /// <param name="row">The row's luminance.</param>
    /// <param name="threshold">The level below which a pixel is dark.</param>
    /// <param name="minLength">The shortest run kept.</param>
    /// <param name="y">The row's index.</param>
    /// <param name="output">Receives the runs.</param>
    /// <param name="useVectors">Whether to use vector instructions; tests and benchmarks turn them off to compare.</param>
    internal static void FindRuns(ReadOnlySpan<byte> row, byte threshold, int minLength, int y, List<DarkRun> output, bool useVectors)
    {
        var scanner = new RunScanner(y, minLength, output);
        var x = useVectors && Vector128.IsHardwareAccelerated ? ScanVector(row, threshold, ref scanner) : 0;
        for (; x < row.Length; x++)
        {
            scanner.Step(x, row[x] < threshold);
        }

        scanner.Finish(row.Length);
    }

    /// <summary>Adds one to the count of every column whose pixel in a row is dark.</summary>
    /// <param name="row">The row's luminance.</param>
    /// <param name="threshold">The level below which a pixel is dark.</param>
    /// <param name="counts">The column counts, one per pixel of the row.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void CountColumns(ReadOnlySpan<byte> row, byte threshold, Span<ushort> counts) => CountColumns(row, threshold, counts, true);

    /// <summary>Adds one to the count of every column whose pixel in a row is dark.</summary>
    /// <param name="row">The row's luminance.</param>
    /// <param name="threshold">The level below which a pixel is dark.</param>
    /// <param name="counts">The column counts, one per pixel of the row.</param>
    /// <param name="useVectors">Whether to use vector instructions; tests and benchmarks turn them off to compare.</param>
    internal static void CountColumns(ReadOnlySpan<byte> row, byte threshold, Span<ushort> counts, bool useVectors)
    {
        var length = Math.Min(row.Length, counts.Length);
        var x = useVectors && Vector128.IsHardwareAccelerated ? CountVector(row[..length], threshold, counts) : 0;
        for (; x < length; x++)
        {
            counts[x] += row[x] < threshold ? (ushort)1 : (ushort)0;
        }
    }

    /// <summary>Finds runs 32 pixels at a time from a mask of the dark ones.</summary>
    /// <param name="row">The row.</param>
    /// <param name="threshold">The dark level.</param>
    /// <param name="scanner">The run state.</param>
    /// <returns>The pixels scanned; the rest is left for the scalar loop.</returns>
    private static int ScanVector(ReadOnlySpan<byte> row, byte threshold, ref RunScanner scanner)
    {
        ref var start = ref MemoryMarshal.GetReference(row);
        var x = 0;
        if (Vector256.IsHardwareAccelerated)
        {
            var level = Vector256.Create(threshold);
            for (; x + Vector256<byte>.Count <= row.Length; x += Vector256<byte>.Count)
            {
                var mask = Vector256.LessThan(Vector256.LoadUnsafe(ref start, (nuint)x), level).ExtractMostSignificantBits();
                scanner.Block(x, mask);
            }

            return x;
        }

        // Processors with 128 bit vectors only (such as ARM) test two halves and join the masks.
        var narrow = Vector128.Create(threshold);
        for (; x + PairBytes <= row.Length; x += PairBytes)
        {
            var low = Vector128.LessThan(Vector128.LoadUnsafe(ref start, (nuint)x), narrow).ExtractMostSignificantBits();
            var high = Vector128.LessThan(Vector128.LoadUnsafe(ref start, (nuint)(x + Vector128<byte>.Count)), narrow).ExtractMostSignificantBits();
            scanner.Block(x, low | (high << HalfMaskBits));
        }

        return x;
    }

    /// <summary>Counts dark pixels per column 16 at a time.</summary>
    /// <param name="row">The row.</param>
    /// <param name="threshold">The dark level.</param>
    /// <param name="counts">The column counts.</param>
    /// <returns>The pixels counted; the rest is left for the scalar loop.</returns>
    private static int CountVector(ReadOnlySpan<byte> row, byte threshold, Span<ushort> counts)
    {
        ref var source = ref MemoryMarshal.GetReference(row);
        ref var target = ref MemoryMarshal.GetReference(counts);
        var level = Vector128.Create(threshold);
        var one = Vector128.Create((byte)1);
        var x = 0;
        for (; x + Vector128<byte>.Count <= row.Length; x += Vector128<byte>.Count)
        {
            var dark = Vector128.LessThan(Vector128.LoadUnsafe(ref source, (nuint)x), level) & one;
            var (lower, upper) = Vector128.Widen(dark);
            (Vector128.LoadUnsafe(ref target, (nuint)x) + lower).StoreUnsafe(ref target, (nuint)x);
            (Vector128.LoadUnsafe(ref target, (nuint)(x + Vector128<ushort>.Count)) + upper).StoreUnsafe(ref target, (nuint)(x + Vector128<ushort>.Count));
        }

        return x;
    }

    /// <summary>Tracks the run being scanned along a row.</summary>
    /// <param name="y">The row.</param>
    /// <param name="minLength">The shortest run kept.</param>
    /// <param name="output">Receives the runs.</param>
    [StructLayout(LayoutKind.Auto)]
    private struct RunScanner(int y, int minLength, List<DarkRun> output)
    {
        /// <summary>The bits in a 32 lane mask.</summary>
        private const int MaskBits = 32;

        /// <summary>Where the current run started, or -1 outside a run.</summary>
        private int _start = -1;

        /// <summary>Takes one pixel.</summary>
        /// <param name="x">The pixel.</param>
        /// <param name="dark">Whether it is dark.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Step(int x, bool dark)
        {
            if (dark == _start >= 0)
            {
                return;
            }

            if (dark)
            {
                _start = x;
            }
            else
            {
                Finish(x);
            }
        }

        /// <summary>Takes 32 pixels from a mask with a bit set for each dark one.</summary>
        /// <param name="x">The first pixel.</param>
        /// <param name="mask">The mask.</param>
        public void Block(int x, uint mask)
        {
            var at = 0;
            while (at < MaskBits)
            {
                // Inside a run look for the next light pixel; outside, for the next dark one.
                var rest = (_start >= 0 ? ~mask : mask) >> at;
                if (rest == 0)
                {
                    return;
                }

                at += BitOperations.TrailingZeroCount(rest);
                Step(x + at, _start < 0);
            }
        }

        /// <summary>Ends the current run, keeping it when long enough.</summary>
        /// <param name="end">The pixel after the run.</param>
        public void Finish(int end)
        {
            if (_start >= 0 && end - _start >= minLength)
            {
                output.Add(new(y, _start, end));
            }

            _start = -1;
        }
    }
}
