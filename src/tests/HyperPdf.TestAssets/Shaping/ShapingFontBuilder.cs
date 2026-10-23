// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;

namespace HyperPdf.TestAssets.Shaping;

/// <summary>
/// Builds a small TrueType font for testing text shaping: every glyph is a box, and the font can carry hand-made GSUB, GPOS and
/// GDEF tables and a legacy kern table. Glyph 0 is the missing glyph; the glyphs added get the numbers 1, 2 and so on.
/// </summary>
public sealed class ShapingFontBuilder
{
    /// <summary>The advance of the missing glyph.</summary>
    private const int NotdefAdvance = 500;

    /// <summary>The glyphs added, as a character (or zero) and an advance.</summary>
    private readonly List<GlyphSpec> _glyphs = [];

    /// <summary>The extra tables by tag.</summary>
    private readonly SortedDictionary<string, byte[]> _tables = new(StringComparer.Ordinal);

    /// <summary>Writes a legacy 'kern' table with one pair.</summary>
    /// <param name="left">The left glyph.</param>
    /// <param name="right">The right glyph.</param>
    /// <param name="value">The kerning value in font units.</param>
    /// <returns>The table.</returns>
    public static byte[] KernTable(int left, int right, int value) =>
        ByteBuffer.Words(0, 1, 0, 0x14, 1, 1, 0, 0, 0, left, right, value & 0xFFFF);

    /// <summary>Adds a glyph.</summary>
    /// <param name="codePoint">The character that maps to the glyph, or zero when no character does.</param>
    /// <param name="advance">The glyph's advance in font units.</param>
    /// <returns>The glyph's number.</returns>
    public int AddGlyph(int codePoint, int advance)
    {
        _glyphs.Add(new(codePoint, advance));
        return _glyphs.Count;
    }

    /// <summary>Adds a table, or replaces one.</summary>
    /// <param name="tag">The four-letter table tag.</param>
    /// <param name="table">The table.</param>
    public void WithTable(string tag, byte[] table) => _tables[tag] = table;

    /// <summary>Adds vertical metrics, giving every glyph the same vertical advance.</summary>
    /// <param name="advance">The vertical advance in font units.</param>
    public void WithVerticalMetrics(int advance)
    {
        var buffer = new ByteBuffer();
        for (var i = 0; i <= _glyphs.Count; i++)
        {
            buffer.Write(advance, 0x50);
        }

        _tables["vhea"] = SfntTables.Vhea(_glyphs.Count + 1);
        _tables["vmtx"] = buffer.ToArray();
    }

    /// <summary>Writes the font file.</summary>
    /// <returns>The font.</returns>
    public byte[] Build()
    {
        var count = _glyphs.Count + 1;
        var tables = new SortedDictionary<uint, byte[]>
        {
            [SfntTables.Tag("cmap")] = Cmap(),
            [SfntTables.Tag("head")] = SfntTables.Head(),
            [SfntTables.Tag("hhea")] = SfntTables.Hhea(count),
            [SfntTables.Tag("hmtx")] = Hmtx(),
            [SfntTables.Tag("maxp")] = SfntTables.Maxp(count),
            [SfntTables.Tag("name")] = SfntTables.Name("PVL Shaping Test"),
            [SfntTables.Tag("OS/2")] = SfntTables.Os2(),
            [SfntTables.Tag("post")] = SfntTables.Post(),
        };
        tables[SfntTables.Tag("glyf")] = Outlines(out var loca);
        tables[SfntTables.Tag("loca")] = loca;
        foreach (var (tag, table) in _tables)
        {
            tables[SfntTables.Tag(tag)] = table;
        }

        return SfntTables.Assemble(tables);
    }

    /// <summary>Writes the advance and left side bearing of each glyph.</summary>
    /// <returns>The table.</returns>
    private byte[] Hmtx()
    {
        var buffer = new ByteBuffer();
        buffer.Write(NotdefAdvance, 0x28);
        foreach (var glyph in _glyphs)
        {
            buffer.Write(glyph.Advance, 0x28);
        }

        return buffer.ToArray();
    }

