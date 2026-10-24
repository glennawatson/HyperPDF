// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.H264;

/// <summary>Reads residual blocks coded with CAVLC, as clauses 7.3.5.3.2 and 9.2 of ITU-T H.264 describe.</summary>
internal static class H264Cavlc
{
    /// <summary>The symbols in a coeff_token table: TotalCoeff from 0 to 16 times 4 plus TrailingOnes.</summary>
    private const int TokenSymbols = 68;

    /// <summary>The values of total_zeros in a table for 4x4 blocks.</summary>
    private const int ZerosSymbols = 16;

    /// <summary>The number of total_zeros tables for 4x4 blocks.</summary>
    private const int ZerosTables = 15;

    /// <summary>The values of total_zeros in a table for 2x2 chroma DC blocks.</summary>
    private const int DcZerosSymbols = 4;

    /// <summary>The number of total_zeros tables for 2x2 chroma DC blocks.</summary>
    private const int DcZerosTables = 3;

    /// <summary>The values of run_before in a table.</summary>
    private const int RunSymbols = 15;

    /// <summary>The number of run_before tables.</summary>
    private const int RunTables = 7;

    /// <summary>The number of coeff_token tables.</summary>
    private const int TokenTables = 6;

    /// <summary>The table for nC below 2.</summary>
    private const int LowNc = 2;

    /// <summary>The table for nC from 2 to 3.</summary>
    private const int MediumNc = 4;

    /// <summary>The table for nC from 4 to 7.</summary>
    private const int HighNc = 8;

    /// <summary>The coeff_token table of 2x2 chroma DC blocks.</summary>
    private const int ChromaDcTable = 4;

    /// <summary>The index of the coeff_token table used when nC is 8 or more.</summary>
    private const int FixedTable = 3;

    /// <summary>The most levels in a block.</summary>
    private const int MaxLevels = 16;

    /// <summary>The level prefix from which the suffix has a size of its own.</summary>
    private const int EscapePrefix = 14;

    /// <summary>The level prefix from which the level is coded in long form.</summary>
    private const int LongPrefix = 15;

    /// <summary>The level prefix from which the long form adds an offset.</summary>
    private const int ExtraPrefix = 16;

    /// <summary>The bits of the level suffix when the prefix is 14 and the suffix length is 0.</summary>
    private const int EscapeSuffixBits = 4;

    /// <summary>The amount a level prefix of 15 or more is reduced by to get the suffix size.</summary>
    private const int LongSuffixOffset = 3;

    /// <summary>The offset added to the level code when the prefix is 15 and the suffix length is 0.</summary>
    private const int LongOffset = 15;

    /// <summary>The offset removed from the level code for prefixes of 16 or more.</summary>
    private const int ExtraOffset = 4096;

    /// <summary>The coeff_token table for nC from 4 to 7.</summary>
    private const int HighTable = 2;

    /// <summary>The amount added to the level code of the first level that is not a trailing one when fewer than 3 trailing ones were read.</summary>
    private const int FirstLevelBias = 2;

    /// <summary>The most trailing ones.</summary>
    private const int MaxTrailingOnes = 3;

    /// <summary>The largest suffix length.</summary>
    private const int MaxSuffixLength = 6;

    /// <summary>The number of coefficients in a block from which the suffix length starts at 1.</summary>
    private const int LongBlock = 10;

    /// <summary>The largest zeros left for which run_before has a table of its own.</summary>
    private const int RunTableLimit = 7;

    /// <summary>The coeff_token tables.</summary>
    private static readonly PrefixCodeTable[] Tokens = BuildTables(H264CavlcData.CoeffTokenLengths, H264CavlcData.CoeffTokenCodes, TokenTables, TokenSymbols);

    /// <summary>The total_zeros tables for 4x4 blocks.</summary>
    private static readonly PrefixCodeTable[] Zeros = BuildTables(H264CavlcData.TotalZerosLengths, H264CavlcData.TotalZerosCodes, ZerosTables, ZerosSymbols);

    /// <summary>The total_zeros tables for 2x2 chroma DC blocks.</summary>
    private static readonly PrefixCodeTable[] DcZeros = BuildTables(H264CavlcData.TotalZerosDc420Lengths, H264CavlcData.TotalZerosDc420Codes, DcZerosTables, DcZerosSymbols);

