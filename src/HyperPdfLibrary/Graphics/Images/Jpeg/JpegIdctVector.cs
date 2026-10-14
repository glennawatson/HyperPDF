// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>
/// The vector form of <see cref="JpegIdct"/>: each 256-bit vector holds one row of eight values, so one pass transforms
/// all eight columns at once. The results equal the scalar code exactly.
/// </summary>
internal static class JpegIdctVector
{
    /// <summary>The control that joins the low 128 bits of two vectors.</summary>
    private const byte LowHalves = 0x20;

    /// <summary>The control that joins the high 128 bits of two vectors.</summary>
    private const byte HighHalves = 0x31;

    /// <summary>The rows written in one step.</summary>
    private const int RowPair = 2;

    /// <summary>Transforms one block of an 8-bit frame.</summary>
    /// <param name="coefficients">The 64 coefficients in storage order.</param>
    /// <param name="quant">The 64 quantizer steps in storage order.</param>
    /// <param name="work">A 64-int scratch buffer.</param>
    /// <param name="destination">Receives the 8x8 samples.</param>
    /// <param name="stride">The distance between rows in <paramref name="destination"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Transform(ReadOnlySpan<short> coefficients, ReadOnlySpan<int> quant, Span<int> work, Span<byte> destination, int stride) =>
        Transform(coefficients, quant, work, destination, stride, 0);

    /// <summary>Transforms one block.</summary>
    /// <param name="coefficients">The 64 coefficients in storage order.</param>
    /// <param name="quant">The 64 quantizer steps in storage order.</param>
    /// <param name="work">A 64-int scratch buffer.</param>
    /// <param name="destination">Receives the 8x8 samples.</param>
    /// <param name="stride">The distance between rows in <paramref name="destination"/>.</param>
    /// <param name="sampleDrop">The bits dropped from each sample: 0 for 8-bit frames, 4 for 12-bit frames.</param>
    internal static void Transform(ReadOnlySpan<short> coefficients, ReadOnlySpan<int> quant, Span<int> work, Span<byte> destination, int stride, int sampleDrop)
    {
        ref var w = ref MemoryMarshal.GetReference(work);
        Dequantize(ref MemoryMarshal.GetReference(coefficients), ref MemoryMarshal.GetReference(quant), ref w);
        Pass(ref w, JpegIdct.FirstPassShift);
        Transpose(work, ref w);
        Pass(ref w, JpegIdct.SecondPassShift + sampleDrop);
        Store(ref w, destination, stride);
    }

