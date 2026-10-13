// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.IO;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Reads the TrueType tables the PDF/A font rules need as veraPDF does: 'hhea' and 'hmtx' are required, 'head', 'cmap',
/// 'maxp' and 'post' are read when present, and reading past the end of the data fails the program.
/// </summary>
internal static class PdfATrueTypeReader
{
    /// <summary>The 'cmap' tag.</summary>
    private const long CmapTag = 0x636D6170;

    /// <summary>The 'hhea' tag.</summary>
    private const long HheaTag = 0x68686561;

    /// <summary>The 'hmtx' tag.</summary>
    private const long HmtxTag = 0x686D7478;

    /// <summary>The 'head' tag.</summary>
    private const long HeadTag = 0x68656164;

    /// <summary>The 'post' tag.</summary>
    private const long PostTag = 0x706F7374;

    /// <summary>The 'maxp' tag.</summary>
    private const long MaxpTag = 0x6D617870;

    /// <summary>The bytes before the table count in the offset table.</summary>
    private const int VersionBytes = 4;

    /// <summary>The bytes after the table count in the offset table.</summary>
    private const int SearchBytes = 6;

    /// <summary>The bytes before unitsPerEm in 'head'.</summary>
    private const int HeadPrefix = 18;

    /// <summary>The bytes between descender and numberOfHMetrics in 'hhea'.</summary>
    private const int HheaGap = 26;

    /// <summary>The left side bearing after each long metric.</summary>
    private const int BearingBytes = 2;

    /// <summary>The bytes after the format of 'post' before format 2 data.</summary>
    private const int PostHeaderRest = 28;

    /// <summary>The 'post' version 2.0 in 16.16 fixed point.</summary>
    private const int PostFormat2 = 0x00020000;

    /// <summary>The 'post' version 2.5 in 16.16 fixed point.</summary>
    private const int PostFormat25 = 0x00025000;

    /// <summary>The bytes of a 16.16 fixed number.</summary>
    private const int FixedBytes = 4;

    /// <summary>Reads a TrueType program.</summary>
    /// <param name="data">The program bytes.</param>
    /// <param name="tables">The tables read, or null when the program does not parse.</param>
    /// <returns><see langword="true"/> when it parses.</returns>
    internal static bool TryRead(byte[] data, out PdfATrueTypeTables? tables)
    {
        try
        {
            tables = Read(new(data));
            return true;
        }
        catch (InvalidDataException)
        {
            tables = null;
            return false;
        }
    }

    /// <summary>Reads the tables.</summary>
    /// <param name="cursor">The data.</param>
    /// <returns>The tables.</returns>
    /// <exception cref="InvalidDataException">The program is truncated or lacks 'hhea' or 'hmtx'.</exception>
    private static PdfATrueTypeTables Read(PdfAFontCursor cursor)
    {
        var directory = ReadDirectory(cursor);
        var unitsPerEm = PdfATrueTypeTables.DefaultUnitsPerEm;
        if (directory.TryGetValue(HeadTag, out var head))
        {
            cursor.Seek(head.Offset);
            cursor.Skip(HeadPrefix);
            unitsPerEm = cursor.ReadUInt16();
        }

        var advances = ReadAdvances(cursor, directory, ReadMetricCount(cursor, directory));
        var cmaps = directory.TryGetValue(CmapTag, out var cmap) ? PdfATrueTypeCmapReader.Read(cursor, cmap.Offset) : null;
        var glyphCount = advances.Length;
        if (directory.TryGetValue(MaxpTag, out var maxp))
        {
            cursor.Seek(maxp.Offset);
            cursor.Skip(VersionBytes);
            glyphCount = cursor.ReadUInt16();
        }

        Dictionary<string, int> names = [with(StringComparer.Ordinal)];
        if (directory.TryGetValue(PostTag, out var post))
        {
            ReadPost(cursor, post, glyphCount, names);
        }

        return new(unitsPerEm, advances, glyphCount, cmaps, names);
    }

    /// <summary>Reads the offset table and table directory.</summary>
    /// <param name="cursor">The data.</param>
    /// <returns>The tables by tag; a later entry replaces an earlier one with the same tag.</returns>
    private static Dictionary<long, PdfATrueTypeTableRecord> ReadDirectory(PdfAFontCursor cursor)
    {
        cursor.Skip(VersionBytes);
        var count = cursor.ReadUInt16();
        cursor.Skip(SearchBytes);
        Dictionary<long, PdfATrueTypeTableRecord> directory = [];
        for (var i = 0; i < count; i++)
        {
            var tag = cursor.ReadUInt32();
            _ = cursor.ReadUInt32();
            var offset = cursor.ReadUInt32();
            var length = cursor.ReadUInt32();
            directory[tag] = new(offset, length);
        }

        return directory;
    }

