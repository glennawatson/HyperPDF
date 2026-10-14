// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>
/// The forward 8x8 DCT and quantization of ISO/IEC 10918-1 (A.3.3). The transform is two passes of the one-dimensional
/// DCT, each a 13-bit fixed-point multiplication by the cosine matrix split into its even and odd halves. The result is
/// quantized with a reciprocal multiplication and stored in zigzag order. The vector and scalar forms give equal results.
/// </summary>
internal static class JpegForwardDct
{
    /// <summary>The fixed-point bits of the cosine matrix.</summary>
    private const int MatrixBits = 13;

    /// <summary>The fraction bits kept between the passes.</summary>
    private const int PassBits = 2;

    /// <summary>The shift that descales the first pass.</summary>
    private const int FirstPassShift = MatrixBits - PassBits;

    /// <summary>The shift that descales the second pass to the coefficient with two fraction bits.</summary>
    private const int SecondPassShift = MatrixBits;

    /// <summary>The level shift that centres 8-bit samples on zero.</summary>
    private const int LevelShift = 128;

    /// <summary>The points on a side of the transform.</summary>
    private const int Side = JpegBlock.Side;

    /// <summary>The scale of the matrix entries: two over the square root of the transform size, which is one half.</summary>
    private const double MatrixScale = 0.5;

    /// <summary>The angle step of the cosine matrix: a sixteenth of a half turn.</summary>
    private const double AngleStep = Math.PI / 16;

    /// <summary>The angle steps in a full turn.</summary>
    private const int AngleCount = 32;

    /// <summary>The angle steps in a half turn.</summary>
    private const int HalfTurn = AngleCount / 2;

    /// <summary>The angle steps in a quarter turn.</summary>
    private const int QuarterTurn = AngleCount / 4;

    /// <summary>The square root of one half, which scales the zero-frequency row of the matrix.</summary>
    private const double RootHalf = 0.70710678118654752;

    /// <summary>The odd outputs of the transform: 1, 3, 5 and 7.</summary>
    private const int OddOutputs = Side / 2;

    /// <summary>The sample differences that each odd output sums.</summary>
    private const int OddTerms = Side / 2;

    /// <summary>The odd output 1.</summary>
    private const int FirstOdd = 0;

    /// <summary>The odd output 3.</summary>
    private const int SecondOdd = 1;

    /// <summary>The odd output 5.</summary>
    private const int ThirdOdd = 2;

    /// <summary>The odd output 7.</summary>
    private const int FourthOdd = 3;

    /// <summary>The offset of the second sample difference's weight.</summary>
    private const int SecondTerm = 1;

    /// <summary>The offset of the third sample difference's weight.</summary>
    private const int ThirdTerm = 2;

    /// <summary>The offset of the fourth sample difference's weight.</summary>
    private const int FourthTerm = 3;

    /// <summary>The shift that spreads the sign of an int over all its bits.</summary>
    private const int SignShift = 31;

    /// <summary>The cosine matrix in 13-bit fixed point; row k is the k-th basis function, column n the n-th sample.</summary>
    private static readonly int[] Matrix = BuildMatrix();

    /// <summary>The zigzag index of each storage position.</summary>
    private static readonly byte[] ZigzagOfStorage = BuildZigzagOfStorage();

    /// <summary>The weight of every sample in the zero-frequency output.</summary>
    private static readonly Vector256<int> Even0 = Vector256.Create(Matrix[0]);

    /// <summary>The weight of the alternating sum in the fourth output.</summary>
    private static readonly Vector256<int> Even4 = Vector256.Create(Matrix[(4 * Side) + 0]);

    /// <summary>The weight of the outer difference in the second output.</summary>
    private static readonly Vector256<int> Even2A = Vector256.Create(Matrix[(2 * Side) + 0]);

    /// <summary>The weight of the inner difference in the second output.</summary>
    private static readonly Vector256<int> Even2B = Vector256.Create(Matrix[(2 * Side) + 1]);

    /// <summary>The weight of the outer difference in the sixth output.</summary>
    private static readonly Vector256<int> Even6A = Vector256.Create(Matrix[(6 * Side) + 0]);

    /// <summary>The weight of the inner difference in the sixth output.</summary>
    private static readonly Vector256<int> Even6B = Vector256.Create(Matrix[(6 * Side) + 1]);

