// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace HyperPdfLibrary.Filters;

/// <summary>
/// Undoes row filters that depend on the pixel to the left by keeping the three or four bytes of a pixel in the lanes of
/// one vector, so a pixel costs one chain of vector steps instead of three or four chains of byte steps.
/// </summary>
internal static class PixelLanes
{
    /// <summary>The offset of the third byte of a pixel.</summary>
    private const int ThirdByte = 2;

    /// <summary>The shift that brings the third byte of four packed bytes down.</summary>
    private const int ThirdByteShift = 16;

    /// <summary>The bytes in an RGBA pixel.</summary>
    private const int FourBytes = 4;

    /// <summary>The bytes in an RGB pixel.</summary>
    private const int ThreeBytes = 3;

    /// <summary>Gets a value indicating whether pixels of a size can be undone in lanes on this machine.</summary>
    /// <param name="pixelBytes">The bytes in a pixel.</param>
    /// <returns><see langword="true"/> for three and four byte pixels when vectors are accelerated.</returns>
    internal static bool Handles(int pixelBytes) => Vector128.IsHardwareAccelerated && pixelBytes is ThreeBytes or FourBytes;

    /// <summary>Reads four bytes as four integers.</summary>
    /// <param name="source">The first byte.</param>
    /// <returns>The bytes, one in each lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector128<int> Widen(ref byte source) =>
        Vector128.WidenLower(Vector128.WidenLower(Vector128.CreateScalar(Unsafe.ReadUnaligned<uint>(ref source)).AsByte())).AsInt32();

    /// <summary>Writes the low byte of the first three or four lanes.</summary>
    /// <param name="target">The first byte to write.</param>
    /// <param name="lanes">The values, each below 256.</param>
    /// <param name="pixelBytes">The bytes to write, three or four.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Store(ref byte target, Vector128<int> lanes, int pixelBytes)
    {
        var words = Vector128.Narrow(lanes.AsUInt32(), lanes.AsUInt32());
        var packed = Vector128.Narrow(words, words).AsUInt32().ToScalar();
        if (pixelBytes == FourBytes)
        {
            Unsafe.WriteUnaligned(ref target, packed);
            return;
        }

        Unsafe.WriteUnaligned(ref target, (ushort)packed);
        Unsafe.Add(ref target, ThirdByte) = (byte)(packed >> ThirdByteShift);
    }

    /// <summary>Adds the pixel to the left to each pixel, in place.</summary>
    /// <param name="current">The start of the row.</param>
    /// <param name="pixelBytes">The bytes in a pixel, three or four.</param>
    /// <param name="start">The first byte, the start of a pixel after the first.</param>
    /// <param name="end">One past the last byte to decode.</param>
    /// <returns>The offset of the first byte not decoded.</returns>
    internal static int UndoSub(ref byte current, int pixelBytes, int start, int end)
    {
        // A three byte pixel is read as four bytes, so the last one needs a byte after it; the fourth lane is never stored.
        var i = start;
        var left = Widen(ref Unsafe.Add(ref current, i - pixelBytes));
        var mask = Vector128.Create((int)byte.MaxValue);
        for (; i + FourBytes <= end; i += pixelBytes)
        {
            left = (Widen(ref Unsafe.Add(ref current, i)) + left) & mask;
            Store(ref Unsafe.Add(ref current, i), left, pixelBytes);
        }

        return i;
    }

    /// <summary>Adds the average of the pixel to the left and the pixel above to each pixel, in place.</summary>
    /// <param name="current">The start of the row.</param>
    /// <param name="above">The start of the row above.</param>
    /// <param name="pixelBytes">The bytes in a pixel, three or four.</param>
    /// <param name="start">The first byte, the start of a pixel after the first.</param>
    /// <param name="end">One past the last byte to decode.</param>
    /// <returns>The offset of the first byte not decoded.</returns>
    internal static int UndoAverage(ref byte current, ref byte above, int pixelBytes, int start, int end)
    {
        var i = start;
        var left = Widen(ref Unsafe.Add(ref current, i - pixelBytes));
        var mask = Vector128.Create((int)byte.MaxValue);
        for (; i + FourBytes <= end; i += pixelBytes)
        {
            var average = Vector128.ShiftRightLogical(left + Widen(ref Unsafe.Add(ref above, i)), 1);
            left = (Widen(ref Unsafe.Add(ref current, i)) + average) & mask;
            Store(ref Unsafe.Add(ref current, i), left, pixelBytes);
        }

        return i;
    }
}
