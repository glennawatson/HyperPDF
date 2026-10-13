// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.Tests.Text;

/// <summary>Checks the generated Unicode tables and the character rules built on them.</summary>
public sealed class TextUnicodeDataTests
{
    /// <summary>The Unicode version the generator is pinned to.</summary>
    private const string PinnedVersion = "18.0.0";

    /// <summary>The number of letters in the Arabic ligature lam alef.</summary>
    private const int LamAlefLength = 2;

    /// <summary>The longest buffer a decomposition needs.</summary>
    private const int BufferLength = TextUnicode.MaxDecomposition;

    /// <summary>Latin, Hebrew and Arabic letters, digits and punctuation have the Bidi_Class the standard gives them.</summary>
    /// <param name="value">The character.</param>
    /// <param name="expected">The class.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments('a', (int)TextBidiClass.L)]
    [Arguments('א', (int)TextBidiClass.R)]
    [Arguments('ا', (int)TextBidiClass.AL)]
    [Arguments('7', (int)TextBidiClass.EN)]
    [Arguments('٧', (int)TextBidiClass.AN)]
    [Arguments('+', (int)TextBidiClass.ES)]
    [Arguments('%', (int)TextBidiClass.ET)]
    [Arguments(',', (int)TextBidiClass.CS)]
    [Arguments('(', (int)TextBidiClass.ON)]
    [Arguments(' ', (int)TextBidiClass.WS)]
    [Arguments('́', (int)TextBidiClass.NSM)]
    [Arguments('‏', (int)TextBidiClass.R)]
    public async Task BidiClassMatchesTheStandard(char value, int expected) =>
        await Assert.That((int)TextUnicode.GetClass(value)).IsEqualTo(expected);

    /// <summary>Brackets and comparison signs have mirrored counterparts, and letters have none.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MirroredCharactersAreFound()
    {
        await Assert.That(TextUnicode.GetMirror('(')).IsEqualTo(')');
        await Assert.That(TextUnicode.GetMirror('<')).IsEqualTo('>');
        await Assert.That(TextUnicode.GetMirror('»')).IsEqualTo('«');
        await Assert.That(TextUnicode.GetMirror('a')).IsEqualTo('a');
    }

    /// <summary>Ligatures and Arabic contextual forms decompose into their letters.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PresentationFormsDecompose()
    {
        var buffer = new char[BufferLength];

        await Assert.That(TextUnicode.Decompose('ﬃ', buffer)).IsEqualTo("ffi".Length);
        await Assert.That(new string(buffer, 0, "ffi".Length)).IsEqualTo("ffi");
        await Assert.That(TextUnicode.Decompose('ﻼ', buffer)).IsEqualTo(LamAlefLength);
        await Assert.That(buffer[0]).IsEqualTo('ل');
        await Assert.That(TextUnicode.Decompose('a', buffer)).IsEqualTo(0);
    }

    /// <summary>Precomposed letters map to their base letter.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BaseLettersAreFound()
    {
        await Assert.That(TextUnicode.GetBaseLetter('é')).IsEqualTo('e');
        await Assert.That(TextUnicode.GetBaseLetter('Å')).IsEqualTo('A');
        await Assert.That(TextUnicode.GetBaseLetter('ấ')).IsEqualTo('a');
        await Assert.That(TextUnicode.GetBaseLetter('x')).IsEqualTo('x');
    }

    /// <summary>Control codes, noncharacters and the byte order mark carry no text; tabs and line breaks do.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HiddenCharactersAreControlsAndNoncharacters()
    {
        await Assert.That(TextUnicode.IsHidden('\u0001')).IsTrue();
        await Assert.That(TextUnicode.IsHidden('￾')).IsTrue();
        await Assert.That(TextUnicode.IsHidden('﻿')).IsTrue();
        await Assert.That(TextUnicode.IsHidden('\t')).IsFalse();
        await Assert.That(TextUnicode.IsHidden('\n')).IsFalse();
        await Assert.That(TextUnicode.IsHidden('a')).IsFalse();
    }

    /// <summary>Han, kana and fullwidth forms are written without spaces; Latin and Hangul are not.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CjkScriptsNeedNoSpaces()
    {
        await Assert.That(TextUnicode.IsCjk('日')).IsTrue();
        await Assert.That(TextUnicode.IsCjk('あ')).IsTrue();
        await Assert.That(TextUnicode.IsCjk('Ａ')).IsTrue();
        await Assert.That(TextUnicode.IsCjk('A')).IsFalse();
        await Assert.That(TextUnicode.IsCjk('가')).IsFalse();
    }

    /// <summary>The generated file records the pinned version.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TablesRecordTheirUnicodeVersion() =>
        await Assert.That(TextUnicodeData.UnicodeVersion).IsEqualTo(PinnedVersion);
}
