// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace HyperPdfLibrary.Export;

/// <summary>Converts rows of BGRA pixels to the RGB and RGBA samples PNG stores, four pixels at a time with byte shuffles.</summary>
internal static class PngPixels
{
    /// <summary>The bytes of a BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>The bytes of an RGB pixel.</summary>
    private const int RgbBytes = 3;

    /// <summary>The index of the alpha byte in a pixel.</summary>
    private const int AlphaIndex = 3;

    /// <summary>The bytes of a 128-bit vector.</summary>
    private const int VectorBytes = 16;

    /// <summary>The pixels converted by one vector step.</summary>
    private const int VectorPixels = 4;

    /// <summary>Determines whether every pixel of a BGRA row or image is fully opaque.</summary>
    /// <param name="bgra">The BGRA pixels.</param>
    /// <returns><see langword="true"/> when no pixel has transparency.</returns>
    internal static bool IsOpaque(ReadOnlySpan<byte> bgra)
    {
        var i = 0;
        if (Vector128.IsHardwareAccelerated)
        {
            // The alpha byte of each of the four pixels in a vector must be all ones.
            var alpha = Vector128.Create(0xFF000000FF000000UL).AsByte();
            ref var source = ref MemoryMarshal.GetReference(bgra);
            for (; i + VectorBytes <= bgra.Length; i += VectorBytes)
            {
                if ((Vector128.LoadUnsafe(ref source, (nuint)i) & alpha) != alpha)
                {
                    return false;
                }
            }
        }

        for (var j = i + AlphaIndex; j < bgra.Length; j += BgraBytes)
        {
            if (bgra[j] != byte.MaxValue)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Converts opaque BGRA pixels to RGB four at a time. Each step stores sixteen bytes, of which twelve are new, and the
    /// next step overwrites the rest, so the last few pixels are left to the caller.
    /// </summary>
    /// <param name="bgra">The pixels of a row.</param>
    /// <param name="samples">Receives the RGB samples of the row.</param>
    /// <param name="pixels">The pixels in the row.</param>
    /// <returns>The pixels converted; the caller converts the rest.</returns>
    internal static int ToRgb(ReadOnlySpan<byte> bgra, Span<byte> samples, int pixels)
    {
        if (!Vector128.IsHardwareAccelerated)
        {
            return 0;
        }

        // Source bytes 2 1 0 6 5 4 10 9 8 14 13 12, then four unused lanes of 0x80.
        var shuffle = Vector128.Create(0x090A040506000102UL, 0x808080800C0D0E08UL).AsByte();
        ref var source = ref MemoryMarshal.GetReference(bgra);
        ref var destination = ref MemoryMarshal.GetReference(samples);
        var x = 0;
        for (; (x * RgbBytes) + VectorBytes <= pixels * RgbBytes; x += VectorPixels)
        {
            Vector128.Shuffle(Vector128.LoadUnsafe(ref source, (nuint)(x * BgraBytes)), shuffle).StoreUnsafe(ref destination, (nuint)(x * RgbBytes));
        }

        return x;
    }

    /// <summary>Converts opaque BGRA pixels to RGBA four at a time, swapping blue and red.</summary>
    /// <param name="bgra">The pixels of a row.</param>
    /// <param name="samples">Receives the RGBA samples of the row.</param>
    /// <param name="pixels">The pixels in the row.</param>
    /// <returns>The pixels converted; the caller converts the rest.</returns>
    internal static int ToRgba(ReadOnlySpan<byte> bgra, Span<byte> samples, int pixels)
    {
        if (!Vector128.IsHardwareAccelerated)
        {
            return 0;
        }

        // Source bytes 2 1 0 3 6 5 4 7 10 9 8 11 14 13 12 15.
        var shuffle = Vector128.Create(0x0704050603000102UL, 0x0F0C0D0E0B08090AUL).AsByte();
        ref var source = ref MemoryMarshal.GetReference(bgra);
        ref var destination = ref MemoryMarshal.GetReference(samples);
        var x = 0;
        for (; x + VectorPixels <= pixels; x += VectorPixels)
        {
            Vector128.Shuffle(Vector128.LoadUnsafe(ref source, (nuint)(x * BgraBytes)), shuffle).StoreUnsafe(ref destination, (nuint)(x * BgraBytes));
        }

        return x;
    }
}
