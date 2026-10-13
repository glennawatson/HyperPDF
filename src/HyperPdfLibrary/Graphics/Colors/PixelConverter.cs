// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>
/// Converts rows of 8-bit device samples to opaque BGRA. Gray and RGB use <see cref="Vector128"/> byte shuffles with
/// a scalar tail that computes exactly the same bytes; other spaces use a 256-entry lookup table.
/// </summary>
internal static class PixelConverter
{
    /// <summary>The bytes in one BGRA pixel.</summary>
    internal const int BytesPerPixel = 4;

    /// <summary>The number of entries in a lookup table indexed by one byte.</summary>
    internal const int LookupSize = 256;

    /// <summary>The largest byte value.</summary>
    internal const int MaxByte = 255;

    /// <summary>The components of an RGB sample.</summary>
    private const int RgbComponents = 3;

    /// <summary>The bytes in one vector.</summary>
    private const int VectorBytes = 16;

    /// <summary>The BGRA pixels in one vector.</summary>
    private const int PixelsPerVector = VectorBytes / BytesPerPixel;

    /// <summary>The opaque alpha byte in a little-endian BGRA word.</summary>
    private const uint OpaqueAlpha = 0xFF00_0000;

    /// <summary>Copies a gray byte into the blue, green and red bytes of a little-endian BGRA word.</summary>
    private const uint GraySpread = 0x0001_0101;

    /// <summary>The shift of the green byte in a little-endian BGRA word.</summary>
    private const int GreenShift = 8;

    /// <summary>The shift of the red byte in a little-endian BGRA word.</summary>
    private const int RedShift = 16;

    /// <summary>The bias that makes a float to byte conversion round to nearest.</summary>
    private const float RoundHalf = 0.5F;

    /// <summary>The offset of the second quarter of the gray shuffle table.</summary>
    private const int SecondQuarter = VectorBytes;

    /// <summary>The offset of the third quarter of the gray shuffle table.</summary>
    private const int ThirdQuarter = 2 * VectorBytes;

    /// <summary>The offset of the last quarter of the gray shuffle table.</summary>
    private const int LastQuarter = 3 * VectorBytes;

    /// <summary>Spreads 4 gray bytes over 4 BGRA pixels; 0x80 lanes become zero and receive alpha.</summary>
    private static readonly Vector128<byte> GrayMask0 = Vector128.Create(GrayShuffle[..VectorBytes]);

    /// <summary>Spreads gray bytes 4 to 7.</summary>
    private static readonly Vector128<byte> GrayMask1 = Vector128.Create(GrayShuffle.Slice(SecondQuarter, VectorBytes));

    /// <summary>Spreads gray bytes 8 to 11.</summary>
    private static readonly Vector128<byte> GrayMask2 = Vector128.Create(GrayShuffle.Slice(ThirdQuarter, VectorBytes));

    /// <summary>Spreads gray bytes 12 to 15.</summary>
    private static readonly Vector128<byte> GrayMask3 = Vector128.Create(GrayShuffle.Slice(LastQuarter, VectorBytes));

    /// <summary>Turns 4 RGB pixels into BGR order with an empty alpha lane.</summary>
    private static readonly Vector128<byte> RgbMask = Vector128.Create(RgbShuffle);

    /// <summary>The alpha lanes of 4 BGRA pixels.</summary>
    private static readonly Vector128<byte> AlphaLanes = Vector128.Create(OpaqueAlpha).AsByte();

    /// <summary>Gets the gray shuffle indices for 16 pixels.</summary>
    private static ReadOnlySpan<byte> GrayShuffle =>
    [
        0x00, 0x00, 0x00, 0x80, 0x01, 0x01, 0x01, 0x80, 0x02, 0x02, 0x02, 0x80, 0x03, 0x03, 0x03, 0x80,
        0x04, 0x04, 0x04, 0x80, 0x05, 0x05, 0x05, 0x80, 0x06, 0x06, 0x06, 0x80, 0x07, 0x07, 0x07, 0x80,
        0x08, 0x08, 0x08, 0x80, 0x09, 0x09, 0x09, 0x80, 0x0A, 0x0A, 0x0A, 0x80, 0x0B, 0x0B, 0x0B, 0x80,
        0x0C, 0x0C, 0x0C, 0x80, 0x0D, 0x0D, 0x0D, 0x80, 0x0E, 0x0E, 0x0E, 0x80, 0x0F, 0x0F, 0x0F, 0x80,
    ];

    /// <summary>Gets the RGB to BGRA shuffle indices.</summary>
    private static ReadOnlySpan<byte> RgbShuffle =>
        [0x02, 0x01, 0x00, 0x80, 0x05, 0x04, 0x03, 0x80, 0x08, 0x07, 0x06, 0x80, 0x0B, 0x0A, 0x09, 0x80];

