// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Data;

namespace HyperPdfLibrary.Fonts;

/// <summary>
/// Turns the private-use code points that symbol fonts such as Word's Symbol map their codes to (U+F020 to U+F0FF)
/// into the Unicode characters those codes mean in the Symbol and ZapfDingbats encodings, so extracted text is readable.
/// </summary>
internal static class PuaSymbols
{
    /// <summary>The first code point of the symbol page.</summary>
    private const int PageStart = 0xF020;

    /// <summary>The last code point of the symbol page.</summary>
    private const int PageEnd = 0xF0FF;

    /// <summary>The mask that keeps the code within the page.</summary>
    private const int CodeMask = 0xFF;

    /// <summary>Finds the font kind a name belongs to.</summary>
    /// <param name="baseFont">The /BaseFont name.</param>
    /// <param name="standard">The standard font the name means.</param>
    /// <returns>The remapping for the font.</returns>
    internal static PuaRemap KindOf(string baseFont, StandardFont standard)
    {
        if (standard == StandardFont.Symbol || baseFont.Contains("Symbol", StringComparison.OrdinalIgnoreCase))
        {
            return PuaRemap.Symbol;
        }

        return standard == StandardFont.ZapfDingbats || baseFont.Contains("Dingbats", StringComparison.OrdinalIgnoreCase)
            ? PuaRemap.Dingbats
            : PuaRemap.None;
    }

    /// <summary>Replaces a single private-use character with the Unicode character its code means.</summary>
    /// <param name="resources">The selected font resources.</param>
    /// <param name="remap">The font's remapping.</param>
    /// <param name="text">The text, which holds <paramref name="length"/> characters.</param>
    /// <param name="length">The number of characters written.</param>
    /// <returns>The new number of characters.</returns>
    internal static int Apply(PdfFontResources resources, PuaRemap remap, Span<char> text, int length)
    {
        if (remap == PuaRemap.None || length != 1 || text[0] < PageStart || text[0] > PageEnd)
        {
            return length;
        }

        var encoding = remap == PuaRemap.Symbol ? FontEncoding.Symbol : FontEncoding.ZapfDingbats;
        var mapped = GlyphNameText.TextOf(resources, FontEncodings.GetGlyphName(encoding, text[0] & CodeMask), remap == PuaRemap.Dingbats);
        return mapped is not null && mapped.AsSpan().TryCopyTo(text) ? mapped.Length : length;
    }
}
