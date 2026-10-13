// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>
/// Converts DeviceCMYK to sRGB with the polynomial of <see cref="CmykPolynomial"/>. ISO 32000-2 section 10.4
/// leaves the conversion from DeviceCMYK to the output device to the processor; this one maps zero ink to paper white
/// exactly and uses process-ink colours on coated stock. Rows run on the widest vectors the hardware accelerates, with a
/// scalar tail that gives the same bytes.
/// </summary>
internal static class CmykConverter
{
    /// <summary>The components of a CMYK pixel.</summary>
    private const int CmykComponents = 4;

    /// <summary>The bytes in an RGB pixel.</summary>
    private const int RgbBytes = 3;

    /// <summary>The pixels converted per block when writing RGB bytes.</summary>
    private const int RgbBlockPixels = 64;

    /// <summary>The bytes of a vector shuffle store.</summary>
    private const int ShuffleStoreBytes = 16;

    /// <summary>The pixels packed by one shuffle.</summary>
    private const int ShufflePixels = 4;

    /// <summary>The index of the black component.</summary>
    private const int BlackIndex = 3;

    /// <summary>The index of the green output.</summary>
    private const int GreenIndex = 1;

    /// <summary>The index of the blue output.</summary>
    private const int BlueIndex = 2;

    /// <summary>The shift of the red byte in a BGRA word.</summary>
    private const int RedShift = 16;

    /// <summary>The shift of the green byte in a BGRA word.</summary>
    private const int GreenShift = 8;

    /// <summary>Gets the shuffle that turns four BGRA pixels into 12 RGB bytes.</summary>
    private static Vector128<byte> RgbShuffle =>
        Vector128.Create<byte>([0x02, 0x01, 0x00, 0x06, 0x05, 0x04, 0x0A, 0x09, 0x08, 0x0E, 0x0D, 0x0C, 0x00, 0x00, 0x00, 0x00]);

    /// <summary>Converts a CMYK colour to a little-endian BGRA word.</summary>
    /// <param name="cyan">The cyan byte.</param>
    /// <param name="magenta">The magenta byte.</param>
    /// <param name="yellow">The yellow byte.</param>
    /// <param name="black">The black byte.</param>
    /// <returns>The opaque BGRA word, to be written little-endian.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint ToBgra(byte cyan, byte magenta, byte yellow, byte black) =>
        CmykKernel.Convert<CmykScalarLanes, float, uint>(cyan | ((uint)magenta << GreenShift) | ((uint)yellow << RedShift) | ((uint)black << (RedShift + GreenShift)));

    /// <summary>Converts a CMYK colour given as floats from 0 to 1 to RGB floats from 0 to 1.</summary>
    /// <param name="cyan">The cyan value.</param>
    /// <param name="magenta">The magenta value.</param>
    /// <param name="yellow">The yellow value.</param>
    /// <param name="black">The black value.</param>
    /// <param name="rgb">Receives red, green and blue.</param>
    internal static void ToRgb(float cyan, float magenta, float yellow, float black, Span<float> rgb)
    {
        CmykPolynomial.Evaluate<CmykScalarLanes, float, uint>(Clamp(cyan), Clamp(magenta), Clamp(yellow), Clamp(black), out var red, out var green, out var blue);
        rgb[0] = Clamp(red);
        rgb[GreenIndex] = Clamp(green);
        rgb[BlueIndex] = Clamp(blue);
    }

    /// <summary>Converts a row of CMYK pixels to opaque BGRA with the widest accelerated vectors.</summary>
    /// <param name="cmyk">Cyan, magenta, yellow and black bytes per pixel.</param>
    /// <param name="bgra">Four bytes per pixel.</param>
    /// <param name="count">The pixel count.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ToBgraRow(ReadOnlySpan<byte> cmyk, Span<byte> bgra, int count) => ToBgraRow(cmyk, bgra, count, CmykLaneWidth.Widest);

    /// <summary>Converts a row of CMYK pixels to opaque BGRA with one lane width; a width the hardware does not accelerate runs scalar.</summary>
    /// <param name="cmyk">Cyan, magenta, yellow and black bytes per pixel.</param>
    /// <param name="bgra">Four bytes per pixel.</param>
    /// <param name="count">The pixel count.</param>
    /// <param name="width">The lane width.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ToBgraRow(ReadOnlySpan<byte> cmyk, Span<byte> bgra, int count, CmykLaneWidth width)
    {
        var length = count * CmykComponents;
        ref var source = ref MemoryMarshal.GetReference(cmyk[..length]);
        ref var destination = ref MemoryMarshal.GetReference(bgra[..length]);
        var done = BitConverter.IsLittleEndian ? ConvertVectors(ref source, ref destination, count, width) : 0;
        _ = CmykKernel.ConvertBlocks<CmykScalarLanes, float, uint>(ref source, ref destination, done, count);
    }