    /// <summary>Converts gray samples to BGRA.</summary>
    /// <param name="gray">One byte per pixel.</param>
    /// <param name="bgra">Four bytes per pixel.</param>
    /// <param name="count">The pixel count.</param>
    internal static void GrayToBgra(ReadOnlySpan<byte> gray, Span<byte> bgra, int count)
    {
        var i = 0;
        if (Vector128.IsHardwareAccelerated)
        {
            ref var source = ref MemoryMarshal.GetReference(gray);
            ref var target = ref MemoryMarshal.GetReference(bgra);
            for (; i <= count - VectorBytes; i += VectorBytes)
            {
                var samples = Vector128.LoadUnsafe(ref source, (nuint)i);
                var offset = (nuint)i * BytesPerPixel;
                (Vector128.Shuffle(samples, GrayMask0) | AlphaLanes).StoreUnsafe(ref target, offset);
                (Vector128.Shuffle(samples, GrayMask1) | AlphaLanes).StoreUnsafe(ref target, offset + SecondQuarter);
                (Vector128.Shuffle(samples, GrayMask2) | AlphaLanes).StoreUnsafe(ref target, offset + ThirdQuarter);
                (Vector128.Shuffle(samples, GrayMask3) | AlphaLanes).StoreUnsafe(ref target, offset + LastQuarter);
            }
        }

        for (; i < count; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bgra[(i * BytesPerPixel)..], (gray[i] * GraySpread) | OpaqueAlpha);
        }
    }

    /// <summary>Converts RGB samples to BGRA.</summary>
    /// <param name="rgb">Three bytes per pixel.</param>
    /// <param name="bgra">Four bytes per pixel.</param>
    /// <param name="count">The pixel count.</param>
    internal static void RgbToBgra(ReadOnlySpan<byte> rgb, Span<byte> bgra, int count)
    {
        var i = 0;
        if (Vector128.IsHardwareAccelerated)
        {
            ref var source = ref MemoryMarshal.GetReference(rgb);
            ref var target = ref MemoryMarshal.GetReference(bgra);

            // Each load reads 16 bytes but uses 12, so stop while a full load still fits.
            for (; i <= count - PixelsPerVector && (i * RgbComponents) + VectorBytes <= rgb.Length; i += PixelsPerVector)
            {
                var samples = Vector128.LoadUnsafe(ref source, (nuint)(i * RgbComponents));
                (Vector128.Shuffle(samples, RgbMask) | AlphaLanes).StoreUnsafe(ref target, (nuint)(i * BytesPerPixel));
            }
        }

        for (; i < count; i++)
        {
            var pixel = rgb.Slice(i * RgbComponents, RgbComponents);
            BinaryPrimitives.WriteUInt32LittleEndian(bgra[(i * BytesPerPixel)..], Pack(pixel[0], pixel[1], pixel[2]));
        }
    }

    /// <summary>Converts one-byte samples through a lookup table of BGRA words.</summary>
    /// <param name="samples">One byte per pixel.</param>
    /// <param name="bgra">Four bytes per pixel.</param>
    /// <param name="count">The pixel count.</param>
    /// <param name="lookup">The 256 native-endian BGRA words.</param>
    internal static void LookupToBgra(ReadOnlySpan<byte> samples, Span<byte> bgra, int count, uint[] lookup)
    {
        var words = MemoryMarshal.Cast<byte, uint>(bgra[..(count * BytesPerPixel)]);
        var table = lookup.AsSpan(0, LookupSize);
        for (var i = 0; i < words.Length; i++)
        {
            words[i] = table[samples[i]];
        }
    }

    /// <summary>Packs a colour into a little-endian BGRA word with opaque alpha.</summary>
    /// <param name="red">The red byte.</param>
    /// <param name="green">The green byte.</param>
    /// <param name="blue">The blue byte.</param>
    /// <returns>The word, to be written little-endian.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint Pack(byte red, byte green, byte blue) => blue | ((uint)green << GreenShift) | ((uint)red << RedShift) | OpaqueAlpha;

    /// <summary>Packs a colour into a BGRA word in native byte order, for lookup tables written through a word span.</summary>
    /// <param name="rgb">The red, green and blue values from 0 to 1.</param>
    /// <returns>The word.</returns>
    internal static uint PackNative(ReadOnlySpan<float> rgb)
    {
        var word = Pack(ToByte(rgb[0]), ToByte(rgb[1]), ToByte(rgb[2]));
        return BitConverter.IsLittleEndian ? word : BinaryPrimitives.ReverseEndianness(word);
    }

    /// <summary>Converts a value from 0 to 1 to a byte, rounding; NaN becomes zero.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The byte.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte ToByte(float value)
    {
        var scaled = (value * MaxByte) + RoundHalf;
        if (scaled >= MaxByte)
        {
            return MaxByte;
        }

        return scaled >= 1 ? (byte)scaled : (byte)0;
    }
}
