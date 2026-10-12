// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Derives the glyph facts of a Type 1 font as veraPDF does: by the /Encoding name when it gives one, else by the
/// program's built-in encoding. Type 1 program widths are whole numbers; CFF widths are not rounded.
/// </summary>
internal static class PdfAType1Glyphs
{
    /// <summary>The text units per em.</summary>
    private const float TextUnits = 1000F;

    /// <summary>Gets the facts of a code.</summary>
    /// <param name="font">The font.</param>
    /// <param name="code">The code.</param>
    /// <returns>The facts.</returns>
    internal static PdfAGlyphFacts Facts(PdfAGlyphFont font, int code)
    {
        var name = font.Encoding.Name(code);
        var program = font.Program;
        if (!program.IsEmbedded)
        {
            return new(name, null, null, null);
        }

        var present = code == 0 || (name is not null ? ContainsGlyph(program, name) : ContainsCode(program, code));
        var programWidth = name is not null ? WidthByName(program, name) : WidthByCode(program, code);
        var dictionaryWidth = PdfASimpleWidths.Width(font, code, true) ?? MetricsWidth(font, name) ?? 0;
        return new(name, present, programWidth < 0 ? font.MissingWidth : programWidth, dictionaryWidth);
    }

    /// <summary>Gets a standard 14 font's width for a glyph name, or .notdef's, as veraPDF's metrics do.</summary>
    /// <param name="font">The font.</param>
    /// <param name="name">The glyph name, or null.</param>
    /// <returns>The width, or null without metrics.</returns>
    internal static double? MetricsWidth(PdfAGlyphFont font, string? name)
    {
        if (font.Metrics is not { } metrics)
        {
            return null;
        }

        if (name is not null && metrics.TryGetWidth(Encoding.Latin1.GetBytes(name), out var width))
        {
            return width;
        }

        // veraPDF falls back on the width of .notdef, then zero.
        return metrics.TryGetWidth(".notdef"u8, out var missing) ? missing : 0;
    }

    /// <summary>Gets a CFF glyph's width in text units.</summary>
    /// <param name="program">The program.</param>
    /// <param name="glyph">The glyph.</param>
    /// <returns>The width.</returns>
    internal static double CffWidth(CffProgram program, int glyph)
    {
        var width = program.GetAdvanceWidth(glyph);
        return program.FontMatrix == FontMatrix.Default ? width : width * (program.FontMatrix.A * TextUnits);
    }

    /// <summary>Determines whether the program has a glyph by name; a Type 1 program does not count .notdef.</summary>
    /// <param name="program">The program.</param>
    /// <param name="name">The glyph name.</param>
    /// <returns><see langword="true"/> when it has.</returns>
    private static bool ContainsGlyph(PdfAFontProgram program, string name)
    {
        var bytes = Encoding.Latin1.GetBytes(name);
        return program.Type1 is { } type1
            ? name != PdfAEncodingTables.Notdef && type1.GetGlyphByName(bytes) >= 0
            : program.Cff is { } cff && cff.GetGlyphByName(bytes) >= 0;
    }

    /// <summary>Determines whether the program has a glyph for a code through its built-in encoding.</summary>
    /// <param name="program">The program.</param>
    /// <param name="code">The code.</param>
    /// <returns><see langword="true"/> when it has.</returns>
    private static bool ContainsCode(PdfAFontProgram program, int code)
    {
        if (program.Type1 is { } type1)
        {
            var glyph = type1.GetGlyphByCharCode(code);
            return glyph >= 0 && !type1.GetGlyphName(glyph).SequenceEqual(".notdef"u8);
        }

        // veraPDF maps a code missing from a CFF font's own encoding to glyph 1, so it always finds a glyph there.
        return program.Cff is not null;
    }

    /// <summary>Gets the program's width for a glyph name, or -1.</summary>
    /// <param name="program">The program.</param>
    /// <param name="name">The glyph name.</param>
    /// <returns>The width.</returns>
    private static double WidthByName(PdfAFontProgram program, string name)
    {
        var bytes = Encoding.Latin1.GetBytes(name);
        if (program.Type1 is { } type1)
        {
            var glyph = type1.GetGlyphByName(bytes);
            return glyph < 0 ? -1 : Type1Width(type1, glyph);
        }

        if (program.Cff is not { } cff)
        {
            return -1;
        }

        var cffGlyph = cff.GetGlyphByName(bytes);
        return CffWidth(cff, cffGlyph < 0 || cffGlyph >= cff.GlyphCount ? 0 : cffGlyph);
    }

    /// <summary>Gets the program's width for a code through its built-in encoding, or -1.</summary>
    /// <param name="program">The program.</param>
    /// <param name="code">The code.</param>
    /// <returns>The width.</returns>
    private static double WidthByCode(PdfAFontProgram program, int code)
    {
        if (program.Type1 is { } type1)
        {
            var glyph = type1.GetGlyphByCharCode(code);
            return glyph < 0 ? -1 : Type1Width(type1, glyph);
        }

        if (program.Cff is not { } cff)
        {
            return -1;
        }

        var cffGlyph = cff.GetGlyphByCharCode(code);
        return CffWidth(cff, cffGlyph < 0 || cffGlyph >= cff.GlyphCount ? 0 : cffGlyph);
    }

    /// <summary>Gets a Type 1 glyph's width in text units, truncated to a whole number as veraPDF stores it.</summary>
    /// <param name="program">The program.</param>
    /// <param name="glyph">The glyph.</param>
    /// <returns>The width.</returns>
    private static double Type1Width(Type1Program program, int glyph)
    {
        var width = program.GetAdvanceWidth(glyph);
        return program.FontMatrix == FontMatrix.Default ? Math.Truncate(width) : Math.Truncate(width * (program.FontMatrix.A * TextUnits));
    }
}
