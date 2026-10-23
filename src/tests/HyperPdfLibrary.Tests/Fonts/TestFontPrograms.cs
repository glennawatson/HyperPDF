// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>
/// Writes tiny Type 1 and CFF font programs whose glyphs are rectangles, so font tests give the same answers on every
/// computer. Glyph A is 400 by 700 units with a 500 advance; glyph B is 500 by 600 with a 600 advance.
/// </summary>
internal static class TestFontPrograms
{
    /// <summary>The left side bearing of both glyphs.</summary>
    internal const int Left = 50;

    /// <summary>The advance of A.</summary>
    internal const int WidthA = 500;

    /// <summary>The advance of B.</summary>
    internal const int WidthB = 600;

    /// <summary>The rectangle width of A.</summary>
    internal const int BoxWidthA = 400;

    /// <summary>The rectangle height of A.</summary>
    internal const int HeightA = 700;

    /// <summary>The rectangle width of B.</summary>
    internal const int BoxWidthB = 500;

    /// <summary>The rectangle height of B.</summary>
    internal const int HeightB = 600;

    /// <summary>The CID of A in the CID-keyed CFF font.</summary>
    internal const int CidA = 34;

    /// <summary>The CID of B in the CID-keyed CFF font.</summary>
    internal const int CidB = 35;

    /// <summary>The Type 1 hsbw operator.</summary>
    private const int Hsbw = 13;

    /// <summary>The rmoveto operator.</summary>
    private const int RMoveTo = 21;

    /// <summary>The hlineto operator.</summary>
    private const int HLineTo = 6;

    /// <summary>The vlineto operator.</summary>
    private const int VLineTo = 7;

    /// <summary>The Type 1 closepath operator.</summary>
    private const int ClosePath = 9;

    /// <summary>The endchar operator.</summary>
    private const int EndChar = 14;

    /// <summary>The eexec key.</summary>
    private const ushort EexecKey = 55_665;

    /// <summary>The charstring key.</summary>
    private const ushort CharstringKey = 4_330;

    /// <summary>The first cipher constant.</summary>
    private const int CipherMultiplier = 52_845;

    /// <summary>The second cipher constant.</summary>
    private const int CipherIncrement = 22_719;

    /// <summary>The shift of the cipher key.</summary>
    private const int KeyShift = 8;

    /// <summary>The random bytes that start encrypted data.</summary>
    private const int RandomPrefix = 4;

    /// <summary>The DICT prefix of a 32-bit integer.</summary>
    private const byte LongIntPrefix = 29;

    /// <summary>The charset DICT operator.</summary>
    private const byte CharsetOperator = 15;

    /// <summary>The CharStrings DICT operator.</summary>
    private const byte CharStringsOperator = 17;

    /// <summary>The Private DICT operator.</summary>
    private const byte PrivateOperator = 18;

    /// <summary>The defaultWidthX Private DICT operator.</summary>
    private const byte DefaultWidthOperator = 20;

    /// <summary>The escape byte of two-byte DICT operators.</summary>
    private const byte Escape = 12;

    /// <summary>The ROS operator's second byte.</summary>
    private const byte RosOperator = 30;

    /// <summary>The FDArray operator's second byte.</summary>
    private const byte FdArrayOperator = 36;

    /// <summary>The FDSelect operator's second byte.</summary>
    private const byte FdSelectOperator = 37;

    /// <summary>The SID of the standard string A.</summary>
    private const int SidA = 34;

    /// <summary>The SID of the standard string B.</summary>
    private const int SidB = 35;

    /// <summary>The SID of the first custom string.</summary>
    private const int FirstCustomSid = 391;

    /// <summary>The bits of a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The glyphs after .notdef in the test programs.</summary>
    private const int GlyphCount = 2;

    /// <summary>Gets the CFF header: version 1.0, header size 4, offset size 1.</summary>
    private static ReadOnlySpan<byte> Header => [0x01, 0x00, 0x04, 0x01];

    /// <summary>Writes a Type 1 program with .notdef, A and B, and a built-in encoding placing A at 65 and B at 66.</summary>
    /// <param name="length1">The length of the cleartext part, for /Length1.</param>
    /// <returns>The program as a PDF /FontFile holds it.</returns>
    internal static byte[] Type1(out int length1)
    {
        byte[] clear =
        [
            .. "%!PS-AdobeFont-1.0: Test 001\n/FontName /Test def\n/FontMatrix [0.001 0 0 0.001 0 0] readonly def\n/FontBBox {0 0 600 700} readonly def\n"u8,
            .. "/Encoding 256 array\n0 1 255 {1 index exch /.notdef put} for\ndup 65 /A put\ndup 66 /B put\nreadonly def\ncurrentdict end\ncurrentfile eexec\n"u8,
        ];
        length1 = clear.Length;
        var text = new List<byte>("dup /Private 8 dict dup begin\n/lenIV 4 def\n/Subrs 0 array\n2 index /CharStrings 3 dict dup begin\n"u8.ToArray());
        AddGlyph(text, ".notdef", Type1Glyph(0, 0, 0));
        AddGlyph(text, "A", Type1Glyph(WidthA, BoxWidthA, HeightA));
        AddGlyph(text, "B", Type1Glyph(WidthB, BoxWidthB, HeightB));
        text.AddRange("end\nend\nreadonly put\nnoaccess put\nmark currentfile closefile\n"u8);
        return [.. clear, .. Encrypt([.. new byte[RandomPrefix], .. text], EexecKey)];
    }

