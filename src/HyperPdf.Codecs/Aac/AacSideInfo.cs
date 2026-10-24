// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.Aac;

/// <summary>Reads the parts of an AAC channel that come before its spectrum: window info, sections, scalefactors, pulses and TNS.</summary>
internal static class AacSideInfo
{
    /// <summary>The bits of the window sequence.</summary>
    private const int SequenceBits = 2;

    /// <summary>The bits of the number of bands of a short window.</summary>
    private const int ShortBandBits = 4;

    /// <summary>The bits of the number of bands of a long window.</summary>
    private const int LongBandBits = 6;

    /// <summary>The bits of the grouping of short windows.</summary>
    private const int GroupingBits = 7;

    /// <summary>The bits of a section's codebook number.</summary>
    private const int CodebookBits = 4;

    /// <summary>The bits of a section length step in a short window.</summary>
    private const int ShortSectionBits = 3;

    /// <summary>The bits of a section length step in a long window.</summary>
    private const int LongSectionBits = 5;

    /// <summary>The offset of a coded scalefactor change, which is stored from 0.</summary>
    private const int ScaleFactorBias = 60;

    /// <summary>The bits of the first noise energy.</summary>
    private const int NoiseStartBits = 9;

    /// <summary>The start of the noise energies relative to the global gain.</summary>
    private const int NoiseBase = 90 + 256;

    /// <summary>The largest scalefactor.</summary>
    private const int MaxScaleFactor = 255;

    /// <summary>The bits of the pulse count.</summary>
    private const int PulseCountBits = 2;

    /// <summary>The bits of the band a pulse run starts in.</summary>
    private const int PulseStartBits = 6;

    /// <summary>The bits of a pulse offset.</summary>
    private const int PulseOffsetBits = 5;

    /// <summary>The bits of a pulse amplitude.</summary>
    private const int PulseAmplitudeBits = 4;

    /// <summary>The bits of the number of TNS filters of a long window.</summary>
    private const int TnsLongFilterBits = 2;

    /// <summary>The bits of a TNS filter length in a long window.</summary>
    private const int TnsLongLengthBits = 6;

    /// <summary>The bits of a TNS filter order in a long window.</summary>
    private const int TnsLongOrderBits = 5;

    /// <summary>The bits of a TNS filter length in a short window.</summary>
    private const int TnsShortLengthBits = 4;

    /// <summary>The bits of a TNS filter order in a short window.</summary>
    private const int TnsShortOrderBits = 3;

    /// <summary>The bits of a TNS coefficient at the coarse resolution.</summary>
    private const int TnsCoarseBits = 3;

    /// <summary>The bits of a TNS coefficient at the fine resolution.</summary>
    private const int TnsFineBits = 4;

    /// <summary>Reads the window info of a channel.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="channel">The channel to fill.</param>
    /// <param name="rateIndex">The sample rate index.</param>
    /// <exception cref="InvalidDataException">The info is not valid.</exception>
    internal static void ReadWindowInfo(ref MsbBitReader reader, AacChannel channel, int rateIndex)
    {
        reader.Skip(1);
        channel.WindowSequence = (int)reader.Read(SequenceBits);
        channel.WindowShape = (int)reader.Read(1);
        if (channel.IsShort)
        {
            channel.MaxSfb = (int)reader.Read(ShortBandBits);
            ReadGroups(channel, (int)reader.Read(GroupingBits));
        }
        else
        {
            channel.MaxSfb = (int)reader.Read(LongBandBits);
            channel.GroupCount = 1;
            channel.GroupLength[0] = 1;
            if (reader.ReadBit())
            {
                throw new InvalidDataException("AAC prediction is not supported.");
            }
        }

        var bands = (channel.IsShort ? AacBands.Short(rateIndex) : AacBands.Long(rateIndex)).Length - 1;
        if (channel.MaxSfb > bands)
        {
            throw new InvalidDataException("An AAC frame has more bands than its sample rate has.");
        }
    }

    /// <summary>Reads the section data: which codebook each band uses.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="channel">The channel to fill.</param>
    /// <exception cref="InvalidDataException">The sections are not valid.</exception>
    internal static void ReadSections(ref MsbBitReader reader, AacChannel channel)
    {
        var length = 0;
        for (var group = 0; group < channel.GroupCount; group++)
        {
            for (var band = 0; band < channel.MaxSfb; band += length)
            {
                length = ReadSection(ref reader, channel, (group * AacChannel.BandStride) + band, channel.MaxSfb - band);
            }
        }
    }

    /// <summary>Reads the scalefactors of the bands that have a codebook.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="channel">The channel to fill.</param>
    /// <exception cref="InvalidDataException">A scalefactor is out of range.</exception>
    internal static void ReadScaleFactors(ref MsbBitReader reader, AacChannel channel)
    {
        var table = AacCodebooks.Scalefactor();
        var factor = channel.GlobalGain;
        var noise = channel.GlobalGain - NoiseBase;
        var position = 0;
        var firstNoise = true;
        for (var group = 0; group < channel.GroupCount; group++)
        {
            for (var band = 0; band < channel.MaxSfb; band++)
            {
                var index = (group * AacChannel.BandStride) + band;
                switch (channel.Codebooks[index])
                {
                    case AacCodebooks.Zero:
                    {
                        channel.ScaleFactors[index] = 0;
                        break;
                    }

                    case AacCodebooks.IntensityInPhase or AacCodebooks.IntensityOutOfPhase:
                    {
                        position += table.Decode(ref reader) - ScaleFactorBias;
                        channel.ScaleFactors[index] = position;
                        break;
                    }

                    case AacCodebooks.Noise:
                    {
                        noise += firstNoise ? (int)reader.Read(NoiseStartBits) : table.Decode(ref reader) - ScaleFactorBias;
                        firstNoise = false;
                        channel.ScaleFactors[index] = noise;
                        break;
                    }

                    default:
                    {
                        factor += table.Decode(ref reader) - ScaleFactorBias;
                        channel.ScaleFactors[index] = factor;
                        break;
                    }
                }
            }
        }

        if (reader.IsOverrun || factor is < 0 or > MaxScaleFactor)
        {
            throw new InvalidDataException("An AAC scalefactor is out of range.");
        }
    }

