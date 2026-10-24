// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>
/// The normalised line spectral frequencies of the SILK layer (RFC 6716 section 4.2.7.5): a vector is picked from a codebook and refined
/// by a predicted residual, stabilised so the frequencies stay apart, and turned into the coefficients of a short-term prediction filter.
/// </summary>
internal static class SilkNlsf
{
    /// <summary>The fractional bits of the frequencies.</summary>
    private const int FrequencyBits = 15;

    /// <summary>The largest frequency, just under half the sampling rate.</summary>
    private const int FrequencyMax = (1 << FrequencyBits) - 1;

    /// <summary>The bits the first stage vector is shifted up by to reach the scale of the frequencies.</summary>
    private const int VectorShift = 7;

    /// <summary>The fractional bits of the residual.</summary>
    private const int ResidualBits = 10;

    /// <summary>The residual offset towards zero, 0.1 in Q10.</summary>
    private const int ResidualAdjust = 102;

    /// <summary>The bits the predicted residual is shifted down by.</summary>
    private const int PredictionShift = 8;

    /// <summary>The bits of the fixed-point scale the filter polynomials are computed in.</summary>
    private const int PolynomialBits = 16;

    /// <summary>The bits of the fraction of a frequency between two points of the cosine table.</summary>
    private const int CosineFractionBits = FrequencyBits - 7;

    /// <summary>The bits the cosine table's values are scaled up by before interpolating.</summary>
    private const int CosineScale = 8;

    /// <summary>The bits the interpolated cosine is shifted down to reach the scale of the polynomials.</summary>
    private const int CosineDownShift = 20 - PolynomialBits;

    /// <summary>The bits the polynomial coefficients are shifted down to reach the scale of the filter.</summary>
    private const int FilterShift = PolynomialBits + 1 - SilkLpc.CoefficientBits;

    /// <summary>The largest filter coefficient that fits a 16 bit number, scaled for the limiting loop.</summary>
    private const int CoefficientCeiling = 163_838;

    /// <summary>The most a filter coefficient may reach in 16 bits.</summary>
    private const int ShortMax = short.MaxValue;

    /// <summary>The shrink factor towards which a filter is moved while its coefficients are too big, 0.999 in Q16.</summary>
    private const int ShrinkTarget = 65_470;

    /// <summary>The bits of the bandwidth scaling that makes the limit proportional to the loudest coefficient.</summary>
    private const int LimitShift = 14;

    /// <summary>The bits the product of the loudest coefficient and its index is shifted down by.</summary>
    private const int LimitIndexShift = 2;

    /// <summary>The smallest inverse prediction gain of a stable filter, one over 10000 in Q30.</summary>
    private const int StableGain = 107_374;

    /// <summary>The amount a filter is shrunk by in the first round of the stability loop, in Q16.</summary>
    private const int StabilizeStart = 1 << 16;

    /// <summary>Two, the step between rounds of shrinking and the pair of points of an angle.</summary>
    private const int Pair = 2;

    /// <summary>The weights of the first vector's frequencies are scaled to this many fractional bits above the value of one.</summary>
    private const int WeightScale = 15 + SilkConstants.NlsfWeightBits;

    /// <summary>The bits the weight is shifted up by before its root is taken.</summary>
    private const int WeightRootShift = 18 - SilkConstants.NlsfWeightBits;

    /// <summary>The bits the residual is shifted up by before it is divided by the root of the weight.</summary>
    private const int WeightDivideShift = 14;

    /// <summary>The unsigned byte range one past its maximum.</summary>
    private const int BytePairs = 2;

    /// <summary>The mask of the three bits of a second stage selection that pick a distribution.</summary>
    private const int SelectMask = 7;

    /// <summary>The symbols of a distribution of residual indices, which is the stride between distributions.</summary>
    private const int EntropyStride = (2 * SilkConstants.NlsfQuantMaxAmplitude) + 1;

    /// <summary>Gets the order the frequencies of a ten coefficient filter are visited in so the polynomial stays accurate.</summary>
    private static ReadOnlySpan<byte> Ordering10 => [0x00, 0x09, 0x06, 0x03, 0x04, 0x05, 0x08, 0x01, 0x02, 0x07];

