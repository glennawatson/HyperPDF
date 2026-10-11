// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.CMaps;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Fonts;

/// <summary>
/// Maps the codes of a TrueType font with an embedded program to glyphs, following PDFium's
/// CPDF_TrueTypeFont::LoadGlyphMap: non-symbolic fonts look glyph names up through the (3,1) Unicode cmap, then the
/// (1,0) Macintosh cmap, then the (3,0) symbol cmap; symbolic fonts use the (3,0) cmap with the F000 page, then (1,0),
/// then the Unicode cmap, then the code as the glyph id.
/// </summary>
internal static class TrueTypeGlyphMapper
{
    /// <summary>The first glyph PDFium assigns when a font has glyph names but no cmap.</summary>
    private const int FirstCharGlyphBase = 3;

    /// <summary>The cmap a non-symbolic lookup goes through.</summary>
    private enum CharmapType
    {
        /// <summary>No usable cmap.</summary>
        Other = 0,

        /// <summary>The Unicode cmap.</summary>
        Unicode = 1,

        /// <summary>The Macintosh Roman cmap.</summary>
        MacRoman = 2,

        /// <summary>The Microsoft symbol cmap.</summary>
        Symbol = 3,
    }

    /// <summary>Maps every code.</summary>
    /// <param name="resources">The selected font resources.</param>
    /// <param name="program">The embedded TrueType program.</param>
    /// <param name="encoding">The font's encoding.</param>
    /// <param name="flags">The descriptor flags.</param>
    /// <param name="toUnicode">The /ToUnicode map, or <see langword="null"/>.</param>
    /// <param name="firstChar">The /FirstChar.</param>
    /// <returns>The map.</returns>
    internal static SimpleGlyphMap Map(PdfFontResources resources, TrueTypeProgram program, SimpleEncoding encoding, FontFlags flags, ToUnicodeMap? toUnicode, int firstChar)
    {
        var map = new SimpleGlyphMap();
        var baseEncoding = DetermineEncoding(program, encoding.Base, flags);
        var effective = baseEncoding == encoding.Base ? encoding : encoding with { Base = baseEncoding };
        var simpleBase = baseEncoding is FontEncoding.WinAnsi or FontEncoding.MacRoman && encoding.Differences is null;
        if (simpleBase || (flags & FontFlags.Nonsymbolic) != 0)
        {
            if (program.HasGlyphNames && !program.HasAnyCmap)
            {
                MapFromFirstChar(map, firstChar);
                return map;
            }

            MapNonsymbolic(resources, program, effective, flags, toUnicode, map);
            return map;
        }

        MapSymbolic(resources, program, effective, map);
        return map;
    }

    /// <summary>Gives each code from /FirstChar a glyph counting from 3, as PDFium does for fonts with names but no cmap.</summary>
    /// <param name="map">The map.</param>
    /// <param name="firstChar">The first code.</param>
    private static void MapFromFirstChar(SimpleGlyphMap map, int firstChar)
    {
        if ((uint)firstChar >= FontEncodings.CodeCount)
        {
            return;
        }

        map.Glyphs.AsSpan(0, firstChar).Clear();
        for (var code = firstChar; code < FontEncodings.CodeCount; code++)
        {
            map.Glyphs[code] = code - firstChar + FirstCharGlyphBase;
        }
    }

    /// <summary>Corrects WinAnsi or MacRoman on a symbolic font to an encoding its cmaps support.</summary>
    /// <param name="program">The program.</param>
    /// <param name="baseEncoding">The base encoding.</param>
    /// <param name="flags">The flags.</param>
    /// <returns>The encoding to use.</returns>
    private static FontEncoding DetermineEncoding(TrueTypeProgram program, FontEncoding baseEncoding, FontFlags flags)
    {
        if ((flags & FontFlags.Symbolic) == 0 || baseEncoding is not (FontEncoding.WinAnsi or FontEncoding.MacRoman) || !program.HasAnyCmap)
        {
            return baseEncoding;
        }

        var supportsWindows = program.HasUnicodeCmap || program.HasSymbolCmap;
        return baseEncoding == FontEncoding.WinAnsi
            ? Supported(supportsWindows, program.HasMacCmap, FontEncoding.WinAnsi, FontEncoding.MacRoman)
            : Supported(program.HasMacCmap, supportsWindows, FontEncoding.MacRoman, FontEncoding.WinAnsi);
    }

