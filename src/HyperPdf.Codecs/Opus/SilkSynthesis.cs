// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>
/// Turns the excitation of a SILK frame into speech (RFC 6716 section 4.2.7.9): the pulses become a noisy excitation, the long-term
/// filter adds back the pitch, and the short-term filter shapes the spectrum, one subframe at a time.
/// </summary>
internal static class SilkSynthesis
{
    /// <summary>The offset of the quantisation levels towards zero, in Q14.</summary>
    private const int LevelAdjust = SilkConstants.QuantLevelAdjust << 4;

    /// <summary>The bits the pulses are shifted up by to reach Q14.</summary>
    private const int PulseShift = 14;

    /// <summary>The bits the quantisation offset is shifted up by to reach Q14.</summary>
    private const int OffsetShift = 4;

    /// <summary>The weight of the middle tap of the long-term filter used to ease from concealed voiced speech into unvoiced speech, 0.25 in Q14.</summary>
    private const int EaseTap = 4096;

    /// <summary>The bits of a gain in Q16 shifted down to Q10.</summary>
    private const int GainShift = 6;

    /// <summary>The fractional bits of the inverse gain.</summary>
    private const int InverseGainBits = 47;

    /// <summary>The fractional bits of a gain change.</summary>
    private const int GainChangeBits = 16;

    /// <summary>The bits the inverse gain is scaled down by to avoid overflow in the long-term prediction.</summary>
    private const int LtpScaleShift = 2;

    /// <summary>The rounding offset of the long-term prediction sum, which keeps the rounding of the multiplies from biasing it down.</summary>
    private const int LtpRounding = 2;

    /// <summary>The bits of the short-term prediction before it is added to the excitation.</summary>
    private const int PredictionShift = 4;

    /// <summary>The bits the synthesised sample is shifted down by to reach 16 bits.</summary>
    private const int OutputShift = 8;

    /// <summary>The subframe from which a frame can no longer ease out of concealed voiced speech.</summary>
    private const int EaseSubframes = SilkConstants.MaxSubframes / 2;

    /// <summary>Gets the quantisation offsets in Q10 for unvoiced then voiced frames, each with a low and a high type.</summary>
    private static ReadOnlySpan<short> QuantizationOffsets => [0x0064, 0x00F0, 0x0020, 0x0064];

    /// <summary>Decodes a frame from its pulses.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="output">Receives the speech.</param>
    /// <param name="scratch">The arrays the synthesis works in.</param>
    internal static void Decode(SilkChannel channel, Span<short> output, SilkSynthesisScratch scratch)
    {
        GenerateExcitation(channel, scratch.Pulses);
        channel.LpcState.CopyTo(scratch.ShortTermState, 0);
        var position = channel.LtpMemoryLength;
        for (var k = 0; k < channel.SubframeCount; k++)
        {
            DecodeSubframe(channel, scratch, k, output, ref position);
            scratch.ShortTermState.AsSpan(channel.SubframeLength, SilkConstants.MaxLpcOrder).CopyTo(scratch.ShortTermState);
        }

        scratch.ShortTermState.AsSpan(0, SilkConstants.MaxLpcOrder).CopyTo(channel.LpcState);
    }

    /// <summary>Makes the excitation of a frame from its pulses: each is scaled, given a bias and flipped at random.</summary>
    /// <param name="channel">The channel, whose excitation is filled in.</param>
    /// <param name="pulses">The pulses.</param>
    private static void GenerateExcitation(SilkChannel channel, ReadOnlySpan<int> pulses)
    {
        var indices = channel.Indices;
        var offset = QuantizationOffsets[((indices.SignalType >> 1) * SilkConstants.Pair) + indices.QuantOffsetType];
        var seed = indices.Seed;
        for (var i = 0; i < channel.FrameLength; i++)
        {
            seed = SilkMath.Rand(seed);
            var excitation = SilkMath.ShiftLeft(pulses[i], PulseShift);
            if (excitation > 0)
            {
                excitation -= LevelAdjust;
            }
            else if (excitation < 0)
            {
                excitation += LevelAdjust;
            }

            excitation += offset << OffsetShift;
            channel.Excitation[i] = seed < 0 ? -excitation : excitation;
            seed = unchecked(seed + pulses[i]);
        }
    }

