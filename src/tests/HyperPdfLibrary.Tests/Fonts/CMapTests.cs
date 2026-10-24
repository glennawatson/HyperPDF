// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Fonts.CMaps;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Tests for embedded CMaps and ToUnicode CMaps.</summary>
public sealed class CMapTests
{
    /// <summary>The code of a single bfchar entry.</summary>
    private const int CodeSpace = 0x03;

    /// <summary>The first code of a string bfrange.</summary>
    private const int RangeStart = 0x10;

    /// <summary>The third code of a string bfrange.</summary>
    private const int RangeThird = 0x12;

    /// <summary>The second code of an array bfrange.</summary>
    private const int ArraySecond = 0x21;

    /// <summary>The code mapped to a surrogate pair.</summary>
    private const int CodeEmoji = 0x30;

    /// <summary>The code after <see cref="CodeEmoji"/>, inside the same range.</summary>
    private const int CodeEmojiNext = 0x31;

    /// <summary>The code mapped to a ligature.</summary>
    private const int CodeLigature = 0x40;

    /// <summary>The code mapped through a glyph name.</summary>
    private const int CodeNamed = 0x41;

    /// <summary>A code inside a large range.</summary>
    private const int LargeRangeCode = 0x8000;

    /// <summary>The code point a large range maps <see cref="LargeRangeCode"/> to.</summary>
    private const int LargeRangeCodePoint = 0x4E00 + 0x7F00;

    /// <summary>The grinning face code point.</summary>
    private const int GrinningFace = 0x1F600;

    /// <summary>The code point after <see cref="GrinningFace"/>.</summary>
    private const int GrinningFaceWithBigEyes = 0x1F601;

    /// <summary>The space a code's text is decoded into.</summary>
    private const int TextSpace = 8;

    /// <summary>A one-byte code in the mixed codespace sample.</summary>
    private const int SingleByteCode = 0x41;

    /// <summary>A two-byte code in the mixed codespace sample.</summary>
    private const int DoubleByteCode = 0x8145;

    /// <summary>The CID of <see cref="SingleByteCode"/>.</summary>
    private const int SingleByteCid = 34;

    /// <summary>The CID of <see cref="DoubleByteCode"/>: 633 plus the offset of 5 in its range.</summary>
    private const int DoubleByteCid = 638;

    /// <summary>The CID notdef ranges give.</summary>
    private const int NotdefCid = 1;

    /// <summary>A code covered only by a notdef range.</summary>
    private const int NotdefCode = 0x9000;

    /// <summary>The bytes one code uses in a two-byte codespace.</summary>
    private const int TwoBytes = 2;

    /// <summary>The CID an Identity CMap gives the bytes 01 02.</summary>
    private const int IdentityCid = 0x0102;

    /// <summary>The size of a large identity-like range: every two-byte code.</summary>
    private const int LargeRangeCount = 1;

    /// <summary>The CID the usecmap sample overrides code 5 with.</summary>
    private const int OverriddenCid = 99;

    /// <summary>The code the usecmap sample overrides.</summary>
    private const int OverriddenCode = 5;

    /// <summary>A code the usecmap sample leaves to its base.</summary>
    private const int BaseCode = 0x1234;

    /// <summary>Gets a ToUnicode CMap with every kind of destination.</summary>
    private static ReadOnlySpan<byte> ToUnicodeSample => """
        /CIDInit /ProcSet findresource begin
        12 dict begin
        begincmap
        /CMapName /Adobe-Identity-UCS def
        1 begincodespacerange
        <00> <FF>
        endcodespacerange
        3 beginbfchar
        <03> <0020>
        <40> <006600660069>
        <41> /Euro
        endbfchar
        4 beginbfrange
        <10> <1F> <0041>
        <20> <22> [<0058> <0059> <005A>]
        <30> <31> <D83DDE00>
        <8000> <8000> <CDEF>
        endbfrange
        endcmap
        """u8;

    /// <summary>Gets a CMap with one- and two-byte codespaces, CID and notdef ranges.</summary>
    private static ReadOnlySpan<byte> MixedCMapSample => """
        %!PS-Adobe-3.0 Resource-CMap
        /CMapName /Test-H def
        /WMode 1 def
        2 begincodespacerange
        <00> <80>
        <8140> <9FFC>
        endcodespacerange
        1 begincidchar
        <41> 34
        endcidchar
        1 begincidrange
        <8140> <817E> 633
        endcidrange
        1 beginnotdefrange
        <9000> <9FFC> 1
        endnotdefrange
        endcmap
        """u8;

    /// <summary>Gets a ToUnicode CMap with one range covering every two-byte code.</summary>
    private static ReadOnlySpan<byte> LargeRangeSample => """
        1 begincodespacerange <0000> <FFFF> endcodespacerange
        1 beginbfrange <0100> <FFFF> <4E00> endbfrange
        """u8;

    /// <summary>Gets a CMap based on Identity-H through usecmap.</summary>
    private static ReadOnlySpan<byte> UseCMapSample => """
        /Identity-H usecmap
        1 begincidchar <0005> 99 endcidchar
        """u8;

