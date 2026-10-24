// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>Operations on the short-term prediction filters of the SILK layer, whose coefficients are 16 bit numbers with 12 fractional bits.</summary>
internal static class SilkLpc
{
    /// <summary>The fractional bits of the filter coefficients.</summary>
    internal const int CoefficientBits = 12;

    /// <summary>The fractional bits of a chirp factor.</summary>
    private const int ChirpBits = 16;

    /// <summary>One in Q16, the factor that leaves a filter unchanged.</summary>
    private const int OneQ16 = 1 << ChirpBits;

    /// <summary>The bits in a 32 bit number.</summary>
    private const int WordBits = 32;

    /// <summary>The bits of the filter coefficients when the inverse gain is computed.</summary>
    private const int GainBits = 24;

    /// <summary>The limit of a coefficient above which the filter is not stable, 0.99975 in Q24.</summary>
    private const int CoefficientLimit = 16_773_022;

    /// <summary>The bits of a reflection coefficient.</summary>
    private const int ReflectionBits = 31;

    /// <summary>The bits of the inverse gain.</summary>
    private const int InverseGainBits = 30;

    /// <summary>The bits the product of two gains is shifted up by to bring it back to Q30.</summary>
    private const int GainProductShift = 2;

    /// <summary>The sum of the coefficients at or above which the filter is not stable, in Q12.</summary>
    private const int DcLimit = 4096;

    /// <summary>The bits the squares of the numbers are scaled down by when they would overflow.</summary>
    private const int EnergyStep = 2;

    /// <summary>The mask of the top bits of the energy that must stay clear.</summary>
    private const int EnergyHeadroom = unchecked((int)0xC0000000);

    /// <summary>The most coefficients of a filter.</summary>
    private const int MaxOrder = SilkConstants.MaxLpcOrder;

    /// <summary>The two arrays of coefficients the stability check swaps between.</summary>
    private const int Arrays = 2;

    /// <summary>Expands the bandwidth of a filter by moving its poles towards the centre of the unit circle.</summary>
    /// <param name="coefficients">The filter, in Q12, without the leading one.</param>
    /// <param name="order">The coefficients.</param>
    /// <param name="chirp">The chirp factor in Q16, typically between 0 and 1.</param>
    internal static void BandwidthExpand(Span<short> coefficients, int order, int chirp)
    {
        var minusOne = chirp - OneQ16;
        for (var i = 0; i < order - 1; i++)
        {
            coefficients[i] = (short)SilkMath.RoundShift(chirp * coefficients[i], ChirpBits);
            chirp += SilkMath.RoundShift(chirp * minusOne, ChirpBits);
        }

        coefficients[order - 1] = (short)SilkMath.RoundShift(chirp * coefficients[order - 1], ChirpBits);
    }

    /// <summary>Expands the bandwidth of a filter whose coefficients are 32 bit numbers.</summary>
    /// <param name="coefficients">The filter, without the leading one.</param>
    /// <param name="order">The coefficients.</param>
    /// <param name="chirp">The chirp factor in Q16.</param>
    internal static void BandwidthExpand32(Span<int> coefficients, int order, int chirp)
    {
        var minusOne = chirp - OneQ16;
        for (var i = 0; i < order - 1; i++)
        {
            coefficients[i] = SilkMath.MultiplyWord(chirp, coefficients[i]);
            chirp += SilkMath.RoundShift(chirp * minusOne, ChirpBits);
        }

        coefficients[order - 1] = SilkMath.MultiplyWord(chirp, coefficients[order - 1]);
    }

    /// <summary>Computes the inverse of the prediction gain of a filter, which is zero when the filter is unstable.</summary>
    /// <param name="coefficients">The filter in Q12.</param>
    /// <param name="order">The coefficients.</param>
    /// <returns>The inverse gain in Q30.</returns>
    internal static int InversePredictionGain(ReadOnlySpan<short> coefficients, int order)
    {
        Span<int> work = stackalloc int[Arrays * MaxOrder];
        var current = work.Slice((order & 1) * MaxOrder, MaxOrder);
        var dc = 0;
        for (var k = 0; k < order; k++)
        {
            dc += coefficients[k];
            current[k] = coefficients[k] << (GainBits - CoefficientBits);
        }

        return dc >= DcLimit ? 0 : InverseGainFromReflections(work, order);
    }

