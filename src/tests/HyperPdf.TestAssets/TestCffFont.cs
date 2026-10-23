// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;

namespace HyperPdf.TestAssets;

/// <summary>
/// Builds an OpenType font with Compact Font Format outlines from the same characters as <see cref="TestFont"/>. The
/// glyphs use hints, a hint mask, local and global subroutines (one local subroutine is never called) and, in the name-keyed
/// form, an accented 'é' made by the accented-character form of <c>endchar</c>. The CID-keyed form has two Font DICTs.
/// </summary>
public static class TestCffFont
{
    /// <summary>The SID of the standard string "e".</summary>
    private const int StandardSidE = 0x46;

    /// <summary>The SID of the standard string "acute".</summary>
    private const int StandardSidAcute = 0x7D;

    /// <summary>The StandardEncoding code of "e".</summary>
    private const int CodeE = 0x65;

    /// <summary>The StandardEncoding code of "acute".</summary>
    private const int CodeAcute = 0xC2;

    /// <summary>The width of glyphs that give none, and the width explicit widths are relative to.</summary>
    private const int DefaultWidth = 0x1F4;

    /// <summary>The first SID of a string in the String INDEX.</summary>
    private const int FirstCustomSid = 0x187;

    /// <summary>The call number that selects subroutine zero with the bias of 107.</summary>
    private const int FirstSubr = -0x6B;

    /// <summary>The vertical origin of every glyph.</summary>
    private const int VerticalOrigin = 0x370;

    /// <summary>The advance height of glyph zero in vertical writing.</summary>
    private const int VerticalAdvance = 0x3E8;

    /// <summary>The operator that ends a charstring.</summary>
    private const byte EndChar = 0xE;

    /// <summary>The operator that moves the pen.</summary>
    private const byte RMoveTo = 0x15;

    /// <summary>The operator that draws lines.</summary>
    private const byte RLineTo = 0x5;

    /// <summary>The operator that returns from a subroutine.</summary>
    private const byte Return = 0xB;

    /// <summary>The operator that calls a local subroutine.</summary>
    private const byte CallSubr = 0xA;

    /// <summary>The operator that calls a global subroutine.</summary>
    private const byte CallGsubr = 0x1D;

    /// <summary>The operator that declares horizontal stems with hint masks.</summary>
    private const byte HStemHm = 0x12;

    /// <summary>The operator that declares a hint mask.</summary>
    private const byte HintMask = 0x13;

    /// <summary>The Top DICT operator of the charset.</summary>
    private const int CharsetOp = 0xF;

    /// <summary>The Top DICT operator of the charstrings.</summary>
    private const int CharStringsOp = 0x11;

    /// <summary>The Top DICT operator of the Private DICT.</summary>
    private const int PrivateOp = 0x12;

    /// <summary>The Private DICT operator of the subroutines.</summary>
    private const int SubrsOp = 0x13;

    /// <summary>The Private DICT operator of the default width.</summary>
    private const int DefaultWidthOp = 0x14;

    /// <summary>The Private DICT operator of the nominal width.</summary>
    private const int NominalWidthOp = 0x15;

    /// <summary>The Top DICT operator of the registry, ordering and supplement.</summary>
    private const int RosOp = 0xC1E;

    /// <summary>The Top DICT operator of the CID count.</summary>
    private const int CidCountOp = 0xC22;

    /// <summary>The Top DICT operator of the Font DICT array.</summary>
    private const int FdArrayOp = 0xC24;

    /// <summary>The Top DICT operator of the Font DICT selector.</summary>
    private const int FdSelectOp = 0xC25;

    /// <summary>The bytes of the CFF header.</summary>
    private const int HeaderLength = 0x4;

    /// <summary>The bytes a fixed-size DICT number and its operator take.</summary>
    private const int EntryLength = 0x6;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 0x8;

    /// <summary>The options of the default font: name-keyed, with accents, no vertical metrics and no licence limits.</summary>
    private static readonly TestCffOptions Default = new();

    /// <summary>Writes the name-keyed font with no licence restrictions.</summary>
    /// <returns>The font file.</returns>
    public static byte[] Create() => Create(Default);

