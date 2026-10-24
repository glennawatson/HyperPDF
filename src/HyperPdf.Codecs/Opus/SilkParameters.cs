// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>Turns the indices of a SILK frame into the gains, filters and pitch lags that the synthesis uses (RFC 6716 sections 4.2.7.4 to 4.2.7.6).</summary>
internal static class SilkParameters
{
    /// <summary>The gain scale offset, in Q7: the lowest gain in decibels plus 16 steps.</summary>
    private const int GainOffset = 2090;

    /// <summary>The step of the gain scale, in Q16.</summary>
    private const int GainStep = 1_907_825;

    /// <summary>The most a gain index may fall from the one before when it is coded without reference to it.</summary>
    private const int MaxGainDrop = 16;

    /// <summary>The bits the filter coefficients of the long-term prediction codebook are shifted up by.</summary>
    private const int LtpShift = 7;

    /// <summary>The shortest pitch lag in milliseconds.</summary>
    private const int MinLagMs = 2;

    /// <summary>The longest pitch lag in milliseconds.</summary>
    private const int MaxLagMs = 18;

    /// <summary>The offsets a pitch contour has for a 20 ms frame at 8 kHz.</summary>
    private const int ContoursNarrow = 11;

    /// <summary>The offsets a pitch contour has for a 10 ms frame at 8 kHz.</summary>
    private const int ContoursNarrow10Ms = 3;

    /// <summary>The offsets a pitch contour has for a 20 ms frame at 12 or 16 kHz.</summary>
    private const int ContoursWide = 34;

    /// <summary>The offsets a pitch contour has for a 10 ms frame at 12 or 16 kHz.</summary>
    private const int Contours10Ms = 12;

    /// <summary>The quarters of interpolation at which the first half of the frame uses the filter of the frame before.</summary>
    private const int InterpolationQuarters = 2;

    /// <summary>Works out the parameters of a frame from its indices.</summary>
    /// <param name="channel">The channel, whose parameters are filled in.</param>
    /// <param name="codingMode">How the frame is coded.</param>
    internal static void Decode(SilkChannel channel, int codingMode)
    {
        var indices = channel.Indices;
        var parameters = channel.Parameters;
        DequantizeGains(channel, codingMode == SilkConstants.CodeConditionally);
        DecodeFilters(channel);
        if (indices.SignalType == SilkConstants.TypeVoiced)
        {
            DecodePitch(channel);
            DecodeLtp(channel);
        }
        else
        {
            Array.Clear(parameters.PitchLags);
            Array.Clear(parameters.LtpCoefficients);
            indices.PeriodicityIndex = 0;
            parameters.LtpScaleQ14 = 0;
        }
    }

    /// <summary>Turns the gain indices into gains on a linear scale, each as a change from the one before.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="conditional">Whether the first index is a change from the frame before.</param>
    private static void DequantizeGains(SilkChannel channel, bool conditional)
    {
        var indices = channel.Indices;
        var gains = channel.Parameters.Gains;
        var previous = channel.LastGainIndex;
        for (var k = 0; k < channel.SubframeCount; k++)
        {
            if (k == 0 && !conditional)
            {
                previous = Math.Max(indices.Gains[k], previous - MaxGainDrop);
            }
            else
            {
                var change = indices.Gains[k] + SilkConstants.MinDeltaGain;
                var threshold = (SilkConstants.Pair * SilkConstants.MaxDeltaGain) - SilkConstants.GainLevels + previous;
                previous += change > threshold ? (change << 1) - threshold : change;
            }

            previous = Math.Clamp(previous, 0, SilkConstants.GainLevels - 1);
            gains[k] = SilkMath.Exp2Q7(SilkMath.LimitGainLog(SilkMath.MultiplyWordBottom(GainStep, previous) + GainOffset));
        }

        channel.LastGainIndex = previous;
    }

