// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>
/// The 8x8 inverse DCT. It is the accurate integer algorithm of the Independent JPEG Group (13-bit constants, two passes),
/// so its output matches libjpeg's default. Blocks with only a DC coefficient are filled directly; other blocks use
/// 256-bit vectors when the hardware accelerates them and scalar code otherwise.
/// </summary>
internal static class JpegIdct
{
    /// <summary>The fixed-point bits of the constants.</summary>
    internal const int ConstBits = 13;

    /// <summary>The extra bits kept between the passes.</summary>
    internal const int PassBits = 2;

    /// <summary>The shift that rounds the first pass back to <see cref="PassBits"/> extra bits.</summary>
    internal const int FirstPassShift = ConstBits - PassBits;

    /// <summary>The shift that rounds the second pass to samples: the constants, the pass bits and the factor of 8 in the DCT.</summary>
    internal const int SecondPassShift = ConstBits + PassBits + 3;

    /// <summary>The level shift that turns signed samples into 0 to 255.</summary>
    internal const int LevelShift = 128;

    /// <summary>The largest sample.</summary>
    internal const int MaxSample = 255;

    /// <summary>The constant 0.298631336 in <see cref="ConstBits"/> bits.</summary>
    internal const int Fix0298 = 2446;

    /// <summary>The constant 0.390180644.</summary>
    internal const int Fix0390 = 3196;

    /// <summary>The constant 0.541196100.</summary>
    internal const int Fix0541 = 4433;

    /// <summary>The constant 0.765366865.</summary>
    internal const int Fix0765 = 6270;

    /// <summary>The constant 0.899976223.</summary>
    internal const int Fix0899 = 7373;

    /// <summary>The constant 1.175875602.</summary>
    internal const int Fix1175 = 9633;

    /// <summary>The constant 1.501321110.</summary>
    internal const int Fix1501 = 12_299;

    /// <summary>The constant 1.847759065.</summary>
    internal const int Fix1847 = 15_137;

    /// <summary>The constant 1.961570560.</summary>
    internal const int Fix1961 = 16_069;

    /// <summary>The constant 2.053119869.</summary>
    internal const int Fix2053 = 16_819;

    /// <summary>The constant 2.562915447.</summary>
    internal const int Fix2562 = 20_995;

    /// <summary>The constant 3.072711026.</summary>
    internal const int Fix3072 = 25_172;

    /// <summary>The rounding of the DC-only shortcut: half of the divide by 8.</summary>
    private const int DcRound = 4;

    /// <summary>The shift of the DC-only shortcut.</summary>
    private const int DcShift = 3;

    /// <summary>Transforms one block into samples.</summary>
    /// <param name="coefficients">The 64 coefficients in storage order.</param>
    /// <param name="quant">The 64 quantizer steps in storage order.</param>
    /// <param name="work">A 64-int scratch buffer.</param>
    /// <param name="destination">Receives the 8x8 samples, row by row, starting at the first element.</param>
    /// <param name="stride">The distance between rows in <paramref name="destination"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">A span is too short for a block.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Transform(ReadOnlySpan<short> coefficients, ReadOnlySpan<int> quant, Span<int> work, Span<byte> destination, int stride) =>
        Transform(coefficients, quant, work, destination, stride, 0);

    /// <summary>Transforms one block into samples, dropping low bits so a 12-bit frame yields 8-bit samples.</summary>
    /// <param name="coefficients">The 64 coefficients in storage order.</param>
    /// <param name="quant">The 64 quantizer steps in storage order.</param>
    /// <param name="work">A 64-int scratch buffer.</param>
    /// <param name="destination">Receives the 8x8 samples, row by row, starting at the first element.</param>
    /// <param name="stride">The distance between rows in <paramref name="destination"/>.</param>
    /// <param name="sampleDrop">The bits dropped from each sample: 0 for 8-bit frames, 4 for 12-bit frames. The level shift stays 128.</param>
    /// <exception cref="ArgumentOutOfRangeException">A span is too short for a block.</exception>
    internal static void Transform(ReadOnlySpan<short> coefficients, ReadOnlySpan<int> quant, Span<int> work, Span<byte> destination, int stride, int sampleDrop)
    {
        Validate(coefficients, quant, work, destination, stride);
        if (!coefficients[1..].ContainsAnyExcept((short)0))
        {
            FillDc(coefficients[0] * quant[0], destination, stride, sampleDrop);
        }
        else if (Vector256.IsHardwareAccelerated)
        {
            JpegIdctVector.Transform(coefficients, quant, work, destination, stride, sampleDrop);
        }
        else
        {
            TransformScalar(coefficients, quant, work, destination, stride, sampleDrop);
        }
    }

