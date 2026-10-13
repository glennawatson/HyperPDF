// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Numerics;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Text;
using HyperPdf.Core.Text.Fonts;
using HyperPdf.Core.Text.Layout;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;
namespace HyperPdf.Engine;

/// <summary>Performs HyperPdfAnnotationTextBoxes annotation operations.</summary>
internal static class HyperPdfAnnotationTextBoxes
{
    /// <summary>The slant of drawn-in italic: the tangent of 12 degrees.</summary>
    internal const float ItalicSlant = 0.2126F;

    /// <summary>The outline width of drawn-in bold, as a share of the font size.</summary>
    internal const float BoldStroke = 0.03F;

    /// <summary>The fill then stroke text render mode, used to draw bold in.</summary>
    internal const int RenderFillStroke = 2;

    /// <summary>The narrowest box, in points, so an empty line still has a place.</summary>
    internal const float MinBoxWidth = 1;

    /// <summary>The thinnest underline, in points.</summary>
    internal const float MinUnderline = 0.5F;

    /// <summary>How far apart, in points, two glyphs' baselines may be and still share a run.</summary>
    internal const float BaselineTolerance = 0.001F;

    /// <summary>The smallest position adjustment written, in thousandths of an em.</summary>
    internal const float MinAdjustment = 0.001F;

    /// <summary>Thousandths of an em.</summary>
    internal const float Thousand = 1000;

    /// <summary>The bytes of a built in font's code.</summary>
    internal const int CodeBytes = 2;

    /// <summary>Writes a text box.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="location">The top-left corner of the box.</param>
    /// <param name="wrapWidth">The width lines wrap at, in points, or 0 to break lines only where the text does.</param>
    /// <param name="text">The text; line breaks start new lines.</param>
    /// <param name="format">How the text looks.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    internal static int AddTextBox(HyperPdfAnnotations annotationState, int pageIndex, PagePoint location, float wrapWidth, string text, TextFormat format)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(format);
        if (string.IsNullOrWhiteSpace(text))
        {
            return -1;
        }