    /// <summary>Converts a row of CMYK pixels to RGB bytes with the widest accelerated vectors.</summary>
    /// <param name="cmyk">Cyan, magenta, yellow and black bytes per pixel.</param>
    /// <param name="rgb">Red, green and blue bytes per pixel.</param>
    /// <param name="count">The pixel count.</param>
    internal static void ToRgbRow(ReadOnlySpan<byte> cmyk, Span<byte> rgb, int count)
    {
        var source = cmyk[..(count * CmykComponents)];
        var target = rgb[..(count * RgbBytes)];
        Span<byte> block = stackalloc byte[RgbBlockPixels * CmykKernel.BytesPerPixel];
        for (var start = 0; start < count; start += RgbBlockPixels)
        {
            var pixels = Math.Min(RgbBlockPixels, count - start);
            ToBgraRow(source[(start * CmykComponents)..], block, pixels);
            BgraToRgb(block, target[(start * RgbBytes)..], pixels);
        }
    }

    /// <summary>Converts the whole vectors of a row, widest first.</summary>
    /// <param name="source">The first CMYK byte.</param>
    /// <param name="destination">The first BGRA byte.</param>
    /// <param name="count">The pixel count.</param>
    /// <param name="width">The lane width.</param>
    /// <returns>The index of the first pixel that was not converted.</returns>
    private static int ConvertVectors(ref byte source, ref byte destination, int count, CmykLaneWidth width)
    {
        var done = 0;
        if (width is CmykLaneWidth.Widest or CmykLaneWidth.Vector512 && Vector512.IsHardwareAccelerated)
        {
            done = CmykKernel.ConvertBlocks<CmykVector512Lanes, Vector512<float>, Vector512<uint>>(ref source, ref destination, done, count);
        }

        if (width is CmykLaneWidth.Widest or CmykLaneWidth.Vector256 && Vector256.IsHardwareAccelerated)
        {
            done = CmykKernel.ConvertBlocks<CmykVector256Lanes, Vector256<float>, Vector256<uint>>(ref source, ref destination, done, count);
        }

        if (width is CmykLaneWidth.Widest or CmykLaneWidth.Vector128 && Vector128.IsHardwareAccelerated)
        {
            done = CmykKernel.ConvertBlocks<CmykVector128Lanes, Vector128<float>, Vector128<uint>>(ref source, ref destination, done, count);
        }

        return done;
    }

    /// <summary>Drops the alpha byte of BGRA pixels and swaps blue and red.</summary>
    /// <param name="bgra">Four bytes per pixel.</param>
    /// <param name="rgb">Three bytes per pixel.</param>
    /// <param name="count">The pixel count.</param>
    private static void BgraToRgb(ReadOnlySpan<byte> bgra, Span<byte> rgb, int count)
    {
        var i = 0;
        if (Vector128.IsHardwareAccelerated)
        {
            ref var source = ref MemoryMarshal.GetReference(bgra);
            ref var target = ref MemoryMarshal.GetReference(rgb);

            // Each store writes 16 bytes but only 12 are pixels; the next store overwrites the rest, so stop while a whole store fits.
            for (; i <= count - ShufflePixels && (i * RgbBytes) + ShuffleStoreBytes <= rgb.Length; i += ShufflePixels)
            {
                var pixels = Vector128.LoadUnsafe(ref source, (nuint)(i * CmykKernel.BytesPerPixel));
                Vector128.Shuffle(pixels, RgbShuffle).StoreUnsafe(ref target, (nuint)(i * RgbBytes));
            }
        }

        for (; i < count; i++)
        {
            var pixel = bgra.Slice(i * CmykKernel.BytesPerPixel, RgbBytes);
            var offset = i * RgbBytes;
            rgb[offset] = pixel[BlueIndex];
            rgb[offset + GreenIndex] = pixel[GreenIndex];
            rgb[offset + BlueIndex] = pixel[0];
        }
    }

    /// <summary>Clamps a value to 0..1; NaN becomes zero.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The clamped value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Clamp(float value)
    {
        if (!(value > 0))
        {
            return 0;
        }

        return value >= 1 ? 1 : value;
    }
}
