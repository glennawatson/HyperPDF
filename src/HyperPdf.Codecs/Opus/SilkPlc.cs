// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>
/// Packet loss concealment for the SILK layer (RFC 6716 section 4.4): after each good frame the pitch, the long-term and short-term
/// filters and the gains are kept, and a lost frame is made by running those filters on noise or on the last pitch pulse, fading as the loss goes on.
/// </summary>
internal static class SilkPlc
{
    /// <summary>The number of levels of attenuation.</summary>
    private const int Attenuations = 2;

    /// <summary>The bandwidth expansion of the filter in each lost frame, 0.99 in Q16.</summary>
    private const int FilterExpansion = 64_881;

    /// <summary>The lowest pitch gain a concealed voiced frame starts with, 0.7 in Q14.</summary>
    private const int PitchGainStartMin = 11_469;

    /// <summary>The highest pitch gain a concealed voiced frame starts with, 0.95 in Q14.</summary>
    private const int PitchGainStartMax = 15_565;

    /// <summary>The highest pitch lag in milliseconds.</summary>
    private const int MaxPitchLagMs = 18;

    /// <summary>The samples of past excitation the noise is picked from.</summary>
    private const int RandBufferSize = 128;

    /// <summary>The mask of an index into the noise buffer.</summary>
    private const int RandBufferMask = RandBufferSize - 1;

    /// <summary>The log2 of the inverse filter gain above which the noise is not reduced.</summary>
    private const int InverseGainHighLog = 3;

    /// <summary>The log2 of the inverse filter gain below which the noise is not reduced further.</summary>
    private const int InverseGainLowLog = 8;

    /// <summary>The slow rise of the pitch lag in each subframe of a lost frame, 0.01 in Q16.</summary>
    private const int PitchDrift = 655;

    /// <summary>The lowest scale of the noise of a voiced frame, 0.2 in Q14.</summary>
    private const int MinRandScale = 3277;

    /// <summary>One, in Q14.</summary>
    private const int OneQ14 = 1 << 14;

    /// <summary>The bits of the gain in the concealed signal.</summary>
    private const int GainBits = 6;

    /// <summary>The samples of a subframe the state assumes before any frame is decoded.</summary>
    private const int InitialSubframeLength = 20;

    /// <summary>The fractional bits of the pitch lag.</summary>
    private const int LagBits = 8;

    /// <summary>The bits the frame length is shifted up by to give half a frame as the starting pitch lag, in Q8.</summary>
    private const int InitialLagShift = LagBits - 1;

    /// <summary>The fractional bits of the scale that raises a weak long-term filter.</summary>
    private const int LowScaleBits = 10;

    /// <summary>The fractional bits of the scale that lowers a strong long-term filter.</summary>
    private const int HighScaleBits = 14;

    /// <summary>The fractional bits the inverse of a gain is computed in.</summary>
    private const int ConcealInverseBits = 46;

    /// <summary>The bits the extended excitation is shifted up by.</summary>
    private const int ExcitationShift = 2;

    /// <summary>The bits of headroom the energy is normalised to before it is divided.</summary>
    private const int EnergyHeadroomBits = 24;

    /// <summary>The bits the root of the energy ratio is shifted up by to give a gain in Q16.</summary>
    private const int RatioShift = 4;

    /// <summary>The factor the slope of the fade-in is steepened by, as a shift.</summary>
    private const int SlopeShift = 2;

    /// <summary>The fractional bits of the fade-in gain.</summary>
    private const int FadeBits = 16;

    /// <summary>The fractional bits of the weights that fade the long-term filter and the noise.</summary>
    private const int FadeWeightBits = 15;

    /// <summary>The bits a scaled signal is shifted down by to reach the scale of the samples.</summary>
    private const int ScaleShift = 8;

    /// <summary>Gets how the long-term filter fades in each lost frame, in Q15: first lost frame then every one after.</summary>
    private static ReadOnlySpan<short> HarmonicAttenuation => [0x7EB8, 0x798A];

    /// <summary>Gets how the noise fades in each lost voiced frame, in Q15.</summary>
    private static ReadOnlySpan<short> VoicedNoiseAttenuation => [0x798A, 0x6666];

    /// <summary>Gets how the noise fades in each lost unvoiced frame, in Q15.</summary>
    private static ReadOnlySpan<short> UnvoicedNoiseAttenuation => [0x7EB8, 0x7333];