    /// <summary>Gets the order the frequencies of a sixteen coefficient filter are visited in so the polynomial stays accurate.</summary>
    private static ReadOnlySpan<byte> Ordering16 => [0x00, 0x0F, 0x08, 0x07, 0x04, 0x0B, 0x0C, 0x03, 0x02, 0x0D, 0x0A, 0x05, 0x06, 0x09, 0x0E, 0x01];

    /// <summary>Unpacks how the second stage of a first stage vector is coded.</summary>
    /// <param name="codebook">The codebook.</param>
    /// <param name="vector">The index of the vector of the first stage.</param>
    /// <param name="entropyIndex">Receives the offset into the residual distributions for each coefficient.</param>
    /// <param name="predictor">Receives the predictor of each coefficient, in Q8.</param>
    internal static void Unpack(SilkCodebook codebook, int vector, Span<short> entropyIndex, Span<byte> predictor)
    {
        var order = codebook.Order;
        var select = codebook.Select.Slice(vector * order / Pair, order / Pair);
        var table = codebook.Predictor;
        for (var i = 0; i < order; i += Pair)
        {
            int entry = select[i / Pair];
            entropyIndex[i] = (short)(((entry >> 1) & SelectMask) * EntropyStride);
            predictor[i] = table[i + ((entry & 1) * (order - 1))];
            entropyIndex[i + 1] = (short)(((entry >> 5) & SelectMask) * EntropyStride);
            predictor[i + 1] = table[i + (((entry >> 4) & 1) * (order - 1)) + 1];
        }
    }

    /// <summary>Decodes a vector of normalised line spectral frequencies from its indices.</summary>
    /// <param name="indices">The indices: the first stage vector, then the residual of each coefficient.</param>
    /// <param name="codebook">The codebook.</param>
    /// <param name="frequencies">Receives the frequencies, in Q15.</param>
    internal static void Decode(ReadOnlySpan<sbyte> indices, SilkCodebook codebook, Span<short> frequencies)
    {
        var order = codebook.Order;
        var vector = codebook.Vectors.Slice(indices[0] * order, order);
        for (var i = 0; i < order; i++)
        {
            frequencies[i] = (short)(vector[i] << VectorShift);
        }

        Span<short> entropyIndex = stackalloc short[SilkConstants.MaxLpcOrder];
        Span<byte> predictor = stackalloc byte[SilkConstants.MaxLpcOrder];
        Span<short> residual = stackalloc short[SilkConstants.MaxLpcOrder];
        Span<short> weights = stackalloc short[SilkConstants.MaxLpcOrder];
        Unpack(codebook, indices[0], entropyIndex, predictor);
        DequantizeResidual(residual, indices[1..], predictor, codebook);
        ComputeWeights(weights, frequencies, order);
        for (var i = 0; i < order; i++)
        {
            var root = SilkMath.SquareRootApproximation(weights[i] << WeightRootShift);
            var adjusted = frequencies[i] + (((int)residual[i] << WeightDivideShift) / root);
            frequencies[i] = (short)Math.Clamp(adjusted, 0, FrequencyMax);
        }

        Stabilize(frequencies, codebook.MinimumDistance, order);
    }

