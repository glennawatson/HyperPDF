// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;
using HyperPdf.Core.Text.Fonts;

namespace HyperPdf.TestAssets;

/// <summary>
/// Writes a small, valid TrueType font so font tests give the same answers on every computer. Each glyph is a
/// rectangle whose width depends on the character; 'é' is a composite of 'e' and an accent; "AV" has a kerning pair.
/// It covers ASCII, some Latin-1, Greek, Cyrillic, Hebrew and one CJK character.
/// </summary>
public static class TestFont
{
    /// <summary>The family name.</summary>
    public static readonly string Family = "PVL Test Sans";

    /// <summary>The font units per em.</summary>
    public static readonly int UnitsPerEm = 0x3E8;

    /// <summary>The kerning of "AV", in font units.</summary>
    public static readonly int AvKerning = -0x50;

    /// <summary>The ascender, in font units.</summary>
    public static readonly int Ascender = 0x320;

    /// <summary>The descender, in font units.</summary>
    public static readonly int Descender = -0xC8;

    /// <summary>The characters given glyphs after .notdef, in glyph order. 'é' (the last Latin character) is composite.</summary>
    public static readonly string Characters =
        " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" + "´" + "αβЖאב中" + "é";

    /// <summary>The family of a face whose licence forbids embedding.</summary>
    public static readonly string LockedFamily = "PVL Locked Sans";

