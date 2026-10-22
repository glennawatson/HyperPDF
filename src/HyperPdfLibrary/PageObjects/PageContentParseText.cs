// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.PageObjects;
using HyperPdfLibrary.Syntax;
using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.PageObjects;

/// <summary>Tracks text state and collects text-showing operators.</summary>
internal static class PageContentParseText
{
    /// <summary>The string length decoded on the stack; longer strings use a pooled array.</summary>
    internal const int StackStringBytes = 256;

    /// <summary>The percentage that is a horizontal scaling of 1.</summary>
    internal const float PercentScale = 100;

    /// <summary>The thousandths of text space a TJ adjustment is given in.</summary>
    internal const float AdjustmentUnits = 1000;

    /// <summary>The highest text render mode.</summary>
    internal const int MaxRenderMode = 7;

    /// <summary>The operand index of the string in the <c>"</c> operator.</summary>
    internal const int SpacingStringOperand = 2;

    /// <summary>The characters one code's text can take; matches <see cref = "PdfFont.MaxUnicodeLength"/>.</summary>
    internal const int UnicodeCapacity = 8;

    /// <summary>Handles <c>BT</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpBeginText(PageContentParseState self, ref ContentReader reader)
    {
        self.Tm = Matrix3x2.Identity;
        self.Tlm = Matrix3x2.Identity;
        self.Position = Vector2.Zero;
    }

    /// <summary>Handles <c>Tc</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpSetCharacterSpacing(PageContentParseState self, ref ContentReader reader) => self.State.CharacterSpacing = reader.Number(0);

    /// <summary>Handles <c>Tw</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpSetWordSpacing(PageContentParseState self, ref ContentReader reader) => self.State.WordSpacing = reader.Number(0);

    /// <summary>Handles <c>Tz</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpSetHorizontalScaling(PageContentParseState self, ref ContentReader reader) => self.State.HorizontalScaling = reader.Number(0) / PageContentParseText.PercentScale;

    /// <summary>Handles <c>TL</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpSetLeading(PageContentParseState self, ref ContentReader reader) => self.State.Leading = reader.Number(0);

    /// <summary>Handles <c>Tf</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpSetFont(PageContentParseState self, ref ContentReader reader)
    {
        var name = reader.Operand(0).Name;
        var value = PageContentParseObjects.FindResource(self, KnownName.Font, name);
        self.State.Font = value.AsDictionary() is { } dictionary ? self.Cache.Fonts.Get(dictionary) : null;
        self.State.FontName = name;
        self.State.FontSize = reader.Number(1);
    }

    /// <summary>Handles <c>Tr</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpSetRenderMode(PageContentParseState self, ref ContentReader reader) => self.State.RenderMode = Math.Clamp((int)reader.Number(0), 0, PageContentParseText.MaxRenderMode);

    /// <summary>Handles <c>Ts</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpSetRise(PageContentParseState self, ref ContentReader reader) => self.State.Rise = reader.Number(0);

    /// <summary>Handles <c>Td</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpMoveText(PageContentParseState self, ref ContentReader reader) => PageContentParseText.MoveText(self, reader.Number(0), reader.Number(1));

    /// <summary>Handles <c>TD</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpMoveTextSetLeading(PageContentParseState self, ref ContentReader reader)
    {
        self.State.Leading = -reader.Number(1);
        PageContentParseText.MoveText(self, reader.Number(0), reader.Number(1));
    }

    /// <summary>Handles <c>Tm</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpSetTextMatrix(PageContentParseState self, ref ContentReader reader)
    {
        self.Tlm = new(
            reader.Number(0),
            reader.Number(1),
            reader.Number(PageContentParse.ThirdOperand),
            reader.Number(PageContentParse.FourthOperand),
            reader.Number(PageContentParse.FifthOperand),
            reader.Number(PageContentParse.SixthOperand));
        self.Tm = self.Tlm;
        self.Position = Vector2.Zero;
    }

    /// <summary>Handles <c>T*</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpNextLine(PageContentParseState self, ref ContentReader reader) => PageContentParseText.MoveText(self, 0, -self.State.Leading);

    /// <summary>Handles <c>Tj</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpShowText(PageContentParseState self, ref ContentReader reader) => PageContentParseText.Show(self, ContentOperator.ShowText, ref reader, 0);

    /// <summary>Handles <c>TJ</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpShowTextArray(PageContentParseState self, ref ContentReader reader) => PageContentParseText.Show(self, ContentOperator.ShowTextArray, ref reader, 0);

    /// <summary>Handles <c>'</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpNextLineShowText(PageContentParseState self, ref ContentReader reader)
    {
        PageContentParseText.MoveText(self, 0, -self.State.Leading);
        PageContentParseText.Show(self, ContentOperator.NextLineShowText, ref reader, 0);
    }

