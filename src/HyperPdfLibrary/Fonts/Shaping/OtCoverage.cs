// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>Reads Coverage tables (OpenType, "Common table formats": Coverage table), which list glyphs in sorted order.</summary>
internal static class OtCoverage
{
    /// <summary>The offset of the glyph count or range count.</summary>
    private const int CountOffset = 2;

    /// <summary>The offset of the first glyph or range.</summary>
    private const int ArrayOffset = 4;

    /// <summary>The bytes of a range record: start glyph, end glyph, start coverage index.</summary>
    private const int RangeBytes = 6;

    /// <summary>The offset of the end glyph in a range record.</summary>
    private const int RangeEnd = 2;

    /// <summary>The offset of the start coverage index in a range record.</summary>
    private const int RangeIndex = 4;

    /// <summary>The format that lists glyphs one by one.</summary>
    private const int GlyphListFormat = 1;

    /// <summary>The format that lists ranges of glyphs.</summary>
    private const int RangeListFormat = 2;

    /// <summary>Finds a glyph's coverage index.</summary>
    /// <param name="table">The layout table.</param>
    /// <param name="offset">The offset of the Coverage table, or zero for none.</param>
    /// <param name="glyph">The glyph.</param>
    /// <returns>The index in the coverage, or -1 when the glyph is not covered.</returns>
    internal static int IndexOf(ReadOnlySpan<byte> table, int offset, int glyph)
    {
        if (offset <= 0 || offset >= table.Length)
        {
            return -1;
        }

        var coverage = table[offset..];
        var count = FontBytes.U16(coverage, CountOffset);
        return FontBytes.U16(coverage, 0) switch
        {
            GlyphListFormat => SearchGlyphs(coverage, count, glyph),
            RangeListFormat => SearchRanges(coverage, count, glyph),
            _ => -1,
        };
    }

    /// <summary>Searches a list of glyphs.</summary>
    /// <param name="coverage">The Coverage table.</param>
    /// <param name="count">The number of glyphs.</param>
    /// <param name="glyph">The glyph.</param>
    /// <returns>The index, or -1.</returns>
    private static int SearchGlyphs(ReadOnlySpan<byte> coverage, int count, int glyph)
    {
        var low = 0;
        var high = count - 1;
        while (low <= high)
        {
            var middle = (low + high) >>> 1;
            var value = FontBytes.U16(coverage, ArrayOffset + (middle * FontBytes.U16Size));
            if (value == glyph)
            {
                return middle;
            }

            if (value < glyph)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return -1;
    }

    /// <summary>Searches a list of glyph ranges.</summary>
    /// <param name="coverage">The Coverage table.</param>
    /// <param name="count">The number of ranges.</param>
    /// <param name="glyph">The glyph.</param>
    /// <returns>The index, or -1.</returns>
    private static int SearchRanges(ReadOnlySpan<byte> coverage, int count, int glyph)
    {
        var low = 0;
        var high = count - 1;
        while (low <= high)
        {
            var middle = (low + high) >>> 1;
            var record = ArrayOffset + (middle * RangeBytes);
            if (glyph < FontBytes.U16(coverage, record))
            {
                high = middle - 1;
            }
            else if (glyph > FontBytes.U16(coverage, record + RangeEnd))
            {
                low = middle + 1;
            }
            else
            {
                return FontBytes.U16(coverage, record + RangeIndex) + glyph - FontBytes.U16(coverage, record);
            }
        }

        return -1;
    }
}
