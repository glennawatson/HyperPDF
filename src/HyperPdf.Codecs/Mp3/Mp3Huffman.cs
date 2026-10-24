// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.Mp3;

/// <summary>Reads the Huffman coded part of a granule: the big values in up to three regions, then the quadruples of values that are 0 or 1.</summary>
internal static class Mp3Huffman
{
    /// <summary>The number of big value tables in the standard.</summary>
    private const int TableCount = 32;

    /// <summary>The value that is followed by extra bits.</summary>
    private const int EscapeValue = 15;

    /// <summary>The bits of the quadruple lookup.</summary>
    private const int QuadBits = 6;

    /// <summary>The shift of the quadruple in a packed code.</summary>
    private const int QuadShift = 24;

    /// <summary>The shift of the code length in a packed code.</summary>
    private const int LengthShift = 19;

    /// <summary>The mask of the code length.</summary>
    private const uint LengthMask = 0x1F;

    /// <summary>The mask of the code in a packed code.</summary>
    private const uint CodeMask = 0x7_FFFF;

    /// <summary>The mask of a quadruple.</summary>
    private const uint QuadMask = 0xF;

    /// <summary>The shift of the quadruple in a quadruple lookup entry.</summary>
    private const int QuadEntryShift = 8;

    /// <summary>The mask of the code length in a quadruple lookup entry.</summary>
    private const uint EntryLengthMask = 0xFF;

    /// <summary>The number of values in a pair.</summary>
    private const int PairSize = 2;

    /// <summary>The number of values in a quadruple.</summary>
    private const int QuadValues = 4;

    /// <summary>The scale factor entries in the first region of a granule of short windows.</summary>
    private const int ShortRegion0Entries = 9;

    /// <summary>The scale factor entries in the first region of a granule that switches windows but not to short ones.</summary>
    private const int SwitchedRegion0Entries = 8;

    /// <summary>The tables of big values, with a gap where the standard has none.</summary>
    private static readonly Mp3HuffmanTable?[] BigTables = BuildBigTables();

    /// <summary>The two tables of quadruples, as lookups of <see cref="QuadBits"/> bits.</summary>
    private static readonly uint[][] QuadTables = [BuildQuadTable(Mp3TableData.QuadCodesA), BuildQuadTable(Mp3TableData.QuadCodesB)];

    /// <summary>Reads the coefficients of a granule as whole numbers.</summary>
    /// <param name="reader">The bits, positioned at the first Huffman code.</param>
    /// <param name="info">The coding of the granule.</param>
    /// <param name="widths">The widths of the scale factor bands.</param>
    /// <param name="endBit">The bit where the granule's data ends.</param>
    /// <param name="values">Receives the 576 values, with zeros beyond the last one coded.</param>
    /// <param name="used">Receives the number of values up to the last one coded.</param>
    /// <returns><see langword="false"/> when the data names a table the standard does not have.</returns>
    internal static bool Decode(ref MsbBitReader reader, Mp3GranuleChannel info, scoped ReadOnlySpan<byte> widths, long endBit, Span<int> values, out int used)
    {
        values.Clear();
        used = 0;
        var big = Math.Min(info.BigValues * PairSize, Mp3Tables.GranuleSamples);
        var region0 = RegionZeroEntries(info);
        var region0End = Math.Min(LinesIn(widths, region0), big);
        var region1End = Math.Min(LinesIn(widths, region0 + info.Region1Count + 1), big);
        var line = 0;
        if (!ReadRegion(ref reader, info.TableSelect[0], values, ref line, region0End)
            || !ReadRegion(ref reader, info.TableSelect[1], values, ref line, region1End)
            || !ReadRegion(ref reader, info.TableSelect[2], values, ref line, big))
        {
            return false;
        }

        ReadQuads(ref reader, QuadTables[info.Count1Table], values, ref line, endBit);
        used = line;
        return true;
    }

    /// <summary>Gets the number of scale factor entries in the first region.</summary>
    /// <param name="info">The coding of the granule.</param>
    /// <returns>The entries.</returns>
    private static int RegionZeroEntries(Mp3GranuleChannel info)
    {
        if (!info.WindowSwitching)
        {
            return info.Region0Count + 1;
        }

        return info.IsShort && !info.MixedBlock ? ShortRegion0Entries : SwitchedRegion0Entries;
    }

    /// <summary>Adds up the widths of the first entries.</summary>
    /// <param name="widths">The widths.</param>
    /// <param name="entries">How many entries to add up.</param>
    /// <returns>The lines they cover.</returns>
    private static int LinesIn(ReadOnlySpan<byte> widths, int entries)
    {
        var lines = 0;
        for (var index = 0; index < entries && index < widths.Length; index++)
        {
            lines += widths[index];
        }

        return entries >= widths.Length ? Mp3Tables.GranuleSamples : lines;
    }