    /// <summary>Writes a bare CFF program with .notdef, A and B named by standard strings, using StandardEncoding.</summary>
    /// <returns>The CFF bytes.</returns>
    internal static byte[] Cff()
    {
        var names = Index("Test"u8.ToArray());
        var strings = Index();
        var globalSubrs = Index();
        var charset = Charset(SidA, SidB);
        var charStrings = GlyphIndex();
        var privateDict = PrivateDict();
        var topSize = Index(TopDict(0, 0, 0, 0)).Length;
        var charsetOffset = Header.Length + names.Length + topSize + strings.Length + globalSubrs.Length;
        var charStringsOffset = charsetOffset + charset.Length;
        var top = Index(TopDict(charsetOffset, charStringsOffset, privateDict.Length, charStringsOffset + charStrings.Length));
        return [.. Header, .. names, .. top, .. strings, .. globalSubrs, .. charset, .. charStrings, .. privateDict];
    }

    /// <summary>Writes a CID-keyed CFF program whose glyphs 1 and 2 are CIDs 34 and 35.</summary>
    /// <returns>The CFF bytes.</returns>
    internal static byte[] CidCff()
    {
        var names = Index("Test"u8.ToArray());
        var strings = Index("Adobe"u8.ToArray(), "Identity"u8.ToArray());
        var globalSubrs = Index();
        var charset = Charset(CidA, CidB);
        var charStrings = GlyphIndex();
        var privateDict = PrivateDict();
        var selector = new byte[GlyphCount + 1];
        var dictArraySize = Index(PrivateEntry(0, 0)).Length;
        var topSize = Index(CidTopDict(0, 0, 0, 0)).Length;
        var charsetOffset = Header.Length + names.Length + topSize + strings.Length + globalSubrs.Length;
        var charStringsOffset = charsetOffset + charset.Length;
        var selectorOffset = charStringsOffset + charStrings.Length;
        var dictArrayOffset = selectorOffset + selector.Length;
        var dictArray = Index(PrivateEntry(privateDict.Length, dictArrayOffset + dictArraySize));
        var top = Index(CidTopDict(charsetOffset, charStringsOffset, dictArrayOffset, selectorOffset));
        return [.. Header, .. names, .. top, .. strings, .. globalSubrs, .. charset, .. charStrings, .. selector, .. dictArray, .. privateDict];
    }

    /// <summary>Builds the CharStrings INDEX of .notdef, A and B.</summary>
    /// <returns>The INDEX.</returns>
    private static byte[] GlyphIndex() => Index(
        new CharstringWriter(false).Numbers(0).Op(EndChar).ToArray(),
        Type2Glyph(WidthA, BoxWidthA, HeightA),
        Type2Glyph(WidthB, BoxWidthB, HeightB));

    /// <summary>Assembles a Type 1 rectangle glyph.</summary>
    /// <param name="width">The advance.</param>
    /// <param name="boxWidth">The rectangle width.</param>
    /// <param name="height">The rectangle height.</param>
    /// <returns>The charstring.</returns>
    private static CharstringWriter Type1Glyph(int width, int boxWidth, int height)
    {
        var writer = new CharstringWriter(true).Numbers(0, width).Op(Hsbw);
        return boxWidth == 0
            ? writer.Op(EndChar)
            : writer.Numbers(Left, 0).Op(RMoveTo).Numbers(boxWidth).Op(HLineTo).Numbers(height).Op(VLineTo)
            .Numbers(-boxWidth).Op(HLineTo).Op(ClosePath).Op(EndChar);
    }

    /// <summary>Assembles a Type 2 rectangle glyph; the width operand is the advance because the nominal width is zero.</summary>
    /// <param name="width">The advance.</param>
    /// <param name="boxWidth">The rectangle width.</param>
    /// <param name="height">The rectangle height.</param>
    /// <returns>The charstring.</returns>
    private static byte[] Type2Glyph(int width, int boxWidth, int height) => new CharstringWriter(false)
        .Numbers(width, Left, 0).Op(RMoveTo).Numbers(boxWidth).Op(HLineTo).Numbers(height).Op(VLineTo).Numbers(-boxWidth).Op(HLineTo).Op(EndChar)
        .ToArray();

