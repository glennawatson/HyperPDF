// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using HyperPdf.Core.Speech;

namespace HyperPdf.Core.Tests.Speech.Listening;

/// <summary>
/// What a listener notices about one spoken sentence: how loud the voiced part is, its peak, how fast it is spoken,
/// how much silence surrounds it, and the longest gap inside it.
/// </summary>
/// <param name="Level">The loudness of the voiced audio in dB below full scale.</param>
/// <param name="Peak">The largest sample.</param>
/// <param name="Rate">Letters spoken per second of voiced audio.</param>
/// <param name="LeadingSilence">Seconds of silence before the voice starts.</param>
/// <param name="TrailingSilence">Seconds of silence after it stops.</param>
/// <param name="LongestGap">The longest silence inside the sentence, in seconds.</param>
/// <param name="IsFinite">Whether every sample is a real number.</param>
[DebuggerDisplay("SpeechMeasurement: {ToString()}")]
public readonly record struct SpeechMeasurement(double Level, float Peak, double Rate, double LeadingSilence, double TrailingSilence, double LongestGap, bool IsFinite)
{
    /// <summary>Samples per analysis window: 20 ms at 24 kHz.</summary>
    private const int Window = 480;

    /// <summary>A window quieter than this is silence.</summary>
    private const double SilenceLevel = 0.01;

    /// <summary>The level of full scale, for decibels.</summary>
    private const double DecibelScale = 20;

    /// <summary>Measures a sentence.</summary>
    /// <param name="audio">The audio.</param>
    /// <param name="letters">The letters in the sentence as spoken.</param>
    /// <returns>The measurement.</returns>
    public static SpeechMeasurement Measure(SpeechAudio audio, int letters)
    {
        ArgumentNullException.ThrowIfNull(audio);
        var samples = audio.Samples;
        var windows = samples.Length / Window;
        var finite = Array.TrueForAll(samples, float.IsFinite);
        var peak = 0F;
        foreach (var sample in samples)
        {
            peak = Math.Max(peak, Math.Abs(sample));
        }

        var first = -1;
        var last = -1;
        var energy = 0.0;
        var voiced = 0;
        var gap = 0;
        var longest = 0;
        for (var w = 0; w < windows; w++)
        {
            var rms = Rms(samples.AsSpan(w * Window, Window));
            if (rms < SilenceLevel)
            {
                gap += first >= 0 ? 1 : 0;
                continue;
            }

            first = first < 0 ? w : first;
            last = w;
            longest = Math.Max(longest, gap);
            gap = 0;
            energy += rms * rms;
            voiced++;
        }

        var seconds = (double)Window / audio.SampleRate;
        var level = voiced == 0 ? double.NegativeInfinity : DecibelScale * Math.Log10(Math.Sqrt(energy / voiced));
        var rate = voiced == 0 ? 0 : letters / (voiced * seconds);
        var leading = first < 0 ? windows * seconds : first * seconds;
        var trailing = last < 0 ? 0 : (windows - last - 1) * seconds;
        return new(level, peak, rate, leading, trailing, longest * seconds, finite);
    }

    /// <inheritdoc/>
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"{Level:0.0} dBFS, peak {Peak:0.00}, {Rate:0.0} letters/s, silence {LeadingSilence:0.00}s/{TrailingSilence:0.00}s, gap {LongestGap:0.00}s");

    /// <summary>Gets the root mean square of a window.</summary>
    /// <param name="window">The samples.</param>
    /// <returns>The level.</returns>
    private static double Rms(ReadOnlySpan<float> window)
    {
        var sum = 0.0;
        foreach (var sample in window)
        {
            sum += sample * sample;
        }

        return Math.Sqrt(sum / window.Length);
    }
}
