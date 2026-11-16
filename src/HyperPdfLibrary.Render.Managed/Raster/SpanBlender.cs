// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace HyperPdfLibrary.Render.Managed.Raster;

/// <summary>
/// Blends a run of colour onto a line of pixels through a line of coverage. Pixels are premultiplied, so source over destination is
/// the source plus the destination scaled by what the source leaves uncovered. Long lines are done with vectors; the lanes use the
/// arithmetic of the one pixel code exactly, so both give the same bytes.
/// </summary>
internal static class SpanBlender
{
    /// <summary>The largest byte, which stands for one.</summary>
    private const uint Full = 255;

    /// <summary>The shift that moves alpha to the lowest byte of a pixel.</summary>
    private const int AlphaShift = 24;

    /// <summary>The mask of the blue and red bytes of a pixel, which are scaled together.</summary>
    private const uint EvenBytes = 0x00FF00FF;

    /// <summary>The rounding term for both bytes of a pair.</summary>
    private const uint RoundPair = 0x00800080;

    /// <summary>The bits of one byte.</summary>
    private const int ByteBits = 8;

    /// <summary>Blends one colour onto a line.</summary>
    /// <param name="pixels">The pixels of the run.</param>
    /// <param name="coverage">The coverage of each pixel of the run.</param>
    /// <param name="color">The premultiplied colour.</param>
    internal static void Solid(Span<uint> pixels, ReadOnlySpan<byte> coverage, uint color)
    {
        var opaque = color >> AlphaShift == Full;
        var run = 0;
        for (var at = 0; at < coverage.Length; at += run)
        {
            var value = coverage[at];
            var rest = coverage[at..];
            run = rest.IndexOfAnyExcept(value);
            run = run < 0 ? rest.Length : run;
            if (value == Full && opaque)
            {
                // A covered run of an opaque colour replaces the pixels, which the runtime fills many at a time.
                pixels.Slice(at, run).Fill(color);
            }
            else if (value != 0)
            {
                Over(pixels.Slice(at, run), Scale(color, value));
            }
        }
    }

    /// <summary>Blends a line of colours onto a line.</summary>
    /// <param name="pixels">The pixels of the run.</param>
    /// <param name="coverage">The coverage of each pixel of the run.</param>
    /// <param name="source">The premultiplied colour of each pixel of the run.</param>
    internal static void Shaded(Span<uint> pixels, ReadOnlySpan<byte> coverage, ReadOnlySpan<uint> source)
    {
        var done = Vector128.IsHardwareAccelerated && coverage.Length >= Vector128<uint>.Count ? ShadedVector(pixels, coverage, source) : 0;
        for (var i = done; i < coverage.Length; i++)
        {
            var value = coverage[i];
            if (value == 0)
            {
                continue;
            }

            var color = value == Full ? source[i] : Scale(source[i], value);
            pixels[i] = color >> AlphaShift == Full ? color : color + Scale(pixels[i], Full - (color >> AlphaShift));
        }
    }

    /// <summary>Multiplies every byte of a pixel by a fraction.</summary>
    /// <param name="pixel">The pixel.</param>
    /// <param name="fraction">The fraction, where 255 is one.</param>
    /// <returns>The scaled pixel, rounded to nearest.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint Scale(uint pixel, uint fraction)
    {
        // Two bytes are scaled in one multiply, with the exact division by 255 done as two shifts.
        var even = ((pixel & EvenBytes) * fraction) + RoundPair;
        even = ((even + ((even >> ByteBits) & EvenBytes)) >> ByteBits) & EvenBytes;
        var odd = (((pixel >> ByteBits) & EvenBytes) * fraction) + RoundPair;
        odd = ((odd + ((odd >> ByteBits) & EvenBytes)) >> ByteBits) & EvenBytes;
        return even | (odd << ByteBits);
    }

