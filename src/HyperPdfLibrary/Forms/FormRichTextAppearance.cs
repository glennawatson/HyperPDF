// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Draws the rich text value of a text field (<c>/RV</c>, ISO 32000-2 12.7.4.3) in its own fonts, sizes and colours, with
/// underline and strike-through, wrapped to the field and aligned as each paragraph asks.
/// </summary>
internal static class FormRichTextAppearance
{
    /// <summary>Half, to centre text within the field.</summary>
    private const float Half = 0.5F;

    /// <summary>One thousand: font measurements are in thousandths of an em.</summary>
    private const float GlyphUnits = 1000;

    /// <summary>The size rich text is drawn at when neither the text, the default style nor the field sets one.</summary>
    private const float DefaultSize = 12;

    /// <summary>The thickness of an underline or strike-through as a fraction of the size.</summary>
    private const float LineWeight = 0.06F;

    /// <summary>The depth of an underline below the baseline as a fraction of the size.</summary>
    private const float UnderlineDepth = 0.12F;

    /// <summary>The height of a strike-through above the baseline as a fraction of the size.</summary>
    private const float StrikeHeight = 0.3F;

    /// <summary>The alignment value for centred text.</summary>
    private const int CenterAlignment = 1;

    /// <summary>The alignment value for right aligned text.</summary>
    private const int RightAlignment = 2;

    /// <summary>The bits in a colour channel.</summary>
    private const int ChannelBits = 8;

    /// <summary>The bits to shift red down by in 0xRRGGBB.</summary>
    private const int RedShift = 16;

    /// <summary>The largest value of a colour channel.</summary>
    private const float ChannelMax = 255;

    /// <summary>The mask of one colour channel.</summary>
    private const int ChannelMask = 0xFF;

    /// <summary>Creates the appearance of a text field from its rich text.</summary>
    /// <param name="context">The widget.</param>
    /// <param name="text">The rich text, read from <c>/RV</c>.</param>
    /// <returns>The Form XObject.</returns>
    internal static PdfStream Create(in AppearanceContext context, ref RichText text)
    {
        var frame = FormFrame.Create(context);
        var da = context.DefaultAppearance;
        var fieldFont = FormFont.Find(context.Store, da.FontName, context.Widget, context.Form, context.PageResources, context.Fonts);
        var used = default(FormFontSet);
        var rules = default(RichRules);
        var target = new RichTarget(context, frame, da.FontSize, da.Color);
        var builder = default(PdfContentBuilder);
        try
        {
            FormBorderAppearance.Paint(ref builder, frame, context.Characteristics, false);
            FormTextAppearance.BeginTextBlock(ref builder, frame);
            WriteParagraphs(ref builder, target, ref rules, ref text, fieldFont, ref used);
            WriteRules(ref builder, target, ref rules);
            FormTextAppearance.EndTextBlock(ref builder);
            return FormAppearanceResources.Finish(ref builder, context, frame, used.AsSpan());
        }
        finally
        {
            builder.Dispose();
            rules.Dispose();
            used.Dispose();
        }
    }

