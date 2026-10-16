// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>
/// Reads TrueType 'cmap' subtables in place. Formats 0, 4, 6, 12 and 13 are searched directly in the font data, so a
/// lookup does not allocate.
/// </summary>
internal static class TrueTypeCmap
{
    /// <summary>The byte encoding table format.</summary>
    internal const int ByteFormat = 0;

    /// <summary>The segment mapping to delta values format.</summary>
    internal const int SegmentFormat = 4;

    /// <summary>The trimmed table mapping format.</summary>
    internal const int TrimmedFormat = 6;

    /// <summary>The segmented coverage format.</summary>
    internal const int CoverageFormat = 12;

    /// <summary>The many-to-one range mapping format.</summary>
    internal const int ManyToOneFormat = 13;

    /// <summary>The offset of the glyph array in a format 0 table.</summary>
    private const int ByteGlyphsOffset = 6;

    /// <summary>The number of codes a format 0 table maps.</summary>
    private const int ByteCodeCount = 256;

    /// <summary>The offset of the doubled segment count in a format 4 table.</summary>
    private const int SegCountOffset = 6;

    /// <summary>The offset of the end codes in a format 4 table.</summary>
    private const int EndCodesOffset = 14;

    /// <summary>The largest code a 16-bit table maps.</summary>
    private const int MaxBmp = 0xFFFF;

    /// <summary>The offset of the first code in a format 6 table.</summary>
    private const int TrimmedFirstOffset = 6;

    /// <summary>The offset of the entry count in a format 6 table.</summary>
    private const int TrimmedCountOffset = 8;

    /// <summary>The offset of the glyph array in a format 6 table.</summary>
    private const int TrimmedGlyphsOffset = 10;

    /// <summary>The offset of the group count in a format 12 or 13 table.</summary>
    private const int GroupCountOffset = 12;

    /// <summary>The offset of the groups in a format 12 or 13 table.</summary>
    private const int GroupsOffset = 16;

    /// <summary>The size of one group.</summary>
    private const int GroupSize = 12;

    /// <summary>The offset of a group's end code.</summary>
    private const int GroupEnd = 4;

    /// <summary>The offset of a group's glyph.</summary>
    private const int GroupGlyph = 8;

    /// <summary>Determines whether a subtable has a format this reader supports.</summary>
    /// <param name="data">The font data.</param>
    /// <param name="table">The subtable offset.</param>
    /// <returns><see langword="true"/> when supported.</returns>
    internal static bool IsSupported(ReadOnlySpan<byte> data, int table) =>
        table >= 0 && table < data.Length && FontBytes.U16(data, table) is ByteFormat or SegmentFormat or TrimmedFormat or CoverageFormat or ManyToOneFormat;

    /// <summary>Looks up a code.</summary>
    /// <param name="data">The font data.</param>
    /// <param name="table">The subtable offset, or -1.</param>
    /// <param name="code">The code.</param>
    /// <returns>The glyph id, or zero when unmapped.</returns>
    /// <exception cref="PdfException">The parsed range exceeds the available data or its numeric width.</exception>
    internal static int Lookup(ReadOnlySpan<byte> data, int table, int code)
    {
        if (table < 0 || code < 0)
        {
            return 0;
        }

        if (table > data.Length - FontBytes.U16Size)
        {
            throw new PdfException(PdfError.Format, "The TrueType cmap offset exceeds its data.");
        }

        return FontBytes.U16(data, table) switch
        {
            ByteFormat => code < ByteCodeCount ? FontBytes.U8(data, table + ByteGlyphsOffset + code) : 0,
            SegmentFormat => LookupSegment(data, table, code),
            TrimmedFormat => LookupTrimmed(data, table, code),
            CoverageFormat => LookupGroups(data, table, code, false),
            ManyToOneFormat => LookupGroups(data, table, code, true),
            _ => 0,
        };
    }

