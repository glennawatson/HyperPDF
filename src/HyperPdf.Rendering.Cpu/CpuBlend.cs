// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Rendering.Cpu;

/// <summary>Blends premultiplied BGRA pixels over a destination, four pixels at a time where the processor has 128 bit vectors.</summary>
public static class CpuBlend
{
    /// <summary>The bytes per pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The bytes one vector step blends.</summary>
    private const int VectorBytes = 16;

    /// <summary>The offset of the alpha byte in a pixel.</summary>
    private const int AlphaOffset = 3;

    /// <summary>The largest channel value.</summary>
    private const int ChannelMax = 255;

    /// <summary>Half of 256, added before the divide by 255 so it rounds to nearest.</summary>
    private const int RoundingBias = 128;

    /// <summary>The number of weight steps when two rows are mixed.</summary>
    private const uint WeightSteps = 256;

    /// <summary>The shift that divides by 256.</summary>
    private const int ByteShift = 8;

    /// <summary>The shift of the alpha channel in a pixel read as a little endian integer.</summary>
    private const int AlphaShift = 24;

    /// <summary>The alpha bits of a pixel read as a little endian integer.</summary>
    private const uint AlphaMask = 0xFF000000;

    /// <summary>Gets the shuffle that copies each pixel's alpha byte over its four channels.</summary>
    private static Vector128<byte> AlphaShuffle => Vector128.Create(AlphaShuffleBytes);

    /// <summary>Gets the byte indexes of the alpha shuffle: the alpha byte of each of the four pixels, four times.</summary>
    private static ReadOnlySpan<byte> AlphaShuffleBytes =>
    [
        0x03, 0x03, 0x03, 0x03, 0x07, 0x07, 0x07, 0x07, 0x0B, 0x0B, 0x0B, 0x0B, 0x0F, 0x0F, 0x0F, 0x0F
    ];

    /// <summary>Tells whether every pixel of a row is fully opaque, so the row can be copied as it is.</summary>
    /// <param name="row">The BGRA row.</param>
    /// <returns><see langword="true"/> when every alpha byte is 255.</returns>
    public static bool IsOpaque(ReadOnlySpan<byte> row)
    {
        var pixels = MemoryMarshal.Cast<byte, uint>(row);
        var i = 0;
        if (Vector128.IsHardwareAccelerated && pixels.Length >= Vector128<uint>.Count)
        {
            var all = Vector128<uint>.AllBitsSet;
            ref var first = ref MemoryMarshal.GetReference(pixels);
            for (; i <= pixels.Length - Vector128<uint>.Count; i += Vector128<uint>.Count)
            {
                all &= Vector128.LoadUnsafe(ref first, (nuint)i);
            }

            if ((all & Vector128.Create(AlphaMask)) != Vector128.Create(AlphaMask))
            {
                return false;
            }
        }

        var rest = AlphaMask;
        for (; i < pixels.Length; i++)
        {
            rest &= pixels[i];
        }

        return rest == AlphaMask;
    }

    /// <summary>Blends a premultiplied source row over a destination row of the same length.</summary>
    /// <param name="to">The destination row.</param>
    /// <param name="from">The source row.</param>
    public static void BlendRow(Span<byte> to, ReadOnlySpan<byte> from)
    {
        var i = 0;
        if (Vector128.IsHardwareAccelerated)
        {
            ref var source = ref MemoryMarshal.GetReference(from);
            ref var target = ref MemoryMarshal.GetReference(to);
            for (; i <= from.Length - VectorBytes; i += VectorBytes)
            {
                var pixels = Vector128.LoadUnsafe(ref source, (nuint)i);
                var inverse = ~Vector128.Shuffle(pixels, AlphaShuffle);
                Over(pixels, inverse, Vector128.LoadUnsafe(ref target, (nuint)i)).StoreUnsafe(ref target, (nuint)i);
            }
        }

        for (; i < from.Length; i += BytesPerPixel)
        {
            var inverse = ChannelMax - from[i + AlphaOffset];
            for (var c = 0; c < BytesPerPixel; c++)
            {
                to[i + c] = (byte)Math.Min(ChannelMax, from[i + c] + PremultipliedColor.DivideBy255(to[i + c] * inverse));
            }
        }
    }