    /// <summary>Reads the pairs of one region.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="tableNumber">The table.</param>
    /// <param name="values">Receives the values.</param>
    /// <param name="line">The next value to read; moved on.</param>
    /// <param name="end">Where the region ends.</param>
    /// <returns><see langword="false"/> when the table is not one the standard has.</returns>
    private static bool ReadRegion(ref MsbBitReader reader, int tableNumber, Span<int> values, ref int line, int end)
    {
        if (line >= end)
        {
            return true;
        }

        var table = BigTables[tableNumber];
        if (table is null)
        {
            if (tableNumber == 0)
            {
                // Table 0 has no codes: the region is all zeros and takes no bits.
                line = end;
                return true;
            }

            return false;
        }

        while (line < end)
        {
            ReadPair(ref reader, table, values, line);
            line += PairSize;
        }

        return true;
    }

    /// <summary>Reads one pair of values and their signs.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="table">The table.</param>
    /// <param name="values">Receives the values.</param>
    /// <param name="line">The index of the first value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReadPair(ref MsbBitReader reader, Mp3HuffmanTable table, Span<int> values, int line)
    {
        var entry = table.Primary[reader.Peek(Mp3HuffmanTable.PrimaryBits)];
        if ((entry & Mp3HuffmanTable.SecondFlag) != 0)
        {
            reader.Skip(Mp3HuffmanTable.PrimaryBits);
            var offset = (int)(entry & Mp3HuffmanTable.OffsetMask);
            entry = table.Secondary[offset + (int)reader.Peek((int)((entry >> Mp3HuffmanTable.BitsShift) & Mp3HuffmanTable.BitsMask))];
        }

        reader.Skip((int)(entry & Mp3HuffmanTable.BitsMask));
        values[line] = ReadSigned(ref reader, (int)((entry >> Mp3HuffmanTable.FirstShift) & Mp3HuffmanTable.ValueMask), table.LinkBits);
        values[line + 1] = ReadSigned(ref reader, (int)((entry >> Mp3HuffmanTable.SecondShift) & Mp3HuffmanTable.ValueMask), table.LinkBits);
    }

    /// <summary>Adds the extra bits of an escaped value and applies its sign bit.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="value">The value from the table.</param>
    /// <param name="linkBits">The extra bits a value of 15 is followed by.</param>
    /// <returns>The value with its sign.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadSigned(ref MsbBitReader reader, int value, int linkBits)
    {
        if (value == EscapeValue && linkBits != 0)
        {
            value += (int)reader.Read(linkBits);
        }

        return value != 0 && reader.ReadBit() ? -value : value;
    }

    /// <summary>Reads the quadruples of values that are 0 or 1 until the granule's data ends.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="table">The quadruple lookup.</param>
    /// <param name="values">Receives the values.</param>
    /// <param name="line">The next value to read; moved on.</param>
    /// <param name="endBit">The bit where the granule's data ends.</param>
    private static void ReadQuads(ref MsbBitReader reader, uint[] table, Span<int> values, ref int line, long endBit)
    {
        while (line + QuadValues <= values.Length && reader.BitPosition < endBit)
        {
            var entry = table[reader.Peek(QuadBits)];
            reader.Skip((int)(entry & EntryLengthMask));
            if (reader.BitPosition > endBit)
            {
                // The code runs past the end of the data, so it was padding.
                return;
            }

            var quad = (int)(entry >> QuadEntryShift);
            for (var index = 0; index < QuadValues; index++)
            {
                if (((quad >> (QuadValues - 1 - index)) & 1) != 0)
                {
                    values[line + index] = reader.ReadBit() ? -1 : 1;
                }
            }

            line += QuadValues;
        }
    }

    /// <summary>Builds every big value table.</summary>
    /// <returns>The tables.</returns>
    private static Mp3HuffmanTable?[] BuildBigTables()
    {
        var tables = new Mp3HuffmanTable?[TableCount];
        var starts = Mp3TableData.BigValueStarts;
        var linkBits = Mp3TableData.LinkBits;
        for (var table = 0; table < TableCount; table++)
        {
            var codes = Mp3TableData.BigValueCodes[starts[table]..starts[table + 1]];
            tables[table] = codes.IsEmpty ? null : Mp3HuffmanTable.Build(codes, linkBits[table]);
        }

        return tables;
    }

    /// <summary>Builds the lookup of a table of quadruples.</summary>
    /// <param name="codes">The codes as packed in <see cref="Mp3TableData"/>.</param>
    /// <returns>The lookup: the quadruple in bits 8 and up and the code length below.</returns>
    private static uint[] BuildQuadTable(ReadOnlySpan<uint> codes)
    {
        var lookup = new uint[1 << QuadBits];
        foreach (var packed in codes)
        {
            var length = (int)((packed >> LengthShift) & LengthMask);
            var code = (int)(packed & CodeMask);
            var quad = (packed >> QuadShift) & QuadMask;
            lookup.AsSpan(code << (QuadBits - length), 1 << (QuadBits - length)).Fill((quad << QuadEntryShift) | (uint)length);
        }

        return lookup;
    }
}