    /// <summary>Decodes one subframe.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="scratch">The arrays the synthesis works in.</param>
    /// <param name="k">The subframe.</param>
    /// <param name="output">Receives the speech of the frame.</param>
    /// <param name="position">Where in the long-term state the subframe's residual goes; advanced.</param>
    private static void DecodeSubframe(SilkChannel channel, SilkSynthesisScratch scratch, int k, Span<short> output, ref int position)
    {
        var parameters = channel.Parameters;
        var length = channel.SubframeLength;
        var gain = parameters.Gains[k];
        var gainQ10 = gain >> GainShift;
        var gainChange = ScaleStateToGain(channel, scratch, gain);
        var signalType = EaseIntoUnvoiced(channel, k) ? SilkConstants.TypeVoiced : channel.Indices.SignalType;
        var excitation = channel.Excitation.AsSpan(k * length, length);
        var residual = excitation;
        if (signalType == SilkConstants.TypeVoiced)
        {
            PrepareLongTermState(channel, scratch, k, gain, gainChange, output, position);
            residual = scratch.Residual.AsSpan(0, length);
            PredictLongTerm(channel, scratch, k, excitation, residual, ref position);
        }

        var coefficients = parameters.Predictors.AsSpan((k >> 1) * SilkConstants.MaxLpcOrder, channel.LpcOrder);
        PredictShortTerm(channel, scratch.ShortTermState, coefficients, residual, output.Slice(k * length, length), gainQ10);
    }

    /// <summary>Scales the state of the short-term filter to the new gain, when the gain has changed.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="scratch">The arrays the synthesis works in.</param>
    /// <param name="gain">The gain of the subframe, in Q16.</param>
    /// <returns>The change of gain, in Q16.</returns>
    private static int ScaleStateToGain(SilkChannel channel, SilkSynthesisScratch scratch, int gain)
    {
        var change = 1 << GainChangeBits;
        if (gain != channel.PreviousGainQ16)
        {
            change = SilkMath.DivideScaled(channel.PreviousGainQ16, gain, GainChangeBits);
            for (var i = 0; i < SilkConstants.MaxLpcOrder; i++)
            {
                scratch.ShortTermState[i] = SilkMath.MultiplyWord(change, scratch.ShortTermState[i]);
            }
        }

        channel.PreviousGainQ16 = gain;
        return change;
    }

    /// <summary>Avoids an abrupt change from concealed voiced speech to unvoiced speech by easing in with a single tap of long-term prediction.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="k">The subframe.</param>
    /// <returns><see langword="true"/> when the subframe is treated as voiced for this reason.</returns>
    private static bool EaseIntoUnvoiced(SilkChannel channel, int k)
    {
        if (channel.LossCount == 0 || channel.PreviousSignalType != SilkConstants.TypeVoiced || channel.Indices.SignalType == SilkConstants.TypeVoiced || k >= EaseSubframes)
        {
            return false;
        }

        var taps = channel.Parameters.LtpCoefficients.AsSpan(k * SilkConstants.LtpOrder, SilkConstants.LtpOrder);
        taps.Clear();
        taps[SilkConstants.LtpCentre] = EaseTap;
        channel.Parameters.PitchLags[k] = channel.PreviousLag;
        return true;
    }

