// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// The inverse multiple component transforms (ISO 15444-1 annex G) and the DC level shift that ends decoding: the
/// reversible RCT on integers, the irreversible ICT on floats with the coefficients of equation G-10, and rounding to the
/// component's integer range.
/// </summary>
internal static class JpxComponentTransform
{
    /// <summary>The ICT red coefficient of Cr.</summary>
    private const float RedFromCr = 1.402F;

    /// <summary>The ICT green coefficient of Cb.</summary>
    private const float GreenFromCb = 0.34413F;

    /// <summary>The ICT green coefficient of Cr.</summary>
    private const float GreenFromCr = 0.71414F;

    /// <summary>The ICT blue coefficient of Cb.</summary>
    private const float BlueFromCb = 1.772F;

    /// <summary>The shift that divides the RCT chroma sum by four.</summary>
    private const int QuarterShift = 2;

    /// <summary>Applies the inverse reversible component transform (equation G-6) in place.</summary>
    /// <param name="first">Y, replaced by red.</param>
    /// <param name="second">Cb, replaced by green.</param>
    /// <param name="third">Cr, replaced by blue.</param>
    internal static void InverseReversible(Span<int> first, Span<int> second, Span<int> third)
    {
        var count = Math.Min(first.Length, Math.Min(second.Length, third.Length));
        ref var y = ref MemoryMarshal.GetReference(first);
        ref var u = ref MemoryMarshal.GetReference(second);
        ref var v = ref MemoryMarshal.GetReference(third);
        var i = 0;
        if (Vector256.IsHardwareAccelerated)
        {
            for (; i <= count - Vector256<int>.Count; i += Vector256<int>.Count)
            {
                var cb = Vector256.LoadUnsafe(ref u, (nuint)i);
                var cr = Vector256.LoadUnsafe(ref v, (nuint)i);
                var g = Vector256.LoadUnsafe(ref y, (nuint)i) - Vector256.ShiftRightArithmetic(cb + cr, QuarterShift);
                (cr + g).StoreUnsafe(ref y, (nuint)i);
                g.StoreUnsafe(ref u, (nuint)i);
                (cb + g).StoreUnsafe(ref v, (nuint)i);
            }
        }

        for (; i < count; i++)
        {
            var g = first[i] - ((second[i] + third[i]) >> QuarterShift);
            first[i] = third[i] + g;
            var b = second[i] + g;
            second[i] = g;
            third[i] = b;
        }
    }

    /// <summary>Applies the inverse irreversible component transform (equation G-3) in place.</summary>
    /// <param name="first">Y, replaced by red.</param>
    /// <param name="second">Cb, replaced by green.</param>
    /// <param name="third">Cr, replaced by blue.</param>
    internal static void InverseIrreversible(Span<float> first, Span<float> second, Span<float> third)
    {
        var count = Math.Min(first.Length, Math.Min(second.Length, third.Length));
        ref var y = ref MemoryMarshal.GetReference(first);
        ref var u = ref MemoryMarshal.GetReference(second);
        ref var v = ref MemoryMarshal.GetReference(third);
        var i = 0;
        if (Vector256.IsHardwareAccelerated)
        {
            var redCr = Vector256.Create(RedFromCr);
            var greenCb = Vector256.Create(GreenFromCb);
            var greenCr = Vector256.Create(GreenFromCr);
            var blueCb = Vector256.Create(BlueFromCb);
            for (; i <= count - Vector256<float>.Count; i += Vector256<float>.Count)
            {
                var luma = Vector256.LoadUnsafe(ref y, (nuint)i);
                var cb = Vector256.LoadUnsafe(ref u, (nuint)i);
                var cr = Vector256.LoadUnsafe(ref v, (nuint)i);
                (luma + (cr * redCr)).StoreUnsafe(ref y, (nuint)i);
                (luma - (cb * greenCb) - (cr * greenCr)).StoreUnsafe(ref u, (nuint)i);
                (luma + (cb * blueCb)).StoreUnsafe(ref v, (nuint)i);
            }
        }

        for (; i < count; i++)
        {
            var luma = first[i];
            var cb = second[i];
            var cr = third[i];
            first[i] = luma + (cr * RedFromCr);
            second[i] = luma - (cb * GreenFromCb) - (cr * GreenFromCr);
            third[i] = luma + (cb * BlueFromCb);
        }
    }

    /// <summary>Adds the DC level shift to integer samples and clamps them to the component's range.</summary>
    /// <param name="source">The samples.</param>
    /// <param name="destination">Receives the shifted samples; may be <paramref name="source"/>.</param>
    /// <param name="range">The component's range.</param>
    internal static void ShiftReversible(ReadOnlySpan<int> source, Span<int> destination, in JpxSampleRange range)
    {
        var count = source.Length;
        ref var s = ref MemoryMarshal.GetReference(source);
        ref var d = ref MemoryMarshal.GetReference(destination[..count]);
        var i = 0;
        if (Vector256.IsHardwareAccelerated)
        {
            var shift = Vector256.Create(range.Shift);
            var min = Vector256.Create(range.Minimum);
            var max = Vector256.Create(range.Maximum);
            for (; i <= count - Vector256<int>.Count; i += Vector256<int>.Count)
            {
                Vector256.Min(Vector256.Max(Vector256.LoadUnsafe(ref s, (nuint)i) + shift, min), max).StoreUnsafe(ref d, (nuint)i);
            }
        }

        for (; i < count; i++)
        {
            destination[i] = Math.Clamp(source[i] + range.Shift, range.Minimum, range.Maximum);
        }
    }

    /// <summary>
    /// Rounds float samples to the nearest integer, ties to even as <c>lrintf</c> does, adds the DC level shift and
    /// clamps them to the component's range.
    /// </summary>
    /// <param name="source">The samples.</param>
    /// <param name="destination">Receives the integers; may share memory with <paramref name="source"/>.</param>
    /// <param name="range">The component's range.</param>
    internal static void ShiftIrreversible(ReadOnlySpan<float> source, Span<int> destination, in JpxSampleRange range)
    {
        var count = source.Length;
        ref var s = ref MemoryMarshal.GetReference(source);
        ref var d = ref MemoryMarshal.GetReference(destination[..count]);
        var low = (float)((long)range.Minimum - range.Shift);
        var high = (float)((long)range.Maximum - range.Shift);
        var i = 0;
        if (Vector256.IsHardwareAccelerated)
        {
            var shift = Vector256.Create(range.Shift);
            var min = Vector256.Create(low);
            var max = Vector256.Create(high);
            var minimum = Vector256.Create(range.Minimum);
            var maximum = Vector256.Create(range.Maximum);
            for (; i <= count - Vector256<float>.Count; i += Vector256<float>.Count)
            {
                var rounded = Vector256.Round(Vector256.Min(Vector256.Max(Vector256.LoadUnsafe(ref s, (nuint)i), min), max));
                var shifted = Vector256.ConvertToInt32(rounded) + shift;

                // Wide precisions do not fit a float exactly, so the integer result is clamped again.
                Vector256.Min(Vector256.Max(shifted, minimum), maximum).StoreUnsafe(ref d, (nuint)i);
            }
        }

        for (; i < count; i++)
        {
            var rounded = MathF.Round(Math.Clamp(source[i], low, high));
            destination[i] = (int)Math.Clamp((long)rounded + range.Shift, range.Minimum, range.Maximum);
        }
    }
}