    /// <summary>Keeps an encoding the font's cmaps support, else switches to the other one, else the built-in encoding.</summary>
    /// <param name="supported">Whether the cmaps support the encoding.</param>
    /// <param name="otherSupported">Whether the cmaps support the other encoding.</param>
    /// <param name="encoding">The encoding.</param>
    /// <param name="other">The other encoding.</param>
    /// <returns>The encoding to use.</returns>
    private static FontEncoding Supported(bool supported, bool otherSupported, FontEncoding encoding, FontEncoding other)
    {
        if (supported)
        {
            return encoding;
        }

        return otherSupported ? other : FontEncoding.None;
    }

    /// <summary>Chooses the cmap for non-symbolic lookups.</summary>
    /// <param name="program">The program.</param>
    /// <param name="flags">The flags.</param>
    /// <returns>The cmap type.</returns>
    private static CharmapType DetermineCharmap(TrueTypeProgram program, FontFlags flags)
    {
        if (program.HasUnicodeCmap)
        {
            return CharmapType.Unicode;
        }

        if (program.HasMacCmap && ((flags & FontFlags.Nonsymbolic) != 0 || !program.HasSymbolCmap))
        {
            return CharmapType.MacRoman;
        }

        return program.HasSymbolCmap ? CharmapType.Symbol : CharmapType.Other;
    }

    /// <summary>Looks a code up in the chosen cmap.</summary>
    /// <param name="program">The program.</param>
    /// <param name="type">The cmap type.</param>
    /// <param name="code">The code.</param>
    /// <returns>The glyph, or zero.</returns>
    private static int Lookup(TrueTypeProgram program, CharmapType type, int code) => type switch
    {
        CharmapType.Unicode => program.LookupUnicodeCmap(code),
        CharmapType.MacRoman => program.LookupMacCmap(code),
        CharmapType.Symbol => program.LookupSymbolCmap(code),
        _ => 0,
    };

    /// <summary>Maps codes by glyph name through the chosen cmap.</summary>
    /// <param name="resources">The selected font resources.</param>
    /// <param name="program">The program.</param>
    /// <param name="encoding">The encoding.</param>
    /// <param name="flags">The flags.</param>
    /// <param name="toUnicode">The /ToUnicode map, or <see langword="null"/>.</param>
    /// <param name="map">The map.</param>
    private static void MapNonsymbolic(PdfFontResources resources, TrueTypeProgram program, SimpleEncoding encoding, FontFlags flags, ToUnicodeMap? toUnicode, SimpleGlyphMap map)
    {
        var type = DetermineCharmap(program, flags);
        for (var code = 0; code < FontEncodings.CodeCount; code++)
        {
            var name = encoding.GetName(code);
            if (name.IsEmpty)
            {
                map.Glyphs[code] = Lookup(program, type, code);
                continue;
            }

            var text = GlyphNameText.TextOf(resources, name, false);
            map.Texts[code] = text;
            var glyph = type == CharmapType.Symbol ? program.LookupSymbolCmap(code) : LookupByName(program, type, name, GlyphNameText.FirstCodePoint(text));
            if (glyph == 0)
            {
                glyph = FallbackByName(program, name, toUnicode, map, code);
            }

            map.Glyphs[code] = glyph;
        }
    }

    /// <summary>Finds a named code's glyph through the cmap.</summary>
    /// <param name="program">The program.</param>
    /// <param name="type">The cmap type.</param>
    /// <param name="name">The glyph name.</param>
    /// <param name="codePoint">The name's code point, or zero.</param>
    /// <returns>The glyph, or zero.</returns>
    private static int LookupByName(TrueTypeProgram program, CharmapType type, ReadOnlySpan<byte> name, int codePoint)
    {
        if (codePoint == 0)
        {
            return 0;
        }

        if (type == CharmapType.Unicode)
        {
            return program.LookupUnicodeCmap(codePoint);
        }

        if (type != CharmapType.MacRoman)
        {
            return 0;
        }

        var macCode = MacRomanCode(name, codePoint);
        return macCode > 0 ? program.LookupMacCmap(macCode) : program.LookupPostName(name);
    }