    /// <summary>Builds an empty Private DICT, so the default and nominal widths are zero.</summary>
    /// <returns>The DICT.</returns>
    private static byte[] PrivateDict() => [.. Int(0), DefaultWidthOperator];

    /// <summary>Builds a DICT holding only a Private entry.</summary>
    /// <param name="size">The Private DICT size.</param>
    /// <param name="offset">The Private DICT offset.</param>
    /// <returns>The DICT.</returns>
    private static byte[] PrivateEntry(int size, int offset) => [.. Int(size), .. Int(offset), PrivateOperator];

    /// <summary>Builds a Top DICT with 32-bit offsets.</summary>
    /// <param name="charset">The charset offset.</param>
    /// <param name="charStrings">The CharStrings offset.</param>
    /// <param name="privateSize">The Private DICT size.</param>
    /// <param name="privateOffset">The Private DICT offset.</param>
    /// <returns>The DICT.</returns>
    private static byte[] TopDict(int charset, int charStrings, int privateSize, int privateOffset) =>
        [.. Int(charset), CharsetOperator, .. Int(charStrings), CharStringsOperator, .. Int(privateSize), .. Int(privateOffset), PrivateOperator];

    /// <summary>Builds a CID-keyed Top DICT with 32-bit offsets.</summary>
    /// <param name="charset">The charset offset.</param>
    /// <param name="charStrings">The CharStrings offset.</param>
    /// <param name="dictArray">The FDArray offset.</param>
    /// <param name="selector">The FDSelect offset.</param>
    /// <returns>The DICT.</returns>
    private static byte[] CidTopDict(int charset, int charStrings, int dictArray, int selector) =>
    [
        .. Int(FirstCustomSid), .. Int(FirstCustomSid + 1), .. Int(0), Escape, RosOperator,
        .. Int(charset), CharsetOperator, .. Int(charStrings), CharStringsOperator,
        .. Int(dictArray), Escape, FdArrayOperator, .. Int(selector), Escape, FdSelectOperator,
    ];

    /// <summary>Builds a format 0 charset.</summary>
    /// <param name="ids">The SID or CID of each glyph after .notdef.</param>
    /// <returns>The charset.</returns>
    private static byte[] Charset(params ReadOnlySpan<int> ids)
    {
        var charset = new byte[1 + (ids.Length * sizeof(ushort))];
        for (var i = 0; i < ids.Length; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(charset.AsSpan(1 + (i * sizeof(ushort))), (ushort)ids[i]);
        }

        return charset;
    }

    /// <summary>Encodes a DICT integer in its five-byte form.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Int(int value)
    {
        var bytes = new byte[1 + sizeof(int)];
        bytes[0] = LongIntPrefix;
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(1), value);
        return bytes;
    }

    /// <summary>Builds an INDEX with one-byte offsets.</summary>
    /// <param name="items">The objects.</param>
    /// <returns>The INDEX.</returns>
    private static byte[] Index(params byte[][] items)
    {
        if (items.Length == 0)
        {
            return [0, 0];
        }

        var output = new List<byte> { (byte)(items.Length >> ByteBits), (byte)items.Length, 1, 1 };
        var offset = 1;
        foreach (var item in items)
        {
            offset += item.Length;
            output.Add((byte)offset);
        }

        foreach (var item in items)
        {
            output.AddRange(item);
        }

        return [.. output];
    }

    /// <summary>Appends a CharStrings entry.</summary>
    /// <param name="text">The private part.</param>
    /// <param name="name">The glyph name.</param>
    /// <param name="charstring">The charstring.</param>
    private static void AddGlyph(List<byte> text, string name, CharstringWriter charstring)
    {
        var encrypted = Encrypt([.. new byte[RandomPrefix], .. charstring.ToArray()], CharstringKey);
        text.AddRange(Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture, $"/{name} {encrypted.Length} RD ")));
        text.AddRange(encrypted);
        text.AddRange(" ND\n"u8);
    }

    /// <summary>Encrypts bytes with the Type 1 cipher.</summary>
    /// <param name="plain">The plain bytes.</param>
    /// <param name="key">The initial key.</param>
    /// <returns>The cipher bytes.</returns>
    private static byte[] Encrypt(byte[] plain, ushort key)
    {
        var cipher = new byte[plain.Length];
        var r = key;
        for (var i = 0; i < plain.Length; i++)
        {
            cipher[i] = (byte)(plain[i] ^ (r >> KeyShift));
            r = (ushort)(((cipher[i] + r) * CipherMultiplier) + CipherIncrement);
        }

        return cipher;
    }
}
