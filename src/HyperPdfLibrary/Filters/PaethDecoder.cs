// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace HyperPdfLibrary.Filters;

/// <summary>
/// Undoes the PNG Paeth filter. Each byte depends on the one a pixel earlier, so the three and four byte pixels are
/// decoded together in the four lanes of one vector, and the choice of predictor uses no branches, which noisy images
/// would mispredict.
/// </summary>
internal static class PaethDecoder
{
    /// <summary>The bytes in an RGBA pixel.</summary>
    private const int FourBytes = 4;

    /// <summary>The byte count between cancellation checks.</summary>
    private const int ChunkLength = 4096;

    /// <summary>The shift that copies the sign of an <see cref="int"/> into every bit.</summary>
    private const int SignShift = 31;

    /// <summary>Undoes the filter on a row that has a row above it, in place.</summary>
    /// <param name="row">The filtered row, decoded in place.</param>
    /// <param name="previous">The decoded previous row, at least as long as <paramref name="row"/>.</param>
    /// <param name="pixelBytes">The bytes in a pixel.</param>
    /// <param name="cancellationToken">Cancels this operation between chunks.</param>
    internal static void Undo(Span<byte> row, ReadOnlySpan<byte> previous, int pixelBytes, CancellationToken cancellationToken)
    {
        // The first pixel has nothing to its left, so the predictor is the byte above.
        var head = Math.Min(pixelBytes, row.Length);
        ref var current = ref MemoryMarshal.GetReference(row);
        ref var above = ref MemoryMarshal.GetReference(previous);
        for (var i = 0; i < head; i++)
        {
            Unsafe.Add(ref current, i) += Unsafe.Add(ref above, i);
        }

        var vectors = PixelLanes.Handles(pixelBytes);
        for (var start = head; start < row.Length; start += ChunkLength)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var end = Math.Min(start + ChunkLength, row.Length);
            var whole = vectors ? UndoPixels(ref current, ref above, pixelBytes, start, end) : start;
            UndoBytes(ref current, ref above, pixelBytes, whole, end);
        }
    }

    /// <summary>Chooses the Paeth predictor without branching.</summary>
    /// <param name="left">The byte to the left.</param>
    /// <param name="up">The byte above.</param>
    /// <param name="upLeft">The byte above and to the left.</param>
    /// <returns>The prediction.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte Predict(int left, int up, int upLeft)
    {
        // The distances from the estimate to each neighbour reduce to differences of the neighbours, so no estimate is formed.
        var fromLeft = Math.Abs(up - upLeft);
        var fromUp = Math.Abs(left - upLeft);
        var fromUpLeft = Math.Abs(left + up - upLeft - upLeft);

        // Each mask is all ones when its neighbour wins; the left wins ties first, then the one above.
        var leftWins = ~(((fromUp - fromLeft) | (fromUpLeft - fromLeft)) >> SignShift);
        var upOrCorner = upLeft ^ ((up ^ upLeft) & ~((fromUpLeft - fromUp) >> SignShift));
        return (byte)(upOrCorner ^ ((left ^ upOrCorner) & leftWins));
    }

    /// <summary>Undoes pixels of three or four bytes, each in the lanes of one vector.</summary>
    /// <param name="current">The start of the row.</param>
    /// <param name="above">The start of the row above.</param>
    /// <param name="pixelBytes">The bytes in a pixel, three or four.</param>
    /// <param name="start">The first byte, the start of a pixel.</param>
    /// <param name="end">One past the last byte to decode.</param>
    /// <returns>The offset of the first byte not decoded.</returns>
    private static int UndoPixels(ref byte current, ref byte above, int pixelBytes, int start, int end)
    {
        // A three byte pixel is read as four bytes, so the last one needs a byte after it; the fourth lane is never stored.
        var i = start;
        var left = PixelLanes.Widen(ref Unsafe.Add(ref current, i - pixelBytes));
        var corner = PixelLanes.Widen(ref Unsafe.Add(ref above, i - pixelBytes));
        for (; i + FourBytes <= end; i += pixelBytes)
        {
            var up = PixelLanes.Widen(ref Unsafe.Add(ref above, i));
            var fromLeft = Vector128.Abs(up - corner);
            var fromUp = Vector128.Abs(left - corner);
            var fromUpLeft = Vector128.Abs(up + left - corner - corner);
            var leftWins = Vector128.LessThanOrEqual(fromLeft, fromUp) & Vector128.LessThanOrEqual(fromLeft, fromUpLeft);
            var prediction = Vector128.ConditionalSelect(leftWins, left, Vector128.ConditionalSelect(Vector128.LessThanOrEqual(fromUp, fromUpLeft), up, corner));
            left = (PixelLanes.Widen(ref Unsafe.Add(ref current, i)) + prediction) & Vector128.Create((int)byte.MaxValue);
            PixelLanes.Store(ref Unsafe.Add(ref current, i), left, pixelBytes);
            corner = up;
        }

        return i;
    }

    /// <summary>Undoes bytes one at a time, for other pixel sizes and the end of a chunk.</summary>
    /// <param name="current">The start of the row.</param>
    /// <param name="above">The start of the row above.</param>
    /// <param name="pixelBytes">The bytes in a pixel.</param>
    /// <param name="start">The first byte.</param>
    /// <param name="end">One past the last byte.</param>
    private static void UndoBytes(ref byte current, ref byte above, int pixelBytes, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            Unsafe.Add(ref current, i) += Predict(Unsafe.Add(ref current, i - pixelBytes), Unsafe.Add(ref above, i), Unsafe.Add(ref above, i - pixelBytes));
        }
    }
}
