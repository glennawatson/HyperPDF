// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Tests for the CFF parser and Type 2 charstring interpreter on a hand-assembled font.</summary>
public sealed class CffProgramTests
{
    /// <summary>The hstemhm operator.</summary>
    private const int HStemHM = 18;

    /// <summary>The hintmask operator.</summary>
    private const int HintMask = 19;

    /// <summary>The rmoveto operator.</summary>
    private const int RMoveTo = 21;

    /// <summary>The rlineto operator.</summary>
    private const int RLineTo = 5;

    /// <summary>The hlineto operator.</summary>
    private const int HLineTo = 6;

    /// <summary>The callsubr operator.</summary>
    private const int CallSubr = 10;

    /// <summary>The callgsubr operator.</summary>
    private const int CallGlobalSubr = 29;

    /// <summary>The return operator.</summary>
    private const int Return = 11;

    /// <summary>The endchar operator.</summary>
    private const int EndChar = 14;

    /// <summary>The hflex operator's second byte.</summary>
    private const int HFlex = 34;

    /// <summary>The operand that calls subroutine 0 with the small-count bias.</summary>
    private const int FirstSubr = -107;

    /// <summary>The default width in the Private DICT.</summary>
    private const int DefaultWidth = 500;

    /// <summary>The nominal width in the Private DICT.</summary>
    private const int NominalWidth = 100;

    /// <summary>The width of A: its width operand plus the nominal width.</summary>
    private const float WidthA = 600;

    /// <summary>The width operand of A and Aacute.</summary>
    private const int WidthOperand = 500;

    /// <summary>The StandardEncoding code of A.</summary>
    private const int CodeA = 65;

    /// <summary>The StandardEncoding code of acute.</summary>
    private const int CodeAcute = 0xC2;

    /// <summary>The glyph id of A.</summary>
    private const int GlyphA = 1;

    /// <summary>The glyph id of acute.</summary>
    private const int GlyphAcute = 2;

    /// <summary>The glyph id of Aacute.</summary>
    private const int GlyphAacute = 3;

    /// <summary>The number of glyphs in the font.</summary>
    private const int GlyphTotal = 4;

    /// <summary>The bytes cut from the end of the font in the damage test.</summary>
    private const int DamagedTail = 20;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The hint mask byte that enables both stems.</summary>
    private const byte BothStems = 0xC0;

    /// <summary>The DICT operator of the charset offset.</summary>
    private const byte CharsetOperator = 15;

    /// <summary>The DICT operator of the CharStrings offset.</summary>
    private const byte CharStringsOperator = 17;

    /// <summary>The DICT operator of the Private DICT size and offset.</summary>
    private const byte PrivateOperator = 18;

    /// <summary>The DICT operator of the local subroutines offset.</summary>
    private const byte SubrsOperator = 19;

    /// <summary>The DICT operator of defaultWidthX.</summary>
    private const byte DefaultWidthOperator = 20;

    /// <summary>The DICT operator of nominalWidthX.</summary>
    private const byte NominalWidthOperator = 21;

    /// <summary>The DICT prefix of a 32-bit integer.</summary>
    private const byte LongIntPrefix = 29;

    /// <summary>The CFF header: version 1.0, header size 4, offset size 1.</summary>
    private static readonly byte[] Header = [0x01, 0x00, 0x04, 0x01];

    /// <summary>The hstemhm operands of A: the width operand and one stem.</summary>
    private static readonly int[] StemArguments = [WidthOperand, 0, 50];

    /// <summary>The implied vstem before A's hint mask.</summary>
    private static readonly int[] MaskArguments = [10, 20];

    /// <summary>The first move of A.</summary>
    private static readonly int[] MoveArguments = [100, 0];

    /// <summary>The first line of A.</summary>
    private static readonly int[] LineArguments = [200, 400];

    /// <summary>The line the local subroutine draws.</summary>
    private static readonly int[] LocalSubrLine = [200, -400];

    /// <summary>The horizontal line the global subroutine draws.</summary>
    private static readonly int[] GlobalSubrLine = [-100];

    /// <summary>The hflex operands.</summary>
    private static readonly int[] FlexArguments = [-10, -10, 10, -10, -10, -10, -10];

    /// <summary>The move of the acute accent.</summary>
    private static readonly int[] AccentMove = [0, 600];

