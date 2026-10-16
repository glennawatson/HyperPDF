// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Tests for the Type 1 parser and charstring interpreter on a hand-assembled, encrypted font.</summary>
public sealed class Type1ProgramTests
{
    /// <summary>The hsbw operator.</summary>
    private const int Hsbw = 13;

    /// <summary>The rmoveto operator.</summary>
    private const int RMoveTo = 21;

    /// <summary>The rlineto operator.</summary>
    private const int RLineTo = 5;

    /// <summary>The hlineto operator.</summary>
    private const int HLineTo = 6;

    /// <summary>The vlineto operator.</summary>
    private const int VLineTo = 7;

    /// <summary>The rrcurveto operator.</summary>
    private const int RRCurveTo = 8;

    /// <summary>The closepath operator.</summary>
    private const int ClosePath = 9;

    /// <summary>The callsubr operator.</summary>
    private const int CallSubr = 10;

    /// <summary>The return operator.</summary>
    private const int Return = 11;

    /// <summary>The endchar operator.</summary>
    private const int EndChar = 14;

    /// <summary>The seac operator's second byte.</summary>
    private const int Seac = 6;

    /// <summary>The div operator's second byte.</summary>
    private const int Div = 12;

    /// <summary>The callothersubr operator's second byte.</summary>
    private const int CallOtherSubr = 16;

    /// <summary>The pop operator's second byte.</summary>
    private const int Pop = 17;

    /// <summary>The setcurrentpoint operator's second byte.</summary>
    private const int SetCurrentPoint = 33;

    /// <summary>The subroutine that adds a flex point.</summary>
    private const int FlexPointSubr = 2;

    /// <summary>The values of one relative point.</summary>
    private const int PointValues = 2;

    /// <summary>The initial eexec key.</summary>
    private const ushort EexecKey = 55_665;

    /// <summary>The initial charstring key.</summary>
    private const ushort CharstringKey = 4_330;

    /// <summary>The first cipher constant.</summary>
    private const int CipherMultiplier = 52_845;

    /// <summary>The second cipher constant.</summary>
    private const int CipherIncrement = 22_719;

    /// <summary>The shift that takes the key's high byte.</summary>
    private const int KeyShift = 8;

    /// <summary>The random bytes before encrypted data.</summary>
    private const int RandomPrefix = 4;

    /// <summary>The PFB segment marker.</summary>
    private const byte PfbMarker = 0x80;

    /// <summary>The PFB ASCII segment type.</summary>
    private const byte PfbAscii = 1;

    /// <summary>The PFB binary segment type.</summary>
    private const byte PfbBinary = 2;

    /// <summary>The PFB end-of-file segment type.</summary>
    private const byte PfbEnd = 3;

    /// <summary>The hexadecimal digits written per line in the PFA form.</summary>
    private const int HexLine = 64;

    /// <summary>The code of A.</summary>
    private const int CodeA = 65;

    /// <summary>The code of B.</summary>
    private const int CodeB = 66;

    /// <summary>The glyph id of A.</summary>
    private const int GlyphA = 1;

    /// <summary>The glyph id of B.</summary>
    private const int GlyphB = 2;

    /// <summary>The glyph id of Aacute.</summary>
    private const int GlyphAacute = 4;

    /// <summary>The number of glyphs in the font.</summary>
    private const int GlyphTotal = 5;

    /// <summary>The width of A.</summary>
    private const float WidthA = 600;

    /// <summary>The scale of the font matrix.</summary>
    private const float MatrixScale = 0.001F;

    /// <summary>The top of the font bounding box.</summary>
    private const float BoxTop = 800;

    /// <summary>The cleartext part of the font.</summary>
    private static readonly byte[] Cleartext = """
        %!FontType1-1.0: Test 001
        /FontName /Test def
        /FontMatrix [0.001 0 0 0.001 0 0] readonly def
        /FontBBox {0 -10 700 800} readonly def
        /Encoding 256 array
        0 1 255 {1 index exch /.notdef put} for
        dup 65 /A put
        dup 66 /B put
        readonly def
        currentfile eexec

        """u8.ToArray();

    /// <summary>The hsbw of A, with the width given as 1200 / 2 to exercise div and 32-bit numbers.</summary>
    private static readonly int[] MetricsA = [50, 1200, 2];