    /// <summary>Turns normalised line spectral frequencies into the coefficients of a monic whitening filter.</summary>
    /// <param name="filter">Receives the filter in Q12.</param>
    /// <param name="frequencies">The frequencies in Q15.</param>
    /// <param name="order">The coefficients, 10 or 16.</param>
    internal static void ToFilter(Span<short> filter, ReadOnlySpan<short> frequencies, int order)
    {
        Span<int> cosines = stackalloc int[SilkConstants.MaxLpcOrder];
        Span<int> even = stackalloc int[(SilkConstants.MaxLpcOrder / Pair) + 1];
        Span<int> odd = stackalloc int[(SilkConstants.MaxLpcOrder / Pair) + 1];
        Span<int> wide = stackalloc int[SilkConstants.MaxLpcOrder];
        FillCosines(cosines, frequencies, order);
        var half = order >> 1;
        FindPolynomial(even, cosines, 0, half);
        FindPolynomial(odd, cosines, 1, half);
        for (var k = 0; k < half; k++)
        {
            var evenSum = even[k + 1] + even[k];
            var oddDifference = odd[k + 1] - odd[k];
            wide[k] = -oddDifference - evenSum;
            wide[order - k - 1] = oddDifference - evenSum;
        }

        var limited = LimitCoefficients(wide[..order], order);
        for (var k = 0; k < order; k++)
        {
            filter[k] = limited ? (short)SilkMath.Saturate16(SilkMath.RoundShift(wide[k], FilterShift)) : (short)SilkMath.RoundShift(wide[k], FilterShift);
            if (limited)
            {
                wide[k] = filter[k] << FilterShift;
            }
        }

        MakeStable(filter, wide, order);
    }

    /// <summary>Dequantises the residual of the second stage, from the last coefficient backwards, each predicted from the one after.</summary>
    /// <param name="residual">Receives the residual, in Q10.</param>
    /// <param name="indices">The residual indices.</param>
    /// <param name="predictor">The predictor of each coefficient, in Q8.</param>
    /// <param name="codebook">The codebook.</param>
    private static void DequantizeResidual(Span<short> residual, ReadOnlySpan<sbyte> indices, ReadOnlySpan<byte> predictor, SilkCodebook codebook)
    {
        var output = 0;
        for (var i = codebook.Order - 1; i >= 0; i--)
        {
            var predicted = SilkMath.MultiplyBottoms(output, predictor[i]) >> PredictionShift;
            output = indices[i] << ResidualBits;
            if (output > 0)
            {
                output -= ResidualAdjust;
            }
            else if (output < 0)
            {
                output += ResidualAdjust;
            }

            output = SilkMath.MultiplyAddWordBottom(predicted, output, codebook.QuantStep);
            residual[i] = (short)output;
        }
    }

    /// <summary>Works out how much weight each frequency gets: the closer it is to its neighbours the more it matters.</summary>
    /// <param name="weights">Receives the weights, in Q2.</param>
    /// <param name="frequencies">The frequencies in Q15.</param>
    /// <param name="order">The coefficients, which is even.</param>
    private static void ComputeWeights(Span<short> weights, ReadOnlySpan<short> frequencies, int order)
    {
        var first = InverseDistance(Math.Max((int)frequencies[0], 1));
        var next = InverseDistance(Math.Max(frequencies[1] - frequencies[0], 1));
        weights[0] = (short)Math.Min(first + next, short.MaxValue);
        for (var k = 1; k < order - 1; k += Pair)
        {
            var left = InverseDistance(Math.Max(frequencies[k + 1] - frequencies[k], 1));
            weights[k] = (short)Math.Min(left + next, short.MaxValue);
            next = InverseDistance(Math.Max(frequencies[k + Pair] - frequencies[k + 1], 1));
            weights[k + 1] = (short)Math.Min(left + next, short.MaxValue);
        }

        var edge = InverseDistance(Math.Max((1 << FrequencyBits) - frequencies[order - 1], 1));
        weights[order - 1] = (short)Math.Min(edge + next, short.MaxValue);
    }

    /// <summary>Gets the weight of a distance between two frequencies.</summary>
    /// <param name="distance">The distance in Q15, at least one.</param>
    /// <returns>The weight in Q2.</returns>
    private static int InverseDistance(int distance) => (1 << WeightScale) / distance;

    /// <summary>Moves frequencies apart until each pair, and the first and last, keep at least their least distance.</summary>
    /// <param name="frequencies">The frequencies in Q15, changed in place.</param>
    /// <param name="minimum">The least distances, one more than the frequencies.</param>
    /// <param name="order">The frequencies.</param>
    private static void Stabilize(Span<short> frequencies, ReadOnlySpan<short> minimum, int order)
    {
        for (var loop = 0; loop < SilkConstants.NlsfStabilizeLoops; loop++)
        {
            var closest = FindClosest(frequencies, minimum, order, out var smallest);
            if (smallest >= 0)
            {
                return;
            }

            if (closest == 0)
            {
                frequencies[0] = minimum[0];
            }
            else if (closest == order)
            {
                frequencies[order - 1] = (short)((1 << FrequencyBits) - minimum[order]);
            }
            else
            {
                SpreadPair(frequencies, minimum, closest, order);
            }
        }

        SortAndSpace(frequencies, minimum, order);
    }

