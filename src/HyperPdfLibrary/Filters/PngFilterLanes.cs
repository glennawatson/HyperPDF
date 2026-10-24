// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace HyperPdfLibrary.Filters;

/// <summary>
/// Filters sixteen bytes of a row at a time for the PNG encoder. Encoding reads only the original row, never the filtered
/// output, so no byte waits for another, and the neighbours of sixteen bytes are loaded together. Each method filters whole
/// groups of sixteen from a start index and returns where it stopped; the caller finishes the rest of the row.
/// </summary>
internal static class PngFilterLanes
{
    /// <summary>The bytes one step filters.</summary>
    private const int Step = 16;

    /// <summary>Gets a value indicating whether sixteen bytes can be filtered at a time on this machine.</summary>
    internal static bool IsAccelerated => Vector128.IsHardwareAccelerated;

    /// <summary>Filters with Sub: each byte minus the one a pixel to its left.</summary>
    /// <param name="row">The row.</param>
    /// <param name="pixelBytes">The bytes in a pixel.</param>
    /// <param name="target">Receives the filtered row.</param>
    /// <param name="start">The first byte, at least one pixel in.</param>
    /// <returns>The index after the last byte filtered.</returns>
    internal static int Sub(ReadOnlySpan<byte> row, int pixelBytes, Span<byte> target, int start)
    {
        ref var source = ref MemoryMarshal.GetReference(row);
        ref var output = ref MemoryMarshal.GetReference(target);
        var i = start;
        for (; i + Step <= row.Length; i += Step)
        {
            Store(ref output, i, Load(ref source, i) - Load(ref source, i - pixelBytes));
        }

        return i;
    }

    /// <summary>Filters with Up: each byte minus the one above.</summary>
    /// <param name="row">The row.</param>
    /// <param name="previous">The row above, as long as the row.</param>
    /// <param name="target">Receives the filtered row.</param>
    /// <param name="start">The first byte.</param>
    /// <returns>The index after the last byte filtered.</returns>
    internal static int Up(ReadOnlySpan<byte> row, ReadOnlySpan<byte> previous, Span<byte> target, int start)
    {
        ref var source = ref MemoryMarshal.GetReference(row);
        ref var above = ref MemoryMarshal.GetReference(previous);
        ref var output = ref MemoryMarshal.GetReference(target);
        var i = start;
        for (; i + Step <= row.Length; i += Step)
        {
            Store(ref output, i, Load(ref source, i) - Load(ref above, i));
        }

        return i;
    }

    /// <summary>Filters with Average: each byte minus the floor of the mean of the left and upper bytes.</summary>
    /// <param name="row">The row.</param>
    /// <param name="previous">The row above, as long as the row.</param>
    /// <param name="pixelBytes">The bytes in a pixel.</param>
    /// <param name="target">Receives the filtered row.</param>
    /// <param name="start">The first byte, at least one pixel in.</param>
    /// <returns>The index after the last byte filtered.</returns>
    internal static int Average(ReadOnlySpan<byte> row, ReadOnlySpan<byte> previous, int pixelBytes, Span<byte> target, int start)
    {
        ref var source = ref MemoryMarshal.GetReference(row);
        ref var above = ref MemoryMarshal.GetReference(previous);
        ref var output = ref MemoryMarshal.GetReference(target);
        var i = start;
        for (; i + Step <= row.Length; i += Step)
        {
            // The floor of the mean of two bytes without overflowing a byte: their shared bits plus half of the bits they differ in.
            var left = Load(ref source, i - pixelBytes);
            var up = Load(ref above, i);
            var mean = (left & up) + Vector128.ShiftRightLogical(left ^ up, 1);
            Store(ref output, i, Load(ref source, i) - mean);
        }

        return i;
    }

