// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>Reads the side information of a SILK frame (RFC 6716 section 4.2.7): its signal type, gains, spectrum, pitch and long-term filter.</summary>
internal static class SilkIndexReader
{
    /// <summary>The bits of every table's total.</summary>
    private const int TotalBits = 8;

    /// <summary>The offset added to the symbol of the signal type when the frame has voice activity.</summary>
    private const int VoiceActivityOffset = 2;

    /// <summary>The bits the most significant part of the first gain is shifted up by.</summary>
    private const int GainHighShift = 3;

    /// <summary>The gain levels each signal type's first-gain distribution has.</summary>
    private const int GainRowSize = SilkConstants.GainLevels / 8;

    /// <summary>The offset subtracted from a pitch lag change to make it signed.</summary>
    private const int PitchDeltaOffset = 9;

    /// <summary>Reads the side information of a frame.</summary>
    /// <param name="channel">The channel, whose indices are filled in.</param>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="frameIndex">The index of the frame in the packet.</param>
    /// <param name="redundant">Whether the frame is a redundancy frame, which always has voice activity.</param>
    /// <param name="codingMode">How the frame is coded: independently or depending on the one before.</param>
    internal static void Read(SilkChannel channel, ref OpusRangeDecoder decoder, int frameIndex, bool redundant, int codingMode)
    {
        var indices = channel.Indices;
        var symbol = redundant || channel.VadFlags[frameIndex]
            ? decoder.ReadSymbol(SilkTables.TypeOffsetVadIcdf, TotalBits) + VoiceActivityOffset
            : decoder.ReadSymbol(SilkTables.TypeOffsetNoVadIcdf, TotalBits);
        indices.SignalType = symbol >> 1;
        indices.QuantOffsetType = symbol & 1;
        ReadGains(indices, ref decoder, channel.SubframeCount, codingMode);
        ReadSpectrum(channel, ref decoder);
        if (indices.SignalType == SilkConstants.TypeVoiced)
        {
            ReadPitch(channel, ref decoder, codingMode);
            ReadLtp(channel, ref decoder, codingMode);
        }

        channel.EntropyPreviousSignalType = indices.SignalType;
        indices.Seed = decoder.ReadSymbol(SilkTables.Uniform4Icdf, TotalBits);
    }

    /// <summary>Reads the gain of each subframe: the first on its own or as a change from the frame before, then changes.</summary>
    /// <param name="indices">The indices to fill in.</param>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="subframes">The subframes.</param>
    /// <param name="codingMode">How the frame is coded.</param>
    private static void ReadGains(SilkIndices indices, ref OpusRangeDecoder decoder, int subframes, int codingMode)
    {
        if (codingMode == SilkConstants.CodeConditionally)
        {
            indices.Gains[0] = (sbyte)decoder.ReadSymbol(SilkTables.DeltaGainIcdf, TotalBits);
        }
        else
        {
            var high = decoder.ReadSymbol(SilkTables.GainIcdf.Slice(indices.SignalType * GainRowSize, GainRowSize), TotalBits) << GainHighShift;
            indices.Gains[0] = (sbyte)(high + decoder.ReadSymbol(SilkTables.Uniform8Icdf, TotalBits));
        }

        for (var i = 1; i < subframes; i++)
        {
            indices.Gains[i] = (sbyte)decoder.ReadSymbol(SilkTables.DeltaGainIcdf, TotalBits);
        }
    }

