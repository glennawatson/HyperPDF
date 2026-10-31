// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.PageObjects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.PageObjects;

/// <summary>Writes text with its original positioning and spacing.</summary>
public static class PdfPageContentTextWriter
{
    /// <summary>The thousandths of text space a <c>TJ</c> number is given in.</summary>
    internal const float Thousandths = 1000;

    /// <summary>The smallest <c>TJ</c> number written; smaller sums are rounding noise.</summary>
    internal const float MinimumAdjustment = 0.0005F;

    /// <summary>The most pieces one glyph adds to a show: a number before it and the string run it ends.</summary>
    private const int PiecesPerGlyph = 2;

    /// <summary>Writes the effects a <c>'</c> or <c>"</c> operator has besides showing text.</summary>
    /// <param name = "builder">The output.</param>
    /// <param name = "text">The text object.</param>
    internal static void WriteTextPrefix(ref PdfContentBuilder builder, PdfTextObject text)
    {
        if (text.ShowOperator == ContentOperator.SetSpacingNextLineShowText)
        {
            builder.SetWordSpacing(text.SpacingOperandWord);
            builder.SetCharacterSpacing(text.SpacingOperandCharacter);
        }

        if (text.ShowOperator is ContentOperator.NextLineShowText or ContentOperator.SetSpacingNextLineShowText)
        {
            builder.NextLine();
        }
    }

    /// <summary>
    /// After a moved show, sets the text matrix back to where the unmoved show left the line and moves the text position
    /// along, so the text after it lands where it did.
    /// </summary>
    /// <param name = "builder">The output.</param>
    /// <param name = "text">The text object.</param>
    internal static void WriteRestoreTextPosition(ref PdfContentBuilder builder, PdfTextObject text)
    {
        var line = text.LineMatrixAfter;
        builder.SetTextMatrix(line.M11, line.M12, line.M21, line.M22, line.M31, line.M32);
        var along = text.IsVertical ? text.PositionAfter.Y : text.PositionAfter.X;
        var adjustment = PdfPageContentTextWriter.ToAdjustment(text, along);
        if (MathF.Abs(adjustment) < PdfPageContentTextWriter.MinimumAdjustment)
        {
            return;
        }

        builder.BeginTextArray();
        builder.AddTextArrayAdjustment(adjustment);
        builder.EndTextArray();
    }

    /// <summary>Converts a distance along the writing direction into a <c>TJ</c> number.</summary>
    /// <param name = "text">The text object, which gives the font size and scaling.</param>
    /// <param name = "distance">The distance in text space units, positive in the writing direction.</param>
    /// <returns>The number that moves by the distance; 0 when the text cannot move.</returns>
    internal static float ToAdjustment(PdfTextObject text, float distance)
    {
        var scale = text.AdjustmentScale;
        return scale == 0 ? 0 : -distance * PdfPageContentTextWriter.Thousandths / scale;
    }

    /// <summary>Writes the show operator for the glyphs that are left, with <c>TJ</c> numbers standing in for those that are not.</summary>
    /// <param name = "builder">The output.</param>
    /// <param name = "text">The text object.</param>
    internal static void WriteShow(ref PdfContentBuilder builder, PdfTextObject text)
    {
        var glyphs = text.Glyphs;

        // Sized from the glyphs, and taken from the shared array pool, so showing text allocates no lists of its own.
        var codeLength = 0;
        for (var i = 0; i < glyphs.Length; i++)
        {
            codeLength += glyphs[i].ByteLength;
        }

        var pieces = new ShowPieces(ArrayPool<ShowItem>.Shared.Rent((glyphs.Length * PiecesPerGlyph) + 1), ArrayPool<byte>.Shared.Rent(Math.Max(codeLength, 1)));
        try
        {
            var pending = 0F;
            for (var i = 0; i < glyphs.Length; i++)
            {
                pending += glyphs[i].Kerning;
                if (text.IsDeleted || text.IsGlyphRemoved(i))
                {
                    pieces.CloseRun();
                    pending += PdfPageContentTextWriter.ToAdjustment(text, glyphs[i].Advance);
                    continue;
                }

                if (MathF.Abs(pending) >= PdfPageContentTextWriter.MinimumAdjustment)
                {
                    pieces.CloseRun();
                    pieces.AddNumber(pending);
                }

                pending = 0;
                pieces.AddCode(text.GetCodeBytes(i));
            }

            pieces.CloseRun();
            pending += text.TrailingKerning;
            if (MathF.Abs(pending) >= PdfPageContentTextWriter.MinimumAdjustment)
            {
                pieces.AddNumber(pending);
            }

            PdfPageContentTextWriter.EmitShow(ref builder, ref pieces);
        }
        finally
        {
            ArrayPool<ShowItem>.Shared.Return(pieces.Items);
            ArrayPool<byte>.Shared.Return(pieces.Codes);
        }
    }

