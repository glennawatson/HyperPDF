// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Records the text-showing operators of the PDF/A text walk: the font and rendering mode they use and the character
/// codes of their strings. Like veraPDF, it reads the strings of the first operand only, so <c>"</c>, whose first
/// operand is a number, shows no glyphs to the rules.
/// </summary>
internal static class PdfATextShow
{
    /// <summary>The longest string decoded on the stack.</summary>
    private const int StackStringBytes = 256;

    /// <summary>Applies a text-showing operator.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="frame">The stream's state.</param>
    /// <param name="reader">The reader, positioned after the operator.</param>
    /// <param name="op">The operator.</param>
    /// <returns><see langword="true"/> when the operator shows text.</returns>
    internal static bool Apply(PdfATextWalkState state, PdfATextFrame frame, ref ContentReader reader, ContentOperator op)
    {
        if (op is not (ContentOperator.ShowText or ContentOperator.ShowTextArray or ContentOperator.NextLineShowText or ContentOperator.SetSpacingNextLineShowText))
        {
            return false;
        }

        if (frame.Graphics.Font is not { } font)
        {
            return true;
        }

        Use(state, frame, font);
        var operand = reader.Operand(0);
        switch (operand.Kind)
        {
            case ContentOperandKind.LiteralString or ContentOperandKind.HexString:
            {
                ShowEncoded(frame, font, operand.Kind == ContentOperandKind.HexString, reader.Body(operand));
                break;
            }

            case ContentOperandKind.Array:
            {
                ShowArray(frame, font, reader.Body(operand));
                break;
            }

            default:
            {
                break;
            }
        }

        return true;
    }

    /// <summary>Records that a font is shown with the current rendering mode, and walks a Type 3 font's glyph procedures.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="frame">The stream's state.</param>
    /// <param name="font">The font.</param>
    private static void Use(PdfATextWalkState state, PdfATextFrame frame, PdfAFontUse font)
    {
        if (font.Modes == 0)
        {
            state.Used.Add(font);
        }

        font.Modes |= 1 << frame.Graphics.Mode;
        if (font.Kind == PdfAFontKind.Type3)
        {
            WalkGlyphProcedures(state, frame, font);
        }
    }

    /// <summary>Walks every glyph procedure of a Type 3 font with the resources veraPDF gives them.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="frame">The stream's state.</param>
    /// <param name="font">The Type 3 font.</param>
    private static void WalkGlyphProcedures(PdfATextWalkState state, PdfATextFrame frame, PdfAFontUse font)
    {
        if (font.Dictionary.GetDictionary(KnownName.CharProcs) is not { } procedures)
        {
            return;
        }

        var fontResources = font.Dictionary.GetDictionary(KnownName.Resources);
        for (var i = 0; i < procedures.Count; i++)
        {
            if (StoreReading.Resolve(state.Store, procedures.GetValueAt(i)).AsStream() is not { } procedure)
            {
                continue;
            }

            var own = procedure.Dictionary.ContainsKey(KnownName.Resources) ? procedure.Dictionary.GetDictionary(KnownName.Resources) : fontResources;
            PdfATextWalk.WalkStream(state, procedure, own, frame.Graphics);
        }
    }

    /// <summary>Shows the strings of a <c>TJ</c> array.</summary>
    /// <param name="frame">The stream's state.</param>
    /// <param name="font">The font.</param>
    /// <param name="body">The bytes between the brackets.</param>
    private static void ShowArray(PdfATextFrame frame, PdfAFontUse font, ReadOnlySpan<byte> body)
    {
        var lexer = new PdfLexer(body);
        while (lexer.Next() is var kind && kind != PdfTokenKind.EndOfData)
        {
            if (kind is PdfTokenKind.LiteralString or PdfTokenKind.HexString)
            {
                ShowEncoded(frame, font, kind == PdfTokenKind.HexString, lexer.Lexeme);
            }
        }
    }

    /// <summary>Decodes a string's escapes and records its codes.</summary>
    /// <param name="frame">The stream's state.</param>
    /// <param name="font">The font.</param>
    /// <param name="hex">Whether the string is hexadecimal.</param>
    /// <param name="raw">The string's bytes between its delimiters.</param>
    private static void ShowEncoded(PdfATextFrame frame, PdfAFontUse font, bool hex, ReadOnlySpan<byte> raw)
    {
        byte[]? rented = null;
        var buffer = raw.Length <= StackStringBytes ? stackalloc byte[StackStringBytes] : (rented = ArrayPool<byte>.Shared.Rent(raw.Length));
        try
        {
            var length = hex ? PdfStringDecoder.DecodeHex(raw, buffer) : PdfStringDecoder.DecodeLiteral(raw, buffer);
            ShowBytes(frame, font, buffer[..length]);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Records the codes of a decoded string.</summary>
    /// <param name="frame">The stream's state.</param>
    /// <param name="font">The font.</param>
    /// <param name="bytes">The string's bytes.</param>
    private static void ShowBytes(PdfATextFrame frame, PdfAFontUse font, ReadOnlySpan<byte> bytes)
    {
        if (font.Kind == PdfAFontKind.Type0 && font.Codes is null)
        {
            return;
        }

        var use = (frame.Graphics.Mode == PdfATextWalk.InvisibleMode ? PdfAGlyphUse.Invisible : PdfAGlyphUse.Rendered)
            | (frame.HasActualText ? PdfAGlyphUse.None : PdfAGlyphUse.WithoutActualText);
        while (!bytes.IsEmpty)
        {
            int code;
            int used;
            if (font.Codes is { } codes)
            {
                used = Math.Max(1, codes.ReadCode(bytes, out code));
            }
            else
            {
                code = bytes[0];
                used = 1;
            }

            font.Glyphs[code] = font.Glyphs.GetValueOrDefault(code) | use;
            bytes = bytes[used..];
        }
    }
}
