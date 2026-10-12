// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;

namespace HyperPdf.Core.Text.Fonts;

/// <summary>
/// Maps characters to glyphs through a font's <c>cmap</c> table, reading the font's own bytes with a binary search, so
/// nothing is copied. Segment maps (format 4) and full Unicode maps (format 12) are read; a full map is preferred.
/// </summary>
[DebuggerDisplay("CharacterMap: format {_format}")]
internal sealed class CharacterMap
{
    /// <summary>The segment map format.</summary>
    private const int SegmentFormat = 4;

    /// <summary>The full Unicode map format.</summary>
    private const int GroupFormat = 12;

    /// <summary>The bytes of the cmap header.</summary>
    private const int HeaderBytes = 4;

    /// <summary>The bytes of an encoding record.</summary>
    private const int EncodingBytes = 8;

    /// <summary>The offset of the subtable offset in an encoding record.</summary>
    private const int EncodingOffset = 4;

    /// <summary>The offset of the segment count (doubled) in a format 4 subtable.</summary>
    private const int SegCountOffset = 6;

    /// <summary>The offset of the end codes in a format 4 subtable.</summary>
    private const int EndCodesOffset = 14;

    /// <summary>The offset of the group count in a format 12 subtable.</summary>
    private const int GroupCountOffset = 12;

    /// <summary>The offset of the first group in a format 12 subtable.</summary>
    private const int GroupsOffset = 16;

    /// <summary>The bytes of a format 12 group.</summary>
    private const int GroupBytes = 12;

    /// <summary>The offset of a group's end code.</summary>
    private const int GroupEnd = 4;

    /// <summary>The offset of a group's first glyph.</summary>
    private const int GroupGlyph = 8;

    /// <summary>The bytes of a 16-bit value.</summary>
    private const int Int16Bytes = 2;

    /// <summary>The Unicode platform.</summary>
    private const int UnicodePlatform = 0;

    /// <summary>The Windows platform.</summary>
    private const int WindowsPlatform = 3;

    /// <summary>The Windows symbol encoding, whose characters sit at 0xF000 and up.</summary>
    private const int SymbolEncoding = 0;

    /// <summary>Where the symbol encoding puts characters.</summary>
    private const int SymbolBase = 0xF000;

    /// <summary>The rank of a full Unicode map, the best.</summary>
    private const int RankFull = 0;

    /// <summary>The rank of a Unicode segment map.</summary>
    private const int RankSegment = 1;

    /// <summary>The rank of a symbol map.</summary>
    private const int RankSymbol = 2;

    /// <summary>The rank of an unusable map.</summary>
    private const int RankNone = int.MaxValue;

    /// <summary>The largest Basic Multilingual Plane code point.</summary>
    private const int LastBmp = 0xFFFF;

    /// <summary>The font file.</summary>
    private readonly byte[] _data;

    /// <summary>Where the chosen subtable starts in the file.</summary>
    private readonly int _offset;

    /// <summary>The chosen subtable's format, or 0 when the font has none usable.</summary>
    private readonly int _format;

    /// <summary>Whether the subtable is a symbol map.</summary>
    private readonly bool _symbol;

    /// <summary>Initializes a new instance of the <see cref="CharacterMap"/> class.</summary>
    /// <param name="data">The font file.</param>
    /// <param name="tableOffset">The cmap table's offset.</param>
    /// <param name="tableLength">The cmap table's length.</param>
    internal CharacterMap(byte[] data, int tableOffset, int tableLength)
    {
        _data = data;
        var table = data.AsSpan(tableOffset, tableLength);
        if (table.Length < HeaderBytes)
        {
            return;
        }

        var count = BinaryPrimitives.ReadUInt16BigEndian(table[Int16Bytes..]);
        var bestRank = RankNone;
        for (var i = 0; i < count && HeaderBytes + ((i + 1) * EncodingBytes) <= table.Length; i++)
        {
            var record = table.Slice(HeaderBytes + (i * EncodingBytes), EncodingBytes);
            var subtable = (int)BinaryPrimitives.ReadUInt32BigEndian(record[EncodingOffset..]);
            if (subtable + Int16Bytes > table.Length)
            {
                continue;
            }

            var format = BinaryPrimitives.ReadUInt16BigEndian(table[subtable..]);
            var symbol = BinaryPrimitives.ReadUInt16BigEndian(record) == WindowsPlatform && BinaryPrimitives.ReadUInt16BigEndian(record[Int16Bytes..]) == SymbolEncoding;
            var rank = Rank(BinaryPrimitives.ReadUInt16BigEndian(record), format, symbol);
            if (rank < bestRank)
            {
                (bestRank, _offset, _format, _symbol) = (rank, tableOffset + subtable, format, symbol);
            }
        }
    }

    /// <summary>Gets a value indicating whether the font maps any characters.</summary>
    internal bool IsUsable => _format != 0;

    /// <summary>Gets the glyph for a character.</summary>
    /// <param name="codePoint">The Unicode code point.</param>
    /// <returns>The glyph, or 0 (the missing glyph).</returns>
    internal ushort Lookup(int codePoint)
    {
        if (_symbol && codePoint <= byte.MaxValue)
        {
            codePoint += SymbolBase;
        }

        return _format switch
        {
            SegmentFormat when codePoint <= LastBmp => LookupSegment(codePoint),
            GroupFormat => LookupGroup(codePoint),
            _ => 0,
        };
    }

