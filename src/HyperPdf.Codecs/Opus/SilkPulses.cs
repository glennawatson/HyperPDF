// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>
/// Reads the excitation of a SILK frame (RFC 6716 section 4.2.7.8): the number of pulses in each block of 16, how they are shared among
/// the entries by splitting the block in halves again and again, the bits below the pulse counts that the loudest blocks need, and the signs.
/// </summary>
internal static class SilkPulses
{
    /// <summary>The bits of every table's total.</summary>
    private const int TotalBits = 8;

    /// <summary>The number of symbols in a distribution of pulse counts.</summary>
    private const int PulseSymbols = SilkConstants.MaxPulses + 2;

    /// <summary>The symbol of the pulse count that says more bits follow.</summary>
    private const int MoreBits = SilkConstants.MaxPulses + 1;

    /// <summary>The count of extra bits after which the escape symbol is no longer possible.</summary>
    private const int MaxExtraBits = 10;

    /// <summary>The number of rate level distributions for each signal type group.</summary>
    private const int RateLevelSymbols = 9;

    /// <summary>The bits the count of extra bits is shifted up by to sit above the pulse count.</summary>
    private const int ExtraShift = 5;

    /// <summary>The mask of the pulse count in a block's sum.</summary>
    private const int CountMask = 0x1F;

    /// <summary>The highest index of the sign distributions, for a block with six pulses or more.</summary>
    private const int SignRows = 6;

    /// <summary>The entries a sign table holds for each combination of signal type and quantisation offset.</summary>
    private const int SignTableSize = 7;

    /// <summary>The signal type's two bits the sign table's row is built from.</summary>
    private const int SignalTypeShift = 1;

    /// <summary>Reads the excitation pulses of a frame.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="pulses">Receives the pulses with their signs, one for each sample, rounded up to a whole block.</param>
    /// <param name="signalType">The signal type.</param>
    /// <param name="quantOffsetType">The quantisation offset type.</param>
    /// <param name="frameLength">The samples of the frame.</param>
    internal static void Decode(ref OpusRangeDecoder decoder, Span<int> pulses, int signalType, int quantOffsetType, int frameLength)
    {
        var rateLevel = decoder.ReadSymbol(SilkTables.RateLevelIcdf.Slice((signalType >> 1) * RateLevelSymbols, RateLevelSymbols), TotalBits);
        var blocks = (frameLength + SilkConstants.ShellBlockLength - 1) >> SilkConstants.ShellBlockLengthLog2;
        Span<int> sums = stackalloc int[SilkConstants.MaxShellBlocks];
        Span<int> extraBits = stackalloc int[SilkConstants.MaxShellBlocks];
        ReadBlockSums(ref decoder, rateLevel, blocks, sums, extraBits);
        for (var i = 0; i < blocks; i++)
        {
            var block = pulses.Slice(i * SilkConstants.ShellBlockLength, SilkConstants.ShellBlockLength);
            if (sums[i] > 0)
            {
                DecodeShell(ref decoder, block, sums[i]);
            }
            else
            {
                block.Clear();
            }
        }

        for (var i = 0; i < blocks; i++)
        {
            if (extraBits[i] <= 0)
            {
                continue;
            }

            ReadExtraBits(ref decoder, pulses.Slice(i * SilkConstants.ShellBlockLength, SilkConstants.ShellBlockLength), extraBits[i]);
            sums[i] |= extraBits[i] << ExtraShift;
        }

        DecodeSigns(ref decoder, pulses, frameLength, signalType, quantOffsetType, sums);
    }