    /// <summary>The weights of the odd outputs, four per output, in the order output 1, 3, 5, 7.</summary>
    private static readonly Vector256<int>[] Odd = BuildOdd();

    /// <summary>Transforms and quantizes one block of 8-bit samples.</summary>
    /// <param name="samples">The first sample of the block; rows are <paramref name="stride"/> apart.</param>
    /// <param name="stride">The distance between rows.</param>
    /// <param name="reciprocals">The 64 reciprocals in storage order from <see cref="JpegQuantizer.BuildReciprocals"/>.</param>
    /// <param name="work">A 64-int scratch buffer.</param>
    /// <param name="output">Receives the 64 quantized coefficients in zigzag order.</param>
    internal static void Transform(ReadOnlySpan<byte> samples, int stride, ReadOnlySpan<int> reciprocals, Span<int> work, Span<short> output)
    {
        if (!Vector256.IsHardwareAccelerated)
        {
            TransformScalar(samples, stride, reciprocals, work, output);
            return;
        }

        ref var w = ref MemoryMarshal.GetReference(work);
        LoadSamples(ref MemoryMarshal.GetReference(samples), stride, ref w);
        Pass(ref w, FirstPassShift);
        JpegIdctVector.Transpose(work, ref w);
        Pass(ref w, SecondPassShift);
        Quantize(ref w, ref MemoryMarshal.GetReference(reciprocals), output);
    }

    /// <summary>Transforms and quantizes one block with scalar code, whatever the hardware.</summary>
    /// <param name="samples">The first sample of the block; rows are <paramref name="stride"/> apart.</param>
    /// <param name="stride">The distance between rows.</param>
    /// <param name="reciprocals">The 64 reciprocals in storage order.</param>
    /// <param name="work">A 64-int scratch buffer.</param>
    /// <param name="output">Receives the 64 quantized coefficients in zigzag order.</param>
    internal static void TransformScalar(ReadOnlySpan<byte> samples, int stride, ReadOnlySpan<int> reciprocals, Span<int> work, Span<short> output)
    {
        for (var row = 0; row < Side; row++)
        {
            for (var column = 0; column < Side; column++)
            {
                work[(row * Side) + column] = samples[(row * stride) + column] - LevelShift;
            }
        }

        for (var lane = 0; lane < Side; lane++)
        {
            PassScalar(work, lane, FirstPassShift);
        }

        JpegIdct.TransposeScalar(work);
        for (var lane = 0; lane < Side; lane++)
        {
            PassScalar(work, lane, SecondPassShift);
        }

        for (var i = 0; i < JpegBlock.Length; i++)
        {
            output[ZigzagOfStorage[i]] = (short)QuantizeOne(work[i], reciprocals[i]);
        }
    }

    /// <summary>Builds the cosine matrix: entry (k, n) is one half of c(k) times the cosine of (2n + 1) k pi / 16, where c(0) is the square root of one half and c(k) is one otherwise.</summary>
    /// <returns>The 64 entries in 13-bit fixed point.</returns>
    private static int[] BuildMatrix()
    {
        // Every angle is a multiple of a sixteenth of a half turn, so the cosines of the first quarter turn give all of
        // them by reflection; this keeps the symmetries of the matrix exact whatever the platform's cosine does.
        var quarter = new double[QuarterTurn + 1];
        for (var m = 0; m < quarter.Length; m++)
        {
            quarter[m] = Math.Cos(m * AngleStep);
        }

        var matrix = new int[JpegBlock.Length];
        for (var k = 0; k < Side; k++)
        {
            var scale = k == 0 ? MatrixScale * RootHalf : MatrixScale;
            for (var n = 0; n < Side; n++)
            {
                var angle = (OddMultiple(n) * k) % AngleCount;
                matrix[(k * Side) + n] = (int)Math.Round(scale * CosineOf(angle, quarter) * (1 << MatrixBits));
            }
        }

        return matrix;
    }

    /// <summary>Gets the odd multiple 2n + 1 that the cosine matrix takes the angle of sample n from.</summary>
    /// <param name="n">The sample number.</param>
    /// <returns>The odd multiple.</returns>
    private static int OddMultiple(int n) => (n << 1) | 1;

