// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>
/// Stops the energy of a band collapsing in a frame of short blocks (RFC 6716 section 4.3.5): a short block that got no pulses is filled
/// with noise as loud as the quietest recent frame suggests, so a gap does not open in the sound.
/// </summary>
internal static class CeltAntiCollapse
{
    /// <summary>The slope at which the noise falls off with the bits the band had per entry.</summary>
    private const float DepthSlope = 0.125F;

    /// <summary>The noise level before it is scaled by the band's size.</summary>
    private const float NoiseHalf = 0.5F;

    /// <summary>The factor the noise is doubled by, since short blocks do not have the same energy as long ones.</summary>
    private const float ShortBlockGain = 2F;

    /// <summary>The extra gain of the longest short block frame.</summary>
    private const float LongestShortGain = 1.41421356F;

    /// <summary>The frame size, as a log2, that has the extra gain.</summary>
    private const int LongestFrameSize = 3;

    /// <summary>The bit of the seed that makes the sign of a noise entry.</summary>
    private const uint SignBit = 0x8000;

    /// <summary>Fills the short blocks that got no pulses with noise.</summary>
    /// <param name="frame">The frame, whose bands, channels, size and allocation say where to look.</param>
    /// <param name="spectrum">The normalised spectrum of the channels, one after another, each <paramref name="size"/> long.</param>
    /// <param name="masks">The collapse masks, a byte for each band and channel.</param>
    /// <param name="energy">The energy state: the current energies and the two frames before.</param>
    /// <param name="seed">The noise generator's seed.</param>
    /// <param name="size">The entries in a channel of the frame.</param>
    internal static void Apply(CeltFrame frame, Span<float> spectrum, ReadOnlySpan<byte> masks, CeltEnergyState energy, uint seed, int size)
    {
        var edges = CeltTables.BandEdges;
        var channels = frame.Channels;
        var lm = frame.FrameSize;
        for (var band = frame.Start; band < frame.End; band++)
        {
            var entries = edges[band + 1] - edges[band];
            var depth = (1 + frame.Pulses[band]) / (entries << lm);
            var thresh = NoiseHalf * float.Exp2(-DepthSlope * depth);
            var scale = 1F / MathF.Sqrt(entries << lm);
            for (var channel = 0; channel < channels; channel++)
            {
                var level = Math.Min(thresh, GetNoiseLevel(frame, energy, band, channel)) * scale;
                var at = (channel * size) + (edges[band] << lm);
                seed = FillCollapsed(spectrum.Slice(at, entries << lm), masks[(band * channels) + channel], entries, level, seed);
            }
        }
    }

    /// <summary>Works out how loud the noise for a band should be from how much its energy rose over the quietest recent frame.</summary>
    /// <param name="frame">The frame.</param>
    /// <param name="energy">The energy state.</param>
    /// <param name="band">The band.</param>
    /// <param name="channel">The channel.</param>
    /// <returns>The level, before it is limited by the band's bits.</returns>
    private static float GetNoiseLevel(CeltFrame frame, CeltEnergyState energy, int band, int channel)
    {
        var at = (channel * CeltEnergy.Bands) + band;
        var previous = energy.Previous[at];
        var beforePrevious = energy.BeforePrevious[at];
        if (frame.Channels == 1)
        {
            previous = Math.Max(previous, energy.Previous[CeltEnergy.Bands + band]);
            beforePrevious = Math.Max(beforePrevious, energy.BeforePrevious[CeltEnergy.Bands + band]);
        }

        var difference = Math.Max(0F, energy.Current[at] - Math.Min(previous, beforePrevious));
        var level = ShortBlockGain * float.Exp2(-difference);
        return frame.FrameSize == LongestFrameSize ? level * LongestShortGain : level;
    }

    /// <summary>Fills the blocks of a band that have no pulse with noise of a level, then renormalises the band.</summary>
    /// <param name="band">The band's entries, the blocks interleaved.</param>
    /// <param name="mask">The collapse mask: a bit for each block that has a pulse.</param>
    /// <param name="entries">The entries in each block.</param>
    /// <param name="level">The size of the noise.</param>
    /// <param name="seed">The noise generator's seed.</param>
    /// <returns>The seed after the noise.</returns>
    private static uint FillCollapsed(Span<float> band, byte mask, int entries, float level, uint seed)
    {
        var blocks = band.Length / entries;
        var changed = false;
        for (var k = 0; k < blocks; k++)
        {
            if ((mask & (1 << k)) != 0)
            {
                continue;
            }

            for (var j = 0; j < entries; j++)
            {
                seed = CeltBandMath.NextRandom(seed);
                band[(j * blocks) + k] = (seed & SignBit) != 0 ? level : -level;
            }

            changed = true;
        }

        if (changed)
        {
            CeltPvq.Renormalise(band, 1F);
        }

        return seed;
    }
}