    /// <summary>Writes a text object: its glyphs less the removed ones, with the text position kept where it would have been.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "builder">The output.</param>
    /// <param name = "text">The text object.</param>
    internal static void WriteText(PdfPageContent state, ref PdfContentBuilder builder, PdfTextObject text)
    {
        if (text.IsOpaque)
        {
            if (!text.IsDeleted)
            {
                PdfPageContentWriter.WriteSource(state, ref builder, text);
            }

            return;
        }

        PdfPageContentTextWriter.WriteTextPrefix(ref builder, text);
        PdfPageContentWriter.WritePaints(state, ref builder, text);
        if (text.IsTransformed && PdfPageContentWriter.TryGetLocalTransform(text, out var matrix))
        {
            var moved = text.TextMatrix * matrix;
            builder.SetTextMatrix(moved.M11, moved.M12, moved.M21, moved.M22, moved.M31, moved.M32);
        }

        PdfPageContentTextWriter.WriteShow(ref builder, text);
        if (text.IsTransformed)
        {
            PdfPageContentTextWriter.WriteRestoreTextPosition(ref builder, text);
        }

        PdfPageContentTextWriter.WriteRestorePaints(state, ref builder, text);
    }

    /// <summary>Puts the colours a recoloured text object changed back.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "builder">The output.</param>
    /// <param name = "text">The text object.</param>
    internal static void WriteRestorePaints(PdfPageContent state, ref PdfContentBuilder builder, PdfTextObject text)
    {
        if (text.ChangedFill is not null)
        {
            PdfPageContentPaintWriter.WritePaint(state, ref builder, text.OriginalFillPaint, false);
        }

        if (text.ChangedStroke is not null)
        {
            PdfPageContentPaintWriter.WritePaint(state, ref builder, text.OriginalStrokePaint, true);
        }
    }

    /// <summary>Writes the collected pieces as <c>Tj</c> when it is one string, otherwise as <c>TJ</c>.</summary>
    /// <param name = "builder">The output.</param>
    /// <param name = "pieces">The pieces and the string bytes they point into.</param>
    private static void EmitShow(ref PdfContentBuilder builder, ref ShowPieces pieces)
    {
        if (pieces.ItemCount == 0)
        {
            return;
        }

        if (pieces.ItemCount == 1 && !pieces.Items[0].IsNumber)
        {
            builder.ShowText(pieces.Codes.AsSpan(pieces.Items[0].Start, pieces.Items[0].Length));
            return;
        }

        builder.BeginTextArray();
        for (var i = 0; i < pieces.ItemCount; i++)
        {
            var item = pieces.Items[i];
            if (item.IsNumber)
            {
                builder.AddTextArrayAdjustment(item.Number);
            }
            else
            {
                builder.AddTextArrayString(pieces.Codes.AsSpan(item.Start, item.Length));
            }
        }

        builder.EndTextArray();
    }

    /// <summary>One piece of a <c>TJ</c> array: a string run or a number.</summary>
    /// <param name = "IsNumber">Whether the piece is a number.</param>
    /// <param name = "Number">The number, in thousandths of text space.</param>
    /// <param name = "Start">The first byte of a string run.</param>
    /// <param name = "Length">The length of a string run.</param>
    private readonly record struct ShowItem(bool IsNumber, float Number, int Start, int Length);

    /// <summary>The pieces of one show and the string bytes they point into, held in arrays rented from the shared pool.</summary>
    private struct ShowPieces
    {
        /// <summary>The offset where the run being collected starts in <see cref = "Codes"/>, or -1 while there is none.</summary>
        private int _runStart;

        /// <summary>Initializes a new instance of the <see cref = "ShowPieces"/> struct.</summary>
        /// <param name = "items">The array for the pieces, big enough for every piece.</param>
        /// <param name = "codes">The array for the string bytes, big enough for every glyph's bytes.</param>
        internal ShowPieces(ShowItem[] items, byte[] codes)
        {
            Items = items;
            Codes = codes;
            _runStart = -1;
        }

        /// <summary>Gets the pieces collected.</summary>
        internal readonly ShowItem[] Items { get; }

        /// <summary>Gets how many pieces are collected.</summary>
        internal int ItemCount { readonly get; private set; }

        /// <summary>Gets the string bytes collected.</summary>
        internal readonly byte[] Codes { get; }

        /// <summary>Gets how many string bytes are collected.</summary>
        internal int CodeCount { readonly get; private set; }

        /// <summary>Adds a <c>TJ</c> number.</summary>
        /// <param name = "number">The number, in thousandths of text space.</param>
        internal void AddNumber(float number)
        {
            Items[ItemCount] = new(true, number, 0, 0);
            ItemCount++;
        }

        /// <summary>Adds the bytes of one glyph to the run being collected, starting a run when there is none.</summary>
        /// <param name = "code">The glyph's bytes.</param>
        internal void AddCode(ReadOnlySpan<byte> code)
        {
            _runStart = _runStart < 0 ? CodeCount : _runStart;
            code.CopyTo(Codes.AsSpan(CodeCount));
            CodeCount += code.Length;
        }

        /// <summary>Ends the string run being collected.</summary>
        internal void CloseRun()
        {
            if (_runStart >= 0 && CodeCount > _runStart)
            {
                Items[ItemCount] = new(false, 0, _runStart, CodeCount - _runStart);
                ItemCount++;
            }

            _runStart = -1;
        }
    }
}