    /// <summary>Sets the concealment state to what it is before any frame.</summary>
    /// <param name="channel">The channel.</param>
    internal static void Reset(SilkChannel channel)
    {
        var plc = channel.Plc;
        plc.PitchLagQ8 = channel.FrameLength << InitialLagShift;
        plc.PreviousGains[0] = SilkChannel.UnityGain;
        plc.PreviousGains[1] = SilkChannel.UnityGain;
        plc.SubframeLength = InitialSubframeLength;
        plc.SubframeCount = SilkConstants.Pair;
    }

    /// <summary>Either keeps the parameters of a good frame, or makes up a lost frame.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="frame">The frame; for a lost frame it receives the concealed signal.</param>
    /// <param name="lost">Whether the frame is lost.</param>
    internal static void Process(SilkChannel channel, Span<short> frame, bool lost)
    {
        if (channel.SampleRateKhz != channel.Plc.SampleRateKhz)
        {
            Reset(channel);
            channel.Plc.SampleRateKhz = channel.SampleRateKhz;
        }

        if (lost)
        {
            Conceal(channel, frame);
            channel.LossCount++;
        }
        else
        {
            Update(channel);
        }
    }

    /// <summary>Blends a good frame in after a concealed one, so a sudden jump in loudness does not click.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="frame">The decoded frame, changed in place.</param>
    /// <param name="length">The samples of the frame.</param>
    internal static void GlueFrames(SilkChannel channel, Span<short> frame, int length)
    {
        var plc = channel.Plc;
        if (channel.LossCount != 0)
        {
            plc.ConcealedEnergy = SilkLpc.SumSquaresShift(frame[..length], out var shift);
            plc.ConcealedEnergyShift = shift;
            plc.LastFrameLost = true;
            return;
        }

        if (plc.LastFrameLost)
        {
            FadeIn(plc, frame, length);
        }

        plc.LastFrameLost = false;
    }

    /// <summary>Keeps the parameters of a good frame that a lost one is made from.</summary>
    /// <param name="channel">The channel.</param>
    private static void Update(SilkChannel channel)
    {
        var plc = channel.Plc;
        var parameters = channel.Parameters;
        var subframes = channel.SubframeCount;
        channel.PreviousSignalType = channel.Indices.SignalType;
        if (channel.Indices.SignalType == SilkConstants.TypeVoiced)
        {
            KeepPitch(channel);
        }
        else
        {
            plc.PitchLagQ8 = (channel.SampleRateKhz * MaxPitchLagMs) << LagBits;
            Array.Clear(plc.LtpCoefficients);
        }

        parameters.Predictors.AsSpan(SilkConstants.MaxLpcOrder, channel.LpcOrder).CopyTo(plc.PreviousLpc);
        plc.PreviousLtpScaleQ14 = parameters.LtpScaleQ14;
        plc.PreviousGains[0] = parameters.Gains[subframes - SilkConstants.Pair];
        plc.PreviousGains[1] = parameters.Gains[subframes - 1];
        plc.SubframeLength = channel.SubframeLength;
        plc.SubframeCount = subframes;
    }

    /// <summary>Finds the subframe with a pitch pulse that has the strongest long-term filter and keeps its pitch and filter.</summary>
    /// <param name="channel">The channel.</param>
    private static void KeepPitch(SilkChannel channel)
    {
        var plc = channel.Plc;
        var parameters = channel.Parameters;
        var subframes = channel.SubframeCount;
        var best = 0;
        for (var j = 0; j * channel.SubframeLength < parameters.PitchLags[subframes - 1] && j != subframes; j++)
        {
            var sum = 0;
            for (var i = 0; i < SilkConstants.LtpOrder; i++)
            {
                sum += parameters.LtpCoefficients[((subframes - 1 - j) * SilkConstants.LtpOrder) + i];
            }

            if (sum <= best)
            {
                continue;
            }

            best = sum;
            plc.PitchLagQ8 = parameters.PitchLags[subframes - 1 - j] << LagBits;
        }

        Array.Clear(plc.LtpCoefficients);
        plc.LtpCoefficients[SilkConstants.LtpCentre] = (short)best;
        if (best < PitchGainStartMin)
        {
            ScaleCoefficients(plc.LtpCoefficients, (PitchGainStartMin << LowScaleBits) / Math.Max(best, 1), LowScaleBits);
        }
        else if (best > PitchGainStartMax)
        {
            ScaleCoefficients(plc.LtpCoefficients, (PitchGainStartMax << HighScaleBits) / Math.Max(best, 1), HighScaleBits);
        }
    }

