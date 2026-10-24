// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;

namespace HyperPdf.Codecs.Flac;

/// <summary>
/// Turns residual back into samples. A block holds its warm-up samples first, then the residual; each function replaces the
/// residual with samples in place. The planes have <see cref="Padding"/> zero values in front of the first sample, so a vector load
/// that reaches back past the start of a block reads zeros rather than needing a check.
/// </summary>
internal static class FlacPredictors
{
    /// <summary>The zero values in front of the first sample of a plane, enough for the longest predictor to be padded to any vector width.</summary>
    internal const int Padding = 32;

    /// <summary>The number of values in a fixed predictor of order 1.</summary>
    private const int FirstOrder = 1;

    /// <summary>The second fixed order.</summary>
    private const int SecondOrder = 2;

    /// <summary>The third fixed order.</summary>
    private const int ThirdOrder = 3;

    /// <summary>The fourth fixed order.</summary>
    private const int FourthOrder = 4;

    /// <summary>The weight of the previous sample in the third and fourth order predictors.</summary>
    private const int Three = 3;

    /// <summary>The weight of the sample before the previous one in the fourth order predictor.</summary>
    private const int Six = 6;

    /// <summary>Restores samples from a fixed predictor, which is the sample prediction of a polynomial through the last few samples.</summary>
    /// <param name="plane">The samples, with <see cref="Padding"/> values in front of the block.</param>
    /// <param name="length">The samples in the block, warm-up included.</param>
    /// <param name="order">The order from 0 to 4.</param>
    internal static void RestoreFixed(Span<int> plane, int length, int order)
    {
        var samples = plane.Slice(Padding, length);
        switch (order)
        {
            case FirstOrder:
            {
                for (var index = order; index < samples.Length; index++)
                {
                    samples[index] += samples[index - 1];
                }

                break;
            }

            case SecondOrder:
            {
                for (var index = order; index < samples.Length; index++)
                {
                    samples[index] += (SecondOrder * samples[index - 1]) - samples[index - SecondOrder];
                }

                break;
            }

            case ThirdOrder:
            {
                for (var index = order; index < samples.Length; index++)
                {
                    samples[index] += (Three * samples[index - 1]) - (Three * samples[index - SecondOrder]) + samples[index - ThirdOrder];
                }

                break;
            }

            case FourthOrder:
            {
                for (var index = order; index < samples.Length; index++)
                {
                    samples[index] += (FourthOrder * samples[index - 1]) - (Six * samples[index - SecondOrder]) + (FourthOrder * samples[index - ThirdOrder]) - samples[index - FourthOrder];
                }

                break;
            }

            default:
            {
                break;
            }
        }
    }

    /// <summary>Restores samples from a linear predictor: each sample is its residual plus a weighted sum of the samples before it, shifted down.</summary>
    /// <param name="plane">The samples, with <see cref="Padding"/> values in front of the block.</param>
    /// <param name="length">The samples in the block, warm-up included.</param>
    /// <param name="coefficients">The weights, the first for the sample just before.</param>
    /// <param name="shift">The right shift applied to the sum.</param>
    /// <param name="bitsPerSample">The bits of the samples, which bounds the size of the sum.</param>
    internal static void RestoreLinear(Span<int> plane, int length, ReadOnlySpan<int> coefficients, int shift, int bitsPerSample)
    {
        var order = coefficients.Length;
        long weight = 0;
        foreach (var coefficient in coefficients)
        {
            weight += Math.Abs((long)coefficient);
        }

        // If no sum of weights times samples can pass 32 bits, the whole sum runs in vector lanes of 32 bits.
        var fits = weight * (1L << (bitsPerSample - 1)) < int.MaxValue;
        if (fits && Vector.IsHardwareAccelerated)
        {
            RestoreLinearVector(plane, length, coefficients, shift);
            return;
        }

        var samples = plane.Slice(Padding, length);
        for (var index = order; index < samples.Length; index++)
        {
            long sum = 0;
            for (var tap = 0; tap < order; tap++)
            {
                sum += (long)coefficients[tap] * samples[index - 1 - tap];
            }

            samples[index] += (int)(sum >> shift);
        }
    }

    /// <summary>Shifts samples left by the wasted bits the encoder found were always zero.</summary>
    /// <param name="plane">The samples, with <see cref="Padding"/> values in front of the block.</param>
    /// <param name="length">The samples in the block.</param>
    /// <param name="shift">The number of wasted bits.</param>
    internal static void RestoreWastedBits(Span<int> plane, int length, int shift)
    {
        var samples = plane.Slice(Padding, length);
        var index = 0;
        var step = Vector<int>.Count;
        for (; index <= samples.Length - step; index += step)
        {
            (new Vector<int>(samples.Slice(index, step)) << shift).CopyTo(samples.Slice(index, step));
        }

        for (; index < samples.Length; index++)
        {
            samples[index] <<= shift;
        }
    }

    /// <summary>The linear predictor with the sum of a sample's weights taken in vector lanes.</summary>
    /// <param name="plane">The samples, with <see cref="Padding"/> values in front of the block.</param>
    /// <param name="length">The samples in the block, warm-up included.</param>
    /// <param name="coefficients">The weights, the first for the sample just before.</param>
    /// <param name="shift">The right shift applied to the sum.</param>
    private static void RestoreLinearVector(Span<int> plane, int length, ReadOnlySpan<int> coefficients, int shift)
    {
        var order = coefficients.Length;
        var width = Vector<int>.Count;
        var padded = ((order + width - 1) / width) * width;

        // The weights are laid out oldest sample first and padded with zeros in front, so one run of vectors reads the samples
        // from <padded> back up to the one before the current sample.
        Span<int> weights = stackalloc int[Padding];
        weights.Clear();
        for (var tap = 0; tap < order; tap++)
        {
            weights[padded - 1 - tap] = coefficients[tap];
        }

        for (var index = order; index < length; index++)
        {
            var window = plane.Slice(Padding + index - padded, padded);
            var total = Vector<int>.Zero;
            for (var offset = 0; offset < padded; offset += width)
            {
                total += new Vector<int>(weights.Slice(offset, width)) * new Vector<int>(window.Slice(offset, width));
            }

            plane[Padding + index] += Vector.Sum(total) >> shift;
        }
    }
}
