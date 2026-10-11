// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
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
        var items = new List<ShowItem>();
        var codes = new List<byte>();
        var runStart = -1;
        var pending = 0F;
        for (var i = 0; i < glyphs.Length; i++)
        {
            pending += glyphs[i].Kerning;
            if (text.IsDeleted || text.IsGlyphRemoved(i))
            {
                PdfPageContentTextWriter.CloseRun(items, codes, ref runStart);
                pending += PdfPageContentTextWriter.ToAdjustment(text, glyphs[i].Advance);
                continue;
            }

            if (MathF.Abs(pending) >= PdfPageContentTextWriter.MinimumAdjustment)
            {
                PdfPageContentTextWriter.CloseRun(items, codes, ref runStart);
                items.Add(new(true, pending, 0, 0));
            }

            pending = 0;
            runStart = runStart < 0 ? codes.Count : runStart;
            codes.AddRange(text.GetCodeBytes(i));
        }

        PdfPageContentTextWriter.CloseRun(items, codes, ref runStart);
        pending += text.TrailingKerning;
        if (MathF.Abs(pending) >= PdfPageContentTextWriter.MinimumAdjustment)
        {
            items.Add(new(true, pending, 0, 0));
        }

        PdfPageContentTextWriter.EmitShow(ref builder, items, codes);
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

    /// <summary>Ends the string run being collected.</summary>
    /// <param name = "items">The pieces of the show.</param>
    /// <param name = "codes">The collected string bytes.</param>
    /// <param name = "runStart">Where the run starts in <paramref name = "codes"/>, or -1; reset to -1.</param>
    private static void CloseRun(List<ShowItem> items, List<byte> codes, ref int runStart)
    {
        if (runStart >= 0 && codes.Count > runStart)
        {
            items.Add(new(false, 0, runStart, codes.Count - runStart));
        }

        runStart = -1;
    }

    /// <summary>Writes the collected pieces as <c>Tj</c> when it is one string, otherwise as <c>TJ</c>.</summary>
    /// <param name = "builder">The output.</param>
    /// <param name = "items">The pieces.</param>
    /// <param name = "codes">The string bytes the pieces point into.</param>
    private static void EmitShow(ref PdfContentBuilder builder, List<ShowItem> items, List<byte> codes)
    {
        if (items.Count == 0)
        {
            return;
        }

        var bytes = CollectionsMarshal.AsSpan(codes);
        if (items.Count == 1 && !items[0].IsNumber)
        {
            builder.ShowText(bytes.Slice(items[0].Start, items[0].Length));
            return;
        }

        builder.BeginTextArray();
        foreach (var item in items)
        {
            if (item.IsNumber)
            {
                builder.AddTextArrayAdjustment(item.Number);
            }
            else
            {
                builder.AddTextArrayString(bytes.Slice(item.Start, item.Length));
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
}
