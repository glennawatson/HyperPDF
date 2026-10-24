// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Objects;

using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Content;

/// <summary>Runs the interpreter's Text operations over its owned state.</summary>
internal static class ContentText
{
    /// <summary>The string length decoded on the stack; longer strings use a pooled array.</summary>
    internal const int StackStringBytes = 256;

    /// <summary>The percentage that is a horizontal scaling of 1.</summary>
    internal const float PercentScale = 100;

    /// <summary>The thousandths of text space a TJ adjustment is given in.</summary>
    internal const float AdjustmentUnits = 1000;

    /// <summary>The highest text render mode.</summary>
    internal const int MaxRenderMode = 7;

    /// <summary>The first text render mode that adds to the clip.</summary>
    internal const int FirstClipMode = 4;

    /// <summary>The invisible text render mode.</summary>
    internal const int InvisibleMode = 3;

    /// <summary>The operand index of the string in the <c>"</c> operator.</summary>
    internal const int SpacingStringOperand = 2;

    /// <summary>Handles <c>BT</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpBeginText(ContentInterpreter self, ref ContentReader reader)
    {
        self.Tm = Matrix3x2.Identity;
        self.Tlm = Matrix3x2.Identity;
    }

    /// <summary>Handles <c>ET</c>: applies the clip collected by the clipping render modes.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpEndText(ContentInterpreter self, ref ContentReader reader)
    {
        if (self.TextClipGlyphs <= 0)
        {
            return;
        }

        var clip = ContentPaths.TakePath(self, self.TextClip);
        self.TextClipGlyphs = 0;
        self.Device.Clip(clip, false, self.State.Ctm);
    }

    /// <summary>Handles <c>Tc</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpSetCharacterSpacing(ContentInterpreter self, ref ContentReader reader) => self.State.CharacterSpacing = reader.Number(0);

    /// <summary>Handles <c>Tw</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpSetWordSpacing(ContentInterpreter self, ref ContentReader reader) => self.State.WordSpacing = reader.Number(0);

    /// <summary>Handles <c>Tz</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpSetHorizontalScaling(ContentInterpreter self, ref ContentReader reader) => self.State.HorizontalScaling = reader.Number(0) / ContentText.PercentScale;

    /// <summary>Handles <c>TL</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpSetLeading(ContentInterpreter self, ref ContentReader reader) => self.State.Leading = reader.Number(0);

    /// <summary>Handles <c>Tf</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpSetFont(ContentInterpreter self, ref ContentReader reader)
    {
        var value = ContentExecution.FindResource(self, KnownName.Font, reader.Operand(0).Name);
        self.State.Font = value.AsDictionary() is { } dictionary ? self.Cache.Fonts.Get(dictionary) : null;
        self.State.FontSize = reader.Number(1);
    }

    /// <summary>Handles <c>Tr</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpSetRenderMode(ContentInterpreter self, ref ContentReader reader) => self.State.RenderMode = Math.Clamp((int)reader.Number(0), 0, ContentText.MaxRenderMode);

    /// <summary>Handles <c>Ts</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpSetRise(ContentInterpreter self, ref ContentReader reader) => self.State.Rise = reader.Number(0);

    /// <summary>Handles <c>Td</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpMoveText(ContentInterpreter self, ref ContentReader reader) => ContentText.MoveText(self, reader.Number(0), reader.Number(1));

    /// <summary>Handles <c>TD</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpMoveTextSetLeading(ContentInterpreter self, ref ContentReader reader)
    {
        self.State.Leading = -reader.Number(1);
        ContentText.MoveText(self, reader.Number(0), reader.Number(1));
    }

    /// <summary>Handles <c>Tm</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpSetTextMatrix(ContentInterpreter self, ref ContentReader reader)
    {
        self.Tlm = new(
            reader.Number(0),
            reader.Number(1),
            reader.Number(ContentGraphicsState.MatrixC),
            reader.Number(ContentGraphicsState.MatrixD),
            reader.Number(ContentGraphicsState.MatrixE),
            reader.Number(ContentGraphicsState.MatrixF));
        self.Tm = self.Tlm;
    }

    /// <summary>Handles <c>T*</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpNextLine(ContentInterpreter self, ref ContentReader reader) => ContentText.MoveText(self, 0, -self.State.Leading);