    /// <summary>Handles <c>"</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpSpacingNextLineShowText(PageContentParseState self, ref ContentReader reader)
    {
        self.State.WordSpacing = reader.Number(0);
        self.State.CharacterSpacing = reader.Number(1);
        PageContentParseText.MoveText(self, 0, -self.State.Leading);
        PageContentParseText.Show(self, ContentOperator.SetSpacingNextLineShowText, ref reader, PageContentParseText.SpacingStringOperand);
    }

    /// <summary>Gets the bounds of every glyph.</summary>
    /// <param name = "glyphs">The glyphs.</param>
    /// <returns>The union of their boxes; empty when there are none.</returns>
    internal static PdfRectangle UnionBoxes(PdfTextGlyph[] glyphs)
    {
        if (glyphs.Length == 0)
        {
            return default;
        }

        var bounds = glyphs[0].Box;
        for (var i = 1; i < glyphs.Length; i++)
        {
            bounds = bounds.Union(glyphs[i].Box);
        }

        return bounds;
    }

    /// <summary>Moves to the start of a new line.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "x">The x offset.</param>
    /// <param name = "y">The y offset.</param>
    internal static void MoveText(PageContentParseState state, float x, float y)
    {
        state.Tlm = Matrix3x2.CreateTranslation(x, y) * state.Tlm;
        state.Tm = state.Tlm;
        state.Position = Vector2.Zero;
    }

    /// <summary>Reads one text-showing operator into a text object.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "op">The operator.</param>
    /// <param name = "reader">The reader.</param>
    /// <param name = "operandIndex">The operand holding the string or array.</param>
    internal static void Show(PageContentParseState state, ContentOperator op, ref ContentReader reader, int operandIndex)
    {
        var operand = reader.Operand(operandIndex);
        var isArray = operand.Kind == ContentOperandKind.Array;
        if (!isArray && operand.Kind is not (ContentOperandKind.LiteralString or ContentOperandKind.HexString))
        {
            return;
        }

        state.Glyphs.Clear();
        state.CodeBytes.Clear();
        _ = state.Text.Clear();
        state.Kerning = 0;
        state.ShowAdvance = 0;
        var startMatrix = state.Tm;
        if (isArray)
        {
            PageContentParseText.ShowArrayItems(state, reader.Body(operand));
        }
        else
        {
            PageContentParseText.ShowEncoded(state, operand.Kind == ContentOperandKind.HexString, reader.Body(operand));
        }

        PageContentParseText.AddTextObject(state, op, startMatrix, ref reader);
    }

    /// <summary>Makes the text object for the show that was read.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "op">The operator.</param>
    /// <param name = "startMatrix">The text matrix before the first glyph.</param>
    /// <param name = "reader">The reader, for the <c>"</c> operator's spacing operands.</param>
    internal static void AddTextObject(PageContentParseState state, ContentOperator op, Matrix3x2 startMatrix, ref ContentReader reader)
    {
        if (state.Glyphs.Count == 0 && state.State.Font is not null)
        {
            // A show of only numbers or an empty string paints nothing; it only moves the text position.
            return;
        }

        var glyphs = state.Glyphs.ToArray();
        var item = new PdfTextObject
        {
            Font = state.State.Font,
            FontName = state.State.FontName,
            FontSize = state.State.FontSize,
            RenderMode = state.State.RenderMode,
            CharacterSpacing = state.State.CharacterSpacing,
            WordSpacing = state.State.WordSpacing,
            HorizontalScaling = state.State.HorizontalScaling,
            Rise = state.State.Rise,
            TextMatrix = startMatrix,
            Text = state.Text.ToString(),
            ShowOperator = op,
            SpacingOperandWord = op == ContentOperator.SetSpacingNextLineShowText ? reader.Number(0) : 0,
            SpacingOperandCharacter = op == ContentOperator.SetSpacingNextLineShowText ? reader.Number(1) : 0,
            CodeBytes = [.. state.CodeBytes],
            TrailingKerning = state.Kerning,
            TotalAdvance = state.ShowAdvance,
            LineMatrixAfter = state.Tlm,
            PositionAfter = state.Position,
            IsOpaque = state.State.Font is null,
        };
        item.SetGlyphs(glyphs);
        PageContentParseObjects.Add(state, item, PageContentParseText.UnionBoxes(glyphs), new(state.OperatorStart, state.OperatorEnd));
    }

    /// <summary>Shows the strings of a TJ array, moving by its numbers.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "body">The bytes between the brackets.</param>
    internal static void ShowArrayItems(PageContentParseState state, ReadOnlySpan<byte> body)
    {
        var lexer = new PdfLexer(body);
        var kind = lexer.Next();
        while (kind != PdfTokenKind.EndOfData)
        {
            if (kind is PdfTokenKind.LiteralString or PdfTokenKind.HexString)
            {
                PageContentParseText.ShowEncoded(state, kind == PdfTokenKind.HexString, lexer.Lexeme);
            }
            else if (kind == PdfTokenKind.Number && PdfNumber.TryParseSingle(lexer.Lexeme, out var adjustment))
            {
                PageContentParseText.Adjust(state, adjustment);
            }

            kind = lexer.Next();
        }
    }