    /// <summary>The line of the acute accent.</summary>
    private static readonly int[] AccentLine = [50, 100];

    /// <summary>The endchar operands of Aacute: width, accent offset, base code and accent code.</summary>
    private static readonly int[] SeacArguments = [WidthOperand, 100, 50, CodeA, CodeAcute];

    /// <summary>The commands A draws.</summary>
    private static readonly string[] OutlineA =
    [
        "M 100 0",
        "L 300 400",
        "L 500 0",
        "L 400 0",
        "C 390 0 380 10 370 10",
        "C 360 10 350 0 340 0",
        "Z",
    ];

    /// <summary>The commands the acute accent draws inside Aacute.</summary>
    private static readonly string[] OutlineAccent = ["M 100 650", "L 150 750", "Z"];

    /// <summary>The font parses, with names, the standard encoding and CIDs.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FontParses()
    {
        var program = Parse();

        await Assert.That(program.GlyphCount).IsEqualTo(GlyphTotal);
        await Assert.That(Encoding.ASCII.GetString(program.GetGlyphName(GlyphA))).IsEqualTo("A");
        await Assert.That(Encoding.ASCII.GetString(program.GetGlyphName(GlyphAacute))).IsEqualTo("Aacute");
        await Assert.That(program.GetGlyphByName("acute"u8)).IsEqualTo(GlyphAcute);
        await Assert.That(program.GetGlyphByCharCode(CodeA)).IsEqualTo(GlyphA);
        await Assert.That(program.GetGlyphByCharCode(CodeAcute)).IsEqualTo(GlyphAcute);
        await Assert.That(program.GetGlyphByUnicode('A')).IsEqualTo(GlyphA);
        await Assert.That(program.GetGlyphByCid(GlyphAcute)).IsEqualTo(GlyphAcute);
        await Assert.That(program.IsCidKeyed).IsFalse();
        await Assert.That(program.FontMatrix).IsEqualTo(FontMatrix.Default);
    }

    /// <summary>Hints, hint masks, local and global subroutines and hflex all decode.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OutlineDecodes()
    {
        var commands = Decode(Parse(), GlyphA);

        await Assert.That(commands).IsEquivalentTo(OutlineA);
    }

    /// <summary>Widths come from the charstring or the Private DICT default.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WidthsParse()
    {
        var program = Parse();

        await Assert.That(program.GetAdvanceWidth(GlyphA)).IsEqualTo(WidthA);
        await Assert.That(program.GetAdvanceWidth(GlyphAcute)).IsEqualTo(DefaultWidth);
        await Assert.That(program.GetAdvanceWidth(GlyphAacute)).IsEqualTo(WidthA);
    }

    /// <summary>The accented-character form of endchar draws the base and the offset accent.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SeacComposes()
    {
        var commands = Decode(Parse(), GlyphAacute);

        await Assert.That(commands).IsEquivalentTo([.. OutlineA, .. OutlineAccent]);
    }

    /// <summary>A truncated Private DICT produces a PDF format error, not a runtime bounds error.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TruncatedPrivateDictionaryThrowsPdfException()
    {
        var font = BuildFont();
        var truncated = font.AsSpan(0, font.Length - DamagedTail).ToArray();
        await Assert.That(() => CffProgram.TryParse(truncated, out _)).Throws<PdfException>();
    }

    /// <summary>A header without font indexes produces a PDF format error.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HeaderWithoutIndexesThrowsPdfException() =>
        await Assert.That(static () => CffProgram.TryParse(Header, out _)).Throws<PdfException>();

    /// <summary>Parses the hand-assembled font.</summary>
    /// <returns>The program.</returns>
    /// <exception cref="InvalidOperationException">The font did not parse.</exception>
    private static CffProgram Parse() =>
        CffProgram.TryParse(BuildFont(), out var program) ? program : throw new InvalidOperationException("The test font did not parse.");

    /// <summary>Decodes a glyph's outline.</summary>
    /// <param name="program">The program.</param>
    /// <param name="glyph">The glyph id.</param>
    /// <returns>The commands.</returns>
    private static List<string> Decode(FontProgram program, int glyph)
    {
        var recorder = new OutlineRecorder();
        program.DecodeGlyph(glyph, ref recorder);
        return recorder.Commands;
    }

