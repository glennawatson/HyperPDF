// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Reads a TrueType 'cmap' table as veraPDF does: every encoding record counts as a subtable, formats 0, 4 and 6 are
/// mapped, other formats map nothing, and glyph ids wrap at 65536.
/// </summary>
internal static class PdfATrueTypeCmapReader
{
    /// <summary>The byte encoding format.</summary>
    private const int ByteFormat = 0;

    /// <summary>The segment mapping format.</summary>
    private const int SegmentFormat = 4;

    /// <summary>The trimmed table format.</summary>
    private const int TrimmedFormat = 6;

    /// <summary>The codes of a byte encoding table.</summary>
    private const int ByteCodes = 256;

    /// <summary>The length and language fields after the format.</summary>
    private const int LengthAndLanguage = 4;

    /// <summary>The searchRange, entrySelector and rangeShift fields.</summary>
    private const int SearchFields = 6;

    /// <summary>The reserved pad between end and start codes.</summary>
    private const int ReservedPad = 2;

    /// <summary>The version field of the table.</summary>
    private const int VersionBytes = 2;

    /// <summary>The bytes of a 16-bit glyph id.</summary>
    private const int GlyphBytes = 2;

    /// <summary>The code that ends a format 4 table.</summary>
    private const int LastCode = 0xFFFF;

    /// <summary>The number of 16-bit glyph ids.</summary>
    private const int GlyphModulus = 0x10000;

    /// <summary>Reads the table.</summary>
    /// <param name="cursor">The data.</param>
    /// <param name="offset">The table offset.</param>
    /// <returns>The subtables.</returns>
    /// <exception cref="InvalidDataException">The table is truncated.</exception>
    internal static PdfATrueTypeCmap[] Read(PdfAFontCursor cursor, long offset)
    {
        cursor.Seek(offset);
        cursor.Skip(VersionBytes);
        var count = cursor.ReadUInt16();
        var subtables = new PdfATrueTypeCmap[count];
        for (var i = 0; i < count; i++)
        {
            var platform = cursor.ReadUInt16();
            var encoding = cursor.ReadUInt16();
            subtables[i] = new(platform, encoding, cursor.ReadUInt32());
        }

        foreach (var subtable in subtables)
        {
            cursor.Seek(subtable.Offset + offset);
            switch (cursor.ReadUInt16())
            {
                case ByteFormat:
                {
                    ReadBytes(cursor, subtable);
                    break;
                }

                case SegmentFormat:
                {
                    ReadSegments(cursor, subtable);
                    break;
                }

                case TrimmedFormat:
                {
                    ReadTrimmed(cursor, subtable);
                    break;
                }

                default:
                {
                    break;
                }
            }
        }

        return subtables;
    }

    /// <summary>Reads a format 0 subtable.</summary>
    /// <param name="cursor">The data, after the format.</param>
    /// <param name="subtable">Receives the map.</param>
    private static void ReadBytes(PdfAFontCursor cursor, PdfATrueTypeCmap subtable)
    {
        cursor.Skip(LengthAndLanguage);
        for (var code = 0; code < ByteCodes; code++)
        {
            subtable.Put(code, cursor.ReadByte());
        }
    }

    /// <summary>Reads a format 6 subtable.</summary>
    /// <param name="cursor">The data, after the format.</param>
    /// <param name="subtable">Receives the map.</param>
    private static void ReadTrimmed(PdfAFontCursor cursor, PdfATrueTypeCmap subtable)
    {
        cursor.Skip(LengthAndLanguage);
        var first = cursor.ReadUInt16();
        var count = cursor.ReadUInt16();
        for (var i = 0; i < count; i++)
        {
            subtable.Put(first + i, cursor.ReadUInt16());
        }
    }

    /// <summary>Reads a format 4 subtable.</summary>
    /// <param name="cursor">The data, after the format.</param>
    /// <param name="subtable">Receives the map.</param>
    private static void ReadSegments(PdfAFontCursor cursor, PdfATrueTypeCmap subtable)
    {
        cursor.Skip(LengthAndLanguage);
        var segments = cursor.ReadUInt16() / GlyphBytes;
        cursor.Skip(SearchFields);
        var ends = ReadArray(cursor, segments);
        cursor.Skip(ReservedPad);
        var starts = ReadArray(cursor, segments);
        var deltas = ReadArray(cursor, segments);
        var rangeOffsets = ReadArray(cursor, segments);
        var glyphArray = cursor.Position;
        for (var i = 0; i < segments; i++)
        {
            if (rangeOffsets[i] == 0)
            {
                for (var code = starts[i]; code <= ends[i]; code++)
                {
                    subtable.Put(code, (deltas[i] + code) % GlyphModulus);
                }
            }
            else if (starts[i] != LastCode && ends[i] != LastCode)
            {
                ReadRangeGlyphs(cursor, subtable, new(i, segments, starts[i], ends[i], deltas[i], rangeOffsets[i]), glyphArray);
            }
        }
    }

    /// <summary>Reads the glyphs of a format 4 segment that indexes the glyph array.</summary>
    /// <param name="cursor">The data.</param>
    /// <param name="subtable">Receives the map.</param>
    /// <param name="segment">The segment.</param>
    /// <param name="glyphArray">The offset of the glyph array, just after the range offsets.</param>
    private static void ReadRangeGlyphs(PdfAFontCursor cursor, PdfATrueTypeCmap subtable, PdfATrueTypeSegment segment, long glyphArray)
    {
        for (var j = 0; j <= segment.End - segment.Start; j++)
        {
            cursor.Seek(glyphArray + (((segment.RangeOffset / GlyphBytes) + j + (segment.Index - segment.Count)) * (long)GlyphBytes));
            var glyph = cursor.ReadUInt16();
            subtable.Put(j + segment.Start, glyph == 0 ? 0 : (glyph + segment.Delta) % GlyphModulus);
        }
    }

    /// <summary>Reads an array of 16-bit numbers.</summary>
    /// <param name="cursor">The data.</param>
    /// <param name="count">The count.</param>
    /// <returns>The numbers.</returns>
    private static int[] ReadArray(PdfAFontCursor cursor, int count)
    {
        var values = new int[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = cursor.ReadUInt16();
        }

        return values;
    }
}