    /// <summary>The shared catalog, written beside the tests on first use.</summary>
    private static readonly Lazy<FontCatalog> SharedCatalog = new(CreateCatalog, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Gets a catalog of the test font's regular and bold faces and a face that may not be embedded.</summary>
    public static FontCatalog Catalog => SharedCatalog.Value;

    /// <summary>Gets the glyph index of the accent used by 'é'.</summary>
    public static int AccentGlyph => Characters.IndexOf('´', StringComparison.Ordinal) + 1;

    /// <summary>Gets the glyph index of 'e'.</summary>
    public static int EGlyph => Characters.IndexOf('e', StringComparison.Ordinal) + 1;

    /// <summary>Gets the glyph index of 'é'.</summary>
    public static int EAcuteGlyph => Characters.Length;

    /// <summary>Gets a glyph's advance width, in font units.</summary>
    /// <param name="c">The character.</param>
    /// <returns>The advance.</returns>
    public static int AdvanceOf(char c) => c switch
    {
        ' ' => 0xFA,
        'i' or 'l' or '.' or ',' or '!' or '\'' => 0xC8,
        'm' or 'w' or 'M' or 'W' => 0x320,
        '中' => 0x3E8,
        _ => 0x1F4,
    };

    /// <summary>Writes the regular face, which may be embedded and subset.</summary>
    /// <returns>The font file.</returns>
    public static byte[] Create() => Create(0, null, false);

    /// <summary>Writes the font.</summary>
    /// <param name="embeddingFlags">The licence flags (OS/0x2 <c>fsType</c>); 0 allows embedding and subsetting.</param>
    /// <param name="family">The family name, or <see langword="null"/> for <see cref="Family"/>.</param>
    /// <param name="bold">Whether the face says it is bold.</param>
    /// <returns>The font file.</returns>
    public static byte[] Create(int embeddingFlags, string? family, bool bold)
    {
        var glyphCount = Characters.Length + 1;
        var (glyf, loca) = Glyphs();
        var tables = new SortedDictionary<uint, byte[]>
        {
            [Tag("cmap")] = Cmap(),
            [Tag("glyf")] = glyf,
            [Tag("head")] = Head(bold),
            [Tag("hhea")] = Hhea(glyphCount),
            [Tag("hmtx")] = Hmtx(),
            [Tag("kern")] = Kern(),
            [Tag("loca")] = loca,
            [Tag("maxp")] = Maxp(glyphCount),
            [Tag("name")] = Name(family ?? Family, bold ? "Bold" : "Regular"),
            [Tag("OS/2")] = Os2(embeddingFlags, bold),
            [Tag("post")] = Post(),
        };
        return Assemble(tables);
    }

    /// <summary>Gets the glyph index of a character.</summary>
    /// <param name="c">The character.</param>
    /// <returns>The glyph, or 0.</returns>
    public static ushort GlyphOf(char c) => (ushort)(Characters.IndexOf(c, StringComparison.Ordinal) + 1);

    /// <summary>Writes the catalog's fonts to a folder beside the tests, one per process, and scans it.</summary>
    /// <returns>The catalog.</returns>
    private static FontCatalog CreateCatalog()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "test-fonts", $"{Environment.ProcessId}");
        _ = Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "regular.ttf"), Create());
        File.WriteAllBytes(Path.Combine(folder, "bold.ttf"), Create(0, null, true));
        File.WriteAllBytes(Path.Combine(folder, "locked.ttf"), Create(0x2, LockedFamily, false));
        return FontCatalog.Scan([folder]);
    }

    /// <summary>Reads a four letter table tag as a number.</summary>
    /// <param name="tag">The tag.</param>
    /// <returns>The number.</returns>
    private static uint Tag(string tag) => BinaryPrimitives.ReadUInt32BigEndian(Encoding.ASCII.GetBytes(tag));

    /// <summary>Writes the glyph outlines and their locations.</summary>
    /// <returns>The glyph and location tables.</returns>
    private static (byte[] Glyf, byte[] Loca) Glyphs()
    {
        using var glyf = new MemoryStream();
        var loca = new byte[(Characters.Length + 0x2) * 0x4];
        WriteRectangle(glyf, 0x32, 0, 0x1C2, 0x2BC);
        for (var i = 0; i < Characters.Length; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(loca.AsSpan((i + 1) * 0x4), (uint)glyf.Length);
            var c = Characters[i];
            if (c == ' ')
            {
                continue;
            }

            if (c == 'é')
            {
                WriteComposite(glyf);
                continue;
            }

            var advance = AdvanceOf(c);
            WriteRectangle(glyf, 0x28, c == '´' ? 0x258 : 0, advance - 0x28, c == '´' ? 0x2F8 : 0x1F4 + (c % 0xC8));
        }

        BinaryPrimitives.WriteUInt32BigEndian(loca.AsSpan((Characters.Length + 1) * 0x4), (uint)glyf.Length);
        return (glyf.ToArray(), loca);
    }

    /// <summary>Writes a glyph that is one rectangle.</summary>
    /// <param name="glyf">The glyph table.</param>
    /// <param name="x0">The left edge.</param>
    /// <param name="y0">The bottom edge.</param>
    /// <param name="x1">The right edge.</param>
    /// <param name="y1">The top edge.</param>
    private static void WriteRectangle(MemoryStream glyf, int x0, int y0, int x1, int y1)
    {
        var bytes = new byte[0xA + 0x2 + 0x2 + 0x4 + 0x8 + 0x8];
        var s = bytes.AsSpan();
        BinaryPrimitives.WriteInt16BigEndian(s, 1);
        BinaryPrimitives.WriteInt16BigEndian(s[0x2..], (short)x0);
        BinaryPrimitives.WriteInt16BigEndian(s[0x4..], (short)y0);
        BinaryPrimitives.WriteInt16BigEndian(s[0x6..], (short)x1);
        BinaryPrimitives.WriteInt16BigEndian(s[0x8..], (short)y1);
        BinaryPrimitives.WriteUInt16BigEndian(s[0xA..], 0x3);
        BinaryPrimitives.WriteUInt16BigEndian(s[0xC..], 0);
        s.Slice(0xE, 0x4).Fill(1);
        short[] xs = [(short)x0, 0, (short)(x1 - x0), 0];
        short[] ys = [(short)y0, (short)(y1 - y0), 0, (short)(y0 - y1)];
        for (var i = 0; i < 0x4; i++)
        {
            BinaryPrimitives.WriteInt16BigEndian(s[(0x12 + (i * 0x2))..], xs[i]);
            BinaryPrimitives.WriteInt16BigEndian(s[(0x1A + (i * 0x2))..], ys[i]);
        }

        glyf.Write(bytes);
        Pad(glyf);
    }

    /// <summary>Writes the composite glyph: 'e' with the accent above it.</summary>
    /// <param name="glyf">The glyph table.</param>
    private static void WriteComposite(MemoryStream glyf)
    {
        var bytes = new byte[0xA + 0x8 + 0x8];
        var s = bytes.AsSpan();
        BinaryPrimitives.WriteInt16BigEndian(s, -1);
        BinaryPrimitives.WriteInt16BigEndian(s[0x2..], 0x28);
        BinaryPrimitives.WriteInt16BigEndian(s[0x4..], 0);
        BinaryPrimitives.WriteInt16BigEndian(s[0x6..], 0x1CC);
        BinaryPrimitives.WriteInt16BigEndian(s[0x8..], 0x2F8);
        BinaryPrimitives.WriteUInt16BigEndian(s[0xA..], 0x0001 | 0x0002 | 0x0020);
        BinaryPrimitives.WriteUInt16BigEndian(s[0xC..], (ushort)EGlyph);
        BinaryPrimitives.WriteUInt16BigEndian(s[0x12..], 0x0001 | 0x0002);
        BinaryPrimitives.WriteUInt16BigEndian(s[0x14..], (ushort)AccentGlyph);
        glyf.Write(bytes);
        Pad(glyf);
    }

    /// <summary>Pads a table to four bytes.</summary>
    /// <param name="stream">The table.</param>
    private static void Pad(MemoryStream stream)
    {
        while (stream.Length % 0x4 != 0)
        {
            stream.WriteByte(0);
        }
    }

    /// <summary>Writes the font header.</summary>
    /// <param name="bold">Whether the face is bold.</param>
    /// <returns>The table.</returns>
    private static byte[] Head(bool bold)
    {
        var t = new byte[0x36];
        BinaryPrimitives.WriteUInt32BigEndian(t, 0x00010000);
        BinaryPrimitives.WriteUInt32BigEndian(t.AsSpan(0x4), 0x00010000);
        BinaryPrimitives.WriteUInt32BigEndian(t.AsSpan(0xC), 0x5F0F3CF5);
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0x10), 0x000B);
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0x12), (ushort)UnitsPerEm);
        BinaryPrimitives.WriteInt16BigEndian(t.AsSpan(0x24), 0);
        BinaryPrimitives.WriteInt16BigEndian(t.AsSpan(0x26), (short)Descender);
        BinaryPrimitives.WriteInt16BigEndian(t.AsSpan(0x28), 0x3E8);
        BinaryPrimitives.WriteInt16BigEndian(t.AsSpan(0x2A), (short)Ascender);
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0x2C), (ushort)(bold ? 1 : 0));
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0x2E), 0x8);
        BinaryPrimitives.WriteInt16BigEndian(t.AsSpan(0x30), 0x2);
        BinaryPrimitives.WriteInt16BigEndian(t.AsSpan(0x32), 1);
        return t;
    }

    /// <summary>Writes the horizontal header.</summary>
    /// <param name="glyphs">The glyph count.</param>
    /// <returns>The table.</returns>
    private static byte[] Hhea(int glyphs)
    {
        var t = new byte[0x24];
        BinaryPrimitives.WriteUInt32BigEndian(t, 0x00010000);
        BinaryPrimitives.WriteInt16BigEndian(t.AsSpan(0x4), (short)Ascender);
        BinaryPrimitives.WriteInt16BigEndian(t.AsSpan(0x6), (short)Descender);
        BinaryPrimitives.WriteInt16BigEndian(t.AsSpan(0x8), 0);
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0xA), 0x3E8);
        BinaryPrimitives.WriteInt16BigEndian(t.AsSpan(0x12), 1);
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0x22), (ushort)glyphs);
        return t;
    }

    /// <summary>Writes the advance and side bearing of every glyph.</summary>
    /// <returns>The table.</returns>
    private static byte[] Hmtx()
    {
        var t = new byte[(Characters.Length + 1) * 0x4];
        BinaryPrimitives.WriteUInt16BigEndian(t, 0x1F4);
        BinaryPrimitives.WriteInt16BigEndian(t.AsSpan(0x2), 0x32);
        for (var i = 0; i < Characters.Length; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan((i + 1) * 0x4), (ushort)AdvanceOf(Characters[i]));
            BinaryPrimitives.WriteInt16BigEndian(t.AsSpan(((i + 1) * 0x4) + 0x2), 0x28);
        }

        return t;
    }

    /// <summary>Writes the maximum profile.</summary>
    /// <param name="glyphs">The glyph count.</param>
    /// <returns>The table.</returns>
    private static byte[] Maxp(int glyphs)
    {
        var t = new byte[0x20];
        BinaryPrimitives.WriteUInt32BigEndian(t, 0x00010000);
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0x4), (ushort)glyphs);
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0x6), 0x4);
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0x8), 1);
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0xA), 0x8);
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0xC), 0x2);
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0xE), 0x2);
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0x1C), 0x2);
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0x1E), 1);
        return t;
    }

    /// <summary>Writes the OS/2 table with the licence flags and weight.</summary>
    /// <param name="embeddingFlags">The licence flags.</param>
    /// <param name="bold">Whether the face is bold.</param>
    /// <returns>The table.</returns>
    private static byte[] Os2(int embeddingFlags, bool bold)
    {
        var t = new byte[0x60];
        BinaryPrimitives.WriteUInt16BigEndian(t, 0x4);
        BinaryPrimitives.WriteInt16BigEndian(t.AsSpan(0x2), 0x1F4);
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0x4), (ushort)(bold ? 0x2BC : 0x190));
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0x6), 0x5);
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0x8), (ushort)embeddingFlags);
        BinaryPrimitives.WriteInt16BigEndian(t.AsSpan(0x1E), 0x8 << 0x8);
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0x3E), (ushort)(bold ? 0x20 : 0x40));
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0x40), 0x20);
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0x42), 0x4E2D);
        BinaryPrimitives.WriteInt16BigEndian(t.AsSpan(0x44), (short)Ascender);
        BinaryPrimitives.WriteInt16BigEndian(t.AsSpan(0x46), (short)Descender);
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0x4A), (ushort)Ascender);
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0x4C), (ushort)-Descender);
        return t;
    }

    /// <summary>Writes a PostScript table without glyph names.</summary>
    /// <returns>The table.</returns>
    private static byte[] Post()
    {
        var t = new byte[0x20];
        BinaryPrimitives.WriteUInt32BigEndian(t, 0x00030000);
        BinaryPrimitives.WriteInt16BigEndian(t.AsSpan(0x8), -0x64);
        BinaryPrimitives.WriteInt16BigEndian(t.AsSpan(0xA), 0x32);
        return t;
    }

    /// <summary>Writes a kerning table with the "AV" pair.</summary>
    /// <returns>The table.</returns>
    private static byte[] Kern()
    {
        var t = new byte[0x4 + 0xE + 0x6];
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0x2), 1);
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0x6), 0x14);
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0x8), 1);
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0xA), 1);
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0xC), 0x6);
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0x12), GlyphOf('A'));
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0x14), GlyphOf('V'));
        BinaryPrimitives.WriteInt16BigEndian(t.AsSpan(0x16), (short)AvKerning);
        return t;
    }

    /// <summary>Writes a segment character map with one segment per character.</summary>
    /// <returns>The table.</returns>
    private static byte[] Cmap()
    {
        var sorted = Characters.Select(static (c, i) => (Code: (int)c, Glyph: i + 1)).OrderBy(static p => p.Code).ToList();
        sorted.Add((0xFFFF, 0));
        var count = sorted.Count;
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
            var (code, glyph) = sorted[i];
            BinaryPrimitives.WriteUInt16BigEndian(sub[(0xE + (i * 0x2))..], (ushort)code);
            BinaryPrimitives.WriteUInt16BigEndian(sub[(0x10 + (count * 0x2) + (i * 0x2))..], (ushort)code);
            BinaryPrimitives.WriteInt16BigEndian(sub[(0x10 + (count * 0x4) + (i * 0x2))..], code == 0xFFFF ? (short)1 : (short)(glyph - code));
        }

        return t;
    }

    /// <summary>Writes the family and style names.</summary>
    /// <param name="family">The family.</param>
    /// <param name="style">The style.</param>
    /// <returns>The table.</returns>
    private static byte[] Name(string family, string style)
    {
        (int Id, string Text)[] names = [(1, family), (0x2, style), (0x4, $"{family} {style}"), (0x6, $"{family.Replace(" ", string.Empty, StringComparison.Ordinal)}-{style}")];
        using var storage = new MemoryStream();
        var t = new byte[0x6 + (names.Length * 0xC)];
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0x2), (ushort)names.Length);
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0x4), (ushort)t.Length);
        for (var i = 0; i < names.Length; i++)
        {
            var bytes = Encoding.BigEndianUnicode.GetBytes(names[i].Text);
            var r = t.AsSpan(0x6 + (i * 0xC));
            BinaryPrimitives.WriteUInt16BigEndian(r, 0x3);
            BinaryPrimitives.WriteUInt16BigEndian(r[0x2..], 1);
            BinaryPrimitives.WriteUInt16BigEndian(r[0x4..], 0x0409);
            BinaryPrimitives.WriteUInt16BigEndian(r[0x6..], (ushort)names[i].Id);
            BinaryPrimitives.WriteUInt16BigEndian(r[0x8..], (ushort)bytes.Length);
            BinaryPrimitives.WriteUInt16BigEndian(r[0xA..], (ushort)storage.Length);
            storage.Write(bytes);
        }

        return [.. t, .. storage.ToArray()];
    }

    /// <summary>Lays the tables out after the directory.</summary>
    /// <param name="tables">The tables by tag.</param>
    /// <returns>The font file.</returns>
    private static byte[] Assemble(SortedDictionary<uint, byte[]> tables)
    {
        var count = tables.Count;
        var offset = 0xC + (count * 0x10);
        var total = offset + tables.Values.Sum(static t => (t.Length + 0x3) / 0x4 * 0x4);
        var file = new byte[total];
        BinaryPrimitives.WriteUInt32BigEndian(file, 0x00010000);
        BinaryPrimitives.WriteUInt16BigEndian(file.AsSpan(0x4), (ushort)count);
        var log = (int)Math.Floor(Math.Log2(count));
        BinaryPrimitives.WriteUInt16BigEndian(file.AsSpan(0x6), (ushort)((1 << log) * 0x10));
        BinaryPrimitives.WriteUInt16BigEndian(file.AsSpan(0x8), (ushort)log);
        BinaryPrimitives.WriteUInt16BigEndian(file.AsSpan(0xA), (ushort)((count * 0x10) - ((1 << log) * 0x10)));
        var i = 0;
        foreach (var (tag, bytes) in tables)
        {
            var record = file.AsSpan(0xC + (i * 0x10));
            BinaryPrimitives.WriteUInt32BigEndian(record, tag);
            BinaryPrimitives.WriteUInt32BigEndian(record[0x4..], Checksum(bytes));
            BinaryPrimitives.WriteUInt32BigEndian(record[0x8..], (uint)offset);
            BinaryPrimitives.WriteUInt32BigEndian(record[0xC..], (uint)bytes.Length);
            bytes.CopyTo(file.AsSpan(offset));
            offset += (bytes.Length + 0x3) / 0x4 * 0x4;
            i++;
        }

        return file;
    }

    /// <summary>Sums a table as big-endian 32-bit words.</summary>
    /// <param name="bytes">The table.</param>
    /// <returns>The checksum.</returns>
    private static uint Checksum(byte[] bytes)
    {
        uint sum = 0;
        var padded = new byte[(bytes.Length + 0x3) / 0x4 * 0x4];
        bytes.CopyTo(padded, 0);
        for (var i = 0; i < padded.Length; i += 0x4)
        {
            sum = unchecked(sum + BinaryPrimitives.ReadUInt32BigEndian(padded.AsSpan(i)));
        }

        return sum;
    }
}