    /// <summary>Assembles a font with .notdef, A, acute and Aacute.</summary>
    /// <returns>The CFF bytes.</returns>
    private static byte[] BuildFont()
    {
        var names = Index("Test"u8.ToArray());
        var strings = Index("Aacute"u8.ToArray());
        var globalSubrs = Index(Type2().Numbers(GlobalSubrLine).Op(HLineTo).Op(Return).ToArray());
        var localSubrs = Index(Type2().Numbers(LocalSubrLine).Op(RLineTo).Op(Return).ToArray());
        var charset = Charset(Sid("A"u8), Sid("acute"u8), CffStandardData.StandardStringCount);
        var charStrings = Index(
            Type2().Op(EndChar).ToArray(),
            GlyphACharstring(),
            Type2().Numbers(AccentMove).Op(RMoveTo).Numbers(AccentLine).Op(RLineTo).Op(EndChar).ToArray(),
            Type2().Numbers(SeacArguments).Op(EndChar).ToArray());
        var privateDict = PrivateDict(PrivateDict(0).Length);
        var topSize = Index(TopDict(0, 0, 0, 0)).Length;
        var charsetOffset = Header.Length + names.Length + topSize + strings.Length + globalSubrs.Length;
        var charStringsOffset = charsetOffset + charset.Length;
        var top = Index(TopDict(charsetOffset, charStringsOffset, privateDict.Length, charStringsOffset + charStrings.Length));
        return [.. Header, .. names, .. top, .. strings, .. globalSubrs, .. charset, .. charStrings, .. privateDict, .. localSubrs];
    }

    /// <summary>Starts a Type 2 charstring.</summary>
    /// <returns>The writer.</returns>
    private static CharstringWriter Type2() => new(false);

    /// <summary>Assembles the charstring of A.</summary>
    /// <returns>The charstring.</returns>
    private static byte[] GlyphACharstring() => Type2()
        .Numbers(StemArguments).Op(HStemHM)
        .Numbers(MaskArguments).Op(HintMask).Raw(BothStems)
        .Numbers(MoveArguments).Op(RMoveTo)
        .Numbers(LineArguments).Op(RLineTo)
        .Numbers(FirstSubr).Op(CallSubr)
        .Numbers(FirstSubr).Op(CallGlobalSubr)
        .Numbers(FlexArguments).EscapeOp(HFlex)
        .Op(EndChar)
        .ToArray();

    /// <summary>Builds the Private DICT.</summary>
    /// <param name="subrsOffset">The offset of the local subroutines from the start of the DICT.</param>
    /// <returns>The DICT.</returns>
    private static byte[] PrivateDict(int subrsOffset) =>
        [.. Int(DefaultWidth), DefaultWidthOperator, .. Int(NominalWidth), NominalWidthOperator, .. Int(subrsOffset), SubrsOperator];

    /// <summary>Builds a Top DICT whose offsets are all 32-bit, so its size does not depend on them.</summary>
    /// <param name="charset">The charset offset.</param>
    /// <param name="charStrings">The CharStrings offset.</param>
    /// <param name="privateSize">The Private DICT size.</param>
    /// <param name="privateOffset">The Private DICT offset.</param>
    /// <returns>The DICT.</returns>
    private static byte[] TopDict(int charset, int charStrings, int privateSize, int privateOffset) =>
        [.. Int(charset), CharsetOperator, .. Int(charStrings), CharStringsOperator, .. Int(privateSize), .. Int(privateOffset), PrivateOperator];

    /// <summary>Builds a format 0 charset.</summary>
    /// <param name="sids">The SID of each glyph after .notdef.</param>
    /// <returns>The charset.</returns>
    private static byte[] Charset(params ReadOnlySpan<int> sids)
    {
        var charset = new byte[1 + (sids.Length * sizeof(ushort))];
        for (var i = 0; i < sids.Length; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(charset.AsSpan(1 + (i * sizeof(ushort))), (ushort)sids[i]);
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

    /// <summary>Finds the SID of a standard string.</summary>
    /// <param name="name">The string.</param>
    /// <returns>The SID.</returns>
    /// <exception cref="InvalidOperationException">The string is not standard.</exception>
    private static int Sid(ReadOnlySpan<byte> name)
    {
        for (var sid = 0; sid < CffStandardData.StandardStringCount; sid++)
        {
            if (CffStandardData.GetStandardString(sid).SequenceEqual(name))
            {
                return sid;
            }
        }

        throw new InvalidOperationException("Not a standard string.");
    }
}