    /// <summary>Handles <c>Tj</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpShowText(ContentInterpreter self, ref ContentReader reader) => ContentText.ShowOperand(self, ref reader, 0);

    /// <summary>Handles <c>TJ</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpShowTextArray(ContentInterpreter self, ref ContentReader reader)
    {
        var array = reader.Operand(0);
        if (array.Kind != ContentOperandKind.Array)
        {
            return;
        }

        self.TextObjects?.BeginTextObject();
        ContentText.ShowArray(self, reader.Body(array));
    }

    /// <summary>Handles <c>'</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpNextLineShowText(ContentInterpreter self, ref ContentReader reader)
    {
        ContentText.MoveText(self, 0, -self.State.Leading);
        ContentText.ShowOperand(self, ref reader, 0);
    }

    /// <summary>Handles <c>"</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpSpacingNextLineShowText(ContentInterpreter self, ref ContentReader reader)
    {
        self.State.WordSpacing = reader.Number(0);
        self.State.CharacterSpacing = reader.Number(1);
        ContentText.MoveText(self, 0, -self.State.Leading);
        ContentText.ShowOperand(self, ref reader, ContentText.SpacingStringOperand);
    }

    /// <summary>
    /// Handles <c>d1</c>: the glyph is a shape painted in the colours in force when it is shown, the fill colour for what it fills
    /// and the stroke colour for what it strokes (ISO 32000-2 9.6.4); colour operators inside the glyph are ignored.
    /// </summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpSetGlyphWidthAndBounds(ContentInterpreter self, ref ContentReader reader) => self.ColorLocked = true;

    /// <summary>Moves to the start of a new line.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "x">The x offset.</param>
    /// <param name = "y">The y offset.</param>
    internal static void MoveText(ContentInterpreter self, float x, float y)
    {
        self.Tlm = Matrix3x2.CreateTranslation(x, y) * self.Tlm;
        self.Tm = self.Tlm;
    }

    /// <summary>Shows the string operand at an index.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "reader">The reader.</param>
    /// <param name = "index">The operand index.</param>
    internal static void ShowOperand(ContentInterpreter self, ref ContentReader reader, int index)
    {
        var operand = reader.Operand(index);
        if (operand.Kind is not (ContentOperandKind.LiteralString or ContentOperandKind.HexString))
        {
            return;
        }

        self.TextObjects?.BeginTextObject();
        var group = ContentText.BeginTextKnockout(self);
        try
        {
            ContentText.ShowEncoded(self, operand.Kind == ContentOperandKind.HexString, reader.Body(operand));
        }
        finally
        {
            ContentText.EndTextKnockout(self, group);
        }
    }

    /// <summary>Shows the strings of a TJ array, moving by its numbers.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "body">The bytes between the brackets.</param>
    internal static void ShowArray(ContentInterpreter self, ReadOnlySpan<byte> body)
    {
        var group = ContentText.BeginTextKnockout(self);
        try
        {
            ContentText.ShowArrayItems(self, body);
        }
        finally
        {
            ContentText.EndTextKnockout(self, group);
        }
    }

    /// <summary>
    /// Starts a knockout group around one text show when text knockout (/TK) is on and the text is translucent or
    /// blended, so overlapping glyphs of the show do not show through each other; the group carries the alpha, blend
    /// mode and soft mask, and the glyphs inside are drawn opaque.
    /// </summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <returns>The group to end, or null when the glyphs are drawn directly.</returns>
    internal static GroupInfo? BeginTextKnockout(ContentInterpreter self)
    {
        if (!ContentText.UsesTextKnockout(self))
        {
            return null;
        }

        var group = new GroupInfo(true, true, self.State.FillAlpha, self.State.BlendMode, self.State.SoftMask, PdfRect.Empty, self.BlendSpace);
        self.Device.BeginGroup(group);
        self.State.FillAlpha = 1;
        self.State.BlendMode = PdfBlendMode.Normal;
        self.State.SoftMask = null;
        return group;
    }

