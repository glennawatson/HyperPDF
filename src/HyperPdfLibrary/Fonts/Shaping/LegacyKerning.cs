// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>Kerning from a font's legacy 'kern' table (OpenType, "kern": format 0 subtables of horizontal kerning pairs), used when the font has no GPOS kerning.</summary>
internal static class LegacyKerning
{
    /// <summary>The offset of the subtable count in the table header.</summary>
    private const int TableCountOffset = 2;

    /// <summary>The bytes of the table header.</summary>
    private const int HeaderBytes = 4;

    /// <summary>The offset of the subtable length in a subtable header.</summary>
    private const int SubtableLengthOffset = 2;

    /// <summary>The offset of the coverage flags in a subtable header.</summary>
    private const int CoverageOffset = 4;

    /// <summary>The offset of the pair count in a format 0 subtable.</summary>
    private const int PairCountOffset = 6;

    /// <summary>The offset of the first pair in a format 0 subtable.</summary>
    private const int PairsOffset = 14;

    /// <summary>The bytes of a kerning pair: left glyph, right glyph and value.</summary>
    private const int PairBytes = 6;

    /// <summary>The coverage bit that says the subtable holds horizontal kerning.</summary>
    private const int Horizontal = 0x0001;

    /// <summary>The coverage bit that says the values are minimums, not kerning.</summary>
    private const int Minimum = 0x0002;

    /// <summary>The coverage bit that says the kerning is across the line, not along it.</summary>
    private const int CrossStream = 0x0004;

    /// <summary>The shift of the format number in the coverage word.</summary>
    private const int FormatShift = 8;

    /// <summary>Adds the kerning of each pair of neighbouring glyphs to the first glyph's advance.</summary>
    /// <param name="kern">The 'kern' table.</param>
    /// <param name="buffer">The glyphs, with positions.</param>
    internal static void Apply(ReadOnlySpan<byte> kern, GlyphBuffer buffer)
    {
        var items = buffer.Items;
        var positions = buffer.Positions;
        var offset = HeaderBytes;
        var tables = FontBytes.U16(kern, TableCountOffset);
        for (var t = 0; t < tables && offset < kern.Length; t++)
        {
            var coverage = FontBytes.U16(kern, offset + CoverageOffset);
            if ((coverage & Horizontal) != 0 && (coverage & (Minimum | CrossStream)) == 0 && coverage >> FormatShift == 0)
            {
                for (var i = 0; i + 1 < items.Length; i++)
                {
                    positions[i].XAdvance += PairValue(kern, offset, items[i].Glyph, items[i + 1].Glyph);
                }
            }

            offset += Math.Max(FontBytes.U16(kern, offset + SubtableLengthOffset), HeaderBytes);
        }
    }

    /// <summary>Looks up a pair in a format 0 subtable.</summary>
    /// <param name="kern">The 'kern' table.</param>
    /// <param name="subtable">The offset of the subtable.</param>
    /// <param name="left">The left glyph.</param>
    /// <param name="right">The right glyph.</param>
    /// <returns>The kerning value; zero when the pair is not listed.</returns>
    private static int PairValue(ReadOnlySpan<byte> kern, int subtable, int left, int right)
    {
        var key = ((uint)left << 16) | (uint)right;
        var low = 0;
        var high = FontBytes.U16(kern, subtable + PairCountOffset) - 1;
        while (low <= high)
        {
            var middle = (low + high) >>> 1;
            var pair = subtable + PairsOffset + (middle * PairBytes);
            var candidate = FontBytes.U32(kern, pair);
            if (candidate == key)
            {
                return FontBytes.S16(kern, pair + FontBytes.U32Size);
            }

            if (candidate < key)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return 0;
    }
}
