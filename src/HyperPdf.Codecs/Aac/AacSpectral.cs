// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.Aac;

/// <summary>Reads the quantised spectrum of an AAC channel.</summary>
internal static class AacSpectral
{
    /// <summary>The value that the escape codebook continues in an escape code.</summary>
    private const int EscapeValue = 16;

    /// <summary>The bits of the escape code before its length grows.</summary>
    private const int EscapeBaseBits = 4;

    /// <summary>The most bits an escape code may have, which keeps a value within 13 bits.</summary>
    private const int EscapeLimit = 12;

    /// <summary>The largest quantised value.</summary>
    private const int MaxValue = 8191;

    /// <summary>Reads the spectrum of every band that has a codebook, into the order the stream codes it.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="channel">The channel to fill.</param>
    /// <param name="offsets">The band edges of the window size.</param>
    /// <exception cref="InvalidDataException">The data is not valid.</exception>
    internal static void Read(ref MsbBitReader reader, AacChannel channel, short[] offsets)
    {
        Array.Clear(channel.Quantised);
        var windowSize = channel.IsShort ? AacChannel.ShortSize : AacChannel.LongSize;
        var groupBase = 0;
        for (var group = 0; group < channel.GroupCount; group++)
        {
            var length = channel.GroupLength[group];
            for (var band = 0; band < channel.MaxSfb; band++)
            {
                var book = channel.Codebooks[(group * AacChannel.BandStride) + band];
                if (book is AacCodebooks.Zero or >= AacCodebooks.Noise)
                {
                    continue;
                }

                var start = offsets[band] * length;
                var end = offsets[band + 1] * length;
                ReadBand(ref reader, channel.Quantised.AsSpan(groupBase + start, end - start), book);
            }

            groupBase += length * windowSize;
        }

        if (reader.IsOverrun)
        {
            throw new InvalidDataException("An AAC frame ends inside its spectrum.");
        }

        if (channel.PulseCount > 0)
        {
            ApplyPulses(channel, offsets);
        }
    }

    /// <summary>Reads the values of one band.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="values">Receives the values.</param>
    /// <param name="book">The codebook.</param>
    private static void ReadBand(ref MsbBitReader reader, Span<int> values, int book)
    {
        var table = AacCodebooks.Spectral(book);
        var symbols = AacCodebooks.Values(book);
        var dimension = AacCodebooks.Dimension(book);
        var unsigned = book is not (<= AacCodebooks.LastSignedQuadBook or AacCodebooks.FirstSignedPairBook or AacCodebooks.LastSignedPairBook);
        for (var index = 0; index < values.Length; index += dimension)
        {
            var symbol = table.Decode(ref reader) * dimension;
            var group = values.Slice(index, dimension);
            for (var digit = 0; digit < group.Length; digit++)
            {
                group[digit] = symbols[symbol + digit];
            }

            if (unsigned)
            {
                ReadSigns(ref reader, group, book == AacCodebooks.Escape);
            }
        }
    }

    /// <summary>Reads the sign bits of a group of magnitudes, and the escape codes of the values that continue.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="group">The magnitudes, which become signed values.</param>
    /// <param name="escapes">Whether a value of 16 continues in an escape code.</param>
    private static void ReadSigns(ref MsbBitReader reader, Span<int> group, bool escapes)
    {
        var negative = 0UL;
        for (var digit = 0; digit < group.Length; digit++)
        {
            if (group[digit] != 0 && reader.ReadBit())
            {
                negative |= 1UL << digit;
            }
        }

        for (var digit = 0; digit < group.Length; digit++)
        {
            var magnitude = escapes && group[digit] == EscapeValue ? ReadEscape(ref reader) : group[digit];
            group[digit] = (negative & (1UL << digit)) != 0 ? -magnitude : magnitude;
        }
    }

    /// <summary>Reads an escape code.</summary>
    /// <param name="reader">The bits.</param>
    /// <returns>The magnitude.</returns>
    private static int ReadEscape(ref MsbBitReader reader)
    {
        var bits = EscapeBaseBits;
        while (reader.ReadBit() && bits < EscapeLimit)
        {
            bits++;
        }

        return Math.Min((1 << bits) + (int)reader.Read(bits), MaxValue);
    }

    /// <summary>Adds the pulses to the quantised spectrum.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="offsets">The band edges of the window size.</param>
    private static void ApplyPulses(AacChannel channel, short[] offsets)
    {
        if (channel.IsShort || channel.PulseStartBand >= offsets.Length - 1)
        {
            return;
        }

        int position = offsets[channel.PulseStartBand];
        for (var pulse = 0; pulse < channel.PulseCount; pulse++)
        {
            position += channel.PulseOffset[pulse];
            if (position >= AacChannel.LongSize)
            {
                return;
            }

            channel.Quantised[position] += channel.Quantised[position] > 0 ? channel.PulseAmplitude[pulse] : -channel.PulseAmplitude[pulse];
        }
    }
}