    /// <summary>Looks up a code in a format 4 table.</summary>
    /// <param name="data">The font data.</param>
    /// <param name="table">The subtable offset.</param>
    /// <param name="code">The code.</param>
    /// <returns>The glyph id, or zero.</returns>
    private static int LookupSegment(ReadOnlySpan<byte> data, int table, int code)
    {
        if (code > MaxBmp)
        {
            return 0;
        }

        var segCount = FontBytes.U16(data, table + SegCountOffset) / FontBytes.U16Size;
        var ends = table + EndCodesOffset;
        var segment = FindSegment(data, ends, segCount, code);
        if (segment < 0)
        {
            return 0;
        }

        var segBytes = segCount * FontBytes.U16Size;
        var starts = ends + segBytes + FontBytes.U16Size;
        var deltas = starts + segBytes;
        var rangeOffsets = deltas + segBytes;
        var start = FontBytes.U16(data, starts + (segment * FontBytes.U16Size));
        if (code < start)
        {
            return 0;
        }

        var delta = FontBytes.U16(data, deltas + (segment * FontBytes.U16Size));
        var rangeOffsetAt = rangeOffsets + (segment * FontBytes.U16Size);
        var rangeOffset = FontBytes.U16(data, rangeOffsetAt);
        if (rangeOffset == 0)
        {
            return (code + delta) & MaxBmp;
        }

        var glyph = FontBytes.U16(data, rangeOffsetAt + rangeOffset + ((code - start) * FontBytes.U16Size));
        return glyph == 0 ? 0 : (glyph + delta) & MaxBmp;
    }

    /// <summary>Finds the first segment whose end code is at least the code.</summary>
    /// <param name="data">The font data.</param>
    /// <param name="ends">The offset of the end codes.</param>
    /// <param name="segCount">The number of segments.</param>
    /// <param name="code">The code.</param>
    /// <returns>The segment, or -1.</returns>
    private static int FindSegment(ReadOnlySpan<byte> data, int ends, int segCount, int code)
    {
        var low = 0;
        var high = segCount;
        while (low < high)
        {
            var middle = (int)((uint)(low + high) >> 1);
            if (FontBytes.U16(data, ends + (middle * FontBytes.U16Size)) < code)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low < segCount ? low : -1;
    }

    /// <summary>Looks up a code in a format 6 table.</summary>
    /// <param name="data">The font data.</param>
    /// <param name="table">The subtable offset.</param>
    /// <param name="code">The code.</param>
    /// <returns>The glyph id, or zero.</returns>
    private static int LookupTrimmed(ReadOnlySpan<byte> data, int table, int code)
    {
        var index = code - FontBytes.U16(data, table + TrimmedFirstOffset);
        return index >= 0 && index < FontBytes.U16(data, table + TrimmedCountOffset)
            ? FontBytes.U16(data, table + TrimmedGlyphsOffset + (index * FontBytes.U16Size))
            : 0;
    }

    /// <summary>Looks up a code in a format 12 or 13 table.</summary>
    /// <param name="data">The font data.</param>
    /// <param name="table">The subtable offset.</param>
    /// <param name="code">The code.</param>
    /// <param name="constant">Whether every code in a group maps to the same glyph (format 13).</param>
    /// <returns>The glyph id, or zero.</returns>
    /// <exception cref="PdfException">The parsed range exceeds the available data or its numeric width.</exception>
    private static int LookupGroups(ReadOnlySpan<byte> data, int table, int code, bool constant)
    {
        var available = Math.Max((data.Length - table - GroupsOffset) / GroupSize, 0);
        var encodedCount = FontBytes.U32(data, table + GroupCountOffset);
        if (encodedCount > available)
        {
            throw new PdfException(PdfError.Format, "The TrueType cmap group count exceeds its data.");
        }

        var count = (int)encodedCount;
        var low = 0;
        var high = count - 1;
        while (low <= high)
        {
            var middle = (int)((uint)(low + high) >> 1);
            var group = table + GroupsOffset + (middle * GroupSize);
            var first = FontBytes.U32(data, group);
            if ((uint)code < first)
            {
                high = middle - 1;
            }
            else if ((uint)code > FontBytes.U32(data, group + GroupEnd))
            {
                low = middle + 1;
            }
            else
            {
                var glyph = (ulong)FontBytes.U32(data, group + GroupGlyph) + (constant ? 0 : (uint)code - first);
                if (glyph > int.MaxValue)
                {
                    throw new PdfException(PdfError.Format, "The TrueType cmap glyph range exceeds its signed width.");
                }

                return (int)glyph;
            }
        }

        return 0;
    }
}