    /// <summary>The run_before tables.</summary>
    private static readonly PrefixCodeTable[] Runs = BuildTables(H264CavlcData.RunBeforeLengths, H264CavlcData.RunBeforeCodes, RunTables, RunSymbols);

    /// <summary>Reads one residual block.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="nc">The number of coefficients of the neighbouring blocks, which picks the coeff_token table; -1 for a 2x2 chroma DC block.</param>
    /// <param name="maxNumCoeff">The most coefficients the block has: 4, 15 or 16.</param>
    /// <param name="coefficients">Receives the levels in scan order; it must hold <paramref name="maxNumCoeff"/> values.</param>
    /// <returns>The number of non-zero coefficients.</returns>
    /// <exception cref="InvalidDataException">The bits are not a valid block.</exception>
    internal static int Read(ref MsbBitReader reader, int nc, int maxNumCoeff, scoped Span<int> coefficients)
    {
        coefficients[..maxNumCoeff].Clear();
        var token = Tokens[TokenTableOf(nc)].Decode(ref reader);
        if (token < 0)
        {
            throw new InvalidDataException("An H.264 coefficient token is not valid.");
        }

        var totalCoeff = token >> 2;
        var trailingOnes = token & MaxTrailingOnes;
        if (totalCoeff == 0)
        {
            return 0;
        }

        if (totalCoeff > maxNumCoeff)
        {
            throw new InvalidDataException("An H.264 block has too many coefficients.");
        }

        Span<int> levels = stackalloc int[MaxLevels];
        ReadLevels(ref reader, totalCoeff, trailingOnes, levels);
        var zerosLeft = totalCoeff < maxNumCoeff ? ReadTotalZeros(ref reader, totalCoeff, maxNumCoeff) : 0;
        PlaceLevels(ref reader, totalCoeff, zerosLeft, levels, coefficients[..maxNumCoeff]);
        return totalCoeff;
    }

    /// <summary>Builds the tables of one family from the flat arrays the generator wrote.</summary>
    /// <param name="lengths">The code lengths of all tables.</param>
    /// <param name="codes">The codes of all tables.</param>
    /// <param name="count">The number of tables.</param>
    /// <param name="symbols">The symbols in each table.</param>
    /// <returns>The tables.</returns>
    private static PrefixCodeTable[] BuildTables(ReadOnlySpan<byte> lengths, ReadOnlySpan<ushort> codes, int count, int symbols)
    {
        var tables = new PrefixCodeTable[count];
        var wide = new uint[symbols];
        for (var table = 0; table < count; table++)
        {
            for (var symbol = 0; symbol < symbols; symbol++)
            {
                wide[symbol] = codes[(table * symbols) + symbol];
            }

            tables[table] = new(lengths.Slice(table * symbols, symbols), wide);
        }

        return tables;
    }

    /// <summary>Finds the coeff_token table for an nC.</summary>
    /// <param name="nc">The nC, or -1 for 2x2 chroma DC blocks.</param>
    /// <returns>The table's index.</returns>
    private static int TokenTableOf(int nc)
    {
        if (nc < 0)
        {
            return ChromaDcTable;
        }

        if (nc < LowNc)
        {
            return 0;
        }

        if (nc < MediumNc)
        {
            return 1;
        }

        return nc < HighNc ? HighTable : FixedTable;
    }

    /// <summary>Reads the levels of the non-zero coefficients, highest frequency first.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="totalCoeff">The number of non-zero coefficients.</param>
    /// <param name="trailingOnes">The number of trailing coefficients with a magnitude of 1.</param>
    /// <param name="levels">Receives the levels.</param>
    private static void ReadLevels(ref MsbBitReader reader, int totalCoeff, int trailingOnes, scoped Span<int> levels)
    {
        var suffixLength = totalCoeff > LongBlock && trailingOnes < MaxTrailingOnes ? 1 : 0;
        for (var index = 0; index < totalCoeff; index++)
        {
            if (index < trailingOnes)
            {
                levels[index] = reader.ReadBit() ? -1 : 1;
                continue;
            }

            var levelCode = ReadLevelCode(ref reader, suffixLength);
            levelCode += index == trailingOnes && trailingOnes < MaxTrailingOnes ? FirstLevelBias : 0;
            levels[index] = LevelOf(levelCode);
            suffixLength = NextSuffixLength(suffixLength, levels[index]);
        }
    }