    /// <summary>Multiplies every byte of four pixels by a fraction each, as the one pixel overload does.</summary>
    /// <param name="pixel">The pixels.</param>
    /// <param name="fraction">The fraction of each pixel, where 255 is one.</param>
    /// <returns>The scaled pixels.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector128<uint> Scale(Vector128<uint> pixel, Vector128<uint> fraction)
    {
        var mask = Vector128.Create(EvenBytes);
        var round = Vector128.Create(RoundPair);
        var even = ((pixel & mask) * fraction) + round;
        even = ((even + (Vector128.ShiftRightLogical(even, ByteBits) & mask)) >> ByteBits) & mask;
        var odd = ((Vector128.ShiftRightLogical(pixel, ByteBits) & mask) * fraction) + round;
        odd = ((odd + (Vector128.ShiftRightLogical(odd, ByteBits) & mask)) >> ByteBits) & mask;
        return even | Vector128.ShiftLeft(odd, ByteBits);
    }

    /// <summary>Multiplies two lines of coverage, as a clip does to a shape.</summary>
    /// <param name="coverage">The shape's coverage, which receives the product.</param>
    /// <param name="mask">The clip's coverage.</param>
    internal static void Multiply(Span<byte> coverage, ReadOnlySpan<byte> mask)
    {
        var done = Vector128.IsHardwareAccelerated && coverage.Length >= Vector128<byte>.Count ? MultiplyVector(coverage, mask) : 0;
        for (var i = done; i < coverage.Length; i++)
        {
            var product = (coverage[i] * (uint)mask[i]) + (RoundPair & Full);
            coverage[i] = (byte)((product + (product >> ByteBits)) >> ByteBits);
        }
    }

    /// <summary>Puts one premultiplied colour over every pixel of a run.</summary>
    /// <param name="pixels">The pixels.</param>
    /// <param name="color">The colour, already scaled by the coverage.</param>
    internal static void Over(Span<uint> pixels, uint color)
    {
        var keep = Full - (color >> AlphaShift);
        var done = 0;
        if (Vector256.IsHardwareAccelerated && pixels.Length >= Vector256<uint>.Count)
        {
            done = OverVector256(pixels, color, keep);
        }
        else if (Vector128.IsHardwareAccelerated && pixels.Length >= Vector128<uint>.Count)
        {
            done = OverVector128(pixels, color, keep);
        }

        for (var i = done; i < pixels.Length; i++)
        {
            pixels[i] = color + Scale(pixels[i], keep);
        }
    }

