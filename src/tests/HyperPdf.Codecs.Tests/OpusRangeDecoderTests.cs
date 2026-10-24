// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs.Opus;

namespace HyperPdf.Codecs.Tests;

/// <summary>Tests that the Opus range decoder reads back what the encoder wrote, for every kind of symbol it handles.</summary>
public sealed class OpusRangeDecoderTests
{
    /// <summary>The seed of the random sequence.</summary>
    private const uint Seed = 8_675_309;

    /// <summary>The first xorshift shift.</summary>
    private const int ShiftA = 13;

    /// <summary>The second xorshift shift.</summary>
    private const int ShiftB = 17;

    /// <summary>The third xorshift shift.</summary>
    private const int ShiftC = 5;

    /// <summary>The symbols coded in each trial.</summary>
    private const int Symbols = 200;

    /// <summary>The trials run.</summary>
    private const int Trials = 50;

    /// <summary>The size of each packet in bytes.</summary>
    private const int PacketBytes = 1_024;

    /// <summary>The kinds of symbol coded.</summary>
    private const int Kinds = 5;

    /// <summary>The frequency total used by the plain symbols.</summary>
    private const uint Total = 37;

    /// <summary>The bits of the total of the table coded symbols.</summary>
    private const int TableBits = 8;

    /// <summary>The symbols in the table.</summary>
    private const uint TableSymbols = 4;

    /// <summary>The most bits a bit with a probability reads.</summary>
    private const uint MaxLogProbability = 7;

    /// <summary>The most raw bits one symbol reads.</summary>
    private const uint MaxRawBits = 24;

    /// <summary>The symbols coded to measure the bits used.</summary>
    private const uint CountedSymbols = 100;

    /// <summary>The largest range of a uniform number.</summary>
    private const uint MaxUniform = 100_000;

    /// <summary>The smallest range of a uniform number.</summary>
    private const uint MinUniform = 2;

    /// <summary>The values a bit takes.</summary>
    private const uint BitValues = 2;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The inverse cumulative frequencies of a four symbol alphabet that totals 256.</summary>
    private static readonly byte[] InverseCdf = [200, 100, 40, 0];

    /// <summary>The kinds of symbol the test codes.</summary>
    private enum SymbolKind
    {
        /// <summary>A symbol with a frequency out of a total.</summary>
        Plain = 0,

        /// <summary>A bit with a probability that is a power of two.</summary>
        Bit = 1,

        /// <summary>A symbol of a table of inverse cumulative frequencies.</summary>
        Table = 2,

        /// <summary>Raw bits read from the back of the packet.</summary>
        Raw = 3,

        /// <summary>A number that is uniform below a limit.</summary>
        Uniform = 4,
    }

    /// <summary>Every kind of symbol, in a random order, is decoded to the value that was encoded.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Decode_RandomSymbols_ReadsBackWhatWasEncoded()
    {
        var state = Seed;
        for (var trial = 0; trial < Trials; trial++)
        {
            var kinds = new SymbolKind[Symbols];
            var values = new uint[Symbols];
            var limits = new uint[Symbols];
            var encoder = new OpusRangeEncoder(PacketBytes);
            for (var i = 0; i < Symbols; i++)
            {
                kinds[i] = (SymbolKind)(Next(ref state) % Kinds);
                (limits[i], values[i]) = Pick(kinds[i], ref state);
                Encode(encoder, kinds[i], values[i], limits[i]);
            }

            var packet = encoder.Finish();

            await Assert.That(Matches(packet, kinds, values, limits)).IsEqualTo(Symbols).Because($"trial {trial}");
        }
    }

    /// <summary>The bits used grow as symbols are read and never pass the packet's size.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Tell_AfterSymbols_CountsWithinThePacket()
    {
        var encoder = new OpusRangeEncoder(PacketBytes);
        for (uint i = 0; i < CountedSymbols; i++)
        {
            encoder.Encode(i % Total, (i % Total) + 1, Total);
        }

        var packet = encoder.Finish();
        var readings = Measure(packet);

        await Assert.That(readings.After).IsGreaterThan(readings.Before);
        await Assert.That(readings.After).IsLessThanOrEqualTo(packet.Length * ByteBits);
        await Assert.That(readings.Fractional).IsGreaterThanOrEqualTo((readings.After - 1) * ByteBits);
    }

    /// <summary>Reads the counted symbols and notes how many bits the decoder says it used.</summary>
    /// <param name="packet">The packet.</param>
    /// <returns>The readings.</returns>
    private static TellReadings Measure(byte[] packet)
    {
        var decoder = new OpusRangeDecoder(packet);
        var before = decoder.Tell();
        for (var i = 0; i < CountedSymbols; i++)
        {
            var symbol = decoder.Decode(Total);
            decoder.Update(symbol, symbol + 1, Total);
        }

        return new(before, decoder.Tell(), decoder.TellFractional());
    }