    /// <summary>Turns a level code into a level: even codes are positive, odd codes negative.</summary>
    /// <param name="levelCode">The level code.</param>
    /// <returns>The level.</returns>
    private static int LevelOf(int levelCode) => (levelCode & 1) == 0 ? (levelCode + FirstLevelBias) >> 1 : (-levelCode - 1) >> 1;

    /// <summary>Works out the suffix length for the next level.</summary>
    /// <param name="suffixLength">The current suffix length.</param>
    /// <param name="level">The level just read.</param>
    /// <returns>The next suffix length.</returns>
    private static int NextSuffixLength(int suffixLength, int level)
    {
        var length = suffixLength == 0 ? 1 : suffixLength;
        return Math.Abs(level) > (MaxTrailingOnes << (length - 1)) && length < MaxSuffixLength ? length + 1 : length;
    }

    /// <summary>Reads a level prefix and suffix and works out the level code.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="suffixLength">The current suffix length.</param>
    /// <returns>The level code.</returns>
    private static int ReadLevelCode(ref MsbBitReader reader, int suffixLength)
    {
        var prefix = reader.ReadUnary();
        var levelCode = Math.Min(LongPrefix, prefix) << suffixLength;
        if (suffixLength > 0 || prefix >= EscapePrefix)
        {
            var size = suffixLength;
            if (prefix == EscapePrefix && suffixLength == 0)
            {
                size = EscapeSuffixBits;
            }
            else if (prefix >= LongPrefix)
            {
                size = prefix - LongSuffixOffset;
            }

            levelCode += size > 0 ? (int)reader.Read(size) : 0;
        }

        levelCode += prefix >= LongPrefix && suffixLength == 0 ? LongOffset : 0;
        levelCode += prefix >= ExtraPrefix ? (1 << (prefix - LongSuffixOffset)) - ExtraOffset : 0;
        return levelCode;
    }

    /// <summary>Reads total_zeros.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="totalCoeff">The number of non-zero coefficients, which picks the table.</param>
    /// <param name="maxNumCoeff">The most coefficients the block has.</param>
    /// <returns>The number of zeros before the last non-zero coefficient.</returns>
    /// <exception cref="InvalidDataException">The bits are not a valid code.</exception>
    private static int ReadTotalZeros(ref MsbBitReader reader, int totalCoeff, int maxNumCoeff)
    {
        var table = maxNumCoeff == DcZerosSymbols ? DcZeros[totalCoeff - 1] : Zeros[totalCoeff - 1];
        var zeros = table.Decode(ref reader);
        return zeros >= 0 ? zeros : throw new InvalidDataException("An H.264 total_zeros code is not valid.");
    }

    /// <summary>Reads the runs of zeros and places the levels among the coefficients.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="totalCoeff">The number of non-zero coefficients.</param>
    /// <param name="zerosLeft">The number of zeros before the last non-zero coefficient.</param>
    /// <param name="levels">The levels, highest frequency first.</param>
    /// <param name="coefficients">Receives the coefficients in scan order.</param>
    /// <exception cref="InvalidDataException">The bits are not valid or the levels do not fit.</exception>
    private static void PlaceLevels(ref MsbBitReader reader, int totalCoeff, int zerosLeft, scoped ReadOnlySpan<int> levels, scoped Span<int> coefficients)
    {
        var position = totalCoeff + zerosLeft - 1;
        for (var index = 0; index < totalCoeff; index++)
        {
            if (position < 0 || position >= coefficients.Length)
            {
                throw new InvalidDataException("An H.264 block has coefficients outside its range.");
            }

            coefficients[position] = levels[index];
            var run = 0;
            if (zerosLeft > 0 && index < totalCoeff - 1)
            {
                run = Runs[Math.Min(zerosLeft, RunTableLimit) - 1].Decode(ref reader);
                if (run < 0 || run > zerosLeft)
                {
                    throw new InvalidDataException("An H.264 run_before code is not valid.");
                }
            }

            zerosLeft -= run;
            position -= 1 + run;
        }
    }
}