    /// <summary>
    /// Blends four pixels at a time. A coverage of 0 scales the colour to nothing and leaves the pixel as it was, and a coverage of 255
    /// leaves the colour as it was, so the lanes need none of the branches the one pixel loop takes.
    /// </summary>
    /// <param name="pixels">The pixels.</param>
    /// <param name="coverage">The coverage of each pixel.</param>
    /// <param name="source">The premultiplied colour of each pixel.</param>
    /// <returns>How many pixels were done, which is a whole number of vectors.</returns>
    private static int ShadedVector(Span<uint> pixels, ReadOnlySpan<byte> coverage, ReadOnlySpan<uint> source)
    {
        var full = Vector128.Create(Full);
        var end = coverage.Length - Vector128<uint>.Count;
        var i = 0;
        for (; i <= end; i += Vector128<uint>.Count)
        {
            var word = Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref MemoryMarshal.GetReference(coverage), i));
            var value = Vector128.WidenLower(Vector128.WidenLower(Vector128.CreateScalar(word).AsByte()));
            var color = Scale(Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(source), (nuint)i), value);
            var keep = full - Vector128.ShiftRightLogical(color, AlphaShift);
            var under = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(pixels), (nuint)i);
            (color + Scale(under, keep)).StoreUnsafe(ref MemoryMarshal.GetReference(pixels), (nuint)i);
        }

        return i;
    }

    /// <summary>Multiplies sixteen bytes of coverage at a time, with the arithmetic of the one byte loop.</summary>
    /// <param name="coverage">The shape's coverage, which receives the product.</param>
    /// <param name="mask">The clip's coverage.</param>
    /// <returns>How many bytes were done, which is a whole number of vectors.</returns>
    private static int MultiplyVector(Span<byte> coverage, ReadOnlySpan<byte> mask)
    {
        var round = Vector128.Create((ushort)(RoundPair & Full));
        var end = coverage.Length - Vector128<byte>.Count;
        var i = 0;
        for (; i <= end; i += Vector128<byte>.Count)
        {
            var shape = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(coverage), (nuint)i);
            var clip = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(mask), (nuint)i);
            var low = MultiplyHalf(Vector128.WidenLower(shape), Vector128.WidenLower(clip), round);
            var high = MultiplyHalf(Vector128.WidenUpper(shape), Vector128.WidenUpper(clip), round);
            Vector128.Narrow(low, high).StoreUnsafe(ref MemoryMarshal.GetReference(coverage), (nuint)i);
        }

        return i;
    }

    /// <summary>Multiplies eight pairs of coverage held as 16 bit lanes.</summary>
    /// <param name="shape">The shape's coverage.</param>
    /// <param name="clip">The clip's coverage.</param>
    /// <param name="round">The rounding term in every lane.</param>
    /// <returns>The products, each from 0 to 255.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ushort> MultiplyHalf(Vector128<ushort> shape, Vector128<ushort> clip, Vector128<ushort> round)
    {
        // The largest sum, 255 * 255 + 128 + 254, still fits a 16 bit lane.
        var product = (shape * clip) + round;
        return Vector128.ShiftRightLogical(product + Vector128.ShiftRightLogical(product, ByteBits), ByteBits);
    }

    /// <summary>Puts a colour over pixels eight at a time, with the arithmetic of the one pixel code on every lane.</summary>
    /// <param name="pixels">The pixels.</param>
    /// <param name="color">The colour, already scaled by the coverage.</param>
    /// <param name="keep">What the colour leaves of the pixels under it, where 255 is all.</param>
    /// <returns>How many pixels were done, which is a whole number of vectors.</returns>
    private static int OverVector256(Span<uint> pixels, uint color, uint keep)
    {
        var mask = Vector256.Create(EvenBytes);
        var round = Vector256.Create(RoundPair);
        var factor = Vector256.Create(keep);
        var add = Vector256.Create(color);
        var end = pixels.Length - Vector256<uint>.Count;
        var i = 0;
        for (; i <= end; i += Vector256<uint>.Count)
        {
            var pixel = Vector256.LoadUnsafe(ref MemoryMarshal.GetReference(pixels), (nuint)i);
            var even = ((pixel & mask) * factor) + round;
            even = ((even + (Vector256.ShiftRightLogical(even, ByteBits) & mask)) >> ByteBits) & mask;
            var odd = ((Vector256.ShiftRightLogical(pixel, ByteBits) & mask) * factor) + round;
            odd = ((odd + (Vector256.ShiftRightLogical(odd, ByteBits) & mask)) >> ByteBits) & mask;
            (add + (even | Vector256.ShiftLeft(odd, ByteBits))).StoreUnsafe(ref MemoryMarshal.GetReference(pixels), (nuint)i);
        }

        return i;
    }

    /// <summary>Puts a colour over pixels four at a time, with the arithmetic of the one pixel code on every lane.</summary>
    /// <param name="pixels">The pixels.</param>
    /// <param name="color">The colour, already scaled by the coverage.</param>
    /// <param name="keep">What the colour leaves of the pixels under it, where 255 is all.</param>
    /// <returns>How many pixels were done, which is a whole number of vectors.</returns>
    private static int OverVector128(Span<uint> pixels, uint color, uint keep)
    {
        var factor = Vector128.Create(keep);
        var add = Vector128.Create(color);
        var end = pixels.Length - Vector128<uint>.Count;
        var i = 0;
        for (; i <= end; i += Vector128<uint>.Count)
        {
            var pixel = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(pixels), (nuint)i);
            (add + Scale(pixel, factor)).StoreUnsafe(ref MemoryMarshal.GetReference(pixels), (nuint)i);
        }

        return i;
    }
}
