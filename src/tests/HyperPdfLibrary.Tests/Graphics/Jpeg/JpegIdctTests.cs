// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Images.Jpeg;

namespace HyperPdfLibrary.Tests.Graphics.Jpeg;

/// <summary>Tests for the JPEG inverse DCT.</summary>
public sealed class JpegIdctTests
{
    /// <summary>The blocks tried with random data.</summary>
    private const int Blocks = 200;

    /// <summary>The seed of the random data.</summary>
    private const uint Seed = 4242;

    /// <summary>The denominator of the cosine angle: 16.</summary>
    private const double AngleDivisor = 16;

    /// <summary>The factor between the sample index and the odd multiple in the cosine angle.</summary>
    private const int Doubled = 2;

    /// <summary>The divisor of the 2D transform sum.</summary>
    private const double SumDivisor = 4;

    /// <summary>The largest magnitude of a random coefficient.</summary>
    private const int CoefficientRange = 300;

    /// <summary>The coefficients set in a sparse random block.</summary>
    private const int SparseCount = 12;

    /// <summary>The largest quantizer step.</summary>
    private const int MaxQuant = 16;

    /// <summary>The stride used for the output, wider than a block.</summary>
    private const int Stride = 24;

    /// <summary>The largest difference from the unrounded exact transform: the integer rounding plus its own error.</summary>
    private const double ReferenceTolerance = 1.5;

    /// <summary>The level shift added to samples.</summary>
    private const double LevelShift = 128.0;

    /// <summary>The DC coefficient of the DC-only block.</summary>
    private const short DcValue = 300;

    /// <summary>The quantizer step of the DC-only block.</summary>
    private const int DcQuant = 3;

    /// <summary>The sample a DC-only block gives: (300 * 3 + 4) / 8 plus the level shift.</summary>
    private const int DcSample = 241;

    /// <summary>The scalar and vector transforms give identical samples.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ScalarMatchesVector()
    {
        var random = new JpegTestRandom(Seed);
        var work = new int[JpegBlock.Length];
        for (var block = 0; block < Blocks; block++)
        {
            var coefficients = RandomCoefficients(random);
            var quant = RandomQuant(random);
            var scalar = new byte[Stride * JpegBlock.Side];
            var vector = new byte[Stride * JpegBlock.Side];
            JpegIdct.TransformScalar(coefficients, quant, work, scalar, Stride);
            JpegIdctVector.Transform(coefficients, quant, work, vector, Stride);

            await Assert.That(vector).IsEquivalentTo(scalar);
        }
    }

    /// <summary>The integer transform stays within one level of a double-precision transform, which fixes the layout.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MatchesExactTransform()
    {
        var random = new JpegTestRandom(Seed);
        var work = new int[JpegBlock.Length];
        for (var block = 0; block < Blocks; block++)
        {
            var coefficients = RandomCoefficients(random);
            var quant = RandomQuant(random);
            var samples = new byte[Stride * JpegBlock.Side];
            JpegIdct.Transform(coefficients, quant, work, samples, Stride);

            await Assert.That(MaxDifference(samples, Exact(coefficients, quant))).IsLessThanOrEqualTo(ReferenceTolerance);
        }
    }

    /// <summary>A block with only a DC coefficient fills with one sample, as the full transform would.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DcOnlyBlockIsFlat()
    {
        var coefficients = new short[JpegBlock.Length];
        coefficients[0] = DcValue;
        var quant = new int[JpegBlock.Length];
        Array.Fill(quant, DcQuant);
        var work = new int[JpegBlock.Length];
        var fast = new byte[Stride * JpegBlock.Side];
        var full = new byte[Stride * JpegBlock.Side];
        JpegIdct.Transform(coefficients, quant, work, fast, Stride);
        JpegIdct.TransformScalar(coefficients, quant, work, full, Stride);

        await Assert.That(fast).IsEquivalentTo(full);
        await Assert.That(fast[0]).IsEqualTo((byte)DcSample);
    }

