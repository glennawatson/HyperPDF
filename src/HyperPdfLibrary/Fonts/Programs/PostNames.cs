// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Fonts.Data;

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>
/// The glyph names of a TrueType 'post' table. Format 1 uses the Macintosh standard order; format 2 maps each glyph to
/// a standard name or a Pascal string in the table; format 3 has no names.
/// </summary>
[DebuggerDisplay("PostNames: format {Format}")]
internal sealed class PostNames
{
    /// <summary>The format 1 version.</summary>
    private const uint Format1 = 0x00010000;

    /// <summary>The format 2 version.</summary>
    private const uint Format2 = 0x00020000;

    /// <summary>The offset of the glyph count in format 2.</summary>
    private const int GlyphCountOffset = 32;

    /// <summary>The offset of the name indices in format 2.</summary>
    private const int IndicesOffset = 34;

    /// <summary>The names of a table with none.</summary>
    private static readonly PostNames Empty = new(0, 0, 0, default, 0);

    /// <summary>The absolute offset of the name indices.</summary>
    private readonly int _indices;

    /// <summary>The number of name indices.</summary>
    private readonly int _count;

    /// <summary>The table whose names are indexed on demand.</summary>
    private readonly TableRange _table;

    /// <summary>The first Pascal string within the table.</summary>
    private readonly int _stringsStart;

    /// <summary>The absolute offset of each Pascal string's length byte.</summary>
    private int[]? _strings;

    /// <summary>Initializes a new instance of the <see cref="PostNames"/> class.</summary>
    /// <param name="format">The format version.</param>
    /// <param name="indices">The absolute offset of the name indices.</param>
    /// <param name="count">The number of name indices.</param>
    /// <param name="table">The table containing Pascal strings.</param>
    /// <param name="stringsStart">The first Pascal string within the table.</param>
    private PostNames(uint format, int indices, int count, TableRange table, int stringsStart)
    {
        Format = format;
        _indices = indices;
        _count = count;
        _table = table;
        _stringsStart = stringsStart;
    }

    /// <summary>Gets the format version.</summary>
    internal uint Format { get; }

    /// <summary>Reads a 'post' table.</summary>
    /// <param name="data">The font data.</param>
    /// <param name="post">The table.</param>
    /// <returns>The names.</returns>
    internal static PostNames Read(ReadOnlySpan<byte> data, TableRange post)
    {
        var table = post.Of(data);
        var format = FontBytes.U32(table, 0);
        if (format == Format1)
        {
            return new(format, 0, CffStandardData.MacGlyphCount, default, 0);
        }

        if (format != Format2 || table.Length < IndicesOffset)
        {
            return Empty;
        }

        var count = Math.Min(FontBytes.U16(table, GlyphCountOffset), (table.Length - IndicesOffset) / FontBytes.U16Size);
        var stringsStart = IndicesOffset + (count * FontBytes.U16Size);
        return new(format, post.Offset + IndicesOffset, count, post, stringsStart);
    }

    /// <summary>Gets a glyph's name.</summary>
    /// <param name="data">The font data.</param>
    /// <param name="glyph">The glyph id.</param>
    /// <returns>The name's bytes, or empty.</returns>
    internal ReadOnlySpan<byte> GetName(ReadOnlySpan<byte> data, int glyph)
    {
        if ((uint)glyph >= (uint)_count)
        {
            return [];
        }

        if (Format == Format1)
        {
            return CffStandardData.GetMacGlyphName(glyph);
        }

        var index = FontBytes.U16(data, _indices + (glyph * FontBytes.U16Size));
        if (index < CffStandardData.MacGlyphCount)
        {
            return CffStandardData.GetMacGlyphName(index);
        }

        index -= CffStandardData.MacGlyphCount;
        var strings = Volatile.Read(ref _strings) ?? CreateStrings(data);
        if (index >= strings.Length)
        {
            return [];
        }

        var offset = strings[index];
        return FontBytes.Slice(data, offset + 1, FontBytes.U8(data, offset));
    }

    /// <summary>Finds where each Pascal string starts.</summary>
    /// <param name="table">The 'post' table.</param>
    /// <param name="start">The offset of the first string in the table.</param>
    /// <param name="tableOffset">The table's offset in the font data.</param>
    /// <returns>The absolute offsets.</returns>
    private static int[] IndexStrings(ReadOnlySpan<byte> table, int start, int tableOffset)
    {
        var count = 0;
        for (var position = start; position < table.Length; position += table[position] + 1)
        {
            count++;
        }

        var strings = new int[count];
        var position2 = start;
        for (var i = 0; i < count; i++)
        {
            strings[i] = tableOffset + position2;
            position2 += table[position2] + 1;
        }

        return strings;
    }

    /// <summary>Indexes custom names only when a glyph requests one.</summary>
    /// <param name="data">The font bytes.</param>
    /// <returns>The published string index.</returns>
    private int[] CreateStrings(ReadOnlySpan<byte> data)
    {
        var strings = IndexStrings(_table.Of(data), _stringsStart, _table.Offset);
        return Interlocked.CompareExchange(ref _strings, strings, null) ?? strings;
    }
}