    /// <summary>Filters with Paeth: each byte minus the neighbour the Paeth rule picks.</summary>
    /// <param name="row">The row.</param>
    /// <param name="previous">The row above, as long as the row.</param>
    /// <param name="pixelBytes">The bytes in a pixel.</param>
    /// <param name="target">Receives the filtered row.</param>
    /// <param name="start">The first byte, at least one pixel in.</param>
    /// <returns>The index after the last byte filtered.</returns>
    internal static int Paeth(ReadOnlySpan<byte> row, ReadOnlySpan<byte> previous, int pixelBytes, Span<byte> target, int start)
    {
        ref var source = ref MemoryMarshal.GetReference(row);
        ref var above = ref MemoryMarshal.GetReference(previous);
        ref var output = ref MemoryMarshal.GetReference(target);
        var i = start;
        for (; i + Step <= row.Length; i += Step)
        {
            var left = Load(ref source, i - pixelBytes);
            var up = Load(ref above, i);
            var corner = Load(ref above, i - pixelBytes);
            var low = Predict(Vector128.WidenLower(left).AsInt16(), Vector128.WidenLower(up).AsInt16(), Vector128.WidenLower(corner).AsInt16());
            var high = Predict(Vector128.WidenUpper(left).AsInt16(), Vector128.WidenUpper(up).AsInt16(), Vector128.WidenUpper(corner).AsInt16());
            Store(ref output, i, Load(ref source, i) - Vector128.Narrow(low.AsUInt16(), high.AsUInt16()));
        }

        return i;
    }

    /// <summary>Adds up how far each byte is from zero when read as a signed value.</summary>
    /// <param name="filtered">The filtered row.</param>
    /// <param name="sum">Receives the sum of the bytes handled.</param>
    /// <returns>The index after the last byte handled.</returns>
    internal static int Cost(ReadOnlySpan<byte> filtered, out long sum)
    {
        ref var source = ref MemoryMarshal.GetReference(filtered);
        var total = Vector128<uint>.Zero;
        var i = 0;
        for (; i + Step <= filtered.Length; i += Step)
        {
            // A byte's distance from zero as a signed value is the smaller of the byte and its negation.
            var bytes = Load(ref source, i);
            var distance = Vector128.Min(bytes, Vector128<byte>.Zero - bytes);
            var pairs = Vector128.WidenLower(distance) + Vector128.WidenUpper(distance);
            total += Vector128.WidenLower(pairs) + Vector128.WidenUpper(pairs);
        }

        sum = Vector128.Sum(total);
        return i;
    }

    /// <summary>Chooses the Paeth predictor for eight bytes without branching.</summary>
    /// <param name="left">The bytes to the left.</param>
    /// <param name="up">The bytes above.</param>
    /// <param name="corner">The bytes above and to the left.</param>
    /// <returns>The predictions, one in each lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<short> Predict(Vector128<short> left, Vector128<short> up, Vector128<short> corner)
    {
        // The distances from the estimate to each neighbour reduce to differences of the neighbours, so no estimate is formed.
        var fromLeft = Vector128.Abs(up - corner);
        var fromUp = Vector128.Abs(left - corner);
        var fromCorner = Vector128.Abs(left + up - corner - corner);
        var leftWins = Vector128.LessThanOrEqual(fromLeft, fromUp) & Vector128.LessThanOrEqual(fromLeft, fromCorner);
        return Vector128.ConditionalSelect(leftWins, left, Vector128.ConditionalSelect(Vector128.LessThanOrEqual(fromUp, fromCorner), up, corner));
    }

    /// <summary>Reads sixteen bytes.</summary>
    /// <param name="start">The start of the row.</param>
    /// <param name="index">The index of the first byte.</param>
    /// <returns>The bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> Load(ref byte start, int index) => Vector128.LoadUnsafe(ref start, (nuint)index);

    /// <summary>Writes sixteen bytes.</summary>
    /// <param name="start">The start of the row.</param>
    /// <param name="index">The index of the first byte.</param>
    /// <param name="value">The bytes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Store(ref byte start, int index, Vector128<byte> value) => value.StoreUnsafe(ref start, (nuint)index);
}