    /// <summary>Appends every mapped character in a range with its glyph.</summary>
    /// <param name="first">The first code point.</param>
    /// <param name="last">The last code point.</param>
    /// <param name="output">Receives the characters and glyphs.</param>
    internal void Map(int first, int last, List<(int CodePoint, ushort Glyph)> output)
    {
        for (var codePoint = first; codePoint <= last; codePoint++)
        {
            var glyph = Lookup(codePoint);
            if (glyph != 0)
            {
                output.Add((codePoint, glyph));
            }
        }
    }

    /// <summary>Ranks a subtable; lower is better.</summary>
    /// <param name="platform">The platform.</param>
    /// <param name="format">The format.</param>
    /// <param name="symbol">Whether it is a symbol map.</param>
    /// <returns>The rank.</returns>
    private static int Rank(int platform, int format, bool symbol) => format switch
    {
        _ when platform is not (UnicodePlatform or WindowsPlatform) => RankNone,
        GroupFormat => RankFull,
        SegmentFormat when symbol => RankSymbol,
        SegmentFormat => RankSegment,
        _ => RankNone,
    };

    /// <summary>Reads a glyph from the segment holding a character.</summary>
    /// <param name="table">The subtable.</param>
    /// <param name="segment">The segment.</param>
    /// <param name="codePoint">The character.</param>
    /// <param name="arrays">Where the start codes, deltas and range offsets start.</param>
    /// <returns>The glyph.</returns>
    private static ushort SegmentGlyph(ReadOnlySpan<byte> table, int segment, int codePoint, (int Starts, int Deltas, int Ranges) arrays)
    {
        var start = BinaryPrimitives.ReadUInt16BigEndian(table[(arrays.Starts + (segment * Int16Bytes))..]);
        if (codePoint < start)
        {
            return 0;
        }

        var delta = BinaryPrimitives.ReadInt16BigEndian(table[(arrays.Deltas + (segment * Int16Bytes))..]);
        var rangeAt = arrays.Ranges + (segment * Int16Bytes);
        var range = BinaryPrimitives.ReadUInt16BigEndian(table[rangeAt..]);
        if (range == 0)
        {
            return (ushort)(codePoint + delta);
        }

        var glyphAt = rangeAt + range + ((codePoint - start) * Int16Bytes);
        if (glyphAt + Int16Bytes > table.Length)
        {
            return 0;
        }

        var glyph = BinaryPrimitives.ReadUInt16BigEndian(table[glyphAt..]);
        return glyph == 0 ? (ushort)0 : (ushort)(glyph + delta);
    }

    /// <summary>Looks a character up in a segment map.</summary>
    /// <param name="codePoint">The character.</param>
    /// <returns>The glyph.</returns>
    private ushort LookupSegment(int codePoint)
    {
        var table = _data.AsSpan(_offset);
        var segments = BinaryPrimitives.ReadUInt16BigEndian(table[SegCountOffset..]) / Int16Bytes;
        const int ends = EndCodesOffset;
        var starts = ends + (segments * Int16Bytes) + Int16Bytes;
        var deltas = starts + (segments * Int16Bytes);
        var ranges = deltas + (segments * Int16Bytes);
        if (ranges + (segments * Int16Bytes) > table.Length)
        {
            return 0;
        }

        var low = 0;
        var high = segments - 1;
        while (low <= high)
        {
            var middle = (low + high) >>> 1;
            if (BinaryPrimitives.ReadUInt16BigEndian(table[(ends + (middle * Int16Bytes))..]) < codePoint)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return low >= segments ? (ushort)0 : SegmentGlyph(table, low, codePoint, (starts, deltas, ranges));
    }

    /// <summary>Looks a character up in a full Unicode map.</summary>
    /// <param name="codePoint">The character.</param>
    /// <returns>The glyph.</returns>
    private ushort LookupGroup(int codePoint)
    {
        var table = _data.AsSpan(_offset);
        if (table.Length < GroupsOffset)
        {
            return 0;
        }

        var groups = (int)Math.Min(BinaryPrimitives.ReadUInt32BigEndian(table[GroupCountOffset..]), (uint)((table.Length - GroupsOffset) / GroupBytes));
        var low = 0;
        var high = groups - 1;
        while (low <= high)
        {
            var middle = (low + high) >>> 1;
            var group = table.Slice(GroupsOffset + (middle * GroupBytes), GroupBytes);
            if (BinaryPrimitives.ReadUInt32BigEndian(group[GroupEnd..]) < (uint)codePoint)
            {
                low = middle + 1;
            }
            else if (BinaryPrimitives.ReadUInt32BigEndian(group) > (uint)codePoint)
            {
                high = middle - 1;
            }
            else
            {
                return (ushort)(BinaryPrimitives.ReadUInt32BigEndian(group[GroupGlyph..]) + (uint)codePoint - BinaryPrimitives.ReadUInt32BigEndian(group));
            }
        }

        return 0;
    }
}
