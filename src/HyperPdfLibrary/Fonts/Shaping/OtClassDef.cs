// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>Reads Class Definition tables (OpenType, "Common table formats": Class Definition table).</summary>
internal static class OtClassDef
{
    /// <summary>The offset of the first glyph of a format 1 table, or of the range count of a format 2 table.</summary>
    private const int FirstOffset = 2;

    /// <summary>The offset of the glyph count in a format 1 table.</summary>
    private const int GlyphCountOffset = 4;

    /// <summary>The offset of the class array in a format 1 table.</summary>
    private const int ClassArrayOffset = 6;

    /// <summary>The offset of the first range record in a format 2 table.</summary>
    private const int RangesOffset = 4;

    /// <summary>The bytes of a range record: start glyph, end glyph, class.</summary>
    private const int RangeBytes = 6;

    /// <summary>The offset of the end glyph in a range record.</summary>
    private const int RangeEnd = 2;

    /// <summary>The offset of the class in a range record.</summary>
    private const int RangeClass = 4;

    /// <summary>The format that gives a class to each glyph of a run.</summary>
    private const int ArrayFormat = 1;

    /// <summary>The format that gives a class to ranges of glyphs.</summary>
    private const int RangeFormat = 2;

    /// <summary>Gets a glyph's class.</summary>
    /// <param name="table">The layout table.</param>
    /// <param name="offset">The offset of the ClassDef table, or zero for none.</param>
    /// <param name="glyph">The glyph.</param>
    /// <returns>The class; zero when the table is missing or does not list the glyph.</returns>
    internal static int ClassOf(ReadOnlySpan<byte> table, int offset, int glyph)
    {
        if (offset <= 0 || offset >= table.Length)
        {
            return 0;
        }

        var classes = table[offset..];
        return FontBytes.U16(classes, 0) switch
        {
            ArrayFormat => FromArray(classes, glyph),
            RangeFormat => FromRanges(classes, glyph),
            _ => 0,
        };
    }

    /// <summary>Reads a class from a format 1 table.</summary>
    /// <param name="classes">The ClassDef table.</param>
    /// <param name="glyph">The glyph.</param>
    /// <returns>The class.</returns>
    private static int FromArray(ReadOnlySpan<byte> classes, int glyph)
    {
        var index = glyph - FontBytes.U16(classes, FirstOffset);
        return (uint)index < (uint)FontBytes.U16(classes, GlyphCountOffset)
            ? FontBytes.U16(classes, ClassArrayOffset + (index * FontBytes.U16Size))
            : 0;
    }

    /// <summary>Reads a class from a format 2 table.</summary>
    /// <param name="classes">The ClassDef table.</param>
    /// <param name="glyph">The glyph.</param>
    /// <returns>The class.</returns>
    private static int FromRanges(ReadOnlySpan<byte> classes, int glyph)
    {
        var low = 0;
        var high = FontBytes.U16(classes, FirstOffset) - 1;
        while (low <= high)
        {
            var middle = (low + high) >>> 1;
            var record = RangesOffset + (middle * RangeBytes);
            if (glyph < FontBytes.U16(classes, record))
            {
                high = middle - 1;
            }
            else if (glyph > FontBytes.U16(classes, record + RangeEnd))
            {
                low = middle + 1;
            }
            else
            {
                return FontBytes.U16(classes, record + RangeClass);
            }
        }

        return 0;
    }
}
