// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Aac;

/// <summary>The stereo tools of a channel pair: mid/side coding and intensity stereo.</summary>
internal static class AacStereo
{
    /// <summary>The mask mode in which every band is mid/side.</summary>
    internal const int AllBands = 2;

    /// <summary>The mask mode in which the mask says which bands are mid/side.</summary>
    internal const int MaskedBands = 1;

    /// <summary>Applies mid/side and intensity stereo to a pair, after both spectra are filled.</summary>
    /// <param name="left">The first channel.</param>
    /// <param name="right">The second channel.</param>
    /// <param name="offsets">The band edges of the window size.</param>
    /// <param name="mask">Which bands the mask marks as mid/side, by group then band.</param>
    /// <param name="mode">0 for no mid/side, 1 to follow the mask, 2 for every band.</param>
    internal static void Apply(AacChannel left, AacChannel right, short[] offsets, bool[] mask, int mode)
    {
        var windowSize = right.IsShort ? AacChannel.ShortSize : AacChannel.LongSize;
        var firstWindow = 0;
        for (var group = 0; group < right.GroupCount; group++)
        {
            var length = right.GroupLength[group];
            for (var band = 0; band < right.MaxSfb; band++)
            {
                var index = (group * AacChannel.BandStride) + band;
                var sideBand = mode == AllBands || (mode == MaskedBands && mask[index]);
                var span = new AacBandSpan(firstWindow, length, windowSize, offsets[band], offsets[band + 1]);
                ApplyBand(left, right, span, index, sideBand);
            }

            firstWindow += length;
        }
    }

    /// <summary>Applies the stereo tool a band asks for.</summary>
    /// <param name="left">The first channel.</param>
    /// <param name="right">The second channel.</param>
    /// <param name="span">Where the band is.</param>
    /// <param name="index">The band's index in the codebooks.</param>
    /// <param name="sideBand">Whether the mask marks the band as mid/side.</param>
    private static void ApplyBand(AacChannel left, AacChannel right, AacBandSpan span, int index, bool sideBand)
    {
        var rightBook = right.Codebooks[index];
        var intensity = rightBook >= AacCodebooks.IntensityOutOfPhase;
        var midSide = sideBand && !intensity && rightBook != AacCodebooks.Noise && left.Codebooks[index] < AacCodebooks.Noise;
        if (!intensity && !midSide)
        {
            return;
        }

        for (var window = 0; window < span.Length; window++)
        {
            var start = span.SpectrumStart(window);
            var first = left.Spectrum.AsSpan(start, span.Width);
            var second = right.Spectrum.AsSpan(start, span.Width);
            if (intensity)
            {
                Intensity(first, second, (rightBook == AacCodebooks.IntensityInPhase) != sideBand, right.ScaleFactors[index]);
            }
            else
            {
                MidSide(first, second);
            }
        }
    }

    /// <summary>Turns mid and side coefficients into left and right.</summary>
    /// <param name="mid">The mid coefficients, which become left.</param>
    /// <param name="side">The side coefficients, which become right.</param>
    private static void MidSide(Span<float> mid, Span<float> side)
    {
        for (var bin = 0; bin < mid.Length; bin++)
        {
            var sum = mid[bin] + side[bin];
            side[bin] = mid[bin] - side[bin];
            mid[bin] = sum;
        }
    }

    /// <summary>Makes the right coefficients from the left by a position.</summary>
    /// <param name="left">The left coefficients.</param>
    /// <param name="right">Receives the right coefficients.</param>
    /// <param name="inPhase">Whether the right channel has the sign of the left.</param>
    /// <param name="position">The intensity position.</param>
    private static void Intensity(ReadOnlySpan<float> left, Span<float> right, bool inPhase, int position)
    {
        var factor = AacDequantizer.GainOf(-position);
        factor = inPhase ? factor : -factor;
        for (var bin = 0; bin < left.Length; bin++)
        {
            right[bin] = left[bin] * factor;
        }
    }
}