    /// <summary>Reads the line spectral frequency indices and the interpolation factor.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="decoder">The range decoder.</param>
    private static void ReadSpectrum(SilkChannel channel, ref OpusRangeDecoder decoder)
    {
        var indices = channel.Indices;
        var codebook = channel.Codebook;
        var first = codebook.IndexIcdf.Slice((indices.SignalType >> 1) * SilkCodebook.Count, SilkCodebook.Count);
        indices.Nlsf[0] = (sbyte)decoder.ReadSymbol(first, TotalBits);
        Span<short> entropyIndex = stackalloc short[SilkConstants.MaxLpcOrder];
        Span<byte> predictor = stackalloc byte[SilkConstants.MaxLpcOrder];
        SilkNlsf.Unpack(codebook, indices.Nlsf[0], entropyIndex, predictor);
        for (var i = 0; i < codebook.Order; i++)
        {
            indices.Nlsf[i + 1] = (sbyte)(ReadResidual(ref decoder, codebook.ResidualIcdf[entropyIndex[i]..]) - SilkConstants.NlsfQuantMaxAmplitude);
        }

        indices.NlsfInterpolation = channel.SubframeCount == SilkConstants.MaxSubframes
            ? decoder.ReadSymbol(SilkTables.NlsfInterpolationIcdf, TotalBits)
            : SilkConstants.NoInterpolation;
    }

    /// <summary>Reads one residual index, with its extension when it is at either end of the range.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="distribution">The distribution of the residual.</param>
    /// <returns>The index, from 0 up.</returns>
    private static int ReadResidual(ref OpusRangeDecoder decoder, ReadOnlySpan<byte> distribution)
    {
        var value = decoder.ReadSymbol(distribution, TotalBits);
        if (value == 0)
        {
            value -= decoder.ReadSymbol(SilkTables.NlsfExtensionIcdf, TotalBits);
        }
        else if (value == SilkConstants.Pair * SilkConstants.NlsfQuantMaxAmplitude)
        {
            value += decoder.ReadSymbol(SilkTables.NlsfExtensionIcdf, TotalBits);
        }

        return value;
    }

    /// <summary>Reads the pitch lag of the first subframe and the contour that shapes the lags of the others.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="codingMode">How the frame is coded.</param>
    private static void ReadPitch(SilkChannel channel, ref OpusRangeDecoder decoder, int codingMode)
    {
        var indices = channel.Indices;
        var absolute = true;
        if (codingMode == SilkConstants.CodeConditionally && channel.EntropyPreviousSignalType == SilkConstants.TypeVoiced)
        {
            var delta = decoder.ReadSymbol(SilkTables.PitchDeltaIcdf, TotalBits);
            if (delta > 0)
            {
                indices.LagIndex = channel.EntropyPreviousLagIndex + delta - PitchDeltaOffset;
                absolute = false;
            }
        }

        if (absolute)
        {
            indices.LagIndex = decoder.ReadSymbol(SilkTables.PitchLagIcdf, TotalBits) * (channel.SampleRateKhz >> 1);
            indices.LagIndex += decoder.ReadSymbol(channel.PitchLagLowBitsIcdf, TotalBits);
        }

        channel.EntropyPreviousLagIndex = indices.LagIndex;
        indices.ContourIndex = decoder.ReadSymbol(channel.PitchContourIcdf, TotalBits);
    }

    /// <summary>Reads the filter of the long-term prediction of each subframe, and its scaling.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="codingMode">How the frame is coded.</param>
    private static void ReadLtp(SilkChannel channel, ref OpusRangeDecoder decoder, int codingMode)
    {
        var indices = channel.Indices;
        indices.PeriodicityIndex = decoder.ReadSymbol(SilkTables.LtpPeriodicityIcdf, TotalBits);
        for (var k = 0; k < channel.SubframeCount; k++)
        {
            indices.LtpFilters[k] = (sbyte)decoder.ReadSymbol(GetLtpIcdf(indices.PeriodicityIndex), TotalBits);
        }

        indices.LtpScaleIndex = codingMode == SilkConstants.CodeIndependently ? decoder.ReadSymbol(SilkTables.LtpScaleIcdf, TotalBits) : 0;
    }

    /// <summary>Gets the distribution of the filter index for a periodicity.</summary>
    /// <param name="periodicity">The periodicity index.</param>
    /// <returns>The distribution.</returns>
    private static ReadOnlySpan<byte> GetLtpIcdf(int periodicity) => periodicity switch
    {
        0 => SilkTables.LtpGain0Icdf,
        1 => SilkTables.LtpGain1Icdf,
        _ => SilkTables.LtpGain2Icdf,
    };
}