    /// <summary>The bfchar, string bfrange, array bfrange and name forms all map.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ToUnicodeFormsMap()
    {
        var map = ToUnicodeMap.Parse(ToUnicodeSample);

        await Assert.That(Text(map, CodeSpace)).IsEqualTo(" ");
        await Assert.That(Text(map, RangeStart)).IsEqualTo("A");
        await Assert.That(Text(map, RangeThird)).IsEqualTo("C");
        await Assert.That(Text(map, ArraySecond)).IsEqualTo("Y");
        await Assert.That(Text(map, CodeLigature)).IsEqualTo("ffi");
        await Assert.That(Text(map, CodeNamed)).IsEqualTo("€");
        await Assert.That(Text(map, LargeRangeCode)).IsEqualTo("췯");
        await Assert.That(Text(map, 0)).IsEqualTo(string.Empty);
    }

    /// <summary>A surrogate pair destination maps, and a range moves on by whole code points.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SurrogatePairsMap()
    {
        var map = ToUnicodeMap.Parse(ToUnicodeSample);

        await Assert.That(Text(map, CodeEmoji)).IsEqualTo(char.ConvertFromUtf32(GrinningFace));
        await Assert.That(Text(map, CodeEmojiNext)).IsEqualTo(char.ConvertFromUtf32(GrinningFaceWithBigEyes));
        await Assert.That(map.TryGetCodePoint(CodeEmoji, out var codePoint)).IsTrue();
        await Assert.That(codePoint).IsEqualTo(GrinningFace);
        await Assert.That(map.TryGetCodePoint(CodeLigature, out _)).IsFalse();
    }

    /// <summary>A range over every two-byte code is stored as one range.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LargeRangeStaysCompact()
    {
        var map = ToUnicodeMap.Parse(LargeRangeSample);

        await Assert.That(map.Count).IsEqualTo(LargeRangeCount);
        await Assert.That(map.TryGetCodePoint(LargeRangeCode, out var codePoint)).IsTrue();
        await Assert.That(codePoint).IsEqualTo(LargeRangeCodePoint);
    }

    /// <summary>Codes are read by the codespace ranges, mixing one- and two-byte codes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MixedCodespacesReadCodes()
    {
        var cmap = CMap.Parse(MixedCMapSample);
        var codes = ReadAll(cmap, [0x41, 0x81, 0x45, 0x90, 0x00]);

        await Assert.That(codes).IsEquivalentTo([SingleByteCode, DoubleByteCode, NotdefCode]);
        await Assert.That(cmap.ToCid(SingleByteCode)).IsEqualTo(SingleByteCid);
        await Assert.That(cmap.ToCid(DoubleByteCode)).IsEqualTo(DoubleByteCid);
        await Assert.That(cmap.ToCid(NotdefCode)).IsEqualTo(NotdefCid);
        await Assert.That(cmap.ToCid(0)).IsEqualTo(0);
        await Assert.That(cmap.IsVertical).IsTrue();
    }

    /// <summary>The identity CMaps read two-byte codes that are their own CIDs.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IdentityReadsTwoBytes()
    {
        var used = CMap.IdentityH.ReadCode([0x01, 0x02, 0x03], out var code);

        await Assert.That(used).IsEqualTo(TwoBytes);
        await Assert.That(CMap.IdentityH.ToCid(code)).IsEqualTo(IdentityCid);
        await Assert.That(CMap.IdentityV.IsVertical).IsTrue();
        await Assert.That(CMap.GetPredefined("Identity-H"u8)).IsSameReferenceAs(CMap.IdentityH);
    }

    /// <summary>A CMap without codespaces falls back to the length the caller gives.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingCodespaceFallsBack()
    {
        var cmap = CMap.Parse("1 begincidchar <41> 7 endcidchar"u8);

        await Assert.That(cmap.ReadCode([0x41, 0x42], 1, out _)).IsEqualTo(1);
        await Assert.That(cmap.ReadCode([0x41, 0x42], out _)).IsEqualTo(TwoBytes);
    }

    /// <summary>A usecmap base gives its codespaces and mappings, and local mappings win.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UseCMapInheritsBase()
    {
        var cmap = CMap.Parse(UseCMapSample);

        await Assert.That(cmap.ToCid(OverriddenCode)).IsEqualTo(OverriddenCid);
        await Assert.That(cmap.ToCid(BaseCode)).IsEqualTo(BaseCode);
        await Assert.That(cmap.ReadCode([0x12, 0x34], out var code)).IsEqualTo(TwoBytes);
        await Assert.That(code).IsEqualTo(BaseCode);
    }

    /// <summary>A usecmap of an unknown name asks the resolver.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UseCMapAsksResolver()
    {
        var requested = string.Empty;
        var cmap = CMap.Parse(
            "/Custom-H usecmap"u8,
            name =>
            {
                requested = Encoding.ASCII.GetString(name);
                return CMap.IdentityH;
            });

        await Assert.That(requested).IsEqualTo("Custom-H");
        await Assert.That(cmap.ToCid(BaseCode)).IsEqualTo(BaseCode);
    }

    /// <summary>Gets a code's text.</summary>
    /// <param name="map">The map.</param>
    /// <param name="code">The code.</param>
    /// <returns>The text, or empty.</returns>
    private static string Text(ToUnicodeMap map, int code)
    {
        Span<char> text = stackalloc char[TextSpace];
        return map.TryGetUnicode(code, text, out var written) ? new string(text[..written]) : string.Empty;
    }

    /// <summary>Reads every code in some bytes.</summary>
    /// <param name="cmap">The CMap.</param>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The codes.</returns>
    private static List<int> ReadAll(CMap cmap, byte[] bytes)
    {
        var codes = new List<int>();
        var position = 0;
        while (position < bytes.Length)
        {
            position += cmap.ReadCode(bytes.AsSpan(position), out var code);
            codes.Add(code);
        }

        return codes;
    }
}