    /// <summary>Reads the pulse data.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="channel">The channel to fill.</param>
    internal static void ReadPulse(ref MsbBitReader reader, AacChannel channel)
    {
        channel.PulseCount = (int)reader.Read(PulseCountBits) + 1;
        channel.PulseStartBand = (int)reader.Read(PulseStartBits);
        for (var pulse = 0; pulse < channel.PulseCount; pulse++)
        {
            channel.PulseOffset[pulse] = (int)reader.Read(PulseOffsetBits);
            channel.PulseAmplitude[pulse] = (int)reader.Read(PulseAmplitudeBits);
        }
    }

    /// <summary>Reads the TNS data.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="channel">The channel to fill.</param>
    internal static void ReadTns(ref MsbBitReader reader, AacChannel channel)
    {
        var isShort = channel.IsShort;
        var filterBits = isShort ? 1 : TnsLongFilterBits;
        var lengthBits = isShort ? TnsShortLengthBits : TnsLongLengthBits;
        var orderBits = isShort ? TnsShortOrderBits : TnsLongOrderBits;
        for (var window = 0; window < channel.WindowCount; window++)
        {
            var count = (int)reader.Read(filterBits);
            channel.TnsFilterCount[window] = (byte)count;
            if (count == 0)
            {
                continue;
            }

            var resolution = reader.ReadBit() ? TnsFineBits : TnsCoarseBits;
            channel.TnsResolution[window] = (byte)resolution;
            for (var filter = 0; filter < count; filter++)
            {
                var slot = (window * AacChannel.TnsFilters) + filter;
                channel.TnsLength[slot] = (byte)reader.Read(lengthBits);
                var order = (int)reader.Read(orderBits);
                channel.TnsFilterOrder[slot] = (byte)order;
                if (order > 0)
                {
                    ReadTnsCoefficients(ref reader, channel, slot, resolution);
                }
            }
        }
    }

    /// <summary>Reads one section: a codebook and the number of bands that use it.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="channel">The channel to fill.</param>
    /// <param name="index">The index of the section's first band in the codebooks.</param>
    /// <param name="remaining">The bands left in the group.</param>
    /// <returns>The number of bands in the section.</returns>
    /// <exception cref="InvalidDataException">The section is not valid.</exception>
    private static int ReadSection(ref MsbBitReader reader, AacChannel channel, int index, int remaining)
    {
        var stepBits = channel.IsShort ? ShortSectionBits : LongSectionBits;
        var escape = (1 << stepBits) - 1;
        var book = (byte)reader.Read(CodebookBits);
        var length = 0;
        var step = (int)reader.Read(stepBits);
        while (step == escape && !reader.IsOverrun)
        {
            length += escape;
            step = (int)reader.Read(stepBits);
        }

        length += step;
        if (length == 0 || length > remaining || book == AacCodebooks.FirstSpecial || reader.IsOverrun)
        {
            throw new InvalidDataException("An AAC section is not valid.");
        }

        channel.Codebooks.AsSpan(index, length).Fill(book);
        return length;
    }

    /// <summary>Reads the direction and coefficients of one TNS filter.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="channel">The channel to fill.</param>
    /// <param name="slot">The window and filter, as a single index.</param>
    /// <param name="resolution">The bits of a coefficient before compression.</param>
    private static void ReadTnsCoefficients(ref MsbBitReader reader, AacChannel channel, int slot, int resolution)
    {
        channel.TnsDownward[slot] = reader.ReadBit();
        var bits = resolution - (reader.ReadBit() ? 1 : 0);
        var order = channel.TnsFilterOrder[slot];
        var start = slot * AacChannel.TnsOrder;
        for (var index = 0; index < order; index++)
        {
            channel.TnsCoefficients[start + index] = (sbyte)reader.ReadSigned(bits);
        }
    }

    /// <summary>Splits the eight short windows into groups from the grouping bits.</summary>
    /// <param name="channel">The channel to fill.</param>
    /// <param name="grouping">The grouping bits: a 1 joins a window to the one before it.</param>
    private static void ReadGroups(AacChannel channel, int grouping)
    {
        channel.GroupCount = 1;
        channel.GroupLength[0] = 1;
        for (var window = 1; window < AacChannel.ShortCount; window++)
        {
            if (((grouping >> (AacChannel.ShortCount - 1 - window)) & 1) != 0)
            {
                channel.GroupLength[channel.GroupCount - 1]++;
            }
            else
            {
                channel.GroupLength[channel.GroupCount] = 1;
                channel.GroupCount++;
            }
        }
    }
}
