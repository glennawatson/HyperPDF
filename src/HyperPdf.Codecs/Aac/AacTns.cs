// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Aac;

/// <summary>Temporal noise shaping: filters the spectrum with all-pole filters, which undoes the shaping the encoder applied.</summary>
internal static class AacTns
{
    /// <summary>The largest filter order of a long window.</summary>
    private const int LongOrder = 20;

    /// <summary>The largest filter order of a short window.</summary>
    private const int ShortOrder = 7;

    /// <summary>The largest filter order the coefficients can hold, plus the leading 1.</summary>
    private const int FilterSize = AacChannel.TnsOrder + 1;

    /// <summary>A quarter turn, the angle whose sine is the largest reflection coefficient.</summary>
    private const float QuarterTurn = MathF.PI / 2F;

    /// <summary>Half a step.</summary>
    private const float HalfStep = 0.5F;

    /// <summary>Filters the windows of a channel that have TNS.</summary>
    /// <param name="channel">The channel, with its spectrum filled.</param>
    /// <param name="offsets">The band edges of the window size.</param>
    /// <param name="rateIndex">The sample rate index.</param>
    internal static void Apply(AacChannel channel, short[] offsets, int rateIndex)
    {
        var isShort = channel.IsShort;
        var windowSize = isShort ? AacChannel.ShortSize : AacChannel.LongSize;
        var limit = isShort ? AacBands.TnsShortBands[rateIndex] : AacBands.TnsLongBands[rateIndex];
        var maxOrder = isShort ? ShortOrder : LongOrder;
        var bands = offsets.Length - 1;
        Span<float> filter = stackalloc float[FilterSize];
        for (var window = 0; window < channel.WindowCount; window++)
        {
            var top = bands;
            for (var index = 0; index < channel.TnsFilterCount[window]; index++)
            {
                var slot = (window * AacChannel.TnsFilters) + index;
                var bottom = Math.Max(top - channel.TnsLength[slot], 0);
                var startBand = Math.Min(Math.Min(bottom, limit), channel.MaxSfb);
                var endBand = Math.Min(Math.Min(top, limit), channel.MaxSfb);
                top = bottom;
                var order = Math.Min(channel.TnsFilterOrder[slot], maxOrder);
                var size = offsets[endBand] - offsets[startBand];
                if (order == 0 || size <= 0)
                {
                    continue;
                }

                BuildFilter(channel.TnsCoefficients.AsSpan(slot * AacChannel.TnsOrder, order), channel.TnsResolution[window], filter);
                var range = channel.Spectrum.AsSpan((window * windowSize) + offsets[startBand], size);
                Run(range, filter[..(order + 1)], channel.TnsDownward[slot]);
            }
        }
    }

    /// <summary>Makes the filter coefficients from the quantised reflection coefficients.</summary>
    /// <param name="coefficients">The quantised coefficients.</param>
    /// <param name="resolution">The bits a coefficient has before compression.</param>
    /// <param name="filter">Receives the filter, whose first element is 1.</param>
    private static void BuildFilter(ReadOnlySpan<sbyte> coefficients, int resolution, Span<float> filter)
    {
        var half = 1 << (resolution - 1);
        var positive = (half - HalfStep) / QuarterTurn;
        var negative = (half + HalfStep) / QuarterTurn;
        filter.Clear();
        filter[0] = 1F;
        Span<float> previous = stackalloc float[FilterSize];
        for (var order = 1; order <= coefficients.Length; order++)
        {
            var value = coefficients[order - 1];
            var reflection = MathF.Sin(value / (value >= 0 ? positive : negative));
            filter[..order].CopyTo(previous);
            for (var index = 1; index < order; index++)
            {
                filter[index] = previous[index] + (reflection * previous[order - index]);
            }

            filter[order] = reflection;
        }
    }

    /// <summary>Runs an all-pole filter over a range of the spectrum in place.</summary>
    /// <param name="range">The coefficients.</param>
    /// <param name="filter">The filter, whose first element is 1.</param>
    /// <param name="downward">Whether to run from the last coefficient to the first.</param>
    private static void Run(Span<float> range, ReadOnlySpan<float> filter, bool downward)
    {
        var order = filter.Length - 1;
        if (downward)
        {
            for (var index = range.Length - 1; index >= 0; index--)
            {
                var sum = range[index];
                for (var tap = 1; tap <= order && index + tap < range.Length; tap++)
                {
                    sum -= filter[tap] * range[index + tap];
                }

                range[index] = sum;
            }
        }
        else
        {
            for (var index = 1; index < range.Length; index++)
            {
                var sum = range[index];
                for (var tap = 1; tap <= order && tap <= index; tap++)
                {
                    sum -= filter[tap] * range[index - tap];
                }

                range[index] = sum;
            }
        }
    }
}