    /// <summary>The scalar transpose swaps rows and columns.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TransposeSwapsRowsAndColumns()
    {
        var matrix = new int[JpegBlock.Length];
        for (var i = 0; i < matrix.Length; i++)
        {
            matrix[i] = i;
        }

        JpegIdct.TransposeScalar(matrix);

        for (var row = 0; row < JpegBlock.Side; row++)
        {
            for (var column = 0; column < JpegBlock.Side; column++)
            {
                await Assert.That(matrix[(row * JpegBlock.Side) + column]).IsEqualTo((column * JpegBlock.Side) + row);
            }
        }
    }

    /// <summary>Makes a sparse block of random coefficients.</summary>
    /// <param name="random">The random source.</param>
    /// <returns>The coefficients.</returns>
    private static short[] RandomCoefficients(JpegTestRandom random)
    {
        var coefficients = new short[JpegBlock.Length];
        coefficients[0] = (short)random.Next(-CoefficientRange, CoefficientRange);
        for (var i = 0; i < SparseCount; i++)
        {
            coefficients[random.Next(0, JpegBlock.Length)] = (short)random.Next(-CoefficientRange, CoefficientRange);
        }

        return coefficients;
    }

    /// <summary>Makes random quantizer steps.</summary>
    /// <param name="random">The random source.</param>
    /// <returns>The steps.</returns>
    private static int[] RandomQuant(JpegTestRandom random)
    {
        var quant = new int[JpegBlock.Length];
        for (var i = 0; i < quant.Length; i++)
        {
            quant[i] = random.Next(1, MaxQuant);
        }

        return quant;
    }

    /// <summary>Computes the exact inverse DCT of a block in storage order.</summary>
    /// <param name="coefficients">The coefficients, indexed by horizontal then vertical frequency.</param>
    /// <param name="quant">The quantizer steps.</param>
    /// <returns>The 8x8 samples, row by row.</returns>
    private static double[] Exact(short[] coefficients, int[] quant)
    {
        var samples = new double[JpegBlock.Length];
        for (var y = 0; y < JpegBlock.Side; y++)
        {
            for (var x = 0; x < JpegBlock.Side; x++)
            {
                var sum = 0.0;
                for (var u = 0; u < JpegBlock.Side; u++)
                {
                    for (var v = 0; v < JpegBlock.Side; v++)
                    {
                        var index = (u * JpegBlock.Side) + v;
                        var horizontal = Math.Cos((((Doubled * x) + 1) * u * Math.PI) / AngleDivisor);
                        var vertical = Math.Cos((((Doubled * y) + 1) * v * Math.PI) / AngleDivisor);
                        sum += Scale(u) * Scale(v) * coefficients[index] * quant[index] * horizontal * vertical;
                    }
                }

                samples[(y * JpegBlock.Side) + x] = (sum / SumDivisor) + LevelShift;
            }
        }

        return samples;
    }

    /// <summary>Gets the DCT normalisation of a frequency.</summary>
    /// <param name="frequency">The frequency index.</param>
    /// <returns>1 divided by the square root of 2 for zero, otherwise 1.</returns>
    private static double Scale(int frequency) => frequency == 0 ? 1 / Math.Sqrt(Doubled) : 1;

    /// <summary>Finds the largest difference between integer samples and exact values, after clamping the exact ones.</summary>
    /// <param name="samples">The output with its stride.</param>
    /// <param name="exact">The exact values.</param>
    /// <returns>The largest absolute difference.</returns>
    private static double MaxDifference(byte[] samples, double[] exact)
    {
        var largest = 0.0;
        for (var y = 0; y < JpegBlock.Side; y++)
        {
            for (var x = 0; x < JpegBlock.Side; x++)
            {
                var expected = Math.Clamp(exact[(y * JpegBlock.Side) + x], 0, byte.MaxValue);
                largest = Math.Max(largest, Math.Abs(samples[(y * Stride) + x] - expected));
            }
        }

        return largest;
    }
}