    /// <summary>Transforms one block with scalar code, whatever the hardware.</summary>
    /// <param name="coefficients">The 64 coefficients in storage order.</param>
    /// <param name="quant">The 64 quantizer steps in storage order.</param>
    /// <param name="work">A 64-int scratch buffer.</param>
    /// <param name="destination">Receives the 8x8 samples.</param>
    /// <param name="stride">The distance between rows in <paramref name="destination"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">A span is too short for a block.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void TransformScalar(ReadOnlySpan<short> coefficients, ReadOnlySpan<int> quant, Span<int> work, Span<byte> destination, int stride) =>
        TransformScalar(coefficients, quant, work, destination, stride, 0);

    /// <summary>Transforms one block with scalar code, dropping low bits so a 12-bit frame yields 8-bit samples.</summary>
    /// <param name="coefficients">The 64 coefficients in storage order.</param>
    /// <param name="quant">The 64 quantizer steps in storage order.</param>
    /// <param name="work">A 64-int scratch buffer.</param>
    /// <param name="destination">Receives the 8x8 samples.</param>
    /// <param name="stride">The distance between rows in <paramref name="destination"/>.</param>
    /// <param name="sampleDrop">The bits dropped from each sample: 0 for 8-bit frames, 4 for 12-bit frames.</param>
    /// <exception cref="ArgumentOutOfRangeException">A span is too short for a block.</exception>
    internal static void TransformScalar(ReadOnlySpan<short> coefficients, ReadOnlySpan<int> quant, Span<int> work, Span<byte> destination, int stride, int sampleDrop)
    {
        Validate(coefficients, quant, work, destination, stride);
        for (var i = 0; i < JpegBlock.Length; i++)
        {
            work[i] = coefficients[i] * quant[i];
        }

        for (var lane = 0; lane < JpegBlock.Side; lane++)
        {
            Pass(work, lane, FirstPassShift);
        }

        TransposeScalar(work);
        for (var lane = 0; lane < JpegBlock.Side; lane++)
        {
            Pass(work, lane, SecondPassShift + sampleDrop);
        }

        for (var y = 0; y < JpegBlock.Side; y++)
        {
            var row = destination.Slice(y * stride, JpegBlock.Side);
            for (var x = 0; x < JpegBlock.Side; x++)
            {
                row[x] = (byte)Math.Clamp(work[(y * JpegBlock.Side) + x] + LevelShift, 0, MaxSample);
            }
        }
    }

    /// <summary>Transposes an 8x8 int matrix in place.</summary>
    /// <param name="matrix">The 64 values.</param>
    internal static void TransposeScalar(Span<int> matrix)
    {
        for (var row = 1; row < JpegBlock.Side; row++)
        {
            for (var column = 0; column < row; column++)
            {
                var upper = (column * JpegBlock.Side) + row;
                var lower = (row * JpegBlock.Side) + column;
                var held = matrix[upper];
                matrix[upper] = matrix[lower];
                matrix[lower] = held;
            }
        }
    }