    /// <summary>Transposes the work matrix in place.</summary>
    /// <param name="work">The matrix as a span, for the scalar fallback.</param>
    /// <param name="origin">The first element of the matrix.</param>
    internal static void Transpose(Span<int> work, ref int origin)
    {
        if (!Avx2.IsSupported)
        {
            JpegIdct.TransposeScalar(work);
            return;
        }

        var r0 = Vector256.LoadUnsafe(ref origin, 0);
        var r1 = Vector256.LoadUnsafe(ref origin, JpegBlock.Row1);
        var r2 = Vector256.LoadUnsafe(ref origin, JpegBlock.Row2);
        var r3 = Vector256.LoadUnsafe(ref origin, JpegBlock.Row3);
        var r4 = Vector256.LoadUnsafe(ref origin, JpegBlock.Row4);
        var r5 = Vector256.LoadUnsafe(ref origin, JpegBlock.Row5);
        var r6 = Vector256.LoadUnsafe(ref origin, JpegBlock.Row6);
        var r7 = Vector256.LoadUnsafe(ref origin, JpegBlock.Row7);

        // Interleave pairs of rows, then pairs of pairs, then join the 128-bit halves.
        var t0 = Avx2.UnpackLow(r0, r1).AsInt64();
        var t1 = Avx2.UnpackHigh(r0, r1).AsInt64();
        var t2 = Avx2.UnpackLow(r2, r3).AsInt64();
        var t3 = Avx2.UnpackHigh(r2, r3).AsInt64();
        var t4 = Avx2.UnpackLow(r4, r5).AsInt64();
        var t5 = Avx2.UnpackHigh(r4, r5).AsInt64();
        var t6 = Avx2.UnpackLow(r6, r7).AsInt64();
        var t7 = Avx2.UnpackHigh(r6, r7).AsInt64();

        var u0 = Avx2.UnpackLow(t0, t2).AsInt32();
        var u1 = Avx2.UnpackHigh(t0, t2).AsInt32();
        var u2 = Avx2.UnpackLow(t1, t3).AsInt32();
        var u3 = Avx2.UnpackHigh(t1, t3).AsInt32();
        var u4 = Avx2.UnpackLow(t4, t6).AsInt32();
        var u5 = Avx2.UnpackHigh(t4, t6).AsInt32();
        var u6 = Avx2.UnpackLow(t5, t7).AsInt32();
        var u7 = Avx2.UnpackHigh(t5, t7).AsInt32();

        Avx2.Permute2x128(u0, u4, LowHalves).StoreUnsafe(ref origin, 0);
        Avx2.Permute2x128(u1, u5, LowHalves).StoreUnsafe(ref origin, JpegBlock.Row1);
        Avx2.Permute2x128(u2, u6, LowHalves).StoreUnsafe(ref origin, JpegBlock.Row2);
        Avx2.Permute2x128(u3, u7, LowHalves).StoreUnsafe(ref origin, JpegBlock.Row3);
        Avx2.Permute2x128(u0, u4, HighHalves).StoreUnsafe(ref origin, JpegBlock.Row4);
        Avx2.Permute2x128(u1, u5, HighHalves).StoreUnsafe(ref origin, JpegBlock.Row5);
        Avx2.Permute2x128(u2, u6, HighHalves).StoreUnsafe(ref origin, JpegBlock.Row6);
        Avx2.Permute2x128(u3, u7, HighHalves).StoreUnsafe(ref origin, JpegBlock.Row7);
    }

    /// <summary>Widens the coefficients and multiplies them by the quantizer steps.</summary>
    /// <param name="coefficients">The first coefficient.</param>
    /// <param name="quant">The first quantizer step.</param>
    /// <param name="work">Receives 64 ints.</param>
    private static void Dequantize(ref short coefficients, ref int quant, ref int work)
    {
        for (var row = 0; row < JpegBlock.Side; row++)
        {
            var offset = (nuint)(row * JpegBlock.Side);
            var shorts = Vector128.LoadUnsafe(ref coefficients, offset);
            var wide = Vector256.Create(Vector128.WidenLower(shorts), Vector128.WidenUpper(shorts));
            (wide * Vector256.LoadUnsafe(ref quant, offset)).StoreUnsafe(ref work, offset);
        }
    }