    /// <summary>Decodes the packet and counts the symbols that match what was encoded.</summary>
    /// <param name="packet">The packet.</param>
    /// <param name="kinds">The kind of each symbol.</param>
    /// <param name="values">The value of each symbol.</param>
    /// <param name="limits">The limit of each symbol.</param>
    /// <returns>The number of symbols that matched.</returns>
    private static int Matches(byte[] packet, SymbolKind[] kinds, uint[] values, uint[] limits)
    {
        var decoder = new OpusRangeDecoder(packet);
        var matched = 0;
        for (var i = 0; i < kinds.Length; i++)
        {
            matched += Read(ref decoder, kinds[i], limits[i]) == values[i] ? 1 : 0;
        }

        return matched;
    }

    /// <summary>Reads one symbol of a kind.</summary>
    /// <param name="decoder">The decoder.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="limit">The kind's limit: the total, the bit count or the range.</param>
    /// <returns>The value.</returns>
    private static uint Read(ref OpusRangeDecoder decoder, SymbolKind kind, uint limit)
    {
        switch (kind)
        {
            case SymbolKind.Plain:
            {
                var symbol = decoder.Decode(Total);
                decoder.Update(symbol, symbol + 1, Total);
                return symbol;
            }

            case SymbolKind.Bit:
            {
                return decoder.ReadBit((int)limit) ? 1U : 0U;
            }

            case SymbolKind.Table:
            {
                return (uint)decoder.ReadSymbol(InverseCdf, TableBits);
            }

            case SymbolKind.Raw:
            {
                return decoder.ReadRaw((int)limit);
            }

            default:
            {
                return decoder.ReadUnsigned(limit);
            }
        }
    }

    /// <summary>Writes one symbol of a kind.</summary>
    /// <param name="encoder">The encoder.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="value">The value.</param>
    /// <param name="limit">The kind's limit.</param>
    private static void Encode(OpusRangeEncoder encoder, SymbolKind kind, uint value, uint limit)
    {
        switch (kind)
        {
            case SymbolKind.Plain:
            {
                encoder.Encode(value, value + 1, Total);
                break;
            }

            case SymbolKind.Bit:
            {
                encoder.EncodeBit(value == 1, (int)limit);
                break;
            }

            case SymbolKind.Table:
            {
                encoder.EncodeSymbol((int)value, InverseCdf, TableBits);
                break;
            }

            case SymbolKind.Raw:
            {
                encoder.EncodeRaw(value, (int)limit);
                break;
            }

            default:
            {
                encoder.EncodeUnsigned(value, limit);
                break;
            }
        }
    }

    /// <summary>Picks a random value and limit for a kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="state">The generator state.</param>
    /// <returns>The limit and the value.</returns>
    private static ValueAndLimit Pick(SymbolKind kind, ref uint state)
    {
        switch (kind)
        {
            case SymbolKind.Plain:
            {
                return new(Total, Next(ref state) % Total);
            }

            case SymbolKind.Bit:
            {
                var logProbability = 1 + (Next(ref state) % MaxLogProbability);
                return new(logProbability, Next(ref state) % BitValues);
            }

            case SymbolKind.Table:
            {
                return new(0, Next(ref state) % TableSymbols);
            }

            case SymbolKind.Raw:
            {
                var bits = 1 + (Next(ref state) % MaxRawBits);
                return new(bits, Next(ref state) & ((1U << (int)bits) - 1));
            }

            default:
            {
                var total = MinUniform + (Next(ref state) % MaxUniform);
                return new(total, Next(ref state) % total);
            }
        }
    }

    /// <summary>Gets the next number of a small repeatable generator.</summary>
    /// <param name="state">The generator state.</param>
    /// <returns>The number.</returns>
    private static uint Next(ref uint state)
    {
        state ^= state << ShiftA;
        state ^= state >> ShiftB;
        state ^= state << ShiftC;
        return state;
    }

    /// <summary>A random value with the limit it was drawn under.</summary>
    /// <param name="Limit">The limit.</param>
    /// <param name="Value">The value.</param>
    private readonly record struct ValueAndLimit(uint Limit, uint Value);

    /// <summary>What the decoder said about the bits it had used.</summary>
    /// <param name="Before">The bits before any symbol.</param>
    /// <param name="After">The bits after the symbols.</param>
    /// <param name="Fractional">The bits after the symbols, in eighths.</param>
    private readonly record struct TellReadings(int Before, int After, int Fractional);
}