    /// <summary>Finds the pair of neighbours, or the end, that is furthest below its least distance.</summary>
    /// <param name="frequencies">The frequencies.</param>
    /// <param name="minimum">The least distances.</param>
    /// <param name="order">The number of frequencies.</param>
    /// <param name="smallest">Receives the margin of the closest pair: negative when it is too close.</param>
    /// <returns>The index of the pair; 0 means the first frequency and <paramref name="order"/> the last.</returns>
    private static int FindClosest(ReadOnlySpan<short> frequencies, ReadOnlySpan<short> minimum, int order, out int smallest)
    {
        smallest = frequencies[0] - minimum[0];
        var closest = 0;
        for (var i = 1; i <= order - 1; i++)
        {
            var margin = frequencies[i] - (frequencies[i - 1] + minimum[i]);
            if (margin >= smallest)
            {
                continue;
            }

            smallest = margin;
            closest = i;
        }

        var end = (1 << FrequencyBits) - (frequencies[order - 1] + minimum[order]);
        return end < smallest ? TakeEnd(order, end, out smallest) : closest;
    }

    /// <summary>Records that the margin at the top end is the smallest.</summary>
    /// <param name="order">The number of frequencies.</param>
    /// <param name="margin">The margin at the top end.</param>
    /// <param name="smallest">Receives the margin.</param>
    /// <returns>The index that stands for the top end.</returns>
    private static int TakeEnd(int order, int margin, out int smallest)
    {
        smallest = margin;
        return order;
    }

    /// <summary>Moves the two frequencies of a pair apart around their centre, keeping the centre where the others allow.</summary>
    /// <param name="frequencies">The frequencies, changed in place.</param>
    /// <param name="minimum">The least distances.</param>
    /// <param name="index">The index of the upper frequency of the pair.</param>
    /// <param name="order">The frequencies.</param>
    private static void SpreadPair(Span<short> frequencies, ReadOnlySpan<short> minimum, int index, int order)
    {
        var lowest = 0;
        for (var k = 0; k < index; k++)
        {
            lowest += minimum[k];
        }

        lowest += minimum[index] >> 1;
        var highest = 1 << FrequencyBits;
        for (var k = order; k > index; k--)
        {
            highest -= minimum[k];
        }

        highest -= minimum[index] >> 1;
        var centre = (short)SilkMath.Limit(SilkMath.RoundShift(frequencies[index - 1] + frequencies[index], 1), lowest, highest);
        frequencies[index - 1] = (short)(centre - (minimum[index] >> 1));
        frequencies[index] = (short)(frequencies[index - 1] + minimum[index]);
    }

    /// <summary>The fallback of the stabiliser: sorts the frequencies and spaces them out one by one.</summary>
    /// <param name="frequencies">The frequencies, changed in place.</param>
    /// <param name="minimum">The least distances.</param>
    /// <param name="order">The frequencies.</param>
    private static void SortAndSpace(Span<short> frequencies, ReadOnlySpan<short> minimum, int order)
    {
        frequencies[..order].Sort();
        frequencies[0] = (short)Math.Max((int)frequencies[0], minimum[0]);
        for (var i = 1; i < order; i++)
        {
            frequencies[i] = (short)Math.Max(frequencies[i], frequencies[i - 1] + minimum[i]);
        }

        frequencies[order - 1] = (short)Math.Min(frequencies[order - 1], (1 << FrequencyBits) - minimum[order]);
        for (var i = order - Pair; i >= 0; i--)
        {
            frequencies[i] = (short)Math.Min(frequencies[i], frequencies[i + 1] - minimum[i + 1]);
        }
    }

