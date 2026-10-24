// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs.H265;

namespace HyperPdf.Codecs.Tests;

/// <summary>Checks the H.265 inverse transform against a direct evaluation of the matrix products of clause 8.6.4.</summary>
public sealed class H265TransformTests
{
    /// <summary>The largest block side.</summary>
    private const int Largest = 32;

    /// <summary>The shift after the first stage.</summary>
    private const int FirstShift = 7;

    /// <summary>The bits that bit depth and the second shift add up to.</summary>
    private const int ShiftTotal = 20;

    /// <summary>The base 2 logarithm of the side of the 4x4 block.</summary>
    private const int DstLog2 = 2;

    /// <summary>The base 2 logarithm of the side of the 8x8 block.</summary>
    private const int Log2Of8 = 3;

    /// <summary>The base 2 logarithm of the side of the 16x16 block.</summary>
    private const int Log2Of16 = 4;

    /// <summary>The base 2 logarithm of the side of the 32x32 block.</summary>
    private const int Log2Of32 = 5;

    /// <summary>A sample bit depth of 8.</summary>
    private const int EightBits = 8;

    /// <summary>A sample bit depth of 10.</summary>
    private const int TenBits = 10;

    /// <summary>The seed of the coefficients.</summary>
    private const int Seed = 4_711;

    /// <summary>The largest coefficient magnitude after scaling.</summary>
    private const int CoefficientRange = 32_768;

    /// <summary>The block sizes, and whether the DST is used: 4x4 DST, then each DCT size.</summary>
    /// <returns>The cases.</returns>
    public static IEnumerable<Func<TransformCase>> Cases()
    {
        yield return static () => new(DstLog2, true, EightBits);
        yield return static () => new(DstLog2, false, EightBits);
        yield return static () => new(Log2Of8, false, EightBits);
        yield return static () => new(Log2Of16, false, EightBits);
        yield return static () => new(Log2Of32, false, EightBits);
        yield return static () => new(Log2Of32, false, TenBits);
    }

    /// <summary>The transform gives the same residual as the direct matrix products for blocks with every extent of coefficients.</summary>
    /// <param name="testCase">The block size, transform and bit depth.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(nameof(Cases))]
    public async Task InverseMatchesDirectEvaluation(TransformCase testCase)
    {
        var side = 1 << testCase.Log2Size;
        var random = new HyperPdfLibrary.Tests.Robustness.SeededRandom(Seed);
        var mismatches = 0;
        for (var extentSide = 1; extentSide <= side; extentSide++)
        {
            var coefficients = new int[side * side];
            for (var y = 0; y < extentSide; y++)
            {
                for (var x = 0; x < extentSide; x++)
                {
                    coefficients[(y * side) + x] = random.Next(-CoefficientRange, CoefficientRange);
                }
            }

            var expected = Reference(coefficients, side, testCase);
            var actual = (int[])coefficients.Clone();
            H265Transform.Inverse(actual, testCase.Log2Size, testCase.Dst, testCase.BitDepth, new(extentSide - 1, extentSide - 1), new int[Largest * Largest]);
            mismatches += expected.AsSpan().SequenceEqual(actual) ? 0 : 1;
        }

        await Assert.That(mismatches).IsEqualTo(0);
    }

    /// <summary>Evaluates the transform as two plain matrix products with 64 bit sums.</summary>
    /// <param name="coefficients">The coefficients, row after row.</param>
    /// <param name="side">The block side.</param>
    /// <param name="testCase">The transform and bit depth.</param>
    /// <returns>The residual.</returns>
    private static int[] Reference(int[] coefficients, int side, TransformCase testCase)
    {
        var matrix = testCase.Dst ? H265TransformMatrix.Dst() : H265TransformMatrix.Dct();
        var rowStride = testCase.Dst ? side : H265TransformMatrix.Side;
        var step = testCase.Dst ? 1 : H265TransformMatrix.Side / side;
        var middle = new int[side * side];
        for (var x = 0; x < side; x++)
        {
            for (var output = 0; output < side; output++)
            {
                long sum = 0;
                for (var j = 0; j < side; j++)
                {
                    sum += (long)matrix[(j * step * rowStride) + output] * coefficients[(j * side) + x];
                }

                middle[(output * side) + x] = (int)Math.Clamp((sum + (1 << (FirstShift - 1))) >> FirstShift, short.MinValue, short.MaxValue);
            }
        }

        var shift = ShiftTotal - testCase.BitDepth;
        var result = new int[side * side];
        for (var y = 0; y < side; y++)
        {
            for (var output = 0; output < side; output++)
            {
                long sum = 0;
                for (var j = 0; j < side; j++)
                {
                    sum += (long)matrix[(j * step * rowStride) + output] * middle[(y * side) + j];
                }

                result[(y * side) + output] = (int)((sum + (1 << (shift - 1))) >> shift);
            }
        }

        return result;
    }

    /// <summary>One transform case.</summary>
    /// <param name="Log2Size">The base 2 logarithm of the block side.</param>
    /// <param name="Dst">Whether the block uses the DST.</param>
    /// <param name="BitDepth">The bit depth of the samples.</param>
    public readonly record struct TransformCase(int Log2Size, bool Dst, int BitDepth);
}