    /// <summary>
    /// Writes the paragraphs from the top of the field down. A field that is not multiline shows only its first line, centred
    /// between the top and bottom edges.
    /// </summary>
    /// <param name="builder">The content.</param>
    /// <param name="target">The widget.</param>
    /// <param name="rules">Receives the underlines and strike-throughs.</param>
    /// <param name="text">The rich text.</param>
    /// <param name="fieldFont">The field's own font.</param>
    /// <param name="used">Receives the fonts used.</param>
    private static void WriteParagraphs(ref PdfContentBuilder builder, in RichTarget target, ref RichRules rules, ref RichText text, FormFont fieldFont, ref FormFontSet used)
    {
        var context = target.Context;
        var multiline = (context.Flags & PdfFieldFlags.Multiline) != 0;
        var maxWidth = multiline ? target.Frame.PaddedWidth : float.MaxValue;
        var y = target.Frame.Height - target.Frame.Padding;
        var firstLine = true;
        for (var p = 0; p < text.ParagraphCount; p++)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var paragraph = text.GetParagraph(p);
            using var block = Layout(context, ref text, paragraph, fieldFont, target.FieldSize, maxWidth, ref used);
            for (var l = 0; l < block.LineCount; l++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                var line = block.GetLine(l);
                var extent = Measure(block, line);
                y = NextBaseline(target.Frame, extent, y, firstLine, multiline);
                WriteLine(ref builder, target, ref rules, block, line, y, paragraph.Alignment);
                firstLine = false;
                if (!multiline)
                {
                    return;
                }
            }
        }
    }

    /// <summary>Finds the baseline of a line.</summary>
    /// <param name="frame">The frame.</param>
    /// <param name="extent">The line's extent.</param>
    /// <param name="previous">The previous baseline, or the top of the text area for the first line.</param>
    /// <param name="firstLine">Whether this is the first line.</param>
    /// <param name="multiline">Whether the field is multiline.</param>
    /// <returns>The baseline.</returns>
    private static float NextBaseline(in FormFrame frame, RichExtent extent, float previous, bool firstLine, bool multiline)
    {
        if (!multiline)
        {
            return ((frame.Height - (extent.Ascent - extent.Descent)) * Half) - extent.Descent;
        }

        return firstLine ? previous - extent.Ascent : previous - extent.Height;
    }

    /// <summary>Resolves the fonts and sizes of a paragraph's runs, encodes them and breaks them into lines.</summary>
    /// <param name="context">The widget.</param>
    /// <param name="text">The rich text.</param>
    /// <param name="paragraph">The paragraph.</param>
    /// <param name="fieldFont">The field's own font.</param>
    /// <param name="fieldSize">The size the field's default appearance sets; 0 for none.</param>
    /// <param name="maxWidth">The widest a line may be.</param>
    /// <param name="used">The fonts chosen so far.</param>
    /// <returns>The paragraph laid out; dispose it when it has been written.</returns>
    private static RichBlock Layout(in AppearanceContext context, ref RichText text, RichParagraph paragraph, FormFont fieldFont, float fieldSize, float maxWidth, ref FormFontSet used)
    {
        // An empty paragraph still takes a line, as tall as the field's text.
        var count = Math.Max(1, paragraph.RunCount);
        var block = context.Fonts?.RentRichBlock(count) ?? RichBlock.Rent(count, null);
        try
        {
            for (var i = 0; i < count; i++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                var hasRun = paragraph.RunCount > 0;
                var run = hasRun ? text.GetRun(paragraph.FirstRun + i) : default;
                var style = hasRun ? run.Style : RichStyle.None;
                var font = FormRichFonts.Resolve(context.Store, fieldFont, style, ref used, context.Fonts);
                block.SetRun(i, font, ChooseSize(style, fieldSize), style, hasRun ? text.GetText(run) : default, context.CancellationToken);
            }

            block.Flow();
            block.Wrap(maxWidth, context.CancellationToken);
            return block;
        }
        catch
        {
            block.Dispose();
            throw;
        }
    }

    /// <summary>Chooses the size of a run: its own, else the field's, else the default.</summary>
    /// <param name="style">The run's style.</param>
    /// <param name="fieldSize">The size the field's default appearance sets; 0 for none.</param>
    /// <returns>The size.</returns>
    private static float ChooseSize(RichStyle style, float fieldSize)
    {
        if (style.Size > 0)
        {
            return style.Size;
        }

        return fieldSize > 0 ? fieldSize : DefaultSize;
    }

    /// <summary>Finds how far a line rises above and falls below its baseline and how tall its row is.</summary>
    /// <param name="block">The paragraph.</param>
    /// <param name="line">The line.</param>
    /// <returns>The extent.</returns>
    private static RichExtent Measure(RichBlock block, TextLine line)
    {
        if (line.Length == 0)
        {
            return ExtentOf(block, block.RunCount - 1);
        }

        var ascent = 0F;
        var descent = 0F;
        var height = 0F;
        for (var u = line.Start; u < line.Start + line.Length; u++)
        {
            var extent = ExtentOf(block, block.RunOf(u));
            ascent = Math.Max(ascent, extent.Ascent);
            descent = Math.Min(descent, extent.Descent);
            height = Math.Max(height, extent.Height);
        }

        return new(ascent, descent, height);
    }

    /// <summary>Finds the extent of one run's font at its size.</summary>
    /// <param name="block">The paragraph.</param>
    /// <param name="run">The run.</param>
    /// <returns>The extent.</returns>
    private static RichExtent ExtentOf(RichBlock block, int run)
    {
        var size = block.GetSize(run) / GlyphUnits;
        var font = block.GetFont(run);
        return new(font.Ascent * size, font.Descent * size, font.LineHeight * size);
    }

    /// <summary>Gets the x position a line starts at for an alignment.</summary>
    /// <param name="target">The widget and its frame.</param>
    /// <param name="alignment">The paragraph's alignment, or no alignment to use the field's.</param>
    /// <param name="width">The line's width.</param>
    /// <returns>The x position.</returns>
    private static float AlignedX(in RichTarget target, int alignment, float width)
    {
        var frame = target.Frame;
        var effective = alignment == RichStyle.NoAlignment ? target.Context.Alignment : alignment;
        return effective switch
        {
            CenterAlignment => (frame.Width - width) * Half,
            RightAlignment => frame.Width - frame.Padding - width,
            _ => frame.Padding,
        };
    }

    /// <summary>Writes one line: a piece of text for each run on it.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="target">The widget.</param>
    /// <param name="rules">Receives the rules.</param>
    /// <param name="block">The paragraph.</param>
    /// <param name="line">The line.</param>
    /// <param name="baseline">The line's baseline.</param>
    /// <param name="alignment">The paragraph's alignment.</param>
    private static void WriteLine(ref PdfContentBuilder builder, in RichTarget target, ref RichRules rules, RichBlock block, TextLine line, float baseline, int alignment)
    {
        var x = AlignedX(target, alignment, line.Width);
        var unit = line.Start;
        var end = line.Start + line.Length;
        while (unit < end)
        {
            var run = block.RunOf(unit);
            var count = 1;
            while (unit + count < end && block.RunOf(unit + count) == run)
            {
                count++;
            }

            var piece = new RichPiece(run, block.IndexOf(unit), count, x, baseline);
            WritePiece(ref builder, target, block, piece);
            var width = block.Measure(unit, count);
            AddRules(ref rules, block, piece, width);
            x += width;
            unit += count;
        }
    }

    /// <summary>Writes a piece of text in one run's font, size and colour.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="target">The widget and its default colour.</param>
    /// <param name="block">The paragraph.</param>
    /// <param name="piece">The piece.</param>
    private static void WritePiece(ref PdfContentBuilder builder, in RichTarget target, RichBlock block, RichPiece piece)
    {
        builder.BeginText();
        SetFillColor(ref builder, block.GetStyle(piece.Run).Color, target.FieldColor);
        builder.SetFont(block.GetFont(piece.Run).NameBytes, block.GetSize(piece.Run));
        builder.SetTextMatrix(1, 0, 0, 1, piece.X, piece.Baseline);
        builder.ShowText(block.Slice(piece.Run, piece.First, piece.Count));
        builder.EndText();
    }

    /// <summary>Records the underline and strike-through a run asks for.</summary>
    /// <param name="rules">Receives the rules.</param>
    /// <param name="block">The paragraph.</param>
    /// <param name="piece">The piece.</param>
    /// <param name="width">The piece's width.</param>
    private static void AddRules(ref RichRules rules, RichBlock block, RichPiece piece, float width)
    {
        var style = block.GetStyle(piece.Run);
        var size = block.GetSize(piece.Run);
        var color = style.Color;
        var thickness = size * LineWeight;
        if (style.Underline)
        {
            rules.Add(new(color, piece.X, piece.Baseline - (size * UnderlineDepth), width, thickness));
        }

        if (style.StrikeOut)
        {
            rules.Add(new(color, piece.X, piece.Baseline + (size * StrikeHeight), width, thickness));
        }
    }

    /// <summary>Draws the recorded underlines and strike-throughs as thin filled bars.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="target">The widget and its default colour.</param>
    /// <param name="rules">The recorded rules.</param>
    private static void WriteRules(ref PdfContentBuilder builder, in RichTarget target, ref RichRules rules)
    {
        for (var i = 0; i < rules.Count; i++)
        {
            var rule = rules.Get(i);
            builder.SaveState();
            SetFillColor(ref builder, rule.Color, target.FieldColor);
            builder.Rectangle(rule.X, rule.Y, rule.Width, rule.Thickness);
            builder.Fill();
            builder.RestoreState();
        }
    }

    /// <summary>Sets the fill colour to a run's colour, or to the field's when the run sets none.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="color">The run's colour as 0xRRGGBB, or no colour.</param>
    /// <param name="fieldColor">The field's colour.</param>
    private static void SetFillColor(ref PdfContentBuilder builder, int color, DaColor fieldColor)
    {
        if (color == RichStyle.NoColor)
        {
            fieldColor.WriteFill(ref builder);
            return;
        }

        builder.SetFillRgb(((color >> RedShift) & ChannelMask) / ChannelMax, ((color >> ChannelBits) & ChannelMask) / ChannelMax, (color & ChannelMask) / ChannelMax);
    }
}