    /// <summary>Writes the glyph outlines and their locations: a box for each glyph.</summary>
    /// <param name="loca">Receives the location table.</param>
    /// <returns>The glyph table.</returns>
    private byte[] Outlines(out byte[] loca)
    {
        using var glyf = new MemoryStream();
        loca = new byte[(_glyphs.Count + 0x2) * 0x4];
        glyf.Write(SfntTables.Box(NotdefAdvance - 0x32, 0x2BC));
        for (var i = 0; i < _glyphs.Count; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(loca.AsSpan((i + 1) * 0x4), (uint)glyf.Length);
            glyf.Write(SfntTables.Box(Math.Max(_glyphs[i].Advance - 0x28, 0x50), 0x1F4));
        }

        BinaryPrimitives.WriteUInt32BigEndian(loca.AsSpan((_glyphs.Count + 1) * 0x4), (uint)glyf.Length);
        return glyf.ToArray();
    }

    /// <summary>Writes a format 4 character map of the characters of the glyphs.</summary>
    /// <returns>The table.</returns>
    private byte[] Cmap()
    {
        var mapped = _glyphs.Select(static (g, i) => new CmapEntry(g.CodePoint, i + 1)).Where(static e => e.Code is > 0 and < 0xFFFF).OrderBy(static e => e.Code).ToList();
        mapped.Add(new(0xFFFF, 0));
        var count = mapped.Count;
        var t = new byte[0xC + 0x10 + (count * 0x8)];
        var s = t.AsSpan();
        BinaryPrimitives.WriteUInt16BigEndian(s[0x2..], 1);
        BinaryPrimitives.WriteUInt16BigEndian(s[0x4..], 0x3);
        BinaryPrimitives.WriteUInt16BigEndian(s[0x6..], 1);
        BinaryPrimitives.WriteUInt32BigEndian(s[0x8..], 0xC);
        var sub = s[0xC..];
        BinaryPrimitives.WriteUInt16BigEndian(sub, 0x4);
        BinaryPrimitives.WriteUInt16BigEndian(sub[0x2..], (ushort)(0x10 + (count * 0x8)));
        BinaryPrimitives.WriteUInt16BigEndian(sub[0x6..], (ushort)(count * 0x2));
        var log = (int)Math.Floor(Math.Log2(count));
        BinaryPrimitives.WriteUInt16BigEndian(sub[0x8..], (ushort)((1 << log) * 0x2));
        BinaryPrimitives.WriteUInt16BigEndian(sub[0xA..], (ushort)log);
        BinaryPrimitives.WriteUInt16BigEndian(sub[0xC..], (ushort)((count * 0x2) - ((1 << log) * 0x2)));
        for (var i = 0; i < count; i++)
        {
            var entry = mapped[i];
            BinaryPrimitives.WriteUInt16BigEndian(sub[(0xE + (i * 0x2))..], (ushort)entry.Code);
            BinaryPrimitives.WriteUInt16BigEndian(sub[(0x10 + (count * 0x2) + (i * 0x2))..], (ushort)entry.Code);
            BinaryPrimitives.WriteInt16BigEndian(sub[(0x10 + (count * 0x4) + (i * 0x2))..], entry.Code == 0xFFFF ? (short)1 : (short)(entry.Glyph - entry.Code));
        }

        return t;
    }

    /// <summary>A glyph's character and advance.</summary>
    /// <param name="CodePoint">The character, or zero.</param>
    /// <param name="Advance">The advance.</param>
    private readonly record struct GlyphSpec(int CodePoint, int Advance);

    /// <summary>One character map entry.</summary>
    /// <param name="Code">The character.</param>
    /// <param name="Glyph">The glyph.</param>
    private readonly record struct CmapEntry(int Code, int Glyph);
}