    /// <summary>Blends one premultiplied colour over every pixel of a destination row.</summary>
    /// <param name="to">The destination row.</param>
    /// <param name="pixel">The premultiplied colour as a little endian BGRA integer.</param>
    public static void BlendColor(Span<byte> to, uint pixel)
    {
        var inverse = ChannelMax - (int)(pixel >> AlphaShift);
        var i = 0;
        if (Vector128.IsHardwareAccelerated)
        {
            var source = Vector128.Create(pixel).AsByte();
            var inverses = Vector128.Create((byte)inverse);
            ref var target = ref MemoryMarshal.GetReference(to);
            for (; i <= to.Length - VectorBytes; i += VectorBytes)
            {
                Over(source, inverses, Vector128.LoadUnsafe(ref target, (nuint)i)).StoreUnsafe(ref target, (nuint)i);
            }
        }

        var targets = MemoryMarshal.Cast<byte, uint>(to[i..]);
        for (var p = 0; p < targets.Length; p++)
        {
            targets[p] = OverPixel(pixel, targets[p], inverse);
        }
    }

    /// <summary>Mixes two rows of the same length into a destination row by a weight out of 256.</summary>
    /// <param name="to">The destination row.</param>
    /// <param name="upper">The row the weight moves away from.</param>
    /// <param name="lower">The row the weight moves toward.</param>
    /// <param name="weight">The weight of the lower row, from 0 to 255.</param>
    public static void MixRows(Span<byte> to, ReadOnlySpan<byte> upper, ReadOnlySpan<byte> lower, uint weight)
    {
        if (weight == 0)
        {
            upper[..to.Length].CopyTo(to);
            return;
        }

        var i = 0;
        if (Vector128.IsHardwareAccelerated)
        {
            var towardLower = Vector128.Create((ushort)weight);
            var towardUpper = Vector128.Create((ushort)(WeightSteps - weight));
            ref var first = ref MemoryMarshal.GetReference(upper);
            ref var second = ref MemoryMarshal.GetReference(lower);
            ref var target = ref MemoryMarshal.GetReference(to);
            for (; i <= to.Length - VectorBytes; i += VectorBytes)
            {
                var above = Vector128.LoadUnsafe(ref first, (nuint)i);
                var below = Vector128.LoadUnsafe(ref second, (nuint)i);
                var low = ((Vector128.WidenLower(above) * towardUpper) + (Vector128.WidenLower(below) * towardLower)) >>> ByteShift;
                var high = ((Vector128.WidenUpper(above) * towardUpper) + (Vector128.WidenUpper(below) * towardLower)) >>> ByteShift;
                Vector128.Narrow(low, high).StoreUnsafe(ref target, (nuint)i);
            }
        }

        for (; i < to.Length; i++)
        {
            to[i] = (byte)(((upper[i] * (WeightSteps - weight)) + (lower[i] * weight)) >> ByteShift);
        }
    }

    /// <summary>Blends one premultiplied pixel over another.</summary>
    /// <param name="source">The source pixel.</param>
    /// <param name="target">The destination pixel.</param>
    /// <param name="inverse">255 minus the source alpha.</param>
    /// <returns>The blended pixel.</returns>
    private static uint OverPixel(uint source, uint target, int inverse)
    {
        var result = 0U;
        for (var shift = 0; shift <= AlphaShift; shift += ByteShift)
        {
            var channel = (int)((source >> shift) & ChannelMax) + PremultipliedColor.DivideBy255((int)((target >> shift) & ChannelMax) * inverse);
            result |= (uint)Math.Min(ChannelMax, channel) << shift;
        }

        return result;
    }

    /// <summary>Blends four premultiplied source pixels over four destination pixels.</summary>
    /// <param name="source">The source pixels.</param>
    /// <param name="inverse">255 minus the source alpha, repeated for each channel of its pixel.</param>
    /// <param name="target">The destination pixels.</param>
    /// <returns>The blended pixels.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> Over(Vector128<byte> source, Vector128<byte> inverse, Vector128<byte> target)
    {
        var low = DivideBy255(Vector128.WidenLower(target) * Vector128.WidenLower(inverse));
        var high = DivideBy255(Vector128.WidenUpper(target) * Vector128.WidenUpper(inverse));
        return Vector128.AddSaturate(source, Vector128.Narrow(low, high));
    }

    /// <summary>Divides eight products of two channel values by 255 and rounds to nearest.</summary>
    /// <param name="product">The products.</param>
    /// <returns>The rounded quotients.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ushort> DivideBy255(Vector128<ushort> product)
    {
        var biased = product + Vector128.Create((ushort)RoundingBias);
        return (biased + (biased >>> ByteShift)) >>> ByteShift;
    }
}
