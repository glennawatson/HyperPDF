// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.Aac;

/// <summary>What an AAC stream says about itself in its audio specific config: the sample rate and the channels.</summary>
/// <param name="RateIndex">The sample rate index of the core coder.</param>
/// <param name="SampleRate">The sample rate of the core coder in Hz.</param>
/// <param name="ChannelConfiguration">The channel configuration, 0 when a program config element in the stream says it.</param>
/// <param name="Channels">The number of channels the config names, or 0 when the stream says.</param>
[DebuggerDisplay("AacConfig: {SampleRate} Hz, configuration {ChannelConfiguration}")]
internal sealed record AacConfig(int RateIndex, int SampleRate, int ChannelConfiguration, int Channels)
{
    /// <summary>The object type of AAC Low Complexity.</summary>
    private const int LowComplexity = 2;

    /// <summary>The object type of Main, which differs from Low Complexity only by prediction.</summary>
    private const int MainProfile = 1;

    /// <summary>The object type that adds spectral band replication to a core object type.</summary>
    private const int BandReplication = 5;

    /// <summary>The object type that adds parametric stereo to a core object type.</summary>
    private const int ParametricStereo = 29;

    /// <summary>The object type value that says the real type follows.</summary>
    private const int ObjectTypeEscape = 31;

    /// <summary>The sample rate index value that says the rate follows in 24 bits.</summary>
    private const int ExplicitRate = 15;

    /// <summary>The bits of an object type.</summary>
    private const int ObjectTypeBits = 5;

    /// <summary>The bits of the extended object type.</summary>
    private const int ObjectTypeExtraBits = 6;

    /// <summary>The first extended object type value.</summary>
    private const int ObjectTypeExtraBase = 32;

    /// <summary>The bits of a sample rate index.</summary>
    private const int RateIndexBits = 4;

    /// <summary>The bits of an explicit sample rate.</summary>
    private const int ExplicitRateBits = 24;

    /// <summary>The bits of a channel configuration.</summary>
    private const int ChannelBits = 4;

    /// <summary>Gets the channel counts of the channel configurations 0 to 7.</summary>
    private static ReadOnlySpan<byte> ConfigurationChannels => [0x0, 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x8];

    /// <summary>Reads the config that starts the decoder's configuration bytes.</summary>
    /// <param name="data">The audio specific config.</param>
    /// <returns>The config.</returns>
    /// <exception cref="NotSupportedException">The config names a coding this decoder does not read.</exception>
    internal static AacConfig Parse(ReadOnlySpan<byte> data)
    {
        var reader = new MsbBitReader(data);
        var type = ReadObjectType(ref reader);
        var rateIndex = ReadRate(ref reader, out var rate);
        var channelConfiguration = (int)reader.Read(ChannelBits);
        if (type is BandReplication or ParametricStereo)
        {
            // The core coder's rate is the first one; the extension rate and the core object type follow.
            _ = ReadRate(ref reader, out _);
            type = ReadObjectType(ref reader);
        }

        if (type is not (LowComplexity or MainProfile) || reader.IsOverrun)
        {
            throw new NotSupportedException("The AAC object type is not Low Complexity.");
        }

        var frameLengthFlag = reader.ReadBit();
        if (frameLengthFlag)
        {
            throw new NotSupportedException("AAC frames of 960 samples are not supported.");
        }

        var channels = channelConfiguration < ConfigurationChannels.Length ? ConfigurationChannels[channelConfiguration] : 0;
        return new(rateIndex, rate, channelConfiguration, channels);
    }

    /// <summary>Reads an object type.</summary>
    /// <param name="reader">The bits.</param>
    /// <returns>The object type.</returns>
    private static int ReadObjectType(ref MsbBitReader reader)
    {
        var type = (int)reader.Read(ObjectTypeBits);
        return type == ObjectTypeEscape ? ObjectTypeExtraBase + (int)reader.Read(ObjectTypeExtraBits) : type;
    }

    /// <summary>Reads a sample rate.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="rate">Receives the rate in Hz.</param>
    /// <returns>The sample rate index, or -1 for a rate that has no index.</returns>
    /// <exception cref="NotSupportedException">The index is reserved.</exception>
    private static int ReadRate(ref MsbBitReader reader, out int rate)
    {
        var index = (int)reader.Read(RateIndexBits);
        if (index == ExplicitRate)
        {
            rate = (int)reader.Read(ExplicitRateBits);
            return AacBands.IndexOf(rate);
        }

        var rates = AacBands.SampleRates;
        if (index >= rates.Length)
        {
            throw new NotSupportedException("The AAC sample rate index is reserved.");
        }

        rate = rates[index];
        return index;
    }
}