    /// <summary>Rewhitens the past output, or rescales it, so the long-term prediction can read it at the subframe's gain.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="scratch">The arrays the synthesis works in.</param>
    /// <param name="k">The subframe.</param>
    /// <param name="gain">The gain of the subframe, in Q16.</param>
    /// <param name="gainChange">The change of gain since the subframe before, in Q16.</param>
    /// <param name="output">The speech of the frame so far.</param>
    /// <param name="position">Where in the long-term state the subframe's residual goes.</param>
    private static void PrepareLongTermState(SilkChannel channel, SilkSynthesisScratch scratch, int k, int gain, int gainChange, Span<short> output, int position)
    {
        var lag = channel.Parameters.PitchLags[k];
        var memory = channel.LtpMemoryLength;
        var state = scratch.LongTermState;
        if (k == 0 || (k == SilkConstants.Pair && channel.Indices.NlsfInterpolation < SilkConstants.NoInterpolation))
        {
            var start = memory - lag - channel.LpcOrder - SilkConstants.LtpCentre;
            if (k == SilkConstants.Pair)
            {
                output[..(SilkConstants.Pair * channel.SubframeLength)].CopyTo(channel.OutputBuffer.AsSpan(memory));
            }

            var coefficients = channel.Parameters.Predictors.AsSpan((k >> 1) * SilkConstants.MaxLpcOrder, channel.LpcOrder);
            SilkLpc.AnalysisFilter(scratch.Whitened.AsSpan(start), channel.OutputBuffer.AsSpan(start + (k * channel.SubframeLength)), coefficients, memory - start, channel.LpcOrder);
            var inverseGain = SilkMath.InvertScaled(gain, InverseGainBits);
            if (k == 0)
            {
                inverseGain = SilkMath.ShiftLeft(SilkMath.MultiplyWordBottom(inverseGain, channel.Parameters.LtpScaleQ14), LtpScaleShift);
            }

            for (var i = 0; i < lag + SilkConstants.LtpCentre; i++)
            {
                state[position - i - 1] = SilkMath.MultiplyWordBottom(inverseGain, scratch.Whitened[memory - i - 1]);
            }
        }
        else if (gainChange != 1 << GainChangeBits)
        {
            for (var i = 0; i < lag + SilkConstants.LtpCentre; i++)
            {
                state[position - i - 1] = SilkMath.MultiplyWord(gainChange, state[position - i - 1]);
            }
        }
    }

    /// <summary>Adds the long-term prediction to the excitation of a subframe.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="scratch">The arrays the synthesis works in.</param>
    /// <param name="k">The subframe.</param>
    /// <param name="excitation">The excitation of the subframe.</param>
    /// <param name="residual">Receives the excitation with the prediction added.</param>
    /// <param name="position">Where in the long-term state the subframe's residual goes; advanced.</param>
    private static void PredictLongTerm(SilkChannel channel, SilkSynthesisScratch scratch, int k, ReadOnlySpan<int> excitation, Span<int> residual, ref int position)
    {
        var taps = channel.Parameters.LtpCoefficients.AsSpan(k * SilkConstants.LtpOrder, SilkConstants.LtpOrder);
        var state = scratch.LongTermState;
        var ahead = position - channel.Parameters.PitchLags[k] + SilkConstants.LtpCentre;
        for (var i = 0; i < channel.SubframeLength; i++)
        {
            var prediction = LtpRounding;
            for (var t = 0; t < SilkConstants.LtpOrder; t++)
            {
                prediction = SilkMath.MultiplyAddWordBottom(prediction, state[ahead + i - t], taps[t]);
            }

            residual[i] = unchecked(excitation[i] + (prediction << 1));
            state[position] = SilkMath.ShiftLeft(residual[i], 1);
            position++;
        }
    }

    /// <summary>Runs the short-term filter over a subframe's residual and scales the result by the gain.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="state">The state of the filter followed by room for the subframe's output.</param>
    /// <param name="coefficients">The filter, in Q12.</param>
    /// <param name="residual">The residual.</param>
    /// <param name="output">Receives the speech.</param>
    /// <param name="gainQ10">The gain, in Q10.</param>
    private static void PredictShortTerm(SilkChannel channel, int[] state, ReadOnlySpan<short> coefficients, ReadOnlySpan<int> residual, Span<short> output, int gainQ10)
    {
        var order = channel.LpcOrder;
        for (var i = 0; i < output.Length; i++)
        {
            var prediction = order >> 1;
            for (var j = 0; j < order; j++)
            {
                prediction = SilkMath.MultiplyAddWordBottom(prediction, state[SilkConstants.MaxLpcOrder + i - 1 - j], coefficients[j]);
            }

            var sample = unchecked(residual[i] + (prediction << PredictionShift));
            state[SilkConstants.MaxLpcOrder + i] = sample;
            output[i] = (short)SilkMath.Saturate16(SilkMath.RoundShift(SilkMath.MultiplyWord(sample, gainQ10), OutputShift));
        }
    }
}
