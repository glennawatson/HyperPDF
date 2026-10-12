// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Derives the glyph facts of a Type 3 font as veraPDF does: a glyph is present when /CharProcs has its encoding name,
/// and its program width is the first operand of the glyph procedure's d0 or d1.
/// </summary>
internal static class PdfAType3FontGlyphs
{
    /// <summary>The operands of d1.</summary>
    private const int D1Operands = 6;

    /// <summary>The position of the d0 keyword.</summary>
    private const int D0Position = 2;

    /// <summary>Gets the facts of a code.</summary>
    /// <param name="font">The font.</param>
    /// <param name="code">The code.</param>
    /// <param name="cancellationToken">Cancels decoding.</param>
    /// <returns>The facts.</returns>
    internal static PdfAGlyphFacts Facts(PdfAGlyphFont font, int code, CancellationToken cancellationToken)
    {
        var name = font.Encoding.Name(code);
        var procedure = name is null ? default : Procedure(font, name);
        var width = procedure.AsStream() is { } stream ? Width(stream, cancellationToken) : -1;
        return new(name, !procedure.IsNull, width < 0 ? font.MissingWidth : width, PdfASimpleWidths.Width(font, code, true) ?? 0);
    }

    /// <summary>Finds a glyph procedure by glyph name.</summary>
    /// <param name="font">The font.</param>
    /// <param name="name">The glyph name.</param>
    /// <returns>The resolved procedure, or null.</returns>
    private static PdfValue Procedure(PdfAGlyphFont font, string name)
    {
        if (font.Primary.GetDictionary(KnownName.CharProcs) is not { } procedures)
        {
            return default;
        }

        var spelling = System.Text.Encoding.Latin1.GetBytes(name);
        for (var i = 0; i < procedures.Count; i++)
        {
            if (font.Store.Names.NameEquals(procedures.GetKeyAt(i), spelling))
            {
                return StoreReading.Resolve(font.Store, procedures.GetValueAt(i));
            }
        }

        return default;
    }

    /// <summary>Reads a glyph procedure's width: d0 after two numbers, or d1 after six; anything else has none.</summary>
    /// <param name="procedure">The glyph procedure.</param>
    /// <param name="cancellationToken">Cancels decoding.</param>
    /// <returns>The width, or -1.</returns>
    private static double Width(PdfStream procedure, CancellationToken cancellationToken)
    {
        if (!PdfAFontStreams.TryDecode(procedure, cancellationToken, out var data))
        {
            return -1;
        }

        var lexer = new PdfLexer(data);
        double width = lexer.Next() == PdfTokenKind.Number && PdfNumber.TryParseSingle(lexer.Lexeme, out var value) ? value : -1;
        for (var position = 1; position < D1Operands; position++)
        {
            if (position == D0Position && IsKeyword(ref lexer, "d0"u8))
            {
                return width;
            }

            if (position != D0Position)
            {
                _ = lexer.Next();
            }
        }

        return IsKeyword(ref lexer, "d1"u8) ? width : -1;
    }

    /// <summary>Reads the next token and tests whether it is a keyword.</summary>
    /// <param name="lexer">The lexer.</param>
    /// <param name="keyword">The keyword.</param>
    /// <returns><see langword="true"/> when the token is the keyword.</returns>
    private static bool IsKeyword(ref PdfLexer lexer, ReadOnlySpan<byte> keyword) => lexer.Next() == PdfTokenKind.Keyword && lexer.Lexeme.SequenceEqual(keyword);
}