    /// <summary>Decodes the short-term prediction filters: one for the second half of the frame, and one for the first, interpolated from the frame before.</summary>
    /// <param name="channel">The channel.</param>
    private static void DecodeFilters(SilkChannel channel)
    {
        var indices = channel.Indices;
        var order = channel.LpcOrder;
        var predictors = channel.Parameters.Predictors;
        Span<short> frequencies = stackalloc short[SilkConstants.MaxLpcOrder];
        SilkNlsf.Decode(indices.Nlsf, channel.Codebook, frequencies);
        SilkNlsf.ToFilter(predictors.AsSpan(SilkConstants.MaxLpcOrder), frequencies, order);
        if (channel.FirstFrameAfterReset)
        {
            indices.NlsfInterpolation = SilkConstants.NoInterpolation;
        }

        if (indices.NlsfInterpolation < SilkConstants.NoInterpolation)
        {
            Span<short> interpolated = stackalloc short[SilkConstants.MaxLpcOrder];
            for (var i = 0; i < order; i++)
            {
                interpolated[i] = (short)(channel.PreviousNlsf[i] + ((indices.NlsfInterpolation * (frequencies[i] - channel.PreviousNlsf[i])) >> InterpolationQuarters));
            }

            SilkNlsf.ToFilter(predictors, interpolated, order);
        }
        else
        {
            predictors.AsSpan(SilkConstants.MaxLpcOrder, order).CopyTo(predictors);
        }

        frequencies[..order].CopyTo(channel.PreviousNlsf);
        if (channel.LossCount == 0)
        {
            return;
        }

        SilkLpc.BandwidthExpand(predictors.AsSpan(0, order), order, SilkConstants.BandwidthExpansionAfterLoss);
        SilkLpc.BandwidthExpand(predictors.AsSpan(SilkConstants.MaxLpcOrder, order), order, SilkConstants.BandwidthExpansionAfterLoss);
    }

    /// <summary>Works out the pitch lag of each subframe from the first lag and the contour.</summary>
    /// <param name="channel">The channel.</param>
    private static void DecodePitch(SilkChannel channel)
    {
        var indices = channel.Indices;
        var rate = channel.SampleRateKhz;
        var longFrame = channel.SubframeCount == SilkConstants.MaxSubframes;
        var offsets = GetPitchOffsets(rate, longFrame, out var contours);
        var minimum = MinLagMs * rate;
        var maximum = MaxLagMs * rate;
        var lag = minimum + indices.LagIndex;
        for (var k = 0; k < channel.SubframeCount; k++)
        {
            channel.Parameters.PitchLags[k] = Math.Clamp(lag + offsets[(k * contours) + indices.ContourIndex], minimum, maximum);
        }
    }

    /// <summary>Gets the table of pitch lag offsets for a rate and frame size.</summary>
    /// <param name="rate">The rate in kHz.</param>
    /// <param name="longFrame">Whether the frame is 20 ms.</param>
    /// <param name="contours">Receives the offsets in a row of the table.</param>
    /// <returns>The offsets, a row for each subframe.</returns>
    private static ReadOnlySpan<sbyte> GetPitchOffsets(int rate, bool longFrame, out int contours)
    {
        if (rate == SilkConstants.NarrowbandKhz)
        {
            contours = longFrame ? ContoursNarrow : ContoursNarrow10Ms;
            return longFrame ? SilkTables.PitchOffsetsNarrow : SilkTables.PitchOffsets10MsNarrow;
        }

        contours = longFrame ? ContoursWide : Contours10Ms;
        return longFrame ? SilkTables.PitchOffsets : SilkTables.PitchOffsets10Ms;
    }

    /// <summary>Looks up the long-term prediction filter of each subframe and its scale.</summary>
    /// <param name="channel">The channel.</param>
    private static void DecodeLtp(SilkChannel channel)
    {
        var indices = channel.Indices;
        var parameters = channel.Parameters;
        var codebook = GetLtpFilters(indices.PeriodicityIndex);
        for (var k = 0; k < channel.SubframeCount; k++)
        {
            for (var i = 0; i < SilkConstants.LtpOrder; i++)
            {
                parameters.LtpCoefficients[(k * SilkConstants.LtpOrder) + i] = (short)(codebook[(indices.LtpFilters[k] * SilkConstants.LtpOrder) + i] << LtpShift);
            }
        }

        parameters.LtpScaleQ14 = SilkTables.LtpScales[indices.LtpScaleIndex];
    }

    /// <summary>Gets the codebook of long-term prediction filters for a periodicity.</summary>
    /// <param name="periodicity">The periodicity index.</param>
    /// <returns>The filters, five taps each.</returns>
    private static ReadOnlySpan<sbyte> GetLtpFilters(int periodicity) => periodicity switch
    {
        0 => SilkTables.LtpFilters0,
        1 => SilkTables.LtpFilters1,
        _ => SilkTables.LtpFilters2,
    };
}