    /// <summary>Writes the font.</summary>
    /// <param name="options">What the font has.</param>
    /// <returns>The font file.</returns>
    public static byte[] Create(TestCffOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var glyphCount = TestFont.Characters.Length + 1;
        Dictionary<string, byte[]> tables = new() { ["CFF "] = options.CidKeyed ? CidKeyedCff() : NameKeyedCff(options), ["maxp"] = Maxp(glyphCount) };
        if (options.Vertical)
        {
            tables["vhea"] = Vhea(glyphCount);
            tables["vmtx"] = Vmtx(glyphCount);
            tables["VORG"] = Vorg();
        }

        foreach (var tag in options.ExtraTables)
        {
            tables[tag] = [1, 0, 0, 0];
        }

        return TestFont.CreateOpenType(tables, options.EmbeddingFlags, options.Family);
    }

    /// <summary>Takes the bare Compact Font Format program out of an OpenType font written by <see cref="Create(TestCffOptions)"/>.</summary>
    /// <param name="openType">The OpenType font file.</param>
    /// <returns>The bytes of its 'CFF ' table.</returns>
    /// <exception cref="InvalidOperationException">The font has no 'CFF ' table.</exception>
    public static byte[] ExtractProgram(byte[] openType)
    {
        ArgumentNullException.ThrowIfNull(openType);
        const int directoryHeader = 12;
        const int recordSize = 16;
        const uint cffTag = 0x43464620;
        var count = BinaryPrimitives.ReadUInt16BigEndian(openType.AsSpan(0x4));
        for (var i = 0; i < count; i++)
        {
            var record = openType.AsSpan(directoryHeader + (i * recordSize));
            if (BinaryPrimitives.ReadUInt32BigEndian(record) != cffTag)
            {
                continue;
            }

            var offset = (int)BinaryPrimitives.ReadUInt32BigEndian(record[0x8..]);
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(record[0xC..]);
            return openType.AsSpan(offset, length).ToArray();
        }

        throw new InvalidOperationException("The font has no CFF table.");
    }

    /// <summary>Gets the advance height of a glyph in vertical writing.</summary>
    /// <param name="glyph">The glyph.</param>
    /// <returns>The advance height in font units.</returns>
    public static int VerticalAdvanceOf(int glyph) => glyph == 0 ? VerticalAdvance : VerticalAdvance - (glyph % 0x3 * 0x64);