    /// <summary>Multiplies the long-term filter by a scale.</summary>
    /// <param name="coefficients">The filter, changed in place.</param>
    /// <param name="scale">The scale.</param>
    /// <param name="bits">The fractional bits of the scale.</param>
    private static void ScaleCoefficients(short[] coefficients, int scale, int bits)
    {
        for (var i = 0; i < coefficients.Length; i++)
        {
            coefficients[i] = (short)(SilkMath.MultiplyBottoms(coefficients[i], scale) >> bits);
        }
    }

    /// <summary>Makes up a lost frame.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="frame">Receives the signal.</param>
    private static void Conceal(SilkChannel channel, Span<short> frame)
    {
        var plc = channel.Plc;
        Span<int> previousGainQ10 = stackalloc int[SilkConstants.Pair];
        previousGainQ10[0] = plc.PreviousGains[0] >> GainBits;
        previousGainQ10[1] = plc.PreviousGains[1] >> GainBits;
        if (channel.FirstFrameAfterReset)
        {
            Array.Clear(plc.PreviousLpc);
        }

        var noiseStart = FindNoiseStart(channel, previousGainQ10);
        var attenuation = Math.Min(Attenuations - 1, channel.LossCount);
        int harmonic = HarmonicAttenuation[attenuation];
        int noiseGain = (channel.PreviousSignalType == SilkConstants.TypeVoiced ? VoicedNoiseAttenuation : UnvoicedNoiseAttenuation)[attenuation];
        SilkLpc.BandwidthExpand(plc.PreviousLpc, channel.LpcOrder, FilterExpansion);
        Span<short> filter = stackalloc short[SilkConstants.MaxLpcOrder];
        plc.PreviousLpc.AsSpan(0, channel.LpcOrder).CopyTo(filter);
        var randScale = plc.RandScaleQ14;
        if (channel.LossCount == 0)
        {
            randScale = FirstLossScale(channel, ref noiseGain);
        }

        var state = new ConcealState(randScale, noiseGain, harmonic, noiseStart);
        Synthesize(channel, frame, filter, state, previousGainQ10[1]);
    }

    /// <summary>Picks the quieter of the last two subframes of excitation as the source of noise.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="gainQ10">The gains of the last two subframes, in Q10.</param>
    /// <returns>Where in the excitation the noise is taken from.</returns>
    private static int FindNoiseStart(SilkChannel channel, ReadOnlySpan<int> gainQ10)
    {
        var plc = channel.Plc;
        var length = plc.SubframeLength;
        Span<short> scaled = stackalloc short[SilkConstants.Pair * SilkConstants.MaxSubframeLength];
        for (var k = 0; k < SilkConstants.Pair; k++)
        {
            for (var i = 0; i < length; i++)
            {
                var excitation = channel.Excitation[i + ((k + plc.SubframeCount - SilkConstants.Pair) * length)];
                scaled[(k * length) + i] = (short)SilkMath.Saturate16(SilkMath.MultiplyWord(excitation, gainQ10[k]) >> ScaleShift);
            }
        }

        var energy1 = SilkLpc.SumSquaresShift(scaled[..length], out var shift1);
        var energy2 = SilkLpc.SumSquaresShift(scaled.Slice(length, length), out var shift2);
        var subframe = (energy1 >> shift2) < (energy2 >> shift1) ? plc.SubframeCount - 1 : plc.SubframeCount;
        return Math.Max(0, (subframe * length) - RandBufferSize);
    }

    /// <summary>Works out the scale of the noise of the first lost frame, which depends on how voiced the last frame was.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="noiseGain">The attenuation of the noise, which is lowered for an unvoiced frame with a strong filter.</param>
    /// <returns>The scale of the noise, in Q14.</returns>
    private static int FirstLossScale(SilkChannel channel, ref int noiseGain)
    {
        var plc = channel.Plc;
        if (channel.PreviousSignalType == SilkConstants.TypeVoiced)
        {
            var scale = OneQ14;
            foreach (var tap in plc.LtpCoefficients)
            {
                scale -= tap;
            }

            scale = Math.Max(MinRandScale, scale);
            return (short)(SilkMath.MultiplyBottoms(scale, plc.PreviousLtpScaleQ14) >> 14);
        }

        var inverseGain = SilkLpc.InversePredictionGain(plc.PreviousLpc, channel.LpcOrder);
        var down = Math.Min((1 << 30) >> InverseGainHighLog, inverseGain);
        down = Math.Max((1 << 30) >> InverseGainLowLog, down);
        down = SilkMath.ShiftLeft(down, InverseGainHighLog);
        noiseGain = SilkMath.MultiplyWordBottom(down, noiseGain) >> 14;
        return OneQ14;
    }