    /// <summary>Ends a text knockout group and restores the state it carried.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "group">The group from <see cref = "ContentText.BeginTextKnockout"/>, or null.</param>
    internal static void EndTextKnockout(ContentInterpreter self, GroupInfo? group)
    {
        if (group is not { } info)
        {
            return;
        }

        self.Device.EndGroup(info);
        self.State.FillAlpha = info.Alpha;
        self.State.BlendMode = info.Blend;
        self.State.SoftMask = info.SoftMask;
    }

    /// <summary>Determines whether a text show needs a knockout group: visible filled text, not Type 3, with /TK and transparency.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <returns><see langword="true"/> when the show is wrapped in a group.</returns>
    internal static bool UsesTextKnockout(ContentInterpreter self) =>
        self.Hidden == 0
        && self.State.TextKnockout
        && self.State.RenderMode is 0 or
        ContentText.FirstClipMode
        && self.State.Font is not null and not PdfType3Font
        && ContentXObjects.HasTransparency(self);

    /// <summary>Shows the strings of a TJ array, moving by its numbers.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "body">The bytes between the brackets.</param>
    internal static void ShowArrayItems(ContentInterpreter self, ReadOnlySpan<byte> body)
    {
        var lexer = new PdfLexer(body);
        var kind = lexer.Next();
        while (kind != PdfTokenKind.EndOfData)
        {
            if (kind is PdfTokenKind.LiteralString or PdfTokenKind.HexString)
            {
                ContentText.ShowEncoded(self, kind == PdfTokenKind.HexString, lexer.Lexeme);
            }
            else if (kind == PdfTokenKind.Number && PdfNumber.TryParseSingle(lexer.Lexeme, out var adjustment))
            {
                ContentText.Adjust(self, adjustment);
            }

            kind = lexer.Next();
        }
    }

    /// <summary>Moves the text position by a TJ adjustment.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "adjustment">The adjustment in thousandths of text space.</param>
    internal static void Adjust(ContentInterpreter self, float adjustment)
    {
        var shift = -adjustment / ContentText.AdjustmentUnits * self.State.FontSize;
        var vertical = self.State.Font?.IsVertical == true;
        self.Tm = (vertical ? Matrix3x2.CreateTranslation(0, shift) : Matrix3x2.CreateTranslation(shift * self.State.HorizontalScaling, 0)) * self.Tm;
    }

