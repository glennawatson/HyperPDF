// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Forms;

/// <summary>Draws text fields, combo boxes and list boxes.</summary>
internal static class FormTextAppearance
{
    /// <summary>Half, to centre text within the field.</summary>
    private const float Half = 0.5F;

    /// <summary>The smallest size text is fitted to.</summary>
    private const float MinimumFitSize = 4;

    /// <summary>The largest size text is fitted to.</summary>
    private const float MaximumFitSize = 12;

    /// <summary>The size a list box draws its options at when the field sets none.</summary>
    private const float DefaultListSize = 12;

    /// <summary>One thousand: font measurements are in thousandths of an em.</summary>
    private const float GlyphUnits = 1000;

    /// <summary>The alignment value for centred text.</summary>
    private const int CenterAlignment = 1;

    /// <summary>The alignment value for right aligned text.</summary>
    private const int RightAlignment = 2;

    /// <summary>The red part of the colour behind selected list options.</summary>
    private const float SelectionRed = 0.600006F;

    /// <summary>The green part of the colour behind selected list options.</summary>
    private const float SelectionGreen = 0.756866F;

    /// <summary>The blue part of the colour behind selected list options.</summary>
    private const float SelectionBlue = 0.854904F;

    /// <summary>Creates the appearance of a text field, or of a combo box without a drop-down arrow.</summary>
    /// <param name="context">The widget.</param>
    /// <param name="display">The text to show.</param>
    /// <returns>The Form XObject.</returns>
    internal static PdfStream CreateText(in AppearanceContext context, ReadOnlySpan<char> display) =>
        CreateRichText(context) ?? CreatePlainText(context, display);

    /// <summary>Creates a text appearance directly from a field's PDF value.</summary>
    /// <param name="context">The widget.</param>
    /// <param name="value">The field value.</param>
    /// <returns>The appearance stream.</returns>
    internal static PdfStream CreateText(in AppearanceContext context, PdfValue value)
    {
        if (CreateRichText(context) is { } rich)
        {
            return rich;
        }

        if (value.AsArray() is { } array)
        {
            value = array.Count > 0 ? array.Get(0) : default;
        }

        var decoded = default(PooledBuffer);
        try
        {
            var bytes = value.AsStringBytes();
            if (value.AsStream() is { } stream)
            {
                _ = stream.Decode(ref decoded, context.CancellationToken);
                bytes = decoded.WrittenSpan;
            }

            if (bytes.IsEmpty)
            {
                return CreatePlainText(context, default);
            }

            var characters = ArrayPool<char>.Shared.Rent(PdfText.GetDecodedLength(bytes));
            try
            {
                var count = PdfText.Decode(bytes, characters);
                return CreatePlainText(context, characters.AsSpan(0, count));
            }
            finally
            {
                ArrayPool<char>.Shared.Return(characters);
            }
        }
        finally
        {
            decoded.Dispose();
        }
    }