        var clamped = format.Clamped();
        var wrap = Math.Max(wrapWidth, 0);
        lock (annotationState.Gate)
        {
            if (HyperPdfAnnotationReading.GetPage(annotationState, pageIndex) is not { } page
                || TextBoxFonts.Resolve(
                    annotationState.FontState,
                    annotationState.Catalog ?? FontCatalog.System,
                    clamped,
                    text) is not { } font)
            {
                return -1;
            }

            // The caller's size and wrap width are physical points; the box is laid out and written in user space units.
            clamped = clamped with { FontSize = clamped.FontSize / page.UserUnit };
            wrap /= page.UserUnit;
            annotationState.Layout.Layout(text, clamped, font.Shaper, wrap);
            var writer = CreateGlyphWriter(annotationState, font, text);
            if (writer is null)
            {
                return -1;
            }

            var origin = HyperPdfAnnotationReading.ToUser(page, location);
            var rectangle = new PdfRectangle(origin.X, origin.Y - annotationState.Layout.Height, origin.X + Math.Max(annotationState.Layout.Width, MinBoxWidth), origin.Y);
            var box = PdfAnnotations.Create(annotationState.Store, KnownName.FreeText, rectangle);
            WriteTextBoxKeys(annotationState, box, text, clamped, wrap);
            WriteTextBoxAppearance(annotationState, box, rectangle, writer, clamped, origin);
            return HyperPdfAnnotationReading.Add(annotationState, pageIndex, page, box, clamped.Color, text, HyperPdfAnnotationKinds.GetTextBoxSubject());
        }
    }

    /// <summary>Reads a text box's text, look and place, from this viewer or another program.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The annotation index.</param>
    /// <returns>The content, or <see langword="null" /> when the annotation is not editable text.</returns>
    internal static TextBoxContent? GetTextBox(HyperPdfAnnotations annotationState, int pageIndex, int index)
    {
        lock (annotationState.Gate)
        {
            if (HyperPdfAnnotationReading.GetPage(annotationState, pageIndex) is not { } page || PdfPageAnnotations.Get(annotationState.Store, page, index) is not { } annotation
                || HyperPdfAnnotationKinds.GetKind(annotationState, annotation) != AnnotationKind.TextBox || HyperPdfAnnotationKinds.IsRemoved(annotationState, annotation))
            {
                return null;
            }

            var bounds = HyperPdfAnnotationReading.ToPageRect(page, PdfAnnotations.GetRectangle(annotation));
            var text = PdfAnnotations.GetText(annotation, annotationState.Names.Text);
            if (TextFormatCodec.TryRead(PdfAnnotations.GetText(annotation, annotationState.Names.Format), out var format, out var wrap))
            {
                return ToPhysical(new(text, format, bounds, wrap), page.UserUnit);
            }

            return ToPhysical(
                annotation.IsName(KnownName.Subtype, KnownName.Stamp) ? OlderTextBox(annotationState, annotation, text, bounds) : ForeignTextBox(annotation, bounds),
                page.UserUnit);
        }
    }

    /// <summary>Scales a text box's font size and wrap width, stored in user space units, to physical points.</summary>
    /// <param name="content">The content, or <see langword="null"/>.</param>
    /// <param name="userUnit">The page's user unit.</param>
    /// <returns>The content in physical points.</returns>
    internal static TextBoxContent? ToPhysical(TextBoxContent? content, float userUnit) =>
        content is null ? null : content with { Format = content.Format with { FontSize = content.Format.FontSize * userUnit }, WrapWidth = content.WrapWidth * userUnit };

    /// <summary>
    /// Gets how far below a text box's top its first baseline is written, in points, in the font the text would be
    /// written in. An editor showing the text lines its own baseline up with this so the text does not move when kept.
    /// </summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="text">The text, which picks a fallback font when the chosen one lacks some of it.</param>
    /// <param name="format">How the text looks.</param>
    /// <returns>The baseline's depth, or <see cref="F:System.Single.NaN" /> when no font can be loaded.</returns>
    internal static float GetFirstBaseline(HyperPdfAnnotations annotationState, string text, TextFormat format)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(format);
        var clamped = format.Clamped();
        var font = TextBoxFonts.Resolve(
            annotationState.FontState,
            HyperPdfAnnotationReading.GetFontCatalog(annotationState),
            clamped,
            text);
        return font is not null ? TextBoxLayout.FirstBaseline(clamped, font.Shaper) : float.NaN;
    }

    /// <summary>Reads free text written by another program from its contents and its default appearance and style.</summary>
    /// <param name="annotation">The free text annotation.</param>
    /// <param name="bounds">Its bounds.</param>
    /// <returns>The content.</returns>
    internal static TextBoxContent ForeignTextBox(PdfDictionary annotation, PageRect bounds)
    {
        var text = PdfAnnotations.GetText(annotation, KnownName.Contents);
        var format = FreeTextStyle.ApplyDefaultAppearance(PdfAnnotations.GetText(annotation, KnownName.DA), TextFormat.Default);
        format = FreeTextStyle.ApplyDefaultStyle(PdfAnnotations.GetText(annotation, KnownName.DS), format).Clamped();
        return new(text, format, bounds, bounds.Width);
    }

    /// <summary>Finds where a cluster's characters end: the nearest later cluster, or the end of the text, stopping at a line break.</summary>
    /// <param name="text">The text.</param>
    /// <param name="glyphs">Every laid out glyph.</param>
    /// <param name="cluster">The index of the cluster's first character.</param>
    /// <returns>The index after its last character.</returns>
    internal static int ClusterEnd(string text, ReadOnlySpan<LaidGlyph> glyphs, int cluster)
    {
        var end = text.Length;
        foreach (var other in glyphs)
        {
            if (other.Cluster > cluster && other.Cluster < end)
            {
                end = other.Cluster;
            }
        }

        var newline = text.AsSpan(cluster, end - cluster).IndexOfAny('\r', '\n');
        return newline < 0 ? end : cluster + newline;
    }

    /// <summary>Writes each line's glyph runs and underline.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="layout">The laid out text.</param>
    /// <param name="writer">How glyphs are written.</param>
    /// <param name="format">The format.</param>
    /// <param name="origin">The box's top-left corner.</param>
    internal static void WriteGlyphLines(ref PdfContentBuilder builder, TextBoxLayout layout, GlyphWriter writer, TextFormat format, Vector2 origin)
    {
        var glyphs = layout.Glyphs;
        foreach (var line in layout.Lines)
        {
            var end = line.GlyphStart + line.GlyphCount;
            var runStart = line.GlyphStart;
            for (var i = line.GlyphStart + 1; i <= end; i++)
            {
                // A glyph raised or lowered from the baseline, such as a placed mark, starts a run of its own.
                if (i < end && Math.Abs(glyphs[i].Y - glyphs[runStart].Y) < BaselineTolerance)
                {
                    continue;
                }

                WriteRun(ref builder, writer, format, glyphs, glyphs[runStart..i], origin);
                runStart = i;
            }

            WriteUnderline(ref builder, layout, line, format, origin);
        }
    }

    /// <summary>Draws a line's underline as a filled bar under its ink.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="layout">The laid out text.</param>
    /// <param name="line">The line.</param>
    /// <param name="format">The format.</param>
    /// <param name="origin">The box's top-left corner.</param>
    internal static void WriteUnderline(ref PdfContentBuilder builder, TextBoxLayout layout, LaidLine line, TextFormat format, Vector2 origin)
    {
        if (!format.IsUnderline || line.Width <= 0)
        {
            return;
        }

        var thickness = Math.Max(layout.UnderlineThickness, MinUnderline);
        var y = origin.Y - (line.Baseline + layout.UnderlineOffset) - (thickness * HyperPdfAnnotationShapes.Half);
        builder.Rectangle(origin.X + line.Left, y, line.Width, thickness);
        builder.Fill();
    }

    /// <summary>Writes one run of glyphs on a shared baseline, each glyph placed exactly with <c>TJ</c> adjustments.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="writer">How glyphs are written.</param>
    /// <param name="format">The format.</param>
    /// <param name="all">Every laid out glyph.</param>
    /// <param name="run">The glyphs.</param>
    /// <param name="origin">The box's top-left corner.</param>
    internal static void WriteRun(ref PdfContentBuilder builder, GlyphWriter writer, TextFormat format, ReadOnlySpan<LaidGlyph> all, ReadOnlySpan<LaidGlyph> run, Vector2 origin)
    {
        if (run.IsEmpty)
        {
            return;
        }

        var size = format.FontSize;
        builder.BeginText();
        builder.SetFont(HyperPdfAnnotationText.GetFontResource(), size);
        if (writer.Font.FakeBold)
        {
            builder.SetTextRenderingMode(RenderFillStroke);
            builder.SetLineWidth(size * BoldStroke);
        }

        builder.SetTextMatrix(1, 0, writer.Font.FakeItalic ? ItalicSlant : 0, 1, origin.X + run[0].X, origin.Y - run[0].Y);
        if (writer.Embedded is { } embedded)
        {
            WriteEmbeddedGlyphs(ref builder, embedded, writer.Text, size, all, run);
        }
        else
        {
            WriteStandardGlyphs(ref builder, writer, size, run);
        }

        builder.EndText();
    }

    /// <summary>Writes a run in an embedded font through the library, which gives each glyph its code, records its text and marks clusters that need <c>/ActualText</c>.</summary>
    /// <param name="builder">The content, inside a text object.</param>
    /// <param name="embedded">The embedded font.</param>
    /// <param name="text">The whole text that was laid out.</param>
    /// <param name="size">The font size.</param>
    /// <param name="all">Every laid out glyph, which tells where each cluster ends.</param>
    /// <param name="run">The glyphs.</param>
    internal static void WriteEmbeddedGlyphs(ref PdfContentBuilder builder, PdfEmbeddedFont embedded, string text, float size, ReadOnlySpan<LaidGlyph> all, ReadOnlySpan<LaidGlyph> run)
    {
        var first = text.Length;
        var last = 0;
        foreach (var glyph in run)
        {
            first = Math.Min(first, glyph.Cluster);
            last = Math.Max(last, ClusterEnd(text, all, glyph.Cluster));
        }

        var shaped = new PdfShapedGlyph[run.Length];
        for (var i = 0; i < run.Length; i++)
        {
            var advance = i + 1 < run.Length ? (run[i + 1].X - run[i].X) / size : embedded.Source.GetAdvance(run[i].Glyph) / Thousand;
            shaped[i] = new(run[i].Glyph, run[i].Cluster - first, advance, 0, 0);
        }

        embedded.ShowGlyphs(ref builder, text.AsSpan(first, Math.Max(last - first, 0)), shaped, size);
    }

    /// <summary>Writes a run in a built in font, each glyph placed exactly with <c>TJ</c> adjustments.</summary>
    /// <param name="builder">The content, inside a text object.</param>
    /// <param name="writer">How glyphs are written.</param>
    /// <param name="size">The font size.</param>
    /// <param name="run">The glyphs.</param>
    internal static void WriteStandardGlyphs(ref PdfContentBuilder builder, GlyphWriter writer, float size, ReadOnlySpan<LaidGlyph> run)
    {
        builder.BeginTextArray();

        // The code goes to the content builder, which may keep a span it is given, so it lives in a pooled array.
        var code = ArrayPool<byte>.Shared.Rent(CodeBytes);
        try
        {
            for (var i = 0; i < run.Length; i++)
            {
                builder.AddTextArrayString(code.AsSpan(0, GlyphWriter.Encode(run[i].Glyph, code)));
                var adjustment = i + 1 < run.Length ? writer.Width(run[i].Glyph) - ((run[i + 1].X - run[i].X) * Thousand / size) : 0;
                if (Math.Abs(adjustment) < MinAdjustment)
                {
                    continue;
                }

                builder.AddTextArrayAdjustment(adjustment);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(code);
        }

        builder.EndTextArray();
    }

    /// <summary>Reads a text box written by an older version of this viewer: Helvetica at a recorded size.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="annotation">The stamp.</param>
    /// <param name="text">Its text.</param>
    /// <param name="bounds">Its bounds.</param>
    /// <returns>The content, or <see langword="null"/> without text.</returns>
    internal static TextBoxContent? OlderTextBox(HyperPdfAnnotations annotationState, PdfDictionary annotation, string text, PageRect bounds)
    {
        if (text.Length == 0)
        {
            return null;
        }

        var size = PdfAnnotations.GetNumberText(annotation, annotationState.Names.FontSize);
        var format = TextFormat.Default with
        {
            FontSize = size > 0 ? size : TextFormat.Default.FontSize,
            Color = HyperPdfAnnotationKinds.GetColor(
            annotationState,
            annotation,
            AnnotationKind.TextBox)
        };
        return new(text, format, bounds, 0);
    }

    /// <summary>Records the text, format and the standard free text entries.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="box">The annotation.</param>
    /// <param name="text">The text.</param>
    /// <param name="format">The format.</param>
    /// <param name="wrapWidth">The wrap width.</param>
    internal static void WriteTextBoxKeys(HyperPdfAnnotations annotationState, PdfDictionary box, string text, TextFormat format, float wrapWidth)
    {
        box.Set(KnownName.IT, PdfValue.FromName(annotationState.Names.TypeWriter));
        PdfAnnotations.SetText(box, KnownName.DA, FreeTextStyle.DefaultAppearance(format));
        PdfAnnotations.SetText(box, KnownName.DS, FreeTextStyle.DefaultStyle(format));
        PdfAnnotations.SetText(box, KnownName.RC, FreeTextStyle.RichText(text, format));
        PdfAnnotations.SetText(box, annotationState.Names.Text, text);
        PdfAnnotations.SetNumberText(box, annotationState.Names.FontSize, format.FontSize);
        PdfAnnotations.SetText(box, annotationState.Names.Format, TextFormatCodec.Write(format, wrapWidth));
    }

    /// <summary>Makes the font resource and code mapping the laid out glyphs are written with, embedding a subset when needed.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="font">The font.</param>
    /// <param name="text">The text.</param>
    /// <returns>The writer, or <see langword="null"/> when the font cannot be embedded.</returns>
    internal static GlyphWriter? CreateGlyphWriter(HyperPdfAnnotations annotationState, TextBoxFont font, string text)
    {
        if (font.Program is not { } program)
        {
            var standard = font.Standard!.Font;
            return new(PdfValue.FromDictionary(AppearanceFontMetrics.CreateFontDictionary(annotationState.Store, standard)), null, text, standard, font);
        }

        if (annotationState.FontState.GetSource(program).Source is not { } source)
        {
            return null;
        }

        var embedded = PdfEmbeddedFont.Create(annotationState.Store, source);
        return new(embedded.Resource, embedded, text, AppearanceFont.Helvetica, font);
    }

    /// <summary>Draws a text box's glyph runs and underlines into its appearance.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="box">The annotation.</param>
    /// <param name="rectangle">Its rectangle.</param>
    /// <param name="writer">How glyphs are written.</param>
    /// <param name="format">The format.</param>
    /// <param name="origin">The box's top-left corner.</param>
    internal static void WriteTextBoxAppearance(
        HyperPdfAnnotations annotationState,
        PdfDictionary box,
        PdfRectangle rectangle,
        GlyphWriter writer,
        TextFormat format,
        Vector2 origin)
    {
        var fonts = new PdfDictionary(annotationState.Store, 1);
        fonts.Set(annotationState.Store.Names.Intern(HyperPdfAnnotationText.GetFontResource()), writer.FontResource);
        var resources = new PdfDictionary(annotationState.Store, 1);
        resources.Set(KnownName.Font, PdfValue.FromDictionary(fonts));
        var builder = default(PdfContentBuilder);
        try
        {
            builder.SaveState();
            PdfAppearances.SetColors(ref builder, format.Color);
            WriteGlyphLines(ref builder, annotationState.Layout, writer, format, origin);
            builder.RestoreState();
            _ = PdfAnnotations.SetNormalAppearance(annotationState.Store, box, builder.ToFormXObject(annotationState.Store, rectangle, resources));
        }
        finally
        {
            builder.Dispose();
            writer.Finish();
        }
    }
}