    /// <summary>Reads the number of pulses of each block, and how many extra bits each block needs.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="rateLevel">The rate level, which picks the distribution.</param>
    /// <param name="blocks">The blocks.</param>
    /// <param name="sums">Receives the pulses of each block.</param>
    /// <param name="extraBits">Receives the extra bits of each block.</param>
    private static void ReadBlockSums(ref OpusRangeDecoder decoder, int rateLevel, int blocks, scoped Span<int> sums, scoped Span<int> extraBits)
    {
        var table = SilkTables.PulsesPerBlockIcdf;
        var own = table.Slice(rateLevel * PulseSymbols, PulseSymbols);
        for (var i = 0; i < blocks; i++)
        {
            extraBits[i] = 0;
            sums[i] = decoder.ReadSymbol(own, TotalBits);
            while (sums[i] == MoreBits)
            {
                extraBits[i]++;
                var last = table[(((SilkConstants.RateLevels - 1) * PulseSymbols) + (extraBits[i] == MaxExtraBits ? 1 : 0))..];
                sums[i] = decoder.ReadSymbol(last, TotalBits);
            }
        }
    }

    /// <summary>Reads the bits below the pulse counts of a block that has them.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="block">The block's pulses, which are shifted up and given their extra bits.</param>
    /// <param name="bits">The extra bits of each pulse.</param>
    private static void ReadExtraBits(ref OpusRangeDecoder decoder, Span<int> block, int bits)
    {
        for (var k = 0; k < block.Length; k++)
        {
            var magnitude = block[k];
            for (var j = 0; j < bits; j++)
            {
                magnitude = (magnitude << 1) + decoder.ReadSymbol(SilkTables.LsbIcdf, TotalBits);
            }

            block[k] = magnitude;
        }
    }

    /// <summary>Shares the pulses of a block of 16 among its entries by splitting it in halves again and again.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="block">Receives the pulses of the block: 16, 8, 4 or 2 entries.</param>
    /// <param name="total">The pulses to share.</param>
    private static void DecodeShell(ref OpusRangeDecoder decoder, Span<int> block, int total)
    {
        if (block.Length == 1)
        {
            block[0] = total;
            return;
        }

        var first = 0;
        if (total > 0)
        {
            var table = GetShellTable(block.Length);
            first = decoder.ReadSymbol(table[SilkTables.ShellCodeOffsets[total]..], TotalBits);
        }

        var half = block.Length >> 1;
        DecodeShell(ref decoder, block[..half], first);
        DecodeShell(ref decoder, block[half..], total - first);
    }

    /// <summary>Gets the distributions that split a block of a size.</summary>
    /// <param name="length">The entries in the block.</param>
    /// <returns>The distributions.</returns>
    private static ReadOnlySpan<byte> GetShellTable(int length) => length switch
    {
        SilkConstants.ShellBlockLength => SilkTables.ShellCode3,
        SilkConstants.ShellBlockLength >> 1 => SilkTables.ShellCode2,
        SilkConstants.ShellBlockLength >> 2 => SilkTables.ShellCode1,
        _ => SilkTables.ShellCode0,
    };

    /// <summary>Reads the sign of every pulse that is not zero.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="pulses">The pulses, to which the signs are applied.</param>
    /// <param name="frameLength">The samples of the frame.</param>
    /// <param name="signalType">The signal type.</param>
    /// <param name="quantOffsetType">The quantisation offset type.</param>
    /// <param name="sums">For each block, its pulse count, with its extra bits above bit 5.</param>
    private static void DecodeSigns(ref OpusRangeDecoder decoder, Span<int> pulses, int frameLength, int signalType, int quantOffsetType, scoped ReadOnlySpan<int> sums)
    {
        var row = SignTableSize * (quantOffsetType + (signalType << SignalTypeShift));
        var table = SilkTables.SignIcdf.Slice(row, SignTableSize);
        var blocks = (frameLength + (SilkConstants.ShellBlockLength >> 1)) >> SilkConstants.ShellBlockLengthLog2;
        Span<byte> icdf = stackalloc byte[2];
        for (var i = 0; i < blocks; i++)
        {
            var count = sums[i];
            if (count <= 0)
            {
                continue;
            }

            icdf[0] = table[Math.Min(count & CountMask, SignRows)];
            icdf[1] = 0;
            var block = pulses.Slice(i * SilkConstants.ShellBlockLength, SilkConstants.ShellBlockLength);
            for (var j = 0; j < block.Length; j++)
            {
                if (block[j] > 0)
                {
                    block[j] *= (decoder.ReadSymbol(icdf, TotalBits) << 1) - 1;
                }
            }
        }
    }
}
