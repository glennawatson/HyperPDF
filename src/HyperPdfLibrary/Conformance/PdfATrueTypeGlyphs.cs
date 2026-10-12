// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Derives the glyph facts of a TrueType font as veraPDF does. A non-symbolic font with /Encoding looks glyphs up by name
/// through the (3, 1) and (1, 0) 'cmap' subtables, then the 'post' names; otherwise codes go through the (3, 0)
/// subtable on the code page of its first code, then (1, 0), then any subtable.
/// </summary>
internal static class PdfATrueTypeGlyphs
{
    /// <summary>The Microsoft platform.</summary>
    private const int Microsoft = 3;

    /// <summary>The Macintosh platform.</summary>
    private const int Macintosh = 1;

    /// <summary>The Microsoft Unicode BMP encoding.</summary>
    private const int UnicodeBmp = 1;

    /// <summary>The high byte mask of a (3, 0) code page.</summary>
    private const int CodePageMask = 0xFF00;

    /// <summary>The symbol code pages veraPDF accepts in a (3, 0) subtable.</summary>
    private static readonly int[] SymbolPages = [0x0000, 0xF000, 0xF100, 0xF200];

    /// <summary>Gets the facts of a code.</summary>
    /// <param name="font">The font.</param>
    /// <param name="code">The code.</param>
    /// <returns>The facts.</returns>
    internal static PdfAGlyphFacts Facts(PdfAGlyphFont font, int code)
    {
        var tables = font.Program.IsEmbedded ? font.Program.TrueType : null;
        var name = Name(font, tables, code);
        if (tables is null)
        {
            return new(name, null, null, null);
        }

        var programWidth = Width(font, tables, code);
        return new(
            name,
            code == 0 || ContainsCode(font, tables, code),
            programWidth < 0 ? font.MissingWidth : programWidth,
            PdfASimpleWidths.Width(font, code, false) ?? 0);
    }

    /// <summary>Gets the glyph name the program gives a code.</summary>
    /// <param name="font">The font.</param>
    /// <param name="code">The code.</param>
    /// <returns>A name, a space for symbolic fonts and fonts without /Encoding, or null.</returns>
    internal static string? ProgramName(PdfAGlyphFont font, int code)
    {
        if (!font.Symbolic && HasEncoding(font) && font.TrueTypeNames is { } names && code < names.Length)
        {
            return names[code];
        }

        return font.Symbolic || !HasEncoding(font) ? " " : null;
    }

    /// <summary>
    /// Gets a code's glyph name: the encoding's, ignored for symbolic fonts; else the program's, or .notdef when the program
    /// lacks the glyph; without a program, .notdef for code 0.
    /// </summary>
    /// <param name="font">The font.</param>
    /// <param name="tables">The parsed tables, or null.</param>
    /// <param name="code">The code.</param>
    /// <returns>The name, or null.</returns>
    private static string? Name(PdfAGlyphFont font, PdfATrueTypeTables? tables, int code)
    {
        if ((font.Symbolic ? null : font.Encoding.Name(code)) is { } name)
        {
            return name;
        }

        if (!font.Program.Present)
        {
            return code == 0 ? PdfAEncodingTables.Notdef : null;
        }

        return ProgramName(font, code) ?? (tables is not null && ContainsCode(font, tables, code) ? null : PdfAEncodingTables.Notdef);
    }

    /// <summary>Determines whether the program has a glyph for a code.</summary>
    /// <param name="font">The font.</param>
    /// <param name="tables">The program's tables.</param>
    /// <param name="code">The code.</param>
    /// <returns><see langword="true"/> when it has.</returns>
    private static bool ContainsCode(PdfAGlyphFont font, PdfATrueTypeTables tables, int code)
    {
        if (!font.Symbolic && HasEncoding(font))
        {
            var name = NameOf(font, code);
            if (name == PdfAEncodingTables.Notdef)
            {
                return false;
            }

            var glyph = GlyphByName(tables, name);
            return glyph > 0 && glyph < tables.GlyphCount;
        }

        return tables.Cmap(Microsoft, 0) is { } symbol
            ? SymbolCode(symbol, code) is { } symbolCode && symbol.Map.ContainsKey(symbolCode) && symbol.Glyph(symbolCode) != 0
            : tables.Cmap(Macintosh, 0) is { } roman && roman.Map.ContainsKey(code) && roman.Glyph(code) != 0;
    }

