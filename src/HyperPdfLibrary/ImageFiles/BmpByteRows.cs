// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>
/// Converts the rows of 24-bit pixels, and of 32-bit pixels whose colours each fill a whole byte, by moving bytes with one
/// shuffle for several pixels at a time. Rows of other layouts, and the pixels left at the end of a row, are converted one
/// pixel at a time by the caller.
/// </summary>
/// <param name="Shuffle">Where each output byte comes from in sixteen input bytes.</param>
/// <param name="InputBytes">The input bytes the shuffle reads for each step, sixteen.</param>
/// <param name="StepBytes">The input and output bytes one step finishes: fifteen for 24-bit pixels, sixteen for 32-bit pixels.</param>
/// <param name="StepPixels">The pixels one step finishes.</param>
/// <param name="IsSupported">Whether this layout can be shuffled and the machine has vectors.</param>
internal readonly record struct BmpByteRows(Vector128<byte> Shuffle, int InputBytes, int StepBytes, int StepPixels, bool IsSupported)
{
    /// <summary>The bytes in an RGB pixel.</summary>
    private const int RgbBytes = 3;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>The bytes in a vector.</summary>
    private const int VectorBytes = 16;

    /// <summary>The pixels of three bytes in sixteen bytes.</summary>
    private const int RgbPixelsPerStep = 5;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The index that makes a shuffle write zero.</summary>
    private const byte Zeroed = 0x80;

    /// <summary>The offset of the green sample in a pixel.</summary>
    private const int GreenIndex = 1;

    /// <summary>The offset of the red sample in a pixel.</summary>
    private const int RedIndex = 2;

    /// <summary>The offset of the alpha sample in a BGRA pixel.</summary>
    private const int AlphaIndex = 3;

    /// <summary>Works out how to convert the rows of a bitmap.</summary>
    /// <param name="header">The header.</param>
    /// <param name="hasAlpha">Whether the output has an alpha sample, so is blue, green, red and alpha; otherwise it is red, green and blue.</param>
    /// <returns>The converter; <see cref="IsSupported"/> is false when rows must be converted pixel by pixel.</returns>
    internal static BmpByteRows For(in BmpHeader header, bool hasAlpha)
    {
        if (!Vector128.IsHardwareAccelerated)
        {
            return default;
        }

        if (header.BitsPerPixel == RgbBytes * ByteBits && !hasAlpha)
        {
            return ForRgb();
        }

        return header.BitsPerPixel == BgraBytes * ByteBits && hasAlpha ? ForBgra(header.Masks) : default;
    }

    /// <summary>Converts as many whole steps of a row as fit.</summary>
    /// <param name="source">The stored row.</param>
    /// <param name="target">Receives the converted pixels of the row.</param>
    /// <param name="width">The pixels in the row.</param>
    /// <returns>The pixels converted, which the caller continues after.</returns>
    internal int Convert(ReadOnlySpan<byte> source, Span<byte> target, int width)
    {
        if (!IsSupported)
        {
            return 0;
        }

        // A step reads sixteen bytes and writes sixteen, so the last step must have that much of the row left on both sides.
        var outputBytes = target.Length;
        var inputBytes = Math.Min(source.Length, width * (StepBytes / StepPixels));
        ref var input = ref MemoryMarshal.GetReference(source);
        ref var output = ref MemoryMarshal.GetReference(target);
        var offset = 0;
        while (offset + InputBytes <= inputBytes && offset + VectorBytes <= outputBytes)
        {
            Vector128.StoreUnsafe(Vector128.Shuffle(Vector128.LoadUnsafe(ref input, (nuint)offset), Shuffle), ref output, (nuint)offset);
            offset += StepBytes;
        }

        return offset / StepBytes * StepPixels;
    }

    /// <summary>Works out the shuffle for 32-bit pixels whose colours each fill a whole byte.</summary>
    /// <param name="masks">The colour masks.</param>
    /// <returns>The converter, or an unsupported one when a colour does not fill exactly one byte.</returns>
    private static BmpByteRows ForBgra(BmpMasks masks)
    {
        var blue = ByteOf(masks.Blue);
        var green = ByteOf(masks.Green);
        var red = ByteOf(masks.Red);
        var alpha = ByteOf(masks.Alpha);
        if (blue < 0 || green < 0 || red < 0 || alpha < 0)
        {
            return default;
        }

        Span<byte> indexes = stackalloc byte[VectorBytes];
        for (var pixel = 0; pixel < VectorBytes; pixel += BgraBytes)
        {
            indexes[pixel] = (byte)(pixel + blue);
            indexes[pixel + GreenIndex] = (byte)(pixel + green);
            indexes[pixel + RedIndex] = (byte)(pixel + red);
            indexes[pixel + AlphaIndex] = (byte)(pixel + alpha);
        }

        return new(Vector128.Create(indexes), VectorBytes, VectorBytes, VectorBytes / BgraBytes, true);
    }

    /// <summary>Finds which byte of a little-endian 32-bit pixel a mask covers when it covers exactly one whole byte.</summary>
    /// <param name="mask">The mask.</param>
    /// <returns>The byte, 0 to 3, or -1.</returns>
    private static int ByteOf(uint mask)
    {
        var shift = mask == 0 ? 0 : BitOperations.TrailingZeroCount(mask);
        return mask != 0 && shift % ByteBits == 0 && (mask >> shift) == byte.MaxValue ? shift / ByteBits : -1;
    }

    /// <summary>Works out the shuffle that swaps blue and red in five 24-bit pixels.</summary>
    /// <returns>The converter.</returns>
    private static BmpByteRows ForRgb()
    {
        Span<byte> indexes = stackalloc byte[VectorBytes];
        for (var pixel = 0; pixel < RgbPixelsPerStep; pixel++)
        {
            var at = pixel * RgbBytes;
            indexes[at] = (byte)(at + RedIndex);
            indexes[at + GreenIndex] = (byte)(at + GreenIndex);
            indexes[at + RedIndex] = (byte)at;
        }

        indexes[VectorBytes - 1] = Zeroed;
        return new(Vector128.Create(indexes), VectorBytes, RgbBytes * RgbPixelsPerStep, RgbPixelsPerStep, true);
    }
}