    /// <summary>The first move of A.</summary>
    private static readonly int[] MoveA = [0, 100];

    /// <summary>The horizontal line of A.</summary>
    private static readonly int[] HorizontalA = [200];

    /// <summary>The vertical line of A.</summary>
    private static readonly int[] VerticalA = [100];

    /// <summary>The curve of A.</summary>
    private static readonly int[] CurveA = [-50, 50, -50, 0, -50, -50];

    /// <summary>The move to the start of A's flex contour.</summary>
    private static readonly int[] FlexStartMove = [200, -200];

    /// <summary>The seven relative flex points: the reference point and the six curve points.</summary>
    private static readonly int[] FlexMoves = [50, 10, -40, -10, 20, 10, 20, 0, 20, 0, 20, -10, 10, 0];

    /// <summary>The operands that end the flex: depth, end point and subroutine 0.</summary>
    private static readonly int[] FlexEnd = [50, 400, 0, 0];

    /// <summary>The last line of A.</summary>
    private static readonly int[] LineA = [0, 100];

    /// <summary>The hsbw of .notdef and B.</summary>
    private static readonly int[] MetricsPlain = [0, 500];

    /// <summary>The square sides of B.</summary>
    private static readonly int[] SideB = [100];

    /// <summary>The hsbw of acute.</summary>
    private static readonly int[] MetricsAcute = [20, 300];

    /// <summary>The move of acute.</summary>
    private static readonly int[] MoveAcute = [0, 600];

    /// <summary>The line of acute.</summary>
    private static readonly int[] LineAcute = [50, 100];

    /// <summary>The hsbw of Aacute.</summary>
    private static readonly int[] MetricsAacute = [50, 600];

    /// <summary>The seac operands: accent side bearing, offset, base code and accent code.</summary>
    private static readonly int[] SeacArguments = [20, 100, 50, CodeA, 0xC2];

    /// <summary>The flex-end subroutine: OtherSubr 0 with three arguments, then the two results become the current point.</summary>
    private static readonly int[] FlexEndCall = [3, 0];

    /// <summary>The flex-start subroutine: OtherSubr 1 with no arguments.</summary>
    private static readonly int[] FlexStartCall = [0, 1];

    /// <summary>The flex-point subroutine: OtherSubr 2 with no arguments.</summary>
    private static readonly int[] FlexPointCall = [0, 2];

    /// <summary>The commands A draws.</summary>
    private static readonly string[] OutlineA =
    [
        "M 50 100",
        "L 250 100",
        "L 250 200",
        "C 200 250 150 250 100 200",
        "Z",
        "M 300 0",
        "C 310 0 330 10 350 10",
        "C 370 10 390 0 400 0",
        "L 400 100",
        "Z",
    ];

    /// <summary>The commands B draws.</summary>
    private static readonly string[] OutlineB = ["M 0 0", "L 100 0", "L 100 100", "Z"];

    /// <summary>The commands the acute accent draws inside Aacute.</summary>
    private static readonly string[] OutlineAccent = ["M 150 650", "L 200 750", "Z"];

    /// <summary>The binary font parses, with its matrix, box, encoding and names.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FontParses()
    {
        var program = Parse(BuildBinaryFont());

        await Assert.That(program.GlyphCount).IsEqualTo(GlyphTotal);
        await Assert.That(program.FontMatrix.A).IsEqualTo(MatrixScale);
        await Assert.That(program.BoundingBox.Top).IsEqualTo(BoxTop);
        await Assert.That(program.GetGlyphByCharCode(CodeA)).IsEqualTo(GlyphA);
        await Assert.That(program.GetGlyphByCharCode(CodeB)).IsEqualTo(GlyphB);
        await Assert.That(program.GetGlyphByCharCode(0)).IsEqualTo(-1);
        await Assert.That(Encoding.ASCII.GetString(program.GetGlyphName(GlyphAacute))).IsEqualTo("Aacute");
        await Assert.That(program.GetGlyphByUnicode('B')).IsEqualTo(GlyphB);
        await Assert.That(program.GetAdvanceWidth(GlyphA)).IsEqualTo(WidthA);
    }

    /// <summary>Lines, curves, div, closepath and flex through OtherSubrs decode.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OutlineDecodes()
    {
        var program = Parse(BuildBinaryFont());

        await Assert.That(Decode(program, GlyphA)).IsEquivalentTo(OutlineA);
        await Assert.That(Decode(program, GlyphB)).IsEquivalentTo(OutlineB);
    }

