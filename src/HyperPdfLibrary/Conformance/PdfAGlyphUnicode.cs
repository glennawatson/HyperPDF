// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Finds the Unicode text veraPDF gives a shown code for the PDF/A ToUnicode rules: /ToUnicode first; then, for a
/// simple font, the Adobe Glyph List text of its glyph name, or a space for a Symbol or ZapfDingbats name, which means
/// "do not check". PDF/A-1 reads Type 1 names as present-or-not and Type 3 fonts by /ToUnicode only.
/// </summary>
internal static class PdfAGlyphUnicode
{
    /// <summary>The text that tells veraPDF's ToUnicode rules not to check a glyph.</summary>
    private const string Unchecked = " ";

    /// <summary>The PDF/A part based on PDF 1.4.</summary>
    private const int Part1 = 1;

    /// <summary>The orderings whose Adobe UCS2 CMaps veraPDF falls back on for composite fonts.</summary>
    private static readonly string[] AdobeOrderings = ["Japan1", "CNS1", "GB1", "Korea1", "KR"];

    /// <summary>Gets the text of a code.</summary>
    /// <param name="font">The font.</param>
    /// <param name="code">The code.</param>
    /// <param name="part">The PDF/A part.</param>
    /// <returns>The text, or null when veraPDF finds none.</returns>
    internal static string? Text(PdfAGlyphFont font, int code, int part) => font.ToUnicode.Text(code) ?? font.Use.Kind switch
    {
        PdfAFontKind.Type0 => CompositeFallback(font),
        PdfAFontKind.Type3 when part == Part1 => null,
        PdfAFontKind.Type1 when part == Part1 => Type1Pdf1(font, code),
        _ => FromName(SimpleName(font, code)),
    };

    /// <summary>Gets the glyph name a simple font's ToUnicode lookup uses: the encoding's, else the program's.</summary>
    /// <param name="font">The font.</param>
    /// <param name="code">The code.</param>
    /// <returns>The name, or null.</returns>
    private static string? SimpleName(PdfAGlyphFont font, int code) => font.Encoding.Name(code) ?? font.Use.Kind switch
    {
        PdfAFontKind.TrueType when font.Program.Present => PdfATrueTypeGlyphs.ProgramName(font, code),
        PdfAFontKind.Type1 when font.Program.Present => ProgramName(font.Program, code),
        _ => null,
    };

    /// <summary>Gets a PDF/A-1 Type 1 font's text: a space when its glyph name is listed, else none.</summary>
    /// <param name="font">The font.</param>
    /// <param name="code">The code.</param>
    /// <returns>A space or null.</returns>
    private static string? Type1Pdf1(PdfAGlyphFont font, int code)
    {
        var name = font.Encoding.Name(code) ?? (font.Program.Present ? ProgramName(font.Program, code) : null);
        return name is not null && (PdfAGlyphNames.IsListed(name) || IsSymbolName(name)) ? Unchecked : null;
    }

    /// <summary>Gets the text a glyph name gives.</summary>
    /// <param name="name">The name, or null.</param>
    /// <returns>The Adobe Glyph List text, a space for Symbol and ZapfDingbats names, or null.</returns>
    private static string? FromName(string? name)
    {
        if (name is null)
        {
            return null;
        }

        return PdfAGlyphNames.Text(name) ?? (IsSymbolName(name) || IsDingbatName(name) ? Unchecked : null);
    }

    /// <summary>
    /// Gets a composite font's text without /ToUnicode. veraPDF then reads the Adobe UCS2 CMap of an Adobe collection;
    /// the library does not hold those CMaps, so such a font counts as mapped.
    /// </summary>
    /// <param name="font">The font.</param>
    /// <returns>A space for an Adobe collection, else null.</returns>
    private static string? CompositeFallback(PdfAGlyphFont font)
    {
        var info = PdfACharacterCollection.FromDictionary(font.Primary.Get(KnownName.CIDSystemInfo));
        return info.Registry == PdfAPredefinedCMaps.Registry && Array.IndexOf(AdobeOrderings, info.Ordering) >= 0 ? Unchecked : null;
    }

    /// <summary>Gets the name a Type 1 or CFF program's built-in encoding gives a code.</summary>
    /// <param name="program">The program.</param>
    /// <param name="code">The code.</param>
    /// <returns>The name, or null.</returns>
    private static string? ProgramName(PdfAFontProgram program, int code)
    {
        if (program.Type1 is { } type1)
        {
            var glyph = type1.GetGlyphByCharCode(code);
            return glyph < 0 ? null : Encoding.Latin1.GetString(type1.GetGlyphName(glyph));
        }

        if (program.Cff is { } cff && !cff.IsCidKeyed)
        {
            var glyph = cff.GetGlyphByCharCode(code);
            return glyph < 0 ? PdfAEncodingTables.Notdef : Encoding.Latin1.GetString(cff.GetGlyphName(glyph));
        }

        return null;
    }

    /// <summary>Determines whether a name is a glyph of the Symbol font.</summary>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsSymbolName(string name) => FontEncodings.GetCode(FontEncoding.Symbol, Encoding.Latin1.GetBytes(name)) >= 0;

    /// <summary>Determines whether a name is a glyph of the ZapfDingbats font.</summary>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsDingbatName(string name) => GlyphList.TryGetDingbatsCodePoint(Encoding.Latin1.GetBytes(name), out _);
}
