// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Fonts;

/// <summary>
/// Maps the codes of a Type 1 font with an embedded program to glyphs, as PDFium's CPDF_Type1Font::LoadGlyphMap does
/// for embedded faces: by glyph name first, then through the program's Unicode names or its built-in encoding.
/// </summary>
internal static class Type1GlyphMapper
{
    /// <summary>Maps every code.</summary>
    /// <param name="resources">The selected font resources.</param>
    /// <param name="program">The embedded program.</param>
    /// <param name="encoding">The font's encoding.</param>
    /// <param name="symbolic">Whether the descriptor flags say symbolic.</param>
    /// <param name="dingbats">Whether glyph names are Zapf Dingbats names.</param>
    /// <returns>The map.</returns>
    internal static SimpleGlyphMap Map(PdfFontResources resources, FontProgram program, SimpleEncoding encoding, bool symbolic, bool dingbats)
    {
        var map = new SimpleGlyphMap();
        for (var code = 0; code < FontEncodings.CodeCount; code++)
        {
            if (symbolic)
            {
                MapSymbolic(resources, program, encoding, dingbats, map, code);
            }
            else
            {
                MapNonsymbolic(resources, program, encoding, dingbats, map, code);
            }
        }

        return map;
    }

    /// <summary>Finds a glyph by name as FreeType's name index does: zero when the name is missing.</summary>
    /// <param name="program">The program.</param>
    /// <param name="name">The glyph name.</param>
    /// <returns>The glyph id, or zero.</returns>
    internal static int NameIndex(FontProgram program, ReadOnlySpan<byte> name) => program is TrueTypeProgram trueType
        ? trueType.LookupPostName(name)
        : Math.Max(program.GetGlyphByName(name), 0);

    /// <summary>Maps a code of a symbolic font: its encoded name, else the program's built-in encoding.</summary>
    /// <param name="resources">The selected font resources.</param>
    /// <param name="program">The program.</param>
    /// <param name="encoding">The encoding.</param>
    /// <param name="dingbats">Whether names are Zapf Dingbats names.</param>
    /// <param name="map">The map.</param>
    /// <param name="code">The code.</param>
    private static void MapSymbolic(PdfFontResources resources, FontProgram program, SimpleEncoding encoding, bool dingbats, SimpleGlyphMap map, int code)
    {
        var name = encoding.GetName(code);
        if (!name.IsEmpty)
        {
            map.Texts[code] = GlyphNameText.TextOf(resources, name, dingbats);
            map.Glyphs[code] = NameIndex(program, name);
            return;
        }

        var glyph = Math.Max(program.GetGlyphByCharCode(code), 0);
        map.Glyphs[code] = glyph;
        if (glyph != 0)
        {
            map.Texts[code] = GlyphNameText.TextOf(resources, program.GetGlyphName(glyph), dingbats);
        }
    }

    /// <summary>Maps a code of a non-symbolic font: its encoded name, else the name's Unicode value.</summary>
    /// <param name="resources">The selected font resources.</param>
    /// <param name="program">The program.</param>
    /// <param name="encoding">The encoding.</param>
    /// <param name="dingbats">Whether names are Zapf Dingbats names.</param>
    /// <param name="map">The map.</param>
    /// <param name="code">The code.</param>
    private static void MapNonsymbolic(PdfFontResources resources, FontProgram program, SimpleEncoding encoding, bool dingbats, SimpleGlyphMap map, int code)
    {
        var name = encoding.GetName(code);
        if (name.IsEmpty)
        {
            return;
        }

        var text = GlyphNameText.TextOf(resources, name, dingbats);
        map.Texts[code] = text;
        var glyph = NameIndex(program, name);
        if (glyph != 0)
        {
            map.Glyphs[code] = glyph;
            return;
        }

        if (name.SequenceEqual(".notdef"u8) || name.SequenceEqual("space"u8))
        {
            map.Texts[code] = " ";
            return;
        }

        var codePoint = GlyphNameText.FirstCodePoint(text);
        map.Glyphs[code] = codePoint != 0 ? Math.Max(program.GetGlyphByUnicode(codePoint), 0) : 0;
    }
}