    /// <summary>Writes the maximum profile of a font with CFF outlines.</summary>
    /// <param name="glyphs">The glyph count.</param>
    /// <returns>The table.</returns>
    private static byte[] Maxp(int glyphs)
    {
        var table = new byte[0x6];
        BinaryPrimitives.WriteUInt32BigEndian(table, 0x00005000);
        BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(0x4), (ushort)glyphs);
        return table;
    }

    /// <summary>Writes the vertical header.</summary>
    /// <param name="glyphs">The glyph count.</param>
    /// <returns>The table.</returns>
    private static byte[] Vhea(int glyphs)
    {
        var table = new byte[0x24];
        BinaryPrimitives.WriteUInt32BigEndian(table, 0x00011000);
        BinaryPrimitives.WriteInt16BigEndian(table.AsSpan(0x4), 0x1F4);
        BinaryPrimitives.WriteInt16BigEndian(table.AsSpan(0x6), -0x1F4);
        BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(0xA), VerticalAdvance);
        BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(0x22), (ushort)glyphs);
        return table;
    }

    /// <summary>Writes the vertical metrics.</summary>
    /// <param name="glyphs">The glyph count.</param>
    /// <returns>The table.</returns>
    private static byte[] Vmtx(int glyphs)
    {
        var table = new byte[glyphs * 0x4];
        for (var i = 0; i < glyphs; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(i * 0x4), (ushort)VerticalAdvanceOf(i));
        }

        return table;
    }

    /// <summary>Writes the vertical origin table with one default and no exceptions.</summary>
    /// <returns>The table.</returns>
    private static byte[] Vorg()
    {
        var table = new byte[0x8];
        BinaryPrimitives.WriteUInt16BigEndian(table, 1);
        BinaryPrimitives.WriteInt16BigEndian(table.AsSpan(0x4), VerticalOrigin);
        return table;
    }

    /// <summary>Gets the SID of a glyph's name in the name-keyed font.</summary>
    /// <param name="c">The character.</param>
    /// <param name="glyph">The glyph number.</param>
    /// <param name="custom">The custom strings, which receive the name when it is not a standard one.</param>
    /// <returns>The SID.</returns>
    private static int NameOf(char c, int glyph, List<byte[]> custom)
    {
        if (c == 'e')
        {
            return StandardSidE;
        }

        if (c == '´')
        {
            return StandardSidAcute;
        }

        custom.Add(System.Text.Encoding.ASCII.GetBytes($"glyph{glyph}"));
        return FirstCustomSid + custom.Count - 1;
    }

    /// <summary>Writes a name-keyed CFF font.</summary>
    /// <param name="options">What the font has.</param>
    /// <returns>The CFF data.</returns>
    private static byte[] NameKeyedCff(TestCffOptions options)
    {
        var characters = TestFont.Characters;
        var custom = new List<byte[]>();
        var charset = new List<byte> { 0 };
        var charStrings = new List<byte[]> { Glyph('\0', 0, options.Accents) };
        for (var i = 0; i < characters.Length; i++)
        {
            var sid = NameOf(characters[i], i + 1, custom);
            charset.Add((byte)(sid >> ByteBits));
            charset.Add((byte)sid);
            charStrings.Add(Glyph(characters[i], i + 1, options.Accents));
        }

        var privateDict = PrivateDict(PrivateDict(0).Length + EntryLength);
        var gsubrs = Index([GlobalSubr()]);
        var strings = Index(custom);
        var name = Index(["PVLTestCff"u8.ToArray()]);
        var charStringsIndex = Index(charStrings);
        var charsetAt = HeaderLength + name.Length + Index([Top(0, 0, 0, 0)]).Length + strings.Length + gsubrs.Length;
        var charStringsAt = charsetAt + charset.Count;
        var top = Index([Top(charsetAt, charStringsAt, privateDict.Length, charStringsAt + charStringsIndex.Length)]);
        return [.. Header(), .. name, .. top, .. strings, .. gsubrs, .. charset, .. charStringsIndex, .. privateDict, .. Index(LocalSubrs())];
    }

    /// <summary>Writes a CID-keyed CFF font with two Font DICTs, the second for the glyphs from the middle on.</summary>
    /// <returns>The CFF data.</returns>
    private static byte[] CidKeyedCff()
    {
        var characters = TestFont.Characters;
        var glyphs = characters.Length + 1;
        var middle = glyphs / 0x2;
        var strings = Index(["Adobe"u8.ToArray(), "Identity"u8.ToArray()]);
        var name = Index(["PVLTestCidCff"u8.ToArray()]);
        var gsubrs = Index([GlobalSubr()]);
        var charset = new byte[] { 0x2, 0, 1, (byte)((glyphs - 0x2) >> ByteBits), (byte)(glyphs - 0x2) };
        var select = new byte[] { 0x3, 0, 0x2, 0, 0, 0, (byte)(middle >> ByteBits), (byte)middle, 1, (byte)(glyphs >> ByteBits), (byte)glyphs };
        var charStrings = new List<byte[]> { Glyph('\0', 0, false) };
        for (var i = 0; i < characters.Length; i++)
        {
            charStrings.Add(Glyph(characters[i], i + 1, false));
        }

        var charStringsIndex = Index(charStrings);
        var privateDict = PrivateDict(PrivateDict(0).Length + EntryLength);
        var subrs = Index(LocalSubrs());
        var blockLength = privateDict.Length + subrs.Length;
        var charsetAt = HeaderLength + name.Length + Index([CidTop(0, 0, 0, 0)]).Length + strings.Length + gsubrs.Length;
        var selectAt = charsetAt + charset.Length;
        var charStringsAt = selectAt + select.Length;
        var arrayAt = charStringsAt + charStringsIndex.Length;
        var firstAt = arrayAt + Index([FontDict(0, 0), FontDict(0, 0)]).Length;
        var array = Index([FontDict(privateDict.Length, firstAt), FontDict(privateDict.Length, firstAt + blockLength)]);
        var top = Index([CidTop(charsetAt, selectAt, charStringsAt, arrayAt)]);
        return [.. Header(), .. name, .. top, .. strings, .. gsubrs, .. charset, .. select, .. charStringsIndex, .. array, .. privateDict, .. subrs, .. privateDict, .. subrs];
    }

    /// <summary>Writes the CFF header.</summary>
    /// <returns>The header.</returns>
    private static byte[] Header() => [1, 0, HeaderLength, 0x4];

    /// <summary>Writes the Top DICT of a name-keyed font.</summary>
    /// <param name="charset">The charset offset.</param>
    /// <param name="charStrings">The CharStrings offset.</param>
    /// <param name="privateSize">The Private DICT's size.</param>
    /// <param name="privateAt">The Private DICT's offset.</param>
    /// <returns>The DICT.</returns>
    private static byte[] Top(int charset, int charStrings, int privateSize, int privateAt)
    {
        var dict = new List<byte>();
        Fixed(dict, CharsetOp, charset);
        Fixed(dict, CharStringsOp, charStrings);
        Fixed(dict, PrivateOp, privateSize, privateAt);
        return [.. dict];
    }

    /// <summary>Writes the Top DICT of a CID-keyed font.</summary>
    /// <param name="charset">The charset offset.</param>
    /// <param name="select">The FDSelect offset.</param>
    /// <param name="charStrings">The CharStrings offset.</param>
    /// <param name="array">The FDArray offset.</param>
    /// <returns>The DICT.</returns>
    private static byte[] CidTop(int charset, int select, int charStrings, int array)
    {
        var dict = new List<byte>();
        Fixed(dict, RosOp, FirstCustomSid, FirstCustomSid + 1, 0);
        Fixed(dict, CidCountOp, TestFont.Characters.Length + 1);
        Fixed(dict, CharsetOp, charset);
        Fixed(dict, FdSelectOp, select);
        Fixed(dict, CharStringsOp, charStrings);
        Fixed(dict, FdArrayOp, array);
        return [.. dict];
    }

    /// <summary>Writes a Font DICT.</summary>
    /// <param name="privateSize">The Private DICT's size.</param>
    /// <param name="privateAt">The Private DICT's offset.</param>
    /// <returns>The DICT.</returns>
    private static byte[] FontDict(int privateSize, int privateAt)
    {
        var dict = new List<byte>();
        Fixed(dict, PrivateOp, privateSize, privateAt);
        return [.. dict];
    }

    /// <summary>Writes a Private DICT with a width pair and the local subroutines right after it.</summary>
    /// <param name="subrsAt">The offset of the subroutines from the start of the DICT, or zero to leave the entry out.</param>
    /// <returns>The DICT.</returns>
    private static byte[] PrivateDict(int subrsAt)
    {
        var dict = new List<byte>();
        Fixed(dict, DefaultWidthOp, DefaultWidth);
        Fixed(dict, NominalWidthOp, DefaultWidth);
        if (subrsAt > 0)
        {
            Fixed(dict, SubrsOp, subrsAt);
        }

        return [.. dict];
    }

    /// <summary>Writes a DICT entry whose operands all take five bytes.</summary>
    /// <param name="dict">The DICT being built.</param>
    /// <param name="op">The operator; a two-byte operator is <c>0xC00 | second byte</c>.</param>
    /// <param name="operands">The operands.</param>
    private static void Fixed(List<byte> dict, int op, params int[] operands)
    {
        foreach (var operand in operands)
        {
            var bytes = new byte[0x4];
            BinaryPrimitives.WriteInt32BigEndian(bytes, operand);
            dict.Add(0x1D);
            dict.AddRange(bytes);
        }

        if (op > byte.MaxValue)
        {
            dict.Add(0xC);
        }

        dict.Add((byte)op);
    }

    /// <summary>Writes the global subroutine: a 100-unit square.</summary>
    /// <returns>The subroutine.</returns>
    private static byte[] GlobalSubr() => [.. Num(0x190), .. Num(0), RLineTo, .. Num(0), .. Num(0x190), RLineTo, .. Num(-0x190), .. Num(0), RLineTo, Return];

    /// <summary>Writes the local subroutines: a triangle that lower case letters call and one nothing calls.</summary>
    /// <returns>The subroutines.</returns>
    private static List<byte[]> LocalSubrs() =>
    [
        [.. Num(0x12C), .. Num(0), RLineTo, .. Num(-0x96), .. Num(0x12C), RLineTo, Return],
        [.. Num(0xA), .. Num(0xA), RLineTo, Return],
    ];

    /// <summary>Writes a glyph's charstring.</summary>
    /// <param name="c">The character, or zero for .notdef.</param>
    /// <param name="glyph">The glyph number.</param>
    /// <param name="accents">Whether 'é' is made from two other glyphs.</param>
    /// <returns>The charstring.</returns>
    private static byte[] Glyph(char c, int glyph, bool accents)
    {
        var advance = glyph == 0 ? DefaultWidth : TestFont.AdvanceOf(c);
        var width = advance == DefaultWidth ? [] : Num(advance - DefaultWidth);
        if (c == 'é' && accents)
        {
            return [.. width, .. Num(0), .. Num(0), .. Num(CodeE), .. Num(CodeAcute), EndChar];
        }

        if (c == ' ')
        {
            return [.. width, EndChar];
        }

        var accent = c == '´';
        var code = new List<byte>();
        WriteOutline(code, c, width, accent ? 0x258 : 0x28, accent ? 0x258 : 0, advance - 0x50, accent ? 0xC8 : 0x1F4 + (c % 0xC8));
        return [.. code];
    }

    /// <summary>Writes the outline of a glyph other than space.</summary>
    /// <param name="code">The charstring being built.</param>
    /// <param name="c">The character.</param>
    /// <param name="width">The encoded width, empty for the default.</param>
    /// <param name="x0">The left edge.</param>
    /// <param name="y0">The bottom edge.</param>
    /// <param name="w">The rectangle width.</param>
    /// <param name="h">The rectangle height.</param>
    private static void WriteOutline(List<byte> code, char c, byte[] width, int x0, int y0, int w, int h)
    {
        code.AddRange(width);
        if (char.IsDigit(c))
        {
            // One horizontal stem and a vertical stem declared by the hint mask's arguments, so the mask is one byte long.
            code.AddRange([.. Num(0), .. Num(0x32), HStemHm, .. Num(0x14), .. Num(0x1E), HintMask, 0x80]);
        }

        code.AddRange([.. Num(x0), .. Num(y0), RMoveTo]);
        if (char.IsAsciiLetterUpper(c))
        {
            code.AddRange([.. Num(FirstSubr), CallGsubr]);
        }
        else if (char.IsAsciiLetterLower(c))
        {
            code.AddRange([.. Num(FirstSubr), CallSubr]);
        }
        else
        {
            code.AddRange([.. Num(w), .. Num(0), RLineTo, .. Num(0), .. Num(h), RLineTo, .. Num(-w), .. Num(0), RLineTo]);
        }

        code.Add(EndChar);
    }

    /// <summary>Encodes a charstring number.</summary>
    /// <param name="value">The value, between -1131 and 1131 or any 16-bit integer.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Num(int value)
    {
        if (value is >= -0x6B and <= 0x6B)
        {
            return [(byte)(value + 0x8B)];
        }

        if (value is >= 0x6C and <= 0x46B)
        {
            return [(byte)(0xF7 + ((value - 0x6C) >> ByteBits)), (byte)(value - 0x6C)];
        }

        return value is <= -0x6C and >= -0x46B
            ? [(byte)(0xFB + ((-value - 0x6C) >> ByteBits)), (byte)(-value - 0x6C)]
            : [0x1C, (byte)(value >> ByteBits), (byte)value];
    }

    /// <summary>Writes a CFF INDEX with four-byte offsets.</summary>
    /// <param name="items">The objects.</param>
    /// <returns>The INDEX.</returns>
    private static byte[] Index(List<byte[]> items)
    {
        if (items.Count == 0)
        {
            return [0, 0];
        }

        var data = new List<byte>();
        var offsets = new List<int> { 1 };
        foreach (var item in items)
        {
            data.AddRange(item);
            offsets.Add(data.Count + 1);
        }

        var result = new List<byte> { (byte)(items.Count >> ByteBits), (byte)items.Count, 0x4 };
        foreach (var offset in offsets)
        {
            result.AddRange([0, 0, (byte)(offset >> ByteBits), (byte)offset]);
        }

        result.AddRange(data);
        return [.. result];
    }
}
