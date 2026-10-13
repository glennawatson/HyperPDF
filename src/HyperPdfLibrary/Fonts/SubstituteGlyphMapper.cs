// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.CMaps;
using HyperPdfLibrary.Fonts.Data;

namespace HyperPdfLibrary.Fonts;

/// <summary>
/// Maps the codes of a non-embedded simple font to glyphs of the system font standing in for it. Glyph names go
/// through Unicode (the Adobe Glyph List, ISO 32000-2, 9.10.2). The Symbol and ZapfDingbats fonts look each name up by its
/// code in the font's own encoding, which the URW stand-ins and Microsoft symbol fonts both map directly.
/// </summary>
internal static class SubstituteGlyphMapper
{
    /// <summary>Maps every code.</summary>
    /// <param name="resources">The selected font resources.</param>
    /// <param name="face">The stand-in face.</param>
    /// <param name="encoding">The font's encoding.</param>
    /// <param name="symbolEncoding">The built-in encoding of a Symbol or ZapfDingbats font, else <see cref="FontEncoding.None"/>.</param>
    /// <param name="symbolic">Whether the descriptor flags say symbolic.</param>
    /// <param name="toUnicode">The /ToUnicode map, or <see langword="null"/>.</param>
    /// <returns>The map.</returns>
    internal static SimpleGlyphMap Map(PdfFontResources resources, SubstituteFace face, SimpleEncoding encoding, FontEncoding symbolEncoding, bool symbolic, ToUnicodeMap? toUnicode)
    {
        var map = new SimpleGlyphMap();
        var dingbats = symbolEncoding == FontEncoding.ZapfDingbats;
        for (var code = 0; code < FontEncodings.CodeCount; code++)
        {
            var name = encoding.GetName(code);
            if (name.IsEmpty)
            {
                var glyph = symbolic || symbolEncoding != FontEncoding.None ? face.GetSymbolGlyph(code) : 0;
                map.Glyphs[code] = glyph != 0 ? glyph : SimpleGlyphMap.NoGlyph;
                continue;
            }

            var text = GlyphNameText.TextOf(resources, name, dingbats);
            map.Texts[code] = text;
            map.Glyphs[code] = MapName(face, name, text, symbolEncoding, toUnicode, code);
        }

        return map;
    }

    /// <summary>Finds a glyph by its PostScript name, then through the built-in code of a symbol font.</summary>
    /// <param name="face">The face.</param>
    /// <param name="name">The glyph name.</param>
    /// <param name="symbolEncoding">The built-in encoding of a symbol font, or <see cref="FontEncoding.None"/>.</param>
    /// <returns>The glyph, or zero.</returns>
    private static int FindByName(SubstituteFace face, ReadOnlySpan<byte> name, FontEncoding symbolEncoding)
    {
        // A bundled face keeps PostScript names, so a name finds its glyph exactly, as in an embedded Type 1 font.
        var glyph = face.GetNamedGlyph(name);
        if (glyph != 0 || symbolEncoding == FontEncoding.None || face.IsBundled)
        {
            return glyph;
        }

        var builtIn = FontEncodings.GetCode(symbolEncoding, name);
        return builtIn >= 0 ? face.GetSymbolGlyph(builtIn) : 0;
    }

    /// <summary>Finds a named code's glyph.</summary>
    /// <param name="face">The face.</param>
    /// <param name="name">The glyph name.</param>
    /// <param name="text">The name's text, or <see langword="null"/>.</param>
    /// <param name="symbolEncoding">The built-in encoding of a symbol font, or <see cref="FontEncoding.None"/>.</param>
    /// <param name="toUnicode">The /ToUnicode map, or <see langword="null"/>.</param>
    /// <param name="code">The code.</param>
    /// <returns>The glyph, or <see cref="SimpleGlyphMap.NoGlyph"/>.</returns>
    private static int MapName(SubstituteFace face, ReadOnlySpan<byte> name, string? text, FontEncoding symbolEncoding, ToUnicodeMap? toUnicode, int code)
    {
        var glyph = FindByName(face, name, symbolEncoding);
        if (glyph == 0)
        {
            glyph = face.GetGlyph(GlyphNameText.FirstCodePoint(text));
        }

        if (glyph == 0 && name.SequenceEqual(".notdef"u8))
        {
            glyph = face.GetGlyph(' ');
        }

        if (glyph == 0 && toUnicode is not null && toUnicode.TryGetCodePoint(code, out var codePoint))
        {
            glyph = face.GetGlyph(codePoint);
        }

        return glyph != 0 ? glyph : SimpleGlyphMap.NoGlyph;
    }
}