    /// <summary>The seac operator draws the base and places the accent by its side bearing and offset.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SeacComposes()
    {
        var program = Parse(BuildBinaryFont());

        await Assert.That(Decode(program, GlyphAacute)).IsEquivalentTo([.. OutlineA, .. OutlineAccent]);
    }

    /// <summary>The PFA hexadecimal form and the PFB segment form parse to the same font.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HexAndPfbFormsParse()
    {
        var hex = Parse(BuildHexFont());
        var pfb = Parse(BuildPfbFont());

        await Assert.That(Decode(hex, GlyphA)).IsEquivalentTo(OutlineA);
        await Assert.That(Decode(pfb, GlyphA)).IsEquivalentTo(OutlineA);
    }

    /// <summary>Data without an eexec part is rejected.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingEexecIsRejected() =>
        await Assert.That(Type1Program.TryParse(Cleartext.AsSpan(0, Cleartext.Length - "currentfile eexec\n"u8.Length), out _)).IsFalse();

    /// <summary>Oversized charstring lengths are rejected before overflow can reach a span slice.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsMaximumCharstringLength()
    {
        byte[] secret = [.. new byte[RandomPrefix], .. "/lenIV -1 def /CharStrings 1 dict dup begin /.notdef 2147483647 RD x ND end"u8];
        byte[] font = [.. Cleartext, .. Encrypt(secret, EexecKey)];
        await Assert.That(() => Type1Program.TryParse(font, out _)).Throws<PdfException>();
    }

    /// <summary>Negative charstring lengths are rejected as an unreadable font.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsNegativeCharstringLength()
    {
        byte[] secret = [.. new byte[RandomPrefix], .. "/lenIV -1 def /CharStrings 1 dict dup begin /.notdef -1 RD x ND end"u8];
        byte[] font = [.. Cleartext, .. Encrypt(secret, EexecKey)];
        await Assert.That(() => Type1Program.TryParse(font, out _)).Throws<PdfException>();
    }

    /// <summary>Parses a font.</summary>
    /// <param name="data">The font bytes.</param>
    /// <returns>The program.</returns>
    /// <exception cref="InvalidOperationException">The font did not parse.</exception>
    private static Type1Program Parse(byte[] data) =>
        Type1Program.TryParse(data, out var program) ? program : throw new InvalidOperationException("The test font did not parse.");

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

    /// <summary>Builds the font with a binary eexec part, as a PDF FontFile holds it.</summary>
    /// <returns>The font bytes.</returns>
    private static byte[] BuildBinaryFont() => [.. Cleartext, .. EncryptedPrivate()];

    /// <summary>Builds the font with a hexadecimal eexec part, as a PFA file holds it.</summary>
    /// <returns>The font bytes.</returns>
    private static byte[] BuildHexFont()
    {
        var hex = Convert.ToHexString(EncryptedPrivate());
        var lines = new StringBuilder();
        for (var i = 0; i < hex.Length; i += HexLine)
        {
            _ = lines.Append(hex.AsSpan(i, Math.Min(HexLine, hex.Length - i))).Append('\n');
        }

        return [.. Cleartext, .. Encoding.ASCII.GetBytes(lines.ToString())];
    }

    /// <summary>Builds the font as PFB segments.</summary>
    /// <returns>The font bytes.</returns>
    private static byte[] BuildPfbFont() =>
        [.. Segment(PfbAscii, Cleartext), .. Segment(PfbBinary, EncryptedPrivate()), PfbMarker, PfbEnd];

    /// <summary>Builds one PFB segment.</summary>
    /// <param name="type">The segment type.</param>
    /// <param name="body">The segment data.</param>
    /// <returns>The segment.</returns>
    private static byte[] Segment(byte type, byte[] body)
    {
        var length = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(length, (uint)body.Length);
        return [PfbMarker, type, .. length, .. body];
    }

