// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace HyperPdfLibrary.Export;

/// <summary>
/// Swaps the first and third sample of each three or four sample pixel, which turns red, green, blue (and alpha) into blue,
/// green, red (and alpha) and back, several pixels at a time with one shuffle.
/// </summary>
internal static class RedBlueSwap
{
    /// <summary>The samples in an RGB pixel.</summary>
    private const int RgbSamples = 3;

    /// <summary>The samples in an RGBA pixel.</summary>
    private const int RgbaSamples = 4;

    /// <summary>The bytes in a vector.</summary>
    private const int VectorBytes = 16;

    /// <summary>The offset of the third sample of a pixel.</summary>
    private const int ThirdIndex = 2;

    /// <summary>The offset of the fourth sample of a pixel.</summary>
    private const int FourthIndex = 3;

    /// <summary>The RGB pixels a vector holds whole, with a byte left over.</summary>
    private const int RgbPixelsPerVector = 5;

    /// <summary>The index that makes a shuffle write zero.</summary>
    private const byte Zeroed = 0x80;

    /// <summary>Swaps whole groups of pixels. For four samples the source and the target may be the same bytes; for three they must differ.</summary>
    /// <param name="source">The pixels to read.</param>
    /// <param name="target">Receives the swapped pixels, at least as long as <paramref name="source"/>.</param>
    /// <param name="samples">The samples in a pixel, three or four.</param>
    /// <returns>The bytes handled, a whole number of pixels; the caller swaps the rest.</returns>
    internal static int Swap(ReadOnlySpan<byte> source, Span<byte> target, int samples)
    {
        if (!Vector128.IsHardwareAccelerated || samples is not (RgbSamples or RgbaSamples))
        {
            return 0;
        }

        // A step of three byte pixels covers fifteen bytes but moves sixteen, so the byte past them is written with zero and
        // written again by the next step or by the caller, as long as the sixteen bytes lie inside both rows.
        var shuffle = samples == RgbaSamples ? Four() : Three();
        var step = samples == RgbaSamples ? VectorBytes : RgbPixelsPerVector * RgbSamples;
        ref var input = ref MemoryMarshal.GetReference(source);
        ref var output = ref MemoryMarshal.GetReference(target);
        var done = 0;
        while (done + VectorBytes <= source.Length && done + VectorBytes <= target.Length)
        {
            Vector128.StoreUnsafe(Vector128.Shuffle(Vector128.LoadUnsafe(ref input, (nuint)done), shuffle), ref output, (nuint)done);
            done += step;
        }

        return done;
    }

    /// <summary>Builds the shuffle for four pixels of four samples.</summary>
    /// <returns>The shuffle indexes.</returns>
    private static Vector128<byte> Four()
    {
        Span<byte> indexes = stackalloc byte[VectorBytes];
        for (var pixel = 0; pixel < VectorBytes; pixel += RgbaSamples)
        {
            indexes[pixel] = (byte)(pixel + ThirdIndex);
            indexes[pixel + 1] = (byte)(pixel + 1);
            indexes[pixel + ThirdIndex] = (byte)pixel;
            indexes[pixel + FourthIndex] = (byte)(pixel + FourthIndex);
        }

        return Vector128.Create(indexes);
    }

    /// <summary>Builds the shuffle for five pixels of three samples.</summary>
    /// <returns>The shuffle indexes.</returns>
    private static Vector128<byte> Three()
    {
        Span<byte> indexes = stackalloc byte[VectorBytes];
        for (var pixel = 0; pixel < RgbPixelsPerVector; pixel++)
        {
            var at = pixel * RgbSamples;
            indexes[at] = (byte)(at + ThirdIndex);
            indexes[at + 1] = (byte)(at + 1);
            indexes[at + ThirdIndex] = (byte)at;
        }

        indexes[VectorBytes - 1] = Zeroed;
        return Vector128.Create(indexes);
    }
}
