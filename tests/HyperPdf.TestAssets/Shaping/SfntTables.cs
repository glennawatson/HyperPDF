// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;

namespace HyperPdf.TestAssets.Shaping;

/// <summary>Writes the tables every small TrueType test font needs and lays them out in a font file.</summary>
internal static class SfntTables
{
    /// <summary>The ascender, in font units.</summary>
    private const int Ascender = 0x320;

    /// <summary>The descender, in font units.</summary>
    private const int Descender = -0xC8;

    /// <summary>Reads a four letter table tag as a number.</summary>
    /// <param name="tag">The tag.</param>
    /// <returns>The number.</returns>
    internal static uint Tag(string tag) => BinaryPrimitives.ReadUInt32BigEndian(Encoding.ASCII.GetBytes(tag));

    /// <summary>Writes the font header, with long glyph locations.</summary>
    /// <returns>The table.</returns>
    internal static byte[] Head()
    {
        var t = new byte[0x36];
        BinaryPrimitives.WriteUInt32BigEndian(t, 0x00010000);
        BinaryPrimitives.WriteUInt32BigEndian(t.AsSpan(0x4), 0x00010000);
        BinaryPrimitives.WriteUInt32BigEndian(t.AsSpan(0xC), 0x5F0F3CF5);
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0x10), 0x000B);
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0x12), 0x3E8);
        BinaryPrimitives.WriteInt16BigEndian(t.AsSpan(0x26), (short)Descender);
        BinaryPrimitives.WriteInt16BigEndian(t.AsSpan(0x28), 0x3E8);
        BinaryPrimitives.WriteInt16BigEndian(t.AsSpan(0x2A), (short)Ascender);
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0x2E), 0x8);
        BinaryPrimitives.WriteInt16BigEndian(t.AsSpan(0x30), 0x2);
        BinaryPrimitives.WriteInt16BigEndian(t.AsSpan(0x32), 1);
        return t;
    }

    /// <summary>Writes the horizontal header.</summary>
    /// <param name="glyphs">The glyph count.</param>
    /// <returns>The table.</returns>
    internal static byte[] Hhea(int glyphs)
    {
        var t = new byte[0x24];
        BinaryPrimitives.WriteUInt32BigEndian(t, 0x00010000);
        BinaryPrimitives.WriteInt16BigEndian(t.AsSpan(0x4), (short)Ascender);
        BinaryPrimitives.WriteInt16BigEndian(t.AsSpan(0x6), (short)Descender);
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0xA), 0x3E8);
        BinaryPrimitives.WriteInt16BigEndian(t.AsSpan(0x12), 1);
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0x22), (ushort)glyphs);
        return t;
    }

    /// <summary>Writes the vertical header.</summary>
    /// <param name="glyphs">The glyph count.</param>
    /// <returns>The table.</returns>
    internal static byte[] Vhea(int glyphs)
    {
        var t = new byte[0x24];
        BinaryPrimitives.WriteInt32BigEndian(t, 0x00011000);
        BinaryPrimitives.WriteInt16BigEndian(t.AsSpan(0x4), 0x1F4);
        BinaryPrimitives.WriteInt16BigEndian(t.AsSpan(0x6), -0x1F4);
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0x22), (ushort)glyphs);
        return t;
    }

    /// <summary>Writes the maximum profile.</summary>
    /// <param name="glyphs">The glyph count.</param>
    /// <returns>The table.</returns>
    internal static byte[] Maxp(int glyphs)
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

    /// <summary>Writes the OS/2 table with a licence that allows embedding.</summary>
    /// <returns>The table.</returns>
    internal static byte[] Os2()
    {
        var t = new byte[0x60];
        BinaryPrimitives.WriteUInt16BigEndian(t, 0x4);
        BinaryPrimitives.WriteInt16BigEndian(t.AsSpan(0x2), 0x1F4);
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0x4), 0x190);
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0x6), 0x5);
        BinaryPrimitives.WriteInt16BigEndian(t.AsSpan(0x1E), 0x8 << 0x8);
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0x3E), 0x40);
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
    internal static byte[] Post()
    {
        var t = new byte[0x20];
        BinaryPrimitives.WriteUInt32BigEndian(t, 0x00030000);
        BinaryPrimitives.WriteInt16BigEndian(t.AsSpan(0x8), -0x64);
        BinaryPrimitives.WriteInt16BigEndian(t.AsSpan(0xA), 0x32);
        return t;
    }

    /// <summary>Writes the naming table with a family, style, full name and PostScript name.</summary>
    /// <param name="family">The family.</param>
    /// <returns>The table.</returns>
    internal static byte[] Name(string family)
    {
        string[] names = [family, "Regular", $"{family} Regular", $"{family.Replace(" ", string.Empty, StringComparison.Ordinal)}-Regular"];
        int[] ids = [1, 0x2, 0x4, 0x6];
        using var storage = new MemoryStream();
        var t = new byte[0x6 + (names.Length * 0xC)];
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0x2), (ushort)names.Length);
        BinaryPrimitives.WriteUInt16BigEndian(t.AsSpan(0x4), (ushort)t.Length);
        for (var i = 0; i < names.Length; i++)
        {
            var bytes = Encoding.BigEndianUnicode.GetBytes(names[i]);
            var r = t.AsSpan(0x6 + (i * 0xC));
            BinaryPrimitives.WriteUInt16BigEndian(r, 0x3);
            BinaryPrimitives.WriteUInt16BigEndian(r[0x2..], 1);
            BinaryPrimitives.WriteUInt16BigEndian(r[0x4..], 0x0409);
            BinaryPrimitives.WriteUInt16BigEndian(r[0x6..], (ushort)ids[i]);
            BinaryPrimitives.WriteUInt16BigEndian(r[0x8..], (ushort)bytes.Length);
            BinaryPrimitives.WriteUInt16BigEndian(r[0xA..], (ushort)storage.Length);
            storage.Write(bytes);
        }

        return [.. t, .. storage.ToArray()];
    }

    /// <summary>Writes a glyph that is one box.</summary>
    /// <param name="x1">The right edge.</param>
    /// <param name="y1">The top edge.</param>
    /// <returns>The glyph, padded to four bytes.</returns>
    internal static byte[] Box(int x1, int y1)
    {
        var bytes = new byte[0x28];
        var s = bytes.AsSpan();
        BinaryPrimitives.WriteInt16BigEndian(s, 1);
        BinaryPrimitives.WriteInt16BigEndian(s[0x2..], 0x28);
        BinaryPrimitives.WriteInt16BigEndian(s[0x6..], (short)x1);
        BinaryPrimitives.WriteInt16BigEndian(s[0x8..], (short)y1);
        BinaryPrimitives.WriteUInt16BigEndian(s[0xA..], 0x3);
        BinaryPrimitives.WriteUInt16BigEndian(s[0xC..], 0);
        s.Slice(0xE, 0x4).Fill(1);
        short[] xs = [0x28, 0, (short)(x1 - 0x28), 0];
        short[] ys = [0, (short)y1, 0, (short)-y1];
        for (var i = 0; i < 0x4; i++)
        {
            BinaryPrimitives.WriteInt16BigEndian(s[(0x12 + (i * 0x2))..], xs[i]);
            BinaryPrimitives.WriteInt16BigEndian(s[(0x1A + (i * 0x2))..], ys[i]);
        }

        return bytes;
    }

    /// <summary>Lays the tables out after the directory.</summary>
    /// <param name="tables">The tables by tag.</param>
    /// <returns>The font file.</returns>
    internal static byte[] Assemble(SortedDictionary<uint, byte[]> tables)
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
