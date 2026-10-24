// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>The pitch comb filter and the de-emphasis that finish the signal of a CELT block (RFC 6716 sections 4.3.7.1 and 4.3.7.2).</summary>
internal static class CeltPostFilter
{
    /// <summary>The lowest pitch period the filter is run with.</summary>
    internal const int MinimumPeriod = 15;

    /// <summary>The factor that brings the decoder's signal to the range of -1 to 1.</summary>
    private const float OutputScale = 1F / 32768F;

    /// <summary>The coefficient of the de-emphasis filter.</summary>
    private const float DeemphasisCoefficient = 0.85000610F;

    /// <summary>Filters a stretch of the signal in place, fading from one set of parameters to another over the overlap.</summary>
    /// <param name="signal">The signal, with the samples before <paramref name="start"/> being the history the filter reads.</param>
    /// <param name="start">The index of the first sample to filter.</param>
    /// <param name="count">The samples to filter.</param>
    /// <param name="from">The parameters the filter fades from.</param>
    /// <param name="to">The parameters the filter fades to.</param>
    internal static void Apply(Span<float> signal, int start, int count, CeltPostFilterSettings from, CeltPostFilterSettings to)
    {
        if (from.Gain == 0F && to.Gain == 0F)
        {
            return;
        }

        var overlap = Math.Min(CeltDecoder.Overlap, count);
        var older = new TapGains(from);
        var newer = new TapGains(to);
        var window = CeltFloatTables.Window;
        for (var i = 0; i < overlap; i++)
        {
            var f = window[i] * window[i];
            var at = start + i;
            signal[at] += (((1F - f) * older.Sum(signal, at, from.Period)) + (f * newer.Sum(signal, at, to.Period)));
        }

        for (var i = overlap; i < count; i++)
        {
            var at = start + i;
            signal[at] += newer.Sum(signal, at, to.Period);
        }
    }

    /// <summary>Runs the de-emphasis filter over one channel and stores the samples scaled to the range of -1 to 1.</summary>
    /// <param name="input">The channel's signal.</param>
    /// <param name="pcm">Receives the samples, with the other channels' samples between them.</param>
    /// <param name="offset">The index of the channel's first sample in <paramref name="pcm"/>.</param>
    /// <param name="channels">The channels in <paramref name="pcm"/>.</param>
    /// <param name="memory">The filter's memory, updated.</param>
    internal static void Deemphasis(ReadOnlySpan<float> input, Span<float> pcm, int offset, int channels, ref float memory)
    {
        var m = memory;
        for (var j = 0; j < input.Length; j++)
        {
            var sample = input[j] + m;
            m = DeemphasisCoefficient * sample;
            pcm[offset + (j * channels)] = sample * OutputScale;
        }

        memory = m;
    }

    /// <summary>The three tap gains of a comb filter.</summary>
    private readonly struct TapGains
    {
        /// <summary>The taps in a tap set.</summary>
        private const int Taps = 3;

        /// <summary>How far the outer taps are from the centre.</summary>
        private const int SideReach = 2;

        /// <summary>The gain of the centre tap.</summary>
        private readonly float _centre;

        /// <summary>The gain of the taps one either side of the centre.</summary>
        private readonly float _near;

        /// <summary>The gain of the taps two either side of the centre.</summary>
        private readonly float _far;

        /// <summary>Whether the filter has no gain, so its taps need not be read.</summary>
        private readonly bool _silent;

        /// <summary>Initializes a new instance of the <see cref="TapGains"/> struct.</summary>
        /// <param name="settings">The filter's parameters.</param>
        internal TapGains(CeltPostFilterSettings settings)
        {
            _silent = settings.Gain == 0F;
            var row = settings.Tapset * Taps;
            _centre = settings.Gain * CeltFloatTables.CombGains[row];
            _near = settings.Gain * CeltFloatTables.CombGains[row + 1];
            _far = settings.Gain * CeltFloatTables.CombGains[row + SideReach];
        }

        /// <summary>Adds up the taps around the sample a period back.</summary>
        /// <param name="signal">The signal.</param>
        /// <param name="at">The index of the sample.</param>
        /// <param name="period">The period.</param>
        /// <returns>The sum.</returns>
        internal float Sum(ReadOnlySpan<float> signal, int at, int period)
        {
            var back = at - period;
            return _silent
                ? 0F
                : (_centre * signal[back])
                    + (_near * (signal[back - 1] + signal[back + 1]))
                    + (_far * (signal[back - SideReach] + signal[back + SideReach]));
        }
    }
}
