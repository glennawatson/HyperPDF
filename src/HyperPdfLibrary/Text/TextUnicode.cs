// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace HyperPdfLibrary.Text;

/// <summary>The character properties text extraction and search need, from the generated Unicode tables and the runtime's character data.</summary>
internal static class TextUnicode
{
    /// <summary>The longest decomposition in the generated presentation-form table.</summary>
    internal const int MaxDecomposition = 20;

    /// <summary>The Unicode noncharacter U+FFFE.</summary>
    private const char NoncharacterFffe = (char)0xFFFE;

    /// <summary>The Unicode noncharacter U+FFFF.</summary>
    private const char NoncharacterFfff = (char)0xFFFF;

    /// <summary>The zero width no-break space, which also serves as a byte order mark.</summary>
    private const char ByteOrderMark = (char)0xFEFF;

    /// <summary>The first CJK Symbols and Punctuation character; the Han, kana and fullwidth blocks that follow need no spaces.</summary>
    private const char CjkSymbolsStart = (char)0x3000;

    /// <summary>The last character of the kana blocks.</summary>
    private const char KanaEnd = (char)0x30FF;

    /// <summary>The first character of CJK Unified Ideographs Extension A.</summary>
    private const char HanExtensionStart = (char)0x3400;

    /// <summary>The last character of CJK Unified Ideographs.</summary>
    private const char HanEnd = (char)0x9FFF;

    /// <summary>The first CJK compatibility ideograph.</summary>
    private const char HanCompatibilityStart = (char)0xF900;

    /// <summary>The last CJK compatibility ideograph.</summary>
    private const char HanCompatibilityEnd = (char)0xFAFF;

    /// <summary>The first fullwidth form.</summary>
    private const char FullwidthStart = (char)0xFF00;

    /// <summary>The last halfwidth or fullwidth form.</summary>
    private const char FullwidthEnd = (char)0xFFEF;

    /// <summary>Gets a character's Bidi_Class. Surrogates count as left-to-right letters.</summary>
    /// <param name="value">The character.</param>
    /// <returns>The class.</returns>
    internal static TextBidiClass GetClass(char value)
    {
        var index = TextUnicodeData.BidiStarts.BinarySearch((ushort)value);
        if (index < 0)
        {
            index = ~index - 1;
        }

        return (TextBidiClass)TextUnicodeData.BidiClasses[index];
    }

    /// <summary>Gets a character's mirrored counterpart (Bidi_Mirroring_Glyph).</summary>
    /// <param name="value">The character.</param>
    /// <returns>The mirrored character, or the character itself when it has none.</returns>
    internal static char GetMirror(char value)
    {
        var index = TextUnicodeData.MirrorKeys.BinarySearch((ushort)value);
        return index < 0 ? value : (char)TextUnicodeData.MirrorValues[index];
    }

    /// <summary>Writes the decomposition of a presentation form, such as a ligature or an Arabic contextual form.</summary>
    /// <param name="value">The character.</param>
    /// <param name="destination">The buffer, at least <see cref="MaxDecomposition"/> characters.</param>
    /// <returns>The characters written; 0 when the character is not a presentation form.</returns>
    internal static int Decompose(char value, Span<char> destination)
    {
        var index = TextUnicodeData.PresentationKeys.BinarySearch((ushort)value);
        if (index < 0)
        {
            return 0;
        }

        var offsets = TextUnicodeData.PresentationOffsets;
        var values = TextUnicodeData.PresentationValues[offsets[index]..offsets[index + 1]];
        for (var i = 0; i < values.Length; i++)
        {
            destination[i] = (char)values[i];
        }

        return values.Length;
    }

    /// <summary>Determines whether a character is a combining mark that a search can ignore.</summary>
    /// <param name="value">The character.</param>
    /// <returns><see langword="true"/> for non-spacing marks.</returns>
    internal static bool IsCombiningMark(char value) => CharUnicodeInfo.GetUnicodeCategory(value) == UnicodeCategory.NonSpacingMark;

    /// <summary>Determines whether a character is a mark of any kind: non-spacing, spacing combining or enclosing. A mark belongs to the letter before it.</summary>
    /// <param name="value">The character.</param>
    /// <returns><see langword="true"/> for marks.</returns>
    internal static bool IsMark(char value) => CharUnicodeInfo.GetUnicodeCategory(value) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;

    /// <summary>Determines whether a character is written without spaces between words: Han, kana, CJK punctuation and fullwidth forms.</summary>
    /// <param name="value">The character.</param>
    /// <returns><see langword="true"/> for those scripts.</returns>
    internal static bool IsCjk(char value) =>
        value is >= CjkSymbolsStart and <= KanaEnd
        or >= HanExtensionStart and <= HanEnd
        or >= HanCompatibilityStart and <= HanCompatibilityEnd
        or >= FullwidthStart and <= FullwidthEnd;

    /// <summary>Determines whether a character is kept out of the page text: control codes other than tab and line breaks, the byte order mark and noncharacters.</summary>
    /// <param name="value">The character.</param>
    /// <returns><see langword="true"/> when the character carries no text.</returns>
    internal static bool IsHidden(char value) =>
        value is NoncharacterFffe or NoncharacterFfff or ByteOrderMark
        || (char.IsControl(value) && value is not ('\t' or '\n' or '\r'));

    /// <summary>Determines whether a character is part of a word for whole-word search.</summary>
    /// <param name="value">The character.</param>
    /// <returns><see langword="true"/> for letters, digits, underscore and combining marks.</returns>
    internal static bool IsWordChar(char value) =>
        char.IsLetterOrDigit(value) || value == '_' || CharUnicodeInfo.GetUnicodeCategory(value) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark;
}