    /// <summary>Moves the text position by a TJ adjustment.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "adjustment">The adjustment in thousandths of text space.</param>
    internal static void Adjust(PageContentParseState state, float adjustment)
    {
        state.Kerning += adjustment;
        var shift = -adjustment / PageContentParseText.AdjustmentUnits * state.State.FontSize;
        var vertical = state.State.Font?.IsVertical == true;
        var move = vertical ? shift : shift * state.State.HorizontalScaling;
        PageContentParseText.Advance(state, vertical, move);
    }

    /// <summary>Moves the text position along the writing direction.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "vertical">Whether the text writes top to bottom.</param>
    /// <param name = "move">The distance in text space units.</param>
    internal static void Advance(PageContentParseState state, bool vertical, float move)
    {
        var step = vertical ? new Vector2(0, move) : new Vector2(move, 0);
        state.Tm = Matrix3x2.CreateTranslation(step) * state.Tm;
        state.Position += step;
        state.ShowAdvance += move;
    }

    /// <summary>Decodes a string's escapes and shows it.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "hex">Whether the string is hexadecimal.</param>
    /// <param name = "raw">The string's bytes between its delimiters.</param>
    internal static void ShowEncoded(PageContentParseState state, bool hex, ReadOnlySpan<byte> raw)
    {
        byte[]? rented = null;
        Span<byte> buffer = raw.Length <= PageContentParseText.StackStringBytes ? stackalloc byte[PageContentParseText.StackStringBytes] : (rented = ArrayPool<byte>.Shared.Rent(raw.Length));
        try
        {
            var length = hex ? PdfStringDecoder.DecodeHex(raw, buffer) : PdfStringDecoder.DecodeLiteral(raw, buffer);
            PageContentParseText.ShowBytes(state, buffer[..length]);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Shows the character codes in decoded string bytes.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "bytes">The bytes.</param>
    internal static void ShowBytes(PageContentParseState state, ReadOnlySpan<byte> bytes)
    {
        var font = state.State.Font;
        if (font is null)
        {
            return;
        }

        while (!bytes.IsEmpty)
        {
            var used = Math.Clamp(font.ReadCode(bytes, out var code), 1, bytes.Length);
            PageContentParseText.ShowCode(state, font, code, bytes[..used]);
            bytes = bytes[used..];
        }
    }

    /// <summary>Records one character code as a glyph and advances the text position.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "font">The font.</param>
    /// <param name = "code">The character code.</param>
    /// <param name = "codeBytes">The bytes the code used.</param>
    internal static void ShowCode(PageContentParseState state, PdfFont font, int code, ReadOnlySpan<byte> codeBytes)
    {
        var fontSize = state.State.FontSize;
        var scaling = state.State.HorizontalScaling;
        var vertical = font.IsVertical;
        var width = font.GetWidth(code);
        var advance = width;
        float originX = 0;
        float originY = 0;
        if (vertical)
        {
            font.GetVerticalMetrics(code, out advance, out originX, out originY);
        }

        var toUser = new Matrix3x2(fontSize * scaling, 0, 0, fontSize, 0, state.State.Rise) * state.Tm * state.State.Ctm;
        var shift = vertical ? Matrix3x2.CreateTranslation(-originX, -originY) : Matrix3x2.Identity;
        var box = TextGeometry.TransformRect(shift * toUser, new(0, font.Descent, width, font.Ascent));
        var origin = Vector2.Transform(Vector2.Transform(Vector2.Zero, shift), toUser);
        var spacing = state.State.CharacterSpacing + (font.IsWordSpace(code, codeBytes.Length) ? state.State.WordSpacing : 0);
        var move = vertical ? (advance * fontSize) + spacing : ((advance * fontSize) + spacing) * scaling;
        var textStart = state.Text.Length;
        PageContentParseText.AppendUnicode(state, font, code);
        var offset = state.CodeBytes.Count;
        foreach (var b in codeBytes)
        {
            state.CodeBytes.Add(b);
        }

        state.Glyphs.Add(new(code, offset, codeBytes.Length, textStart, state.Text.Length - textStart, state.Kerning, move, origin, box));
        state.Kerning = 0;
        PageContentParseText.Advance(state, vertical, move);
    }

    /// <summary>Appends a code's Unicode text.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "font">The font.</param>
    /// <param name = "code">The character code.</param>
    internal static void AppendUnicode(PageContentParseState state, PdfFont font, int code)
    {
        Span<char> unicode = stackalloc char[PageContentParseText.UnicodeCapacity];
        var count = font.GetUnicode(code, unicode);
        _ = state.Text.Append(unicode[..Math.Clamp(count, 0, unicode.Length)]);
    }
}