    /// <summary>Checks that the spans can hold a block.</summary>
    /// <param name="coefficients">The coefficients.</param>
    /// <param name="quant">The quantizer steps.</param>
    /// <param name="work">The scratch buffer.</param>
    /// <param name="destination">The sample destination.</param>
    /// <param name="stride">The row distance.</param>
    private static void Validate(ReadOnlySpan<short> coefficients, ReadOnlySpan<int> quant, Span<int> work, Span<byte> destination, int stride)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(coefficients.Length, JpegBlock.Length);
        ArgumentOutOfRangeException.ThrowIfLessThan(quant.Length, JpegBlock.Length);
        ArgumentOutOfRangeException.ThrowIfLessThan(work.Length, JpegBlock.Length);
        ArgumentOutOfRangeException.ThrowIfLessThan(stride, JpegBlock.Side);
        ArgumentOutOfRangeException.ThrowIfLessThan(destination.Length, ((JpegBlock.Side - 1) * stride) + JpegBlock.Side);
    }

    /// <summary>Fills a block whose only coefficient is DC.</summary>
    /// <param name="dc">The dequantized DC coefficient.</param>
    /// <param name="destination">Receives the samples.</param>
    /// <param name="stride">The row distance.</param>
    /// <param name="sampleDrop">The bits dropped from each sample.</param>
    private static void FillDc(int dc, Span<byte> destination, int stride, int sampleDrop)
    {
        var sample = (byte)Math.Clamp(((dc + (DcRound << sampleDrop)) >> (DcShift + sampleDrop)) + LevelShift, 0, MaxSample);
        for (var y = 0; y < JpegBlock.Side; y++)
        {
            destination.Slice(y * stride, JpegBlock.Side).Fill(sample);
        }
    }

    /// <summary>Runs the one-dimensional transform down one column of the work matrix.</summary>
    /// <param name="work">The matrix, 8 rows of 8.</param>
    /// <param name="lane">The column.</param>
    /// <param name="shift">The rounding shift of this pass.</param>
    private static void Pass(Span<int> work, int lane, int shift)
    {
        ref var origin = ref MemoryMarshal.GetReference(work);
        var rounding = 1 << (shift - 1);
        var i0 = Unsafe.Add(ref origin, lane);
        var i1 = Unsafe.Add(ref origin, lane + JpegBlock.Side);
        var i2 = Unsafe.Add(ref origin, lane + JpegBlock.Row2);
        var i3 = Unsafe.Add(ref origin, lane + JpegBlock.Row3);
        var i4 = Unsafe.Add(ref origin, lane + JpegBlock.Row4);
        var i5 = Unsafe.Add(ref origin, lane + JpegBlock.Row5);
        var i6 = Unsafe.Add(ref origin, lane + JpegBlock.Row6);
        var i7 = Unsafe.Add(ref origin, lane + JpegBlock.Row7);

        var z1 = (i2 + i6) * Fix0541;
        var even2 = z1 - (i6 * Fix1847);
        var even3 = z1 + (i2 * Fix0765);
        var even0 = (i0 + i4) << ConstBits;
        var even1 = (i0 - i4) << ConstBits;
        var t10 = even0 + even3;
        var t13 = even0 - even3;
        var t11 = even1 + even2;
        var t12 = even1 - even2;

        var s1 = i7 + i1;
        var s2 = i5 + i3;
        var s3 = i7 + i3;
        var s4 = i5 + i1;
        var s5 = (s3 + s4) * Fix1175;
        var odd0 = i7 * Fix0298;
        var odd1 = i5 * Fix2053;
        var odd2 = i3 * Fix3072;
        var odd3 = i1 * Fix1501;
        s1 *= -Fix0899;
        s2 *= -Fix2562;
        s3 = (s3 * -Fix1961) + s5;
        s4 = (s4 * -Fix0390) + s5;
        odd0 += s1 + s3;
        odd1 += s2 + s4;
        odd2 += s2 + s3;
        odd3 += s1 + s4;

        Unsafe.Add(ref origin, lane) = (t10 + odd3 + rounding) >> shift;
        Unsafe.Add(ref origin, lane + JpegBlock.Row7) = (t10 - odd3 + rounding) >> shift;
        Unsafe.Add(ref origin, lane + JpegBlock.Side) = (t11 + odd2 + rounding) >> shift;
        Unsafe.Add(ref origin, lane + JpegBlock.Row6) = (t11 - odd2 + rounding) >> shift;
        Unsafe.Add(ref origin, lane + JpegBlock.Row2) = (t12 + odd1 + rounding) >> shift;
        Unsafe.Add(ref origin, lane + JpegBlock.Row5) = (t12 - odd1 + rounding) >> shift;
        Unsafe.Add(ref origin, lane + JpegBlock.Row3) = (t13 + odd0 + rounding) >> shift;
        Unsafe.Add(ref origin, lane + JpegBlock.Row4) = (t13 - odd0 + rounding) >> shift;
    }
}