    /// <summary>Runs the long-term filter on noise, then the short-term filter on that, to make the concealed frame.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="frame">Receives the signal.</param>
    /// <param name="filter">The short-term filter, in Q12.</param>
    /// <param name="state">How the noise and the long-term filter fade.</param>
    /// <param name="gainQ10">The gain of the last subframe, in Q10.</param>
    private static void Synthesize(SilkChannel channel, Span<short> frame, ReadOnlySpan<short> filter, ConcealState state, int gainQ10)
    {
        var plc = channel.Plc;
        var memory = channel.LtpMemoryLength;
        Span<short> whitened = stackalloc short[SilkConstants.MaxFrameLength];
        Span<int> signal = stackalloc int[SilkConstants.Pair * SilkConstants.MaxFrameLength];
        var lag = SilkMath.RoundShift(plc.PitchLagQ8, LagBits);
        var start = memory - lag - channel.LpcOrder - SilkConstants.LtpCentre;
        SilkLpc.AnalysisFilter(whitened[start..], channel.OutputBuffer.AsSpan(start), filter, memory - start, channel.LpcOrder);
        var inverseGain = Math.Min(SilkMath.InvertScaled(plc.PreviousGains[1], ConcealInverseBits), int.MaxValue >> 1);
        for (var i = start + channel.LpcOrder; i < memory; i++)
        {
            signal[i] = SilkMath.MultiplyWordBottom(inverseGain, whitened[i]);
        }

        var seed = plc.RandSeed;
        var randScale = state.RandScale;
        var position = memory;
        for (var k = 0; k < channel.SubframeCount; k++)
        {
            seed = ExtendExcitation(channel, signal, ref position, lag, new(seed, randScale, state.NoiseStart));
            FadeSubframe(plc, state.HarmonicGain);
            randScale = SilkMath.MultiplyBottoms(randScale, state.NoiseGain) >> FadeWeightBits;
            plc.PitchLagQ8 = Math.Min(SilkMath.MultiplyAddWordBottom(plc.PitchLagQ8, plc.PitchLagQ8, PitchDrift), (MaxPitchLagMs * channel.SampleRateKhz) << LagBits);
            lag = SilkMath.RoundShift(plc.PitchLagQ8, LagBits);
        }

        SynthesizeShortTerm(channel, frame, signal, filter, gainQ10);
        plc.RandSeed = seed;
        plc.RandScaleQ14 = randScale;
        Array.Fill(channel.Parameters.PitchLags, lag);
    }

    /// <summary>Applies the long-term filter to noise for one subframe, extending the signal.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="signal">The signal, extended in place.</param>
    /// <param name="position">Where the next sample goes; advanced.</param>
    /// <param name="lag">The pitch lag.</param>
    /// <param name="step">The noise generator's seed, the scale of the noise and where the noise is taken from.</param>
    /// <returns>The generator's seed after the subframe.</returns>
    private static int ExtendExcitation(SilkChannel channel, Span<int> signal, ref int position, int lag, ExcitationStep step)
    {
        var plc = channel.Plc;
        var seed = step.Seed;
        var ahead = position - lag + SilkConstants.LtpCentre;
        for (var i = 0; i < channel.SubframeLength; i++)
        {
            var prediction = 2;
            for (var t = 0; t < SilkConstants.LtpOrder; t++)
            {
                prediction = SilkMath.MultiplyAddWordBottom(prediction, signal[ahead + i - t], plc.LtpCoefficients[t]);
            }

            seed = SilkMath.Rand(seed);
            var noise = channel.Excitation[step.NoiseStart + ((seed >> 25) & RandBufferMask)];
            signal[position] = SilkMath.ShiftLeft(SilkMath.MultiplyAddWordBottom(prediction, noise, step.Scale), ExcitationShift);
            position++;
        }

        return seed;
    }

