// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Aac;

/// <summary>
/// Turns the quantised spectrum of a channel into coefficients: each value to the power of 4/3 times the band's gain, with random
/// noise in the bands that ask for it. The gain also holds the scale of the inverse transform and of the output samples, so the
/// transform's result is already in the range -1 to 1.
/// </summary>
internal static class AacDequantizer
{
    /// <summary>The scalefactor that gives a gain of 1.</summary>
    private const int UnityScaleFactor = 100;

    /// <summary>The base the scalefactors are powers of.</summary>
    private const float GainBase = 2F;

    /// <summary>How much a scalefactor step changes the gain, as a power of the base.</summary>
    private const float StepExponent = 0.25F;

    /// <summary>The power the quantised values are raised to.</summary>
    private const float Power = 4F / 3F;

    /// <summary>The number of values with a power in the table.</summary>
    private const int PowerTableSize = 8192;

    /// <summary>The multiplier of the noise generator.</summary>
    private const uint NoiseMultiplier = 1_664_525;

    /// <summary>The increment of the noise generator.</summary>
    private const uint NoiseIncrement = 1_013_904_223;

    /// <summary>The size of the output sample range the transform result is scaled to fit.</summary>
    private const float SampleRange = 32768F;

    /// <summary>The first exponent the gain table holds.</summary>
    private const int GainTableStart = -128;

    /// <summary>The number of exponents the gain table holds.</summary>
    private const int GainTableSize = 640;

    /// <summary>The quantised values raised to the power of 4/3.</summary>
    private static readonly float[] PowerTable = BuildPowerTable();

    /// <summary>The base raised to a quarter of each exponent, from <see cref="GainTableStart"/>.</summary>
    private static readonly float[] GainTable = BuildGainTable();

    /// <summary>Fills the spectrum of a channel.</summary>
    /// <param name="channel">The channel, with its quantised values read.</param>
    /// <param name="offsets">The band edges of the window size.</param>
    /// <param name="noise">The state of the noise generator.</param>
    /// <returns>The state of the noise generator after this channel.</returns>
    internal static uint Run(AacChannel channel, short[] offsets, uint noise)
    {
        Array.Clear(channel.Spectrum);
        var windowSize = channel.IsShort ? AacChannel.ShortSize : AacChannel.LongSize;
        var windowScale = 1F / (windowSize * SampleRange);
        var groupBase = 0;
        var firstWindow = 0;
        for (var group = 0; group < channel.GroupCount; group++)
        {
            var length = channel.GroupLength[group];
            for (var band = 0; band < channel.MaxSfb; band++)
            {
                var index = (group * AacChannel.BandStride) + band;
                var book = channel.Codebooks[index];
                var span = new AacBandSpan(firstWindow, length, windowSize, offsets[band], offsets[band + 1]);
                if (book == AacCodebooks.Noise)
                {
                    var level = GainOf(channel.ScaleFactors[index]) * windowScale;
                    noise = FillNoise(channel.Spectrum, span, level, noise);
                }
                else if (book != AacCodebooks.Zero && book < AacCodebooks.FirstSpecial)
                {
                    var gain = GainOf(channel.ScaleFactors[index] - UnityScaleFactor) * windowScale;
                    Scale(channel, groupBase, span, gain);
                }
            }

            groupBase += length * windowSize;
            firstWindow += length;
        }

        return noise;
    }

    /// <summary>Gets the base raised to a quarter of an exponent.</summary>
    /// <param name="exponent">The exponent.</param>
    /// <returns>The gain.</returns>
    internal static float GainOf(int exponent)
    {
        var slot = exponent - GainTableStart;
        return (uint)slot < GainTableSize ? GainTable[slot] : MathF.Pow(GainBase, StepExponent * exponent);
    }

    /// <summary>Scales the values of one band of one group into the spectrum.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="groupBase">The index of the group's first value in the quantised values.</param>
    /// <param name="span">Where the band is.</param>
    /// <param name="gain">The gain.</param>
    private static void Scale(AacChannel channel, int groupBase, AacBandSpan span, float gain)
    {
        var width = span.Width;
        var table = PowerTable;
        for (var window = 0; window < span.Length; window++)
        {
            var source = channel.Quantised.AsSpan(groupBase + (span.Start * span.Length) + (window * width), width);
            var target = channel.Spectrum.AsSpan(span.SpectrumStart(window), width);
            for (var bin = 0; bin < source.Length; bin++)
            {
                var value = source[bin];
                var magnitude = table[Math.Min(Math.Abs(value), PowerTableSize - 1)] * gain;
                target[bin] = value < 0 ? -magnitude : magnitude;
            }
        }
    }

    /// <summary>Fills one band of each window of a group with noise whose energy is the level squared.</summary>
    /// <param name="spectrum">The spectrum.</param>
    /// <param name="span">Where the band is.</param>
    /// <param name="level">The square root of the energy of the noise in a band.</param>
    /// <param name="state">The state of the noise generator.</param>
    /// <returns>The state of the noise generator after the noise.</returns>
    private static uint FillNoise(float[] spectrum, AacBandSpan span, float level, uint state)
    {
        for (var window = 0; window < span.Length; window++)
        {
            var target = spectrum.AsSpan(span.SpectrumStart(window), span.Width);
            var energy = 0F;
            for (var bin = 0; bin < target.Length; bin++)
            {
                state = (state * NoiseMultiplier) + NoiseIncrement;
                var value = (float)(int)state;
                target[bin] = value;
                energy += value * value;
            }

            var scale = energy > 0F ? level / MathF.Sqrt(energy) : 0F;
            for (var bin = 0; bin < target.Length; bin++)
            {
                target[bin] *= scale;
            }
        }

        return state;
    }

    /// <summary>Builds the table of gains.</summary>
    /// <returns>The table.</returns>
    private static float[] BuildGainTable()
    {
        var table = new float[GainTableSize];
        for (var slot = 0; slot < table.Length; slot++)
        {
            table[slot] = MathF.Pow(GainBase, StepExponent * (slot + GainTableStart));
        }

        return table;
    }

    /// <summary>Builds the table of values raised to the power of 4/3.</summary>
    /// <returns>The table.</returns>
    private static float[] BuildPowerTable()
    {
        var table = new float[PowerTableSize];
        for (var value = 0; value < table.Length; value++)
        {
            table[value] = MathF.Pow(value, Power);
        }

        return table;
    }
}