    /// <summary>Reads numberOfHMetrics from 'hhea'.</summary>
    /// <param name="cursor">The data.</param>
    /// <param name="directory">The tables.</param>
    /// <returns>The count.</returns>
    /// <exception cref="InvalidDataException">There is no 'hhea' table.</exception>
    private static int ReadMetricCount(PdfAFontCursor cursor, Dictionary<long, PdfATrueTypeTableRecord> directory)
    {
        if (!directory.TryGetValue(HheaTag, out var hhea))
        {
            throw new InvalidDataException("The TrueType program has no 'hhea' table.");
        }

        cursor.Seek(hhea.Offset);
        cursor.Skip(VersionBytes);
        _ = cursor.ReadInt16();
        _ = cursor.ReadInt16();
        cursor.Skip(HheaGap);
        return cursor.ReadUInt16();
    }

    /// <summary>Reads the advance widths of 'hmtx'.</summary>
    /// <param name="cursor">The data.</param>
    /// <param name="directory">The tables.</param>
    /// <param name="count">The number of long metrics.</param>
    /// <returns>The advances.</returns>
    /// <exception cref="InvalidDataException">There is no 'hmtx' table, or it is truncated.</exception>
    private static int[] ReadAdvances(PdfAFontCursor cursor, Dictionary<long, PdfATrueTypeTableRecord> directory, int count)
    {
        if (!directory.TryGetValue(HmtxTag, out var hmtx))
        {
            throw new InvalidDataException("The TrueType program has no 'hmtx' table.");
        }

        cursor.Seek(hmtx.Offset);
        var advances = new int[count];
        for (var i = 0; i < count; i++)
        {
            advances[i] = cursor.ReadUInt16();
            cursor.Skip(BearingBytes);
        }

        return advances;
    }

    /// <summary>Reads the glyph names of a format 2 or 2.5 'post' table.</summary>
    /// <param name="cursor">The data.</param>
    /// <param name="post">The table.</param>
    /// <param name="glyphCount">The glyph count.</param>
    /// <param name="names">Receives the glyph of each name.</param>
    /// <exception cref="InvalidDataException">The table is truncated or names a glyph outside the Macintosh list.</exception>
    private static void ReadPost(PdfAFontCursor cursor, PdfATrueTypeTableRecord post, int glyphCount, Dictionary<string, int> names)
    {
        cursor.Seek(post.Offset);
        Span<byte> fixedBytes = stackalloc byte[FixedBytes];
        cursor.ReadAvailable(fixedBytes);
        var format = BinaryPrimitives.ReadInt32BigEndian(fixedBytes);
        cursor.Skip(PostHeaderRest);
        if (format == PostFormat2)
        {
            ReadPostNames(cursor, post, names);
            return;
        }

        for (var i = 0; format == PostFormat25 && i < glyphCount; i++)
        {
            var index = (sbyte)cursor.ReadByte() + i;
            if ((uint)index >= CffStandardData.MacGlyphCount)
            {
                throw new InvalidDataException("The 'post' table names a glyph outside the Macintosh list.");
            }

            names[Encoding.ASCII.GetString(CffStandardData.GetMacGlyphName(index))] = i;
        }
    }

    /// <summary>Reads the names of a format 2 'post' table.</summary>
    /// <param name="cursor">The data, after the header.</param>
    /// <param name="post">The table.</param>
    /// <param name="names">Receives the glyph of each name.</param>
    /// <exception cref="InvalidDataException">The table is truncated.</exception>
    private static void ReadPostNames(PdfAFontCursor cursor, PdfATrueTypeTableRecord post, Dictionary<string, int> names)
    {
        var count = cursor.ReadUInt16();
        var indexes = ArrayPool<int>.Shared.Rent(count);
        string[]? strings = null;
        var stringCount = 0;
        try
        {
            for (var i = 0; i < count; i++)
            {
                indexes[i] = cursor.ReadUInt16();
            }

            // A name is at most 255 bytes, so one stack buffer reads them all.
            Span<byte> bytes = stackalloc byte[byte.MaxValue];
            while (cursor.Position < post.Offset + post.Length)
            {
                var name = bytes[..cursor.ReadByte()];
                cursor.ReadAvailable(name);
                PooledArrays.EnsureRoom(ref strings, stringCount, clear: true)[stringCount] = Encoding.Latin1.GetString(name);
                stringCount++;
            }

            _ = names.EnsureCapacity(count);
            for (var i = 0; i < count; i++)
            {
                var index = indexes[i];
                if (index < CffStandardData.MacGlyphCount)
                {
                    names[Encoding.ASCII.GetString(CffStandardData.GetMacGlyphName(index))] = i;
                }
                else if (index - CffStandardData.MacGlyphCount < stringCount)
                {
                    names[strings![index - CffStandardData.MacGlyphCount]] = i;
                }
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(indexes);
            PooledArrays.Return(ref strings, stringCount, clear: true);
        }
    }
}
