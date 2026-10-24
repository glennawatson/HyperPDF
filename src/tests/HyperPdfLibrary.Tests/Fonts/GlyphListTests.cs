// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Fonts.Data;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Tests for the Adobe Glyph List lookups.</summary>
public sealed class GlyphListTests
{
    /// <summary>The space a glyph name's text is decoded into.</summary>
    private const int TextSpace = 16;

    /// <summary>The code point of the letter A.</summary>
    private const int LetterA = 0x41;

    /// <summary>The code point of the euro sign.</summary>
    private const int Euro = 0x20AC;

    /// <summary>The code point of the grinning face emoji, outside the BMP.</summary>
    private const int GrinningFace = 0x1F600;

    /// <summary>The code point of the first ITC Zapf Dingbats glyph, a1.</summary>
    private const int DingbatA1 = 0x2701;

    /// <summary>A known name maps to its code point.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KnownNameMapsToCodePoint()
    {
        var found = GlyphList.TryGetCodePoint("A"u8, out var codePoint);

        await Assert.That(found).IsTrue();
        await Assert.That(codePoint).IsEqualTo(LetterA);
    }

    /// <summary>The uniXXXX form maps to its code point.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UniNameMapsToCodePoint()
    {
        var found = GlyphList.TryGetCodePoint("uni20AC"u8, out var codePoint);

        await Assert.That(found).IsTrue();
        await Assert.That(codePoint).IsEqualTo(Euro);
    }

    /// <summary>The uXXXXX form maps outside the BMP, as a surrogate pair.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UNameMapsOutsideBmp()
    {
        var found = GlyphList.TryGetCodePoint("u1F600"u8, out var codePoint);

        await Assert.That(found).IsTrue();
        await Assert.That(codePoint).IsEqualTo(GrinningFace);
        await Assert.That(Text("u1F600"u8)).IsEqualTo(char.ConvertFromUtf32(GrinningFace));
    }

    /// <summary>An underscore ligature name splits into its components.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LigatureNameSplits()
    {
        await Assert.That(Text("f_f_i"u8)).IsEqualTo("ffi");
        await Assert.That(GlyphList.TryGetCodePoint("f_f_i"u8, out _)).IsFalse();
    }

    /// <summary>A suffix after a period is ignored, and several uni groups give several characters.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SuffixAndUniSequencesWork()
    {
        await Assert.That(Text("a.sc"u8)).IsEqualTo("a");
        await Assert.That(Text("uni00410042"u8)).IsEqualTo("AB");
        await Assert.That(Text(".notdef"u8)).IsEqualTo(string.Empty);
        await Assert.That(Text("uniD800"u8)).IsEqualTo(string.Empty);
    }

    /// <summary>A code point maps back to its preferred name.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CodePointMapsToName()
    {
        await Assert.That(Name(LetterA)).IsEqualTo("A");
        await Assert.That(Name(Euro)).IsEqualTo("Euro");
        await Assert.That(Name(' ')).IsEqualTo("space");
    }

    /// <summary>A Zapf Dingbats glyph name maps through the dingbats list.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DingbatNameMaps()
    {
        var found = GlyphList.TryGetDingbatsCodePoint("a1"u8, out var codePoint);

        await Assert.That(found).IsTrue();
        await Assert.That(codePoint).IsEqualTo(DingbatA1);
    }

    /// <summary>Decodes a glyph name to text.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The text, or empty.</returns>
    private static string Text(ReadOnlySpan<byte> name)
    {
        Span<char> text = stackalloc char[TextSpace];
        return GlyphList.TryGetUnicode(name, text, out var written) ? new string(text[..written]) : string.Empty;
    }

    /// <summary>Finds the preferred name of a code point.</summary>
    /// <param name="codePoint">The code point.</param>
    /// <returns>The name, or empty.</returns>
    private static string Name(int codePoint) =>
        GlyphList.TryGetName(codePoint, out var name) ? Encoding.ASCII.GetString(name) : string.Empty;
}
