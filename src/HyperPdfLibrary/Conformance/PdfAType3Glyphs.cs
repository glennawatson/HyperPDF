// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Follows the Type 3 glyphs a content stream shows into their glyph procedures, for the transparency search. Only the
/// glyphs shown count: a procedure that is never drawn adds no transparency to the page.
/// </summary>
internal static class PdfAType3Glyphs
{
    /// <summary>The operand index of the string shown by <c>"</c>.</summary>
    internal const int SpacingStringOperand = 2;

    /// <summary>The longest string decoded on the stack.</summary>
    private const int StackStringBytes = 256;

    /// <summary>Reads the font a <c>Tf</c> selects, keeping it only when it is a Type 3 font with glyph procedures.</summary>
    /// <param name="font">The font dictionary, or <see langword="null"/>.</param>
    /// <param name="resources">The resources in force, used when the font has none.</param>
    /// <returns>The font, or the default value for any other font.</returns>
    internal static PdfAShownFont Select(PdfDictionary? font, PdfDictionary resources)
    {
        if (font is null || !font.IsName(KnownName.Subtype, KnownName.Type3) || font.GetDictionary(KnownName.CharProcs) is not { } procedures)
        {
            return default;
        }

        var encoding = font.Get(KnownName.Encoding).IsNull
            ? new SimpleEncoding(FontEncoding.None, null)
            : SimpleEncoding.Read(font, new(FontEncoding.None, false, false, false, false));
        return new(procedures, encoding, font.GetDictionary(KnownName.Resources) ?? resources);
    }

    /// <summary>Determines whether the glyphs of a shown string draw with transparency.</summary>
    /// <param name="walk">The search.</param>
    /// <param name="font">The Type 3 font in force.</param>
    /// <param name="reader">The reader, on a text-showing operator.</param>
    /// <param name="index">The index of the string operand.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns><see langword="true"/> when one does.</returns>
    internal static bool StringUses(PdfATransparencyWalk walk, in PdfAShownFont font, ref ContentReader reader, int index, int depth)
    {
        var operand = reader.Operand(index);
        return font.Procedures is not null
            && operand.Kind is ContentOperandKind.LiteralString or ContentOperandKind.HexString
            && EncodedUses(walk, font, operand.Kind == ContentOperandKind.HexString, reader.Body(operand), depth);
    }

    /// <summary>Determines whether the glyphs of the strings in a <c>TJ</c> array draw with transparency.</summary>
    /// <param name="walk">The search.</param>
    /// <param name="font">The Type 3 font in force.</param>
    /// <param name="reader">The reader, on <c>TJ</c>.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns><see langword="true"/> when one does.</returns>
    internal static bool ArrayUses(PdfATransparencyWalk walk, in PdfAShownFont font, ref ContentReader reader, int depth)
    {
        var array = reader.Operand(0);
        if (font.Procedures is null || array.Kind != ContentOperandKind.Array)
        {
            return false;
        }

        var lexer = new PdfLexer(reader.Body(array));
        for (var kind = lexer.Next(); kind != PdfTokenKind.EndOfData; kind = lexer.Next())
        {
            if (kind is PdfTokenKind.LiteralString or PdfTokenKind.HexString && EncodedUses(walk, font, kind == PdfTokenKind.HexString, lexer.Lexeme, depth))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Decodes a string's escapes and checks the glyph of each code.</summary>
    /// <param name="walk">The search.</param>
    /// <param name="font">The Type 3 font in force.</param>
    /// <param name="hex">Whether the string is hexadecimal.</param>
    /// <param name="raw">The string's bytes between its delimiters.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns><see langword="true"/> when a glyph draws with transparency.</returns>
    private static bool EncodedUses(PdfATransparencyWalk walk, in PdfAShownFont font, bool hex, ReadOnlySpan<byte> raw, int depth)
    {
        byte[]? rented = null;
        Span<byte> buffer = raw.Length <= StackStringBytes ? stackalloc byte[StackStringBytes] : (rented = ArrayPool<byte>.Shared.Rent(raw.Length));
        try
        {
            var length = hex ? PdfStringDecoder.DecodeHex(raw, buffer) : PdfStringDecoder.DecodeLiteral(raw, buffer);
            foreach (var code in buffer[..length])
            {
                if (GlyphUses(walk, font, code, depth))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Determines whether the glyph procedure of one code draws with transparency.</summary>
    /// <param name="walk">The search.</param>
    /// <param name="font">The Type 3 font in force.</param>
    /// <param name="code">The character code.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool GlyphUses(PdfATransparencyWalk walk, in PdfAShownFont font, int code, int depth)
    {
        var name = font.Encoding!.Value.GetName(code);
        return !name.IsEmpty
            && font.Procedures!.Get(walk.Names.Intern(name)).AsStream() is { } glyph
            && PdfATransparencyUse.ProcedureUses(walk, glyph, font.Resources, depth);
    }
}