    /// <summary>Finds the cosine of a multiple of a sixteenth of a half turn from the cosines of the first quarter turn.</summary>
    /// <param name="angle">The multiple, from 0 to 31.</param>
    /// <param name="quarter">The cosines of the multiples from 0 to 8.</param>
    /// <returns>The cosine.</returns>
    private static double CosineOf(int angle, double[] quarter)
    {
        var fold = angle > HalfTurn ? AngleCount - angle : angle;
        return fold <= QuarterTurn ? quarter[fold] : -quarter[HalfTurn - fold];
    }

    /// <summary>Inverts the zigzag order: finds the zigzag index of each storage position.</summary>
    /// <returns>The 64 zigzag indices.</returns>
    private static byte[] BuildZigzagOfStorage()
    {
        var inverse = new byte[JpegBlock.Length];
        var zigzag = JpegBlock.Zigzag;
        for (var i = 0; i < JpegBlock.Length; i++)
        {
            inverse[zigzag[i]] = (byte)i;
        }

        return inverse;
    }

    /// <summary>Builds the vectors of odd-output weights.</summary>
    /// <returns>Sixteen vectors: for each of the outputs 1, 3, 5 and 7, the weights of the four sample differences.</returns>
    private static Vector256<int>[] BuildOdd()
    {
        var odd = new Vector256<int>[OddOutputs * OddTerms];
        for (var output = 0; output < OddOutputs; output++)
        {
            for (var n = 0; n < OddTerms; n++)
            {
                odd[(output * OddTerms) + n] = Vector256.Create(Matrix[(OddMultiple(output) * Side) + n]);
            }
        }

        return odd;
    }

    /// <summary>Rounds a pass result of the second transform down and quantizes it to the nearest multiple, ties away from zero.</summary>
    /// <param name="value">The coefficient with two fraction bits.</param>
    /// <param name="reciprocal">The step's reciprocal.</param>
    /// <returns>The quantized coefficient.</returns>
    private static int QuantizeOne(int value, int reciprocal)
    {
        var quantized = ((Math.Abs(value) * reciprocal) + (1 << (JpegQuantizer.ReciprocalBits - 1))) >> JpegQuantizer.ReciprocalBits;
        return value < 0 ? -quantized : quantized;
    }

    /// <summary>Runs the one-dimensional transform down one column of the work matrix with scalar code.</summary>
    /// <param name="work">The matrix, 8 rows of 8.</param>
    /// <param name="lane">The column.</param>
    /// <param name="shift">The rounding shift of this pass.</param>
    private static void PassScalar(Span<int> work, int lane, int shift)
    {
        Span<int> input = stackalloc int[Side];
        for (var n = 0; n < Side; n++)
        {
            input[n] = work[lane + (n * Side)];
        }

        var rounding = 1 << (shift - 1);
        for (var k = 0; k < Side; k++)
        {
            var sum = 0;
            for (var n = 0; n < Side; n++)
            {
                sum += Matrix[(k * Side) + n] * input[n];
            }

            work[lane + (k * Side)] = (sum + rounding) >> shift;
        }
    }