    /// <summary>Filters a signal with the inverse of a short-term prediction filter, which whitens it.</summary>
    /// <param name="output">Receives the residual; the first <paramref name="order"/> samples are zero.</param>
    /// <param name="input">The signal.</param>
    /// <param name="coefficients">The filter in Q12.</param>
    /// <param name="length">The samples.</param>
    /// <param name="order">The coefficients.</param>
    internal static void AnalysisFilter(Span<short> output, ReadOnlySpan<short> input, ReadOnlySpan<short> coefficients, int length, int order)
    {
        for (var ix = order; ix < length; ix++)
        {
            var prediction = 0;
            for (var j = 0; j < order; j++)
            {
                prediction = SilkMath.MultiplyAddBottoms(prediction, input[ix - 1 - j], coefficients[j]);
            }

            var residual = unchecked((input[ix] << CoefficientBits) - prediction);
            output[ix] = (short)SilkMath.Saturate16(SilkMath.RoundShift(residual, CoefficientBits));
        }

        output[..order].Clear();
    }

    /// <summary>Finds how many bits to shift the energy of a signal right so it fits in 32 bits.</summary>
    /// <param name="signal">The signal.</param>
    /// <param name="shift">Receives the bits the energy was shifted by.</param>
    /// <returns>The energy after the shift.</returns>
    internal static int SumSquaresShift(ReadOnlySpan<short> signal, out int shift)
    {
        var energy = 0;
        shift = 0;
        var last = signal.Length - 1;
        var i = 0;
        while (i < last)
        {
            energy = SilkMath.MultiplyAddBottoms(energy, signal[i], signal[i]);
            energy = SilkMath.MultiplyAddBottoms(energy, signal[i + 1], signal[i + 1]);
            i += Arrays;
            if (energy >= 0)
            {
                continue;
            }

            energy = (int)((uint)energy >> EnergyStep);
            shift = EnergyStep;
            break;
        }

        for (; i < last; i += Arrays)
        {
            var pair = SilkMath.MultiplyAddBottoms(SilkMath.MultiplyBottoms(signal[i], signal[i]), signal[i + 1], signal[i + 1]);
            energy = unchecked(energy + (int)((uint)pair >> shift));
            if (energy >= 0)
            {
                continue;
            }

            energy = (int)((uint)energy >> EnergyStep);
            shift += EnergyStep;
        }

        if (i == last)
        {
            energy = unchecked(energy + (int)((uint)SilkMath.MultiplyBottoms(signal[i], signal[i]) >> shift));
        }

        if ((energy & EnergyHeadroom) != 0)
        {
            energy = (int)((uint)energy >> EnergyStep);
            shift += EnergyStep;
        }

        return energy;
    }

    /// <summary>Steps down from the highest order of a filter to its reflection coefficients, multiplying up the inverse gain.</summary>
    /// <param name="work">Two arrays of coefficients; the one the order picks holds the filter.</param>
    /// <param name="order">The coefficients.</param>
    /// <returns>The inverse gain in Q30, or zero if a reflection coefficient shows the filter is unstable.</returns>
    private static int InverseGainFromReflections(Span<int> work, int order)
    {
        var current = work.Slice((order & 1) * MaxOrder, MaxOrder);
        var inverseGain = 1 << InverseGainBits;
        for (var k = order - 1; k > 0; k--)
        {
            if (Math.Abs(current[k]) > CoefficientLimit)
            {
                return 0;
            }

            var reflection = -SilkMath.ShiftLeft(current[k], ReflectionBits - GainBits);
            var gain = (1 << InverseGainBits) - SilkMath.MultiplyHigh(reflection, reflection);
            var scaleBits = WordBits - SilkMath.LeadingZeros(Math.Abs(gain));
            var inverse = SilkMath.InvertScaled(gain, scaleBits + InverseGainBits);
            inverseGain = SilkMath.ShiftLeft(SilkMath.MultiplyHigh(inverseGain, gain), GainProductShift);
            var previous = current;
            current = work.Slice((k & 1) * MaxOrder, MaxOrder);
            for (var n = 0; n < k; n++)
            {
                var temp = previous[n] - (int)SilkMath.RoundShift((long)previous[k - n - 1] * reflection, ReflectionBits);
                current[n] = (int)SilkMath.RoundShift((long)temp * inverse, scaleBits);
            }
        }

        if (Math.Abs(current[0]) > CoefficientLimit)
        {
            return 0;
        }

        var last = -SilkMath.ShiftLeft(current[0], ReflectionBits - GainBits);
        var lastGain = (1 << InverseGainBits) - SilkMath.MultiplyHigh(last, last);
        return SilkMath.ShiftLeft(SilkMath.MultiplyHigh(inverseGain, lastGain), GainProductShift);
    }
}