    /// <summary>Decodes a string's escapes and shows it.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "hex">Whether the string is hexadecimal.</param>
    /// <param name = "raw">The string's bytes between its delimiters.</param>
    internal static void ShowEncoded(ContentInterpreter self, bool hex, ReadOnlySpan<byte> raw)
    {
        // A literal with no escape and no carriage return is already its own value, so it is shown without being copied.
        if (!hex && PdfStringDecoder.IsPlainLiteral(raw))
        {
            ContentText.ShowBytes(self, raw);
            return;
        }

        byte[]? rented = null;
        Span<byte> buffer = raw.Length <= ContentText.StackStringBytes ? stackalloc byte[ContentText.StackStringBytes] : (rented = ArrayPool<byte>.Shared.Rent(raw.Length));
        try
        {
            var length = hex ? PdfStringDecoder.DecodeHex(raw, buffer) : PdfStringDecoder.DecodeLiteral(raw, buffer);
            ContentText.ShowBytes(self, buffer[..length]);
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
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "bytes">The bytes.</param>
    internal static void ShowBytes(ContentInterpreter self, ReadOnlySpan<byte> bytes)
    {
        var font = self.State.Font;
        if (font is null)
        {
            return;
        }

        while (!bytes.IsEmpty)
        {
            var used = Math.Clamp(font.ReadCode(bytes, out var code), 1, bytes.Length);
            ContentText.ShowCode(self, font, code, used);
            bytes = bytes[used..];
        }
    }

    /// <summary>Shows one character code and advances the text position.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "font">The font.</param>
    /// <param name = "code">The character code.</param>
    /// <param name = "length">The bytes the code used.</param>
    internal static void ShowCode(ContentInterpreter self, PdfFont font, int code, int length)
    {
        var fontSize = self.State.FontSize;
        var scaling = self.State.HorizontalScaling;
        var vertical = font.IsVertical;
        var advance = font.GetWidth(code);
        float originX = 0;
        float originY = 0;
        if (vertical)
        {
            font.GetVerticalMetrics(code, out advance, out originX, out originY);
        }

        var origin = vertical ? Matrix3x2.CreateTranslation(-originX, -originY) : Matrix3x2.Identity;
        var glyphMatrix = font.FontMatrix * origin * new Matrix3x2(fontSize * scaling, 0, 0, fontSize, 0, self.State.Rise) * self.Tm;
        var isSpace = font.IsWordSpace(code, length);
        var spacing = self.State.CharacterSpacing + (isSpace ? self.State.WordSpacing : 0);
        var move = vertical ? (advance * fontSize) + spacing : ((advance * fontSize) + spacing) * scaling;
        if (self.Hidden == 0)
        {
            Span<char> unicode = stackalloc char[PdfFont.MaxUnicodeLength];
            var count = font.GetUnicode(code, unicode);
            var glyph = new GlyphEvent(font, code, unicode[..Math.Clamp(count, 0, unicode.Length)], new(glyphMatrix, self.Tm, move), fontSize, isSpace);
            ContentPainting.DrawGlyph(self, in glyph, ref self.State);
            ContentText.DrawGlyphContent(self, font, code, glyphMatrix);
        }
        else
        {
            // Hidden optional content draws nothing, but its clipping text still clips what follows.
            ContentText.AddTextClip(self, font, code, glyphMatrix);
        }

        self.Tm = (vertical ? Matrix3x2.CreateTranslation(0, move) : Matrix3x2.CreateTranslation(move, 0)) * self.Tm;
    }

    /// <summary>Runs a Type 3 glyph's content, or adds a glyph outline to the text clip.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "font">The font.</param>
    /// <param name = "code">The character code.</param>
    /// <param name = "glyphMatrix">The matrix from glyph space to user space.</param>
    internal static void DrawGlyphContent(ContentInterpreter self, PdfFont font, int code, Matrix3x2 glyphMatrix)
    {
        if (font is PdfType3Font type3)
        {
            if (self.State.RenderMode != ContentText.InvisibleMode)
            {
                ContentText.DrawType3Glyph(self, type3, code, glyphMatrix);
            }

            return;
        }

        ContentText.AddTextClip(self, font, code, glyphMatrix);
    }

    /// <summary>Adds a glyph's outline to the text clip when the render mode clips (4 to 7).</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "font">The font.</param>
    /// <param name = "code">The character code.</param>
    /// <param name = "glyphMatrix">The matrix from glyph space to user space.</param>
    internal static void AddTextClip(ContentInterpreter self, PdfFont font, int code, Matrix3x2 glyphMatrix)
    {
        if (self.State.RenderMode < ContentText.FirstClipMode || font is PdfType3Font || font.GetOutline(code)is not { } outline)
        {
            return;
        }

        self.TextClip.AddPath(outline, glyphMatrix);
        self.TextClipGlyphs++;
    }

    /// <summary>Runs a Type 3 glyph's content stream.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "font">The font.</param>
    /// <param name = "code">The character code.</param>
    /// <param name = "glyphMatrix">The matrix from glyph space to user space.</param>
    internal static void DrawType3Glyph(ContentInterpreter self, PdfType3Font font, int code, Matrix3x2 glyphMatrix)
    {
        if (self.Depth >= PdfLimits.MaxDrawDepth || font.GetCharProc(code) is not { } procedure)
        {
            return;
        }

        var tm = self.Tm;
        var tlm = self.Tlm;
        var locked = self.ColorLocked;
        var patternBase = self.PatternBase;
        ContentGraphicsState.SaveState(self);
        self.State.Ctm = glyphMatrix * self.State.Ctm;
        self.State.Font = null;
        self.Resources.Add(font.Resources ?? ContentExecution.CurrentResources(self));
        self.Depth++;
        var scope = self.Device as IType3GlyphScope;
        scope?.BeginType3Glyph(font, code);
        try
        {
            ContentExecution.RunStreamBody(self, procedure);
        }
        finally
        {
            scope?.EndType3Glyph();
            self.Depth--;
            self.Resources.RemoveAt(self.Resources.Count - 1);
            ContentGraphicsState.RestoreState(self);
            self.Tm = tm;
            self.Tlm = tlm;
            self.ColorLocked = locked;
            self.PatternBase = patternBase;
        }
    }
}