    /// <summary>Widens eight rows of samples to ints and centres them on zero.</summary>
    /// <param name="samples">The first sample.</param>
    /// <param name="stride">The distance between rows.</param>
    /// <param name="work">Receives the 64 values.</param>
    private static void LoadSamples(ref byte samples, int stride, ref int work)
    {
        var shift = Vector256.Create(LevelShift);
        for (var row = 0; row < Side; row++)
        {
            var bytes = Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref samples, row * stride))).AsByte();
            var shorts = Vector128.WidenLower(bytes);
            var wide = Vector256.Create(Vector128.WidenLower(shorts), Vector128.WidenUpper(shorts)).AsInt32();
            (wide - shift).StoreUnsafe(ref work, (nuint)(row * Side));
        }
    }

    /// <summary>Runs the one-dimensional transform on all eight columns at once; each vector holds one row.</summary>
    /// <param name="work">The matrix, updated in place.</param>
    /// <param name="shift">The rounding shift of this pass.</param>
    private static void Pass(ref int work, int shift)
    {
        var x0 = Vector256.LoadUnsafe(ref work, 0);
        var x1 = Vector256.LoadUnsafe(ref work, JpegBlock.Row1);
        var x2 = Vector256.LoadUnsafe(ref work, JpegBlock.Row2);
        var x3 = Vector256.LoadUnsafe(ref work, JpegBlock.Row3);
        var x4 = Vector256.LoadUnsafe(ref work, JpegBlock.Row4);
        var x5 = Vector256.LoadUnsafe(ref work, JpegBlock.Row5);
        var x6 = Vector256.LoadUnsafe(ref work, JpegBlock.Row6);
        var x7 = Vector256.LoadUnsafe(ref work, JpegBlock.Row7);

        // Samples n and 7 - n meet the even outputs as sums and the odd outputs as differences.
        var s0 = x0 + x7;
        var s1 = x1 + x6;
        var s2 = x2 + x5;
        var s3 = x3 + x4;
        var d0 = x0 - x7;
        var d1 = x1 - x6;
        var d2 = x2 - x5;
        var d3 = x3 - x4;
        var outer = s0 - s3;
        var inner = s1 - s2;
        var rounding = Vector256.Create(1 << (shift - 1));

        Descale((s0 + s1 + s2 + s3) * Even0, rounding, shift).StoreUnsafe(ref work, 0);
        Descale((outer * Even2A) + (inner * Even2B), rounding, shift).StoreUnsafe(ref work, JpegBlock.Row2);
        Descale((s0 - s1 - s2 + s3) * Even4, rounding, shift).StoreUnsafe(ref work, JpegBlock.Row4);
        Descale((outer * Even6A) + (inner * Even6B), rounding, shift).StoreUnsafe(ref work, JpegBlock.Row6);
        Descale(OddSum(FirstOdd, d0, d1, d2, d3), rounding, shift).StoreUnsafe(ref work, JpegBlock.Row1);
        Descale(OddSum(SecondOdd, d0, d1, d2, d3), rounding, shift).StoreUnsafe(ref work, JpegBlock.Row3);
        Descale(OddSum(ThirdOdd, d0, d1, d2, d3), rounding, shift).StoreUnsafe(ref work, JpegBlock.Row5);
        Descale(OddSum(FourthOdd, d0, d1, d2, d3), rounding, shift).StoreUnsafe(ref work, JpegBlock.Row7);
    }

    /// <summary>Weighs the four differences for one odd output.</summary>
    /// <param name="output">The odd output: 0 for the first, 1 for the third, 2 for the fifth, 3 for the seventh.</param>
    /// <param name="d0">The outermost difference.</param>
    /// <param name="d1">The second difference.</param>
    /// <param name="d2">The third difference.</param>
    /// <param name="d3">The innermost difference.</param>
    /// <returns>The weighted sum.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> OddSum(int output, Vector256<int> d0, Vector256<int> d1, Vector256<int> d2, Vector256<int> d3)
    {
        var weights = output * OddTerms;
        return (d0 * Odd[weights]) + (d1 * Odd[weights + SecondTerm]) + (d2 * Odd[weights + ThirdTerm]) + (d3 * Odd[weights + FourthTerm]);
    }

    /// <summary>Rounds and shifts right.</summary>
    /// <param name="value">The values.</param>
    /// <param name="rounding">Half of the divisor, in every lane.</param>
    /// <param name="shift">The shift.</param>
    /// <returns>The descaled values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> Descale(Vector256<int> value, Vector256<int> rounding, int shift) =>
        Vector256.ShiftRightArithmetic(value + rounding, shift);

    /// <summary>Quantizes the transformed block and stores it in zigzag order.</summary>
    /// <param name="work">The transformed block, 64 ints with two fraction bits.</param>
    /// <param name="reciprocals">The first reciprocal.</param>
    /// <param name="output">Receives the 64 coefficients in zigzag order.</param>
    private static void Quantize(ref int work, ref int reciprocals, Span<short> output)
    {
        Span<short> rows = stackalloc short[JpegBlock.Length];
        var half = Vector256.Create(1 << (JpegQuantizer.ReciprocalBits - 1));
        for (var row = 0; row < Side; row++)
        {
            var offset = (nuint)(row * Side);
            var value = Vector256.LoadUnsafe(ref work, offset);
            var sign = Vector256.ShiftRightArithmetic(value, SignShift);
            var quantized = Vector256.ShiftRightLogical((((value ^ sign) - sign) * Vector256.LoadUnsafe(ref reciprocals, offset)) + half, JpegQuantizer.ReciprocalBits);
            var signed = (quantized ^ sign) - sign;
            Vector128.Narrow(signed.GetLower(), signed.GetUpper()).StoreUnsafe(ref MemoryMarshal.GetReference(rows), offset);
        }

        for (var i = 0; i < JpegBlock.Length; i++)
        {
            output[ZigzagOfStorage[i]] = rows[i];
        }
    }
}