    /// <summary>Creates the appearance of a list box.</summary>
    /// <param name="context">The widget.</param>
    /// <returns>The Form XObject.</returns>
    internal static PdfStream CreateList(in AppearanceContext context)
    {
        var frame = FormFrame.Create(context);
        var da = context.DefaultAppearance;
        var font = FormFont.Find(context.Store, da.FontName, context.Widget, context.Form, context.PageResources, context.Fonts);
        var builder = default(PdfContentBuilder);
        try
        {
            FormBorderAppearance.Paint(ref builder, frame, context.Characteristics, false);
            WriteList(ref builder, context, frame, da, font);
            return FormAppearanceResources.Finish(ref builder, context, frame, font);
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Starts the marked content that holds a field's text, clipped to the inside of the border.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="frame">The frame.</param>
    internal static void BeginTextBlock(ref PdfContentBuilder builder, in FormFrame frame)
    {
        builder.BeginMarkedContent("Tx"u8);
        builder.SaveState();
        Clip(ref builder, frame);
    }

    /// <summary>Ends the marked content that holds a field's text.</summary>
    /// <param name="builder">The content.</param>
    internal static void EndTextBlock(ref PdfContentBuilder builder)
    {
        builder.RestoreState();
        builder.EndMarkedContent();
    }

    /// <summary>Creates a rich appearance when the field has readable rich text.</summary>
    /// <param name="context">The widget.</param>
    /// <returns>The rich appearance, or null to use plain text.</returns>
    private static PdfStream? CreateRichText(in AppearanceContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        var rich = default(RichText);
        try
        {
            if (FormRichText.TryRead(context, ref rich))
            {
                return FormRichTextAppearance.Create(context, ref rich);
            }
        }
        finally
        {
            rich.Dispose();
        }

        return null;
    }

    /// <summary>Creates a plain text appearance from decoded characters.</summary>
    /// <param name="context">The widget.</param>
    /// <param name="display">The characters.</param>
    /// <returns>The Form XObject.</returns>
    private static PdfStream CreatePlainText(in AppearanceContext context, ReadOnlySpan<char> display)
    {
        var frame = FormFrame.Create(context);
        var da = context.DefaultAppearance;
        var font = FormFont.Find(context.Store, da.FontName, context.Widget, context.Form, context.PageResources, context.Fonts);
        var builder = default(PdfContentBuilder);
        try
        {
            FormBorderAppearance.Paint(ref builder, frame, context.Characteristics, false);
            WriteText(ref builder, context, frame, da, font, display);
            return FormAppearanceResources.Finish(ref builder, context, frame, font);
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Writes a text field's or combo box's content.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="context">The widget.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="da">The default appearance.</param>
    /// <param name="font">The font.</param>
    /// <param name="display">The text to show.</param>
    private static void WriteText(ref PdfContentBuilder builder, in AppearanceContext context, in FormFrame frame, DefaultAppearance da, FormFont font, ReadOnlySpan<char> display)
    {
        if (display.IsEmpty)
        {
            return;
        }

        var isText = context.Type == PdfFieldType.Text;
        using var codes = EncodeDisplay(context, font, display);
        var comb = isText && context.MaxLength > 0 && (context.Flags & PdfFieldFlags.Comb) != 0;
        if (comb)
        {
            WriteComb(ref builder, context, frame, da, font, codes);
        }
        else if (isText && (context.Flags & PdfFieldFlags.Multiline) != 0)
        {
            WriteMultiline(ref builder, context, frame, da, font, codes);
        }
        else
        {
            WriteSingleLine(ref builder, context, frame, da, font, codes);
        }
    }

    /// <summary>Encodes displayed characters, masking password fields in rented storage.</summary>
    /// <param name="context">The widget.</param>
    /// <param name="font">The font.</param>
    /// <param name="display">The text.</param>
    /// <returns>The encoded characters, disposed after drawing.</returns>
    private static FormCodes EncodeDisplay(in AppearanceContext context, FormFont font, ReadOnlySpan<char> display)
    {
        if (context.Type != PdfFieldType.Text || (context.Flags & PdfFieldFlags.Password) == 0)
        {
            return font.Encode(display, context.CancellationToken);
        }

        var characters = ArrayPool<char>.Shared.Rent(display.Length);
        try
        {
            var masked = characters.AsSpan(0, display.Length);
            masked.Fill('*');
            return font.Encode(masked, context.CancellationToken);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(characters);
        }
    }

    /// <summary>Writes a list box's options, with the selected ones highlighted.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="context">The widget.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="da">The default appearance.</param>
    /// <param name="font">The font.</param>
    private static void WriteList(ref PdfContentBuilder builder, in AppearanceContext context, in FormFrame frame, DefaultAppearance da, FormFont font)
    {
        var field = context.Field;
        var count = FormChoices.Count(field);
        var size = da.FontSize > 0 ? da.FontSize : DefaultListSize;
        var rowHeight = font.LineHeight * size / GlyphUnits;
        var useIndices = FormChoices.UsesSelectedIndices(field);
        var top = FieldAttributes.Find(field, KnownName.TI).AsInt32();
        builder.BeginMarkedContent("Tx"u8);
        builder.SaveState();
        Clip(ref builder, frame);
        var y = frame.Height - frame.Border.Width;
        for (var i = Math.Max(0, top); i < count && y > 0; i++)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var rowBottom = y - rowHeight;
            if (FormChoices.IsSelected(field, i, useIndices))
            {
                builder.SetFillRgb(SelectionRed, SelectionGreen, SelectionBlue);
                builder.Rectangle(frame.Border.Width, rowBottom, frame.InnerWidth, rowHeight);
                builder.Fill();
            }

            using (var label = font.EncodePdfText(FormChoices.GetLabelBytes(field, i), context.CancellationToken))
            {
                WriteListRow(ref builder, frame, da, font, label, size, rowBottom);
            }

            y = rowBottom;
        }

        builder.RestoreState();
        builder.EndMarkedContent();
    }

    /// <summary>Writes one list box row.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="da">The default appearance.</param>
    /// <param name="font">The font.</param>
    /// <param name="codes">The label's codes.</param>
    /// <param name="size">The font size.</param>
    /// <param name="rowBottom">The row's lower edge.</param>
    private static void WriteListRow(ref PdfContentBuilder builder, in FormFrame frame, DefaultAppearance da, FormFont font, FormCodes codes, float size, float rowBottom)
    {
        BeginText(ref builder, da, font, size);
        builder.MoveText(frame.Padding, rowBottom - (font.Descent * size / GlyphUnits));
        builder.ShowText(codes.Slice(0, codes.Length));
        EndText(ref builder);
    }

    /// <summary>Starts the text object: colour and font.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="da">The default appearance.</param>
    /// <param name="font">The font.</param>
    /// <param name="size">The font size.</param>
    private static void BeginText(ref PdfContentBuilder builder, DefaultAppearance da, FormFont font, float size)
    {
        builder.BeginText();
        da.Color.WriteFill(ref builder);
        builder.SetFont(font.NameBytes, size);
    }

    /// <summary>Ends the text object.</summary>
    /// <param name="builder">The content.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EndText(ref PdfContentBuilder builder) => builder.EndText();

    /// <summary>Clips drawing to the area inside the border.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="frame">The frame.</param>
    private static void Clip(ref PdfContentBuilder builder, in FormFrame frame)
    {
        builder.Rectangle(frame.Border.Width, frame.Border.Width, frame.InnerWidth, frame.InnerHeight);
        builder.Clip();
        builder.EndPath();
    }

    /// <summary>Chooses the size of single-line text: the field's own, or one that fits the field's height and width.</summary>
    /// <param name="font">The font.</param>
    /// <param name="codes">The text's codes.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="requested">The size the default appearance sets; 0 to fit.</param>
    /// <param name="comb">Whether the text is spread over boxes, so its width does not limit the size.</param>
    /// <returns>The size.</returns>
    private static float ChooseSingleLineSize(FormFont font, FormCodes codes, in FormFrame frame, float requested, bool comb)
    {
        if (requested > 0)
        {
            return requested;
        }

        var lineHeight = (font.Ascent - font.Descent) / GlyphUnits;
        var size = Math.Clamp(frame.PaddedHeight / lineHeight, MinimumFitSize, MaximumFitSize);
        var width = codes.Measure(0, codes.Length) * size / GlyphUnits;
        return !comb && width > frame.PaddedWidth && width > 0 ? Math.Max(MinimumFitSize, size * frame.PaddedWidth / width) : size;
    }

    /// <summary>Wraps multi-line text, shrinking automatically sized text until it fits the height.</summary>
    /// <param name="font">The font.</param>
    /// <param name="codes">The text's codes.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="requested">The size the default appearance sets; 0 to fit.</param>
    /// <param name="lines">Receives the lines.</param>
    /// <param name="cancellationToken">A token that cancels the layout.</param>
    /// <returns>The size.</returns>
    private static float LayOutMultiline(FormFont font, FormCodes codes, in FormFrame frame, float requested, ref TextLines lines, CancellationToken cancellationToken)
    {
        var size = requested > 0 ? requested : MaximumFitSize;
        while (true)
        {
            lines.Clear();
            TextLayout.Wrap(codes, size, frame.PaddedWidth, ref lines, cancellationToken);
            var tooTall = lines.Count * font.LineHeight * size / GlyphUnits > frame.PaddedHeight;
            if (requested > 0 || !tooTall || size <= MinimumFitSize)
            {
                return size;
            }

            size = Math.Max(MinimumFitSize, size - 1);
        }
    }

    /// <summary>Gets the x position a line starts at for the field's alignment.</summary>
    /// <param name="context">The widget.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="width">The line's width.</param>
    /// <returns>The x position.</returns>
    private static float AlignedX(in AppearanceContext context, in FormFrame frame, float width) => context.Alignment switch
    {
        CenterAlignment => (frame.Width - width) * Half,
        RightAlignment => frame.Width - frame.Padding - width,
        _ => frame.Padding,
    };

    /// <summary>Gets the baseline that centres a line of text between the top and bottom edges.</summary>
    /// <param name="font">The font.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="size">The font size.</param>
    /// <returns>The y position.</returns>
    private static float CenteredBaseline(FormFont font, in FormFrame frame, float size) =>
        ((frame.Height - ((font.Ascent - font.Descent) * size / GlyphUnits)) * Half) - (font.Descent * size / GlyphUnits);

    /// <summary>Writes one line of text centred between the top and bottom edges.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="context">The widget.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="da">The default appearance.</param>
    /// <param name="font">The font.</param>
    /// <param name="codes">The text's codes.</param>
    private static void WriteSingleLine(ref PdfContentBuilder builder, in AppearanceContext context, in FormFrame frame, DefaultAppearance da, FormFont font, FormCodes codes)
    {
        var size = ChooseSingleLineSize(font, codes, frame, da.FontSize, false);
        var line = TextLayout.FirstLine(codes, size);
        BeginTextBlock(ref builder, frame);
        BeginText(ref builder, da, font, size);
        builder.MoveText(AlignedX(context, frame, line.Width), CenteredBaseline(font, frame, size));
        builder.ShowText(codes.Slice(line.Start, line.Length));
        EndText(ref builder);
        EndTextBlock(ref builder);
    }

    /// <summary>Writes wrapped lines from the top of the field.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="context">The widget.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="da">The default appearance.</param>
    /// <param name="font">The font.</param>
    /// <param name="codes">The text's codes.</param>
    private static void WriteMultiline(ref PdfContentBuilder builder, in AppearanceContext context, in FormFrame frame, DefaultAppearance da, FormFont font, FormCodes codes)
    {
        var lines = default(TextLines);
        try
        {
            var size = LayOutMultiline(font, codes, frame, da.FontSize, ref lines, context.CancellationToken);
            var leading = font.LineHeight * size / GlyphUnits;
            var x = 0F;
            var y = 0F;
            BeginTextBlock(ref builder, frame);
            BeginText(ref builder, da, font, size);
            for (var i = 0; i < lines.Count; i++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                var nextX = AlignedX(context, frame, lines[i].Width);
                var nextY = i == 0 ? frame.Height - frame.Padding - (font.Ascent * size / GlyphUnits) : y - leading;
                builder.MoveText(nextX - x, nextY - y);
                builder.ShowText(codes.Slice(lines[i].Start, lines[i].Length));
                x = nextX;
                y = nextY;
            }

            EndText(ref builder);
            EndTextBlock(ref builder);
        }
        finally
        {
            lines.Dispose();
        }
    }

    /// <summary>Writes a comb field: one character centred in each box, with dividers between the boxes.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="context">The widget.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="da">The default appearance.</param>
    /// <param name="font">The font.</param>
    /// <param name="codes">The text's codes.</param>
    private static void WriteComb(ref PdfContentBuilder builder, in AppearanceContext context, in FormFrame frame, DefaultAppearance da, FormFont font, FormCodes codes)
    {
        var boxes = context.MaxLength;
        var cell = frame.InnerWidth / boxes;
        var size = ChooseSingleLineSize(font, codes, frame, da.FontSize, true);
        var count = Math.Min(codes.Length, boxes);
        var first = context.Alignment switch
        {
            CenterAlignment => (boxes - count) >> 1,
            RightAlignment => boxes - count,
            _ => 0,
        };
        BeginTextBlock(ref builder, frame);
        WriteCombDividers(ref builder, context, frame, boxes, cell);
        BeginText(ref builder, da, font, size);
        for (var i = 0; i < count; i++)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var width = codes.GetWidth(i) * size / GlyphUnits;
            var x = frame.Border.Width + ((first + i) * cell) + ((cell - width) * Half);
            builder.SetTextMatrix(1, 0, 0, 1, x, CenteredBaseline(font, frame, size));
            builder.ShowText(codes.Slice(i, 1));
        }

        EndText(ref builder);
        EndTextBlock(ref builder);
    }

    /// <summary>Draws the lines between the boxes of a comb field, in the border colour.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="context">The widget.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="boxes">The number of boxes.</param>
    /// <param name="cell">The width of a box.</param>
    private static void WriteCombDividers(ref PdfContentBuilder builder, in AppearanceContext context, in FormFrame frame, int boxes, float cell)
    {
        if (frame.Border.Width <= 0 || context.Characteristics?.GetArray(KnownName.BC) is not { } color)
        {
            return;
        }

        builder.SaveState();
        if (FormAppearanceColor.TryWriteColor(ref builder, color, true))
        {
            builder.SetLineWidth(frame.Border.Width);
            for (var i = 1; i < boxes; i++)
            {
                var x = frame.Border.Width + (i * cell);
                builder.MoveTo(x, frame.Border.Width);
                builder.LineTo(x, frame.Height - frame.Border.Width);
            }

            builder.Stroke();
        }

        builder.RestoreState();
    }
}