    /// <summary>Runs the one-dimensional transform on all eight columns.</summary>
    /// <param name="work">The matrix, 8 rows of 8, updated in place.</param>
    /// <param name="shift">The rounding shift of this pass.</param>
    private static void Pass(ref int work, int shift)
    {
        var i0 = Vector256.LoadUnsafe(ref work, 0);
        var i1 = Vector256.LoadUnsafe(ref work, JpegBlock.Row1);
        var i2 = Vector256.LoadUnsafe(ref work, JpegBlock.Row2);
        var i3 = Vector256.LoadUnsafe(ref work, JpegBlock.Row3);
        var i4 = Vector256.LoadUnsafe(ref work, JpegBlock.Row4);
        var i5 = Vector256.LoadUnsafe(ref work, JpegBlock.Row5);
        var i6 = Vector256.LoadUnsafe(ref work, JpegBlock.Row6);
        var i7 = Vector256.LoadUnsafe(ref work, JpegBlock.Row7);

        var z1 = (i2 + i6) * Vector256.Create(JpegIdct.Fix0541);
        var even2 = z1 - (i6 * Vector256.Create(JpegIdct.Fix1847));
        var even3 = z1 + (i2 * Vector256.Create(JpegIdct.Fix0765));
        var even0 = Vector256.ShiftLeft(i0 + i4, JpegIdct.ConstBits);
        var even1 = Vector256.ShiftLeft(i0 - i4, JpegIdct.ConstBits);
        var t10 = even0 + even3;
        var t13 = even0 - even3;
        var t11 = even1 + even2;
        var t12 = even1 - even2;

        var s1 = i7 + i1;
        var s2 = i5 + i3;
        var s3 = i7 + i3;
        var s4 = i5 + i1;
        var s5 = (s3 + s4) * Vector256.Create(JpegIdct.Fix1175);
        var odd0 = i7 * Vector256.Create(JpegIdct.Fix0298);
        var odd1 = i5 * Vector256.Create(JpegIdct.Fix2053);
        var odd2 = i3 * Vector256.Create(JpegIdct.Fix3072);
        var odd3 = i1 * Vector256.Create(JpegIdct.Fix1501);
        s1 *= Vector256.Create(-JpegIdct.Fix0899);
        s2 *= Vector256.Create(-JpegIdct.Fix2562);
        s3 = (s3 * Vector256.Create(-JpegIdct.Fix1961)) + s5;
        s4 = (s4 * Vector256.Create(-JpegIdct.Fix0390)) + s5;
        odd0 += s1 + s3;
        odd1 += s2 + s4;
        odd2 += s2 + s3;
        odd3 += s1 + s4;

        var rounding = Vector256.Create(1 << (shift - 1));
        Descale(t10 + odd3, rounding, shift).StoreUnsafe(ref work, 0);
        Descale(t11 + odd2, rounding, shift).StoreUnsafe(ref work, JpegBlock.Row1);
        Descale(t12 + odd1, rounding, shift).StoreUnsafe(ref work, JpegBlock.Row2);
        Descale(t13 + odd0, rounding, shift).StoreUnsafe(ref work, JpegBlock.Row3);
        Descale(t13 - odd0, rounding, shift).StoreUnsafe(ref work, JpegBlock.Row4);
        Descale(t12 - odd1, rounding, shift).StoreUnsafe(ref work, JpegBlock.Row5);
        Descale(t11 - odd2, rounding, shift).StoreUnsafe(ref work, JpegBlock.Row6);
        Descale(t10 - odd3, rounding, shift).StoreUnsafe(ref work, JpegBlock.Row7);
    }

    /// <summary>Rounds and shifts right.</summary>
    /// <param name="value">The values.</param>
    /// <param name="rounding">Half of the divisor, in every lane.</param>
    /// <param name="shift">The shift.</param>
    /// <returns>The descaled values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> Descale(Vector256<int> value, Vector256<int> rounding, int shift) =>
        Vector256.ShiftRightArithmetic(value + rounding, shift);

    /// <summary>Adds the level shift, clamps to bytes and writes the eight rows.</summary>
    /// <param name="work">The transformed matrix.</param>
    /// <param name="destination">Receives the samples.</param>
    /// <param name="stride">The distance between rows.</param>
    private static void Store(ref int work, Span<byte> destination, int stride)
    {
        ref var target = ref MemoryMarshal.GetReference(destination);
        var shift = Vector256.Create(JpegIdct.LevelShift);
        var max = Vector256.Create(JpegIdct.MaxSample);
        for (var row = 0; row < JpegBlock.Side; row += RowPair)
        {
            var first = Vector256.Min(Vector256.Max(Vector256.LoadUnsafe(ref work, (nuint)(row * JpegBlock.Side)) + shift, Vector256<int>.Zero), max);
            var second = Vector256.Min(Vector256.Max(Vector256.LoadUnsafe(ref work, (nuint)((row + 1) * JpegBlock.Side)) + shift, Vector256<int>.Zero), max);
            var shorts = Vector256.Narrow(first.AsUInt32(), second.AsUInt32());
            var pair = Vector256.Narrow(shorts, shorts).AsUInt64();
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref target, row * stride), pair.GetElement(0));
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref target, (row + 1) * stride), pair.GetElement(1));
        }
    }
}
