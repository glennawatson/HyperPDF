// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>
/// Comfort noise for the SILK layer: while frames arrive without voice activity it keeps a smoothed estimate of the background's spectrum
/// and loudness, and when a frame is lost it plays noise made from that estimate.
/// </summary>
internal static class SilkCng
{
    /// <summary>The seed the noise generator starts from.</summary>
    private const int InitialSeed = 3_176_576;

    /// <summary>The smoothing of the spectrum estimate, 0.25 in Q16.</summary>
    private const int FrequencySmoothing = 16_348;

    /// <summary>The smoothing of the gain estimate, 0.25 to the power of a quarter in Q16.</summary>
    private const int GainSmoothing = 4_634;

    /// <summary>The largest index of the excitation buffer, as a mask.</summary>
    private const int BufferMask = 255;

    /// <summary>The bits of the generator's number that pick an excitation sample.</summary>
    private const int IndexShift = 24;

    /// <summary>The bits the gain is shifted down by before it scales the excitation.</summary>
    private const int GainShift = 4;

    /// <summary>The bits of the filtered signal.</summary>
    private const int SignalBits = 4;

    /// <summary>The bits the synthesised sample is shifted down by to reach 16 bits.</summary>
    private const int OutputShift = 6;

    /// <summary>Spreads the smoothed spectrum evenly and clears the smoothed gain.</summary>
    /// <param name="channel">The channel.</param>
    internal static void Reset(SilkChannel channel)
    {
        var cng = channel.Cng;
        var step = short.MaxValue / (channel.LpcOrder + 1);
        var accumulated = 0;
        for (var i = 0; i < channel.LpcOrder; i++)
        {
            accumulated += step;
            cng.SmoothedFrequencies[i] = (short)accumulated;
        }

        cng.SmoothedGainQ16 = 0;
        cng.RandSeed = InitialSeed;
    }

    /// <summary>Updates the background estimate from a frame without voice activity, and adds comfort noise when the packet was lost.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="frame">The decoded frame, to which the noise is added.</param>
    /// <param name="length">The samples of the frame.</param>
    internal static void Apply(SilkChannel channel, Span<short> frame, int length)
    {
        var cng = channel.Cng;
        if (channel.SampleRateKhz != cng.SampleRateKhz)
        {
            Reset(channel);
            cng.SampleRateKhz = channel.SampleRateKhz;
        }

        if (channel.LossCount == 0 && channel.PreviousSignalType == SilkConstants.TypeNoVoiceActivity)
        {
            Update(channel);
        }

        if (channel.LossCount != 0)
        {
            AddNoise(channel, frame, length);
        }
        else
        {
            cng.SynthesisState.AsSpan(0, channel.LpcOrder).Clear();
        }
    }

    /// <summary>Folds a frame without voice activity into the background estimate.</summary>
    /// <param name="channel">The channel.</param>
    private static void Update(SilkChannel channel)
    {
        var cng = channel.Cng;
        var gains = channel.Parameters.Gains;
        for (var i = 0; i < channel.LpcOrder; i++)
        {
            cng.SmoothedFrequencies[i] += (short)SilkMath.MultiplyWordBottom(channel.PreviousNlsf[i] - cng.SmoothedFrequencies[i], FrequencySmoothing);
        }

        var loudest = FindLoudest(gains, channel.SubframeCount);
        var length = channel.SubframeLength;
        cng.Excitation.AsSpan(0, (channel.SubframeCount - 1) * length).CopyTo(cng.Excitation.AsSpan(length));
        channel.Excitation.AsSpan(loudest * length, length).CopyTo(cng.Excitation);
        for (var i = 0; i < channel.SubframeCount; i++)
        {
            cng.SmoothedGainQ16 += SilkMath.MultiplyWordBottom(gains[i] - cng.SmoothedGainQ16, GainSmoothing);
        }
    }

    /// <summary>Finds the subframe with the highest gain.</summary>
    /// <param name="gains">The gains of the subframes.</param>
    /// <param name="count">The subframes.</param>
    /// <returns>The index of the loudest subframe; the first if all are zero.</returns>
    private static int FindLoudest(ReadOnlySpan<int> gains, int count)
    {
        var loudest = 0;
        var maxGain = 0;
        for (var i = 0; i < count; i++)
        {
            if (gains[i] <= maxGain)
            {
                continue;
            }

            maxGain = gains[i];
            loudest = i;
        }

        return loudest;
    }

    /// <summary>Synthesises noise from the background estimate and adds it to a frame.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="length">The samples of the frame.</param>
    private static void AddNoise(SilkChannel channel, Span<short> frame, int length)
    {
        var cng = channel.Cng;
        var order = channel.LpcOrder;
        Span<int> signal = stackalloc int[SilkConstants.MaxFrameLength + SilkConstants.MaxLpcOrder];
        Span<short> filter = stackalloc short[SilkConstants.MaxLpcOrder];
        GenerateExcitation(cng, signal[SilkConstants.MaxLpcOrder..], length);
        SilkNlsf.ToFilter(filter, cng.SmoothedFrequencies, order);
        cng.SynthesisState.AsSpan(0, SilkConstants.MaxLpcOrder).CopyTo(signal);
        for (var i = 0; i < length; i++)
        {
            var sum = order >> 1;
            for (var j = 0; j < order; j++)
            {
                sum = SilkMath.MultiplyAddWordBottom(sum, signal[SilkConstants.MaxLpcOrder + i - 1 - j], filter[j]);
            }

            signal[SilkConstants.MaxLpcOrder + i] += sum << SignalBits;
            frame[i] = (short)SilkMath.Saturate16(frame[i] + SilkMath.RoundShift(sum, OutputShift));
        }

        signal.Slice(length, SilkConstants.MaxLpcOrder).CopyTo(cng.SynthesisState);
    }

    /// <summary>Picks random samples of the excitation buffer, scaled by the smoothed gain.</summary>
    /// <param name="cng">The comfort noise state.</param>
    /// <param name="residual">Receives the excitation.</param>
    /// <param name="length">The samples.</param>
    private static void GenerateExcitation(SilkCngState cng, Span<int> residual, int length)
    {
        var mask = BufferMask;
        while (mask > length)
        {
            mask >>= 1;
        }

        var seed = cng.RandSeed;
        for (var i = 0; i < length; i++)
        {
            seed = SilkMath.Rand(seed);
            residual[i] = (short)SilkMath.Saturate16(SilkMath.MultiplyWord(cng.Excitation[(seed >> IndexShift) & mask], cng.SmoothedGainQ16 >> GainShift));
        }

        cng.RandSeed = seed;
    }
}