    /// <summary>Builds and eexec-encrypts the private part.</summary>
    /// <returns>The encrypted bytes.</returns>
    private static byte[] EncryptedPrivate()
    {
        var text = new List<byte>("dup /Private 8 dict dup begin\n/lenIV 4 def\n/Subrs 3 array\n"u8.ToArray());
        AddSubr(text, 0, Type1().Numbers(FlexEndCall).EscapeOp(CallOtherSubr).EscapeOp(Pop).EscapeOp(Pop).EscapeOp(SetCurrentPoint).Op(Return));
        AddSubr(text, 1, Type1().Numbers(FlexStartCall).EscapeOp(CallOtherSubr).Op(Return));
        AddSubr(text, FlexPointSubr, Type1().Numbers(FlexPointCall).EscapeOp(CallOtherSubr).Op(Return));
        text.AddRange("2 index /CharStrings 5 dict dup begin\n"u8);
        AddGlyph(text, ".notdef", Type1().Numbers(MetricsPlain).Op(Hsbw).Op(EndChar));
        AddGlyph(text, "A", CharstringA());
        AddGlyph(text, "B", Type1().Numbers(MetricsPlain).Op(Hsbw).Numbers(0, 0).Op(RMoveTo).Numbers(SideB).Op(HLineTo).Numbers(SideB).Op(VLineTo).Op(ClosePath).Op(EndChar));
        AddGlyph(text, "acute", Type1().Numbers(MetricsAcute).Op(Hsbw).Numbers(MoveAcute).Op(RMoveTo).Numbers(LineAcute).Op(RLineTo).Op(ClosePath).Op(EndChar));
        AddGlyph(text, "Aacute", Type1().Numbers(MetricsAacute).Op(Hsbw).Numbers(SeacArguments).EscapeOp(Seac));
        text.AddRange("end\nend\nreadonly put\nnoaccess put\nmark currentfile closefile\n"u8);
        return Encrypt([.. new byte[RandomPrefix], .. text], EexecKey);
    }

    /// <summary>Starts a Type 1 charstring.</summary>
    /// <returns>The writer.</returns>
    private static CharstringWriter Type1() => new(true);

    /// <summary>Assembles the charstring of A.</summary>
    /// <returns>The writer.</returns>
    private static CharstringWriter CharstringA()
    {
        var writer = Type1()
            .Numbers(MetricsA).EscapeOp(Div).Op(Hsbw)
            .Numbers(MoveA).Op(RMoveTo)
            .Numbers(HorizontalA).Op(HLineTo)
            .Numbers(VerticalA).Op(VLineTo)
            .Numbers(CurveA).Op(RRCurveTo)
            .Op(ClosePath)
            .Numbers(FlexStartMove).Op(RMoveTo)
            .Numbers(1).Op(CallSubr);
        for (var i = 0; i < FlexMoves.Length; i += PointValues)
        {
            _ = writer.Numbers(FlexMoves.AsSpan(i, PointValues)).Op(RMoveTo).Numbers(FlexPointSubr).Op(CallSubr);
        }

        return writer.Numbers(FlexEnd).Op(CallSubr).Numbers(LineA).Op(RLineTo).Op(ClosePath).Op(EndChar);
    }

    /// <summary>Appends a subroutine entry.</summary>
    /// <param name="text">The private part.</param>
    /// <param name="index">The subroutine number.</param>
    /// <param name="charstring">The subroutine.</param>
    private static void AddSubr(List<byte> text, int index, CharstringWriter charstring)
    {
        var encrypted = EncryptCharstring(charstring);
        text.AddRange(Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture, $"dup {index} {encrypted.Length} RD ")));
        text.AddRange(encrypted);
        text.AddRange(" NP\n"u8);
    }

    /// <summary>Appends a CharStrings entry.</summary>
    /// <param name="text">The private part.</param>
    /// <param name="name">The glyph name.</param>
    /// <param name="charstring">The charstring.</param>
    private static void AddGlyph(List<byte> text, string name, CharstringWriter charstring)
    {
        var encrypted = EncryptCharstring(charstring);
        text.AddRange(Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture, $"/{name} {encrypted.Length} RD ")));
        text.AddRange(encrypted);
        text.AddRange(" ND\n"u8);
    }

    /// <summary>Encrypts a charstring with its four random bytes.</summary>
    /// <param name="charstring">The charstring.</param>
    /// <returns>The encrypted bytes.</returns>
    private static byte[] EncryptCharstring(CharstringWriter charstring) =>
        Encrypt([.. new byte[RandomPrefix], .. charstring.ToArray()], CharstringKey);

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
