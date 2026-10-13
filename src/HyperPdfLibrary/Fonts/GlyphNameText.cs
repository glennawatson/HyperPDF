// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Fonts.Data;

namespace HyperPdfLibrary.Fonts;

/// <summary>Maps glyph names to Unicode through the Adobe Glyph List, its uniXXXX forms and the Zapf Dingbats list.</summary>
internal static class GlyphNameText
{
    /// <summary>The longest text one glyph name maps to.</summary>
    private const int MaxText = 8;

    /// <summary>Gets the text a glyph name stands for.</summary>
    /// <param name="resources">The selected font resources.</param>
    /// <param name="name">The glyph name.</param>
    /// <param name="dingbats">Whether to try the Zapf Dingbats names first.</param>
    /// <returns>The text, or <see langword="null"/> when the name means nothing.</returns>
    internal static string? TextOf(PdfFontResources resources, ReadOnlySpan<byte> name, bool dingbats)
    {
        if (name.IsEmpty)
        {
            return null;
        }

        if (dingbats && GlyphList.TryGetDingbatsCodePoint(name, out var dingbat))
        {
            return char.ConvertFromUtf32(dingbat);
        }

        Span<char> text = stackalloc char[MaxText];
        if (!GlyphList.TryGetUnicode(name, text, out var written))
        {
            return null;
        }

        return written == 1 ? SingleCharacter(resources.GlyphNames, text[0]) : new string(text[..written]);
    }

    /// <summary>Gets the first code point of a glyph name's text, for fallback lookups that take one character.</summary>
    /// <param name="text">The text, or <see langword="null"/>.</param>
    /// <returns>The code point, or zero.</returns>
    internal static int FirstCodePoint(string? text) =>
        text is { Length: > 0 } && Rune.DecodeFromUtf16(text, out var rune, out _) == System.Buffers.OperationStatus.Done ? rune.Value : 0;

    /// <summary>Gets a one-character string, shared for the cached blocks.</summary>
    /// <param name="state">The selected glyph-name cache.</param>
    /// <param name="value">The character.</param>
    /// <returns>The string.</returns>
    private static string SingleCharacter(GlyphNameTextState state, char value)
    {
        if (value < GlyphNameTextState.LatinEnd)
        {
            return Shared(state.LatinTexts, value, value);
        }

        return value is >= (char)GlyphNameTextState.PunctuationStart and < (char)GlyphNameTextState.PunctuationEnd
            ? Shared(state.PunctuationTexts, value - GlyphNameTextState.PunctuationStart, value)
            : value.ToString();
    }

    /// <summary>Gets a cached one-character string, creating and publishing it on first use.</summary>
    /// <param name="texts">The block's cache.</param>
    /// <param name="slot">The character's slot in the block.</param>
    /// <param name="value">The character.</param>
    /// <returns>The string.</returns>
    private static string Shared(string?[] texts, int slot, char value)
    {
        if (Volatile.Read(ref texts[slot]) is { } cached)
        {
            return cached;
        }

        // Two threads may both create the string; either copy is equal, so the last write is harmless.
        var created = value.ToString();
        Volatile.Write(ref texts[slot], created);
        return created;
    }
}