    /// <summary>Finds the Mac Roman code of a character.</summary>
    /// <param name="name">The glyph name.</param>
    /// <param name="codePoint">The character.</param>
    /// <returns>The code, or zero when Mac Roman lacks it.</returns>
    private static int MacRomanCode(ReadOnlySpan<byte> name, int codePoint)
    {
        var code = GlyphList.TryGetName(codePoint, out var listed) ? FontEncodings.GetCode(FontEncoding.MacRoman, listed) : -1;
        if (code < 0)
        {
            code = FontEncodings.GetCode(FontEncoding.MacRoman, name);
        }

        return Math.Max(code, 0);
    }

    /// <summary>Falls back for a named code: .notdef draws a space; else the 'post' name, then the /ToUnicode text.</summary>
    /// <param name="program">The program.</param>
    /// <param name="name">The glyph name.</param>
    /// <param name="toUnicode">The /ToUnicode map, or <see langword="null"/>.</param>
    /// <param name="map">The map.</param>
    /// <param name="code">The code.</param>
    /// <returns>The glyph, or zero.</returns>
    private static int FallbackByName(TrueTypeProgram program, ReadOnlySpan<byte> name, ToUnicodeMap? toUnicode, SimpleGlyphMap map, int code)
    {
        if (name.SequenceEqual(".notdef"u8))
        {
            return program.LookupUnicodeCmap(' ');
        }

        var glyph = program.LookupPostName(name);
        if (glyph != 0 || toUnicode is null || !toUnicode.TryGetCodePoint(code, out var codePoint))
        {
            return glyph;
        }

        map.Texts[code] = char.ConvertFromUtf32(codePoint);
        return program.LookupUnicodeCmap(codePoint);
    }

    /// <summary>Maps the codes of a symbolic font by code.</summary>
    /// <param name="resources">The selected font resources.</param>
    /// <param name="program">The program.</param>
    /// <param name="encoding">The encoding.</param>
    /// <param name="map">The map.</param>
    private static void MapSymbolic(PdfFontResources resources, TrueTypeProgram program, SimpleEncoding encoding, SimpleGlyphMap map)
    {
        if (program.HasSymbolCmap && MapSymbolCmap(resources, program, encoding, map))
        {
            return;
        }

        if (program.HasMacCmap)
        {
            for (var code = 0; code < FontEncodings.CodeCount; code++)
            {
                map.Glyphs[code] = program.LookupMacCmap(code);
                map.Texts[code] = GlyphNameText.TextOf(resources, FontEncodings.GetGlyphName(FontEncoding.MacRoman, code), false);
            }

            return;
        }

        if (!program.HasUnicodeCmap || !MapUnicodeCmap(program, map))
        {
            for (var code = 0; code < FontEncodings.CodeCount; code++)
            {
                map.Glyphs[code] = code;
            }
        }
    }

    /// <summary>Maps codes through the (3,0) symbol cmap.</summary>
    /// <param name="resources">The selected font resources.</param>
    /// <param name="program">The program.</param>
    /// <param name="encoding">The encoding.</param>
    /// <param name="map">The map.</param>
    /// <returns><see langword="true"/> when some code found a glyph.</returns>
    private static bool MapSymbolCmap(PdfFontResources resources, TrueTypeProgram program, SimpleEncoding encoding, SimpleGlyphMap map)
    {
        for (var code = 0; code < FontEncodings.CodeCount; code++)
        {
            map.Glyphs[code] = program.LookupSymbolCmap(code);
        }

        if (!map.HasAnyGlyph())
        {
            return false;
        }

        var namesFromMac = encoding.Base == FontEncoding.None && program.HasMacCmap;
        for (var code = 0; code < FontEncodings.CodeCount; code++)
        {
            var name = namesFromMac ? FontEncodings.GetGlyphName(FontEncoding.MacRoman, code) : encoding.GetName(code);
            map.Texts[code] = GlyphNameText.TextOf(resources, name, false);
        }

        return true;
    }

    /// <summary>Maps codes as Unicode values through the Unicode cmap, as embedded symbolic fonts are read.</summary>
    /// <param name="program">The program.</param>
    /// <param name="map">The map.</param>
    /// <returns><see langword="true"/> when some code found a glyph.</returns>
    private static bool MapUnicodeCmap(TrueTypeProgram program, SimpleGlyphMap map)
    {
        for (var code = 0; code < FontEncodings.CodeCount; code++)
        {
            map.Glyphs[code] = program.LookupUnicodeCmap(code);
            map.Texts[code] = code == 0 ? null : char.ConvertFromUtf32(code);
        }

        return map.HasAnyGlyph();
    }
}