    /// <summary>Gets the program's width for a code, or -1 when it has none.</summary>
    /// <param name="font">The font.</param>
    /// <param name="tables">The program's tables.</param>
    /// <param name="code">The code.</param>
    /// <returns>The width.</returns>
    private static double Width(PdfAGlyphFont font, PdfATrueTypeTables tables, int code)
    {
        if (font.Symbolic || !HasEncoding(font))
        {
            return SymbolicWidth(tables, code);
        }

        var name = NameOf(font, code);
        var glyph = GlyphByName(tables, name);
        if (tables.Cmaps.Length != 0 || tables.PostGlyphs.ContainsKey(name))
        {
            return tables.Width(glyph);
        }

        return tables.HasCmapTable ? tables.Width(AnyGlyph(tables, code)) : 0;
    }

    /// <summary>Gets the width of a code read through the 'cmap' subtables directly.</summary>
    /// <param name="tables">The program's tables.</param>
    /// <param name="code">The code.</param>
    /// <returns>The width.</returns>
    private static double SymbolicWidth(PdfATrueTypeTables tables, int code)
    {
        var symbol = tables.Cmap(Microsoft, 0);
        var glyph = symbol is not null && SymbolCode(symbol, code) is { } symbolCode ? symbol.Glyph(symbolCode) : 0;
        if (glyph != 0)
        {
            return tables.Width(glyph);
        }

        if (tables.Cmap(Macintosh, 0) is { } roman)
        {
            return tables.Width(roman.Glyph(code));
        }

        return tables.HasCmapTable ? tables.Width(AnyGlyph(tables, code)) : 0;
    }

    /// <summary>Finds a glyph by name: through Unicode in (3, 1), then Mac OS Roman in (1, 0), then the 'post' names.</summary>
    /// <param name="tables">The program's tables.</param>
    /// <param name="name">The glyph name.</param>
    /// <returns>The glyph, or 0.</returns>
    private static int GlyphByName(PdfATrueTypeTables tables, string name)
    {
        var glyph = 0;
        if (tables.Cmap(Microsoft, UnicodeBmp) is { } unicode)
        {
            glyph = unicode.Glyph(PdfAGlyphNames.FirstCodePoint(name));
        }

        if (glyph == 0 && tables.Cmap(Macintosh, 0) is { } roman && PdfAEncodingTables.MacOsRomanCode(name) is >= 0 and var romanCode)
        {
            glyph = roman.Glyph(romanCode);
        }

        return glyph != 0 ? glyph : tables.PostGlyphs.GetValueOrDefault(name);
    }

    /// <summary>Gets the glyph of a code from the first subtable that maps it.</summary>
    /// <param name="tables">The program's tables.</param>
    /// <param name="code">The code.</param>
    /// <returns>The glyph, or 0.</returns>
    private static int AnyGlyph(PdfATrueTypeTables tables, int code)
    {
        foreach (var cmap in tables.Cmaps)
        {
            if (cmap.Map.TryGetValue(code, out var glyph))
            {
                return glyph;
            }
        }

        return 0;
    }

    /// <summary>Moves a one-byte code onto the code page of a (3, 0) subtable, read from its first code.</summary>
    /// <param name="symbol">The subtable.</param>
    /// <param name="code">The code.</param>
    /// <returns>The code in the subtable, or null when its first code is not on a symbol page veraPDF accepts.</returns>
    private static int? SymbolCode(PdfATrueTypeCmap symbol, int code)
    {
        var page = symbol.SampleCode & CodePageMask;
        return Array.IndexOf(SymbolPages, page) >= 0 ? page + code : null;
    }

    /// <summary>Gets the name the program's encoding table gives a code.</summary>
    /// <param name="font">The font.</param>
    /// <param name="code">The code.</param>
    /// <returns>The name.</returns>
    private static string NameOf(PdfAGlyphFont font, int code) =>
        font.TrueTypeNames is { } names && code < names.Length ? names[code] : PdfAEncodingTables.Notdef;

    /// <summary>Determines whether the font dictionary has an /Encoding entry.</summary>
    /// <param name="font">The font.</param>
    /// <returns><see langword="true"/> when it has.</returns>
    private static bool HasEncoding(PdfAGlyphFont font) => !font.Primary.Get(Objects.KnownName.Encoding).IsNull;
}