    /// <summary>Lowers the long-term filter for the next subframe.</summary>
    /// <param name="plc">The concealment state.</param>
    /// <param name="harmonicGain">The attenuation, in Q15.</param>
    private static void FadeSubframe(SilkPlcState plc, int harmonicGain)
    {
        for (var j = 0; j < SilkConstants.LtpOrder; j++)
        {
            plc.LtpCoefficients[j] = (short)(SilkMath.MultiplyBottoms(harmonicGain, plc.LtpCoefficients[j]) >> FadeWeightBits);
        }
    }

    /// <summary>Runs the short-term filter over the concealed excitation and scales the result.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="frame">Receives the signal.</param>
    /// <param name="signal">The excitation, which also holds the filter's state.</param>
    /// <param name="filter">The filter, in Q12.</param>
    /// <param name="gainQ10">The gain, in Q10.</param>
    private static void SynthesizeShortTerm(SilkChannel channel, Span<short> frame, Span<int> signal, ReadOnlySpan<short> filter, int gainQ10)
    {
        var order = channel.LpcOrder;
        var state = signal[(channel.LtpMemoryLength - SilkConstants.MaxLpcOrder)..];
        channel.LpcState.CopyTo(state);
        for (var i = 0; i < channel.FrameLength; i++)
        {
            var prediction = order >> 1;
            for (var j = 0; j < order; j++)
            {
                prediction = SilkMath.MultiplyAddWordBottom(prediction, state[SilkConstants.MaxLpcOrder + i - 1 - j], filter[j]);
            }

            state[SilkConstants.MaxLpcOrder + i] += prediction << 4;
            frame[i] = (short)SilkMath.Saturate16(SilkMath.RoundShift(SilkMath.MultiplyWord(state[SilkConstants.MaxLpcOrder + i], gainQ10), ScaleShift));
        }

        state.Slice(channel.FrameLength, SilkConstants.MaxLpcOrder).CopyTo(channel.LpcState);
    }

    /// <summary>Fades a good frame in from the level of the concealed signal before it when it is louder.</summary>
    /// <param name="plc">The concealment state.</param>
    /// <param name="frame">The decoded frame, changed in place.</param>
    /// <param name="length">The samples of the frame.</param>
    private static void FadeIn(SilkPlcState plc, Span<short> frame, int length)
    {
        var energy = SilkLpc.SumSquaresShift(frame[..length], out var shift);
        var concealed = plc.ConcealedEnergy;
        if (shift > plc.ConcealedEnergyShift)
        {
            concealed >>= shift - plc.ConcealedEnergyShift;
        }
        else if (shift < plc.ConcealedEnergyShift)
        {
            energy >>= plc.ConcealedEnergyShift - shift;
        }

        if (energy <= concealed)
        {
            return;
        }

        var zeros = SilkMath.LeadingZeros(concealed) - 1;
        concealed = SilkMath.ShiftLeft(concealed, zeros);
        energy >>= Math.Max(EnergyHeadroomBits - zeros, 0);
        var fraction = concealed / Math.Max(energy, 1);
        var gain = SilkMath.ShiftLeft(SilkMath.SquareRootApproximation(fraction), RatioShift);
        var slope = ((1 << FadeBits) - gain) / length;
        slope = SilkMath.ShiftLeft(slope, SlopeShift);
        for (var i = 0; i < length; i++)
        {
            frame[i] = (short)SilkMath.MultiplyWordBottom(gain, frame[i]);
            gain += slope;
            if (gain > 1 << FadeBits)
            {
                break;
            }
        }
    }

    /// <summary>How the noise and the long-term filter fade over a lost frame.</summary>
    /// <param name="RandScale">The scale of the noise, in Q14.</param>
    /// <param name="NoiseGain">How the noise fades in each subframe, in Q15.</param>
    /// <param name="HarmonicGain">How the long-term filter fades in each subframe, in Q15.</param>
    /// <param name="NoiseStart">Where in the excitation the noise is taken from.</param>
    private readonly record struct ConcealState(int RandScale, int NoiseGain, int HarmonicGain, int NoiseStart);

    /// <summary>What a subframe of excitation needs to know about the noise.</summary>
    /// <param name="Seed">The noise generator's seed.</param>
    /// <param name="Scale">The scale of the noise, in Q14.</param>
    /// <param name="NoiseStart">Where in the excitation the noise is taken from.</param>
    private readonly record struct ExcitationStep(int Seed, int Scale, int NoiseStart);
}