    /// <summary>Converts frequencies to twice their cosine, with a piecewise linear approximation, in the order that keeps the polynomial accurate.</summary>
    /// <param name="cosines">Receives the values in Q16.</param>
    /// <param name="frequencies">The frequencies in Q15.</param>
    /// <param name="order">The coefficients.</param>
    private static void FillCosines(Span<int> cosines, ReadOnlySpan<short> frequencies, int order)
    {
        var ordering = order == SilkConstants.MaxLpcOrder ? Ordering16 : Ordering10;
        var table = SilkTables.LsfCosine;
        for (var k = 0; k < order; k++)
        {
            var integer = frequencies[k] >> CosineFractionBits;
            var fraction = frequencies[k] - (integer << CosineFractionBits);
            var value = table[integer];
            var delta = table[integer + 1] - value;
            cosines[ordering[k]] = SilkMath.RoundShift((value << CosineScale) + (delta * fraction), CosineDownShift);
        }
    }

    /// <summary>Builds the polynomial whose roots are every other frequency by multiplying out its factors.</summary>
    /// <param name="output">Receives the polynomial in Q16, <paramref name="half"/> plus one coefficients.</param>
    /// <param name="cosines">The interleaved twice-cosines of the frequencies.</param>
    /// <param name="start">The index of the first cosine to use: 0 for the even polynomial and 1 for the odd.</param>
    /// <param name="half">Half the order of the filter.</param>
    private static void FindPolynomial(Span<int> output, ReadOnlySpan<int> cosines, int start, int half)
    {
        output[0] = 1 << PolynomialBits;
        output[1] = -cosines[start];
        for (var k = 1; k < half; k++)
        {
            var factor = cosines[start + (Pair * k)];
            output[k + 1] = (output[k - 1] << 1) - (int)SilkMath.RoundShift((long)factor * output[k], PolynomialBits);
            for (var n = k; n > 1; n--)
            {
                output[n] += output[n - Pair] - (int)SilkMath.RoundShift((long)factor * output[n - 1], PolynomialBits);
            }

            output[1] -= factor;
        }
    }

    /// <summary>Shrinks the coefficients when the largest would not fit in a 16 bit number.</summary>
    /// <param name="wide">The coefficients, in Q17, changed in place.</param>
    /// <param name="order">The coefficients.</param>
    /// <returns><see langword="true"/> when the loop gave up and the caller must clip the coefficients.</returns>
    private static bool LimitCoefficients(Span<int> wide, int order)
    {
        for (var i = 0; i < SilkConstants.MaxLpcLimitIterations; i++)
        {
            var largest = 0;
            var index = 0;
            for (var k = 0; k < order; k++)
            {
                var magnitude = Math.Abs(wide[k]);
                if (magnitude <= largest)
                {
                    continue;
                }

                largest = magnitude;
                index = k;
            }

            largest = SilkMath.RoundShift(largest, FilterShift);
            if (largest <= ShortMax)
            {
                return false;
            }

            largest = Math.Min(largest, CoefficientCeiling);
            var excess = SilkMath.ShiftLeft(largest - ShortMax, LimitShift) / ((largest * (index + 1)) >> LimitIndexShift);
            SilkLpc.BandwidthExpand32(wide, order, ShrinkTarget - excess);
        }

        return true;
    }

    /// <summary>Shrinks the filter towards the centre of the unit circle until it is stable.</summary>
    /// <param name="filter">The filter in Q12, changed in place.</param>
    /// <param name="wide">The filter at full precision, changed in place.</param>
    /// <param name="order">The coefficients.</param>
    private static void MakeStable(Span<short> filter, Span<int> wide, int order)
    {
        for (var i = 0; i < SilkConstants.MaxLpcStabilizeIterations; i++)
        {
            if (SilkLpc.InversePredictionGain(filter, order) >= StableGain)
            {
                return;
            }

            SilkLpc.BandwidthExpand32(wide, order, StabilizeStart - (Pair << i));
            for (var k = 0; k < order; k++)
            {
                filter[k] = (short)SilkMath.RoundShift(wide[k], FilterShift);
            }
        }
    }
}
