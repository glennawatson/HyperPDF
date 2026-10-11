// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
namespace PdfViewerLite.HyperPdf;

/// <summary>Performs HyperPdfAnnotationText annotation operations.</summary>
internal static class HyperPdfAnnotationText
{
    /// <summary>The line height as a multiple of the font size.</summary>
    internal const float LineHeight = 1.25F;

    /// <summary>How far below the top of a line its baseline sits, as a multiple of the font size.</summary>
    internal const float Ascent = 0.8F;

    /// <summary>The most lines written; later lines are dropped, as the PDFium engine drops them.</summary>
    internal const int MaxLines = 128;

    /// <summary>The font size of a stamp's word.</summary>
    internal const float StampFontSize = 18;

    /// <summary>The space between a stamp's word and its frame.</summary>
    internal const float StampPadding = 4;

    /// <summary>The width of a stamp's frame.</summary>
    internal const float StampFrame = 2;

    /// <summary>Gets FontResource.</summary>
    /// <returns>The current value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<byte> GetFontResource() => "F1"u8;

    /// <summary>Writes text on the page, or places a typed signature.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="location">The top-left corner of the first line.</param>
    /// <param name="text">The text; line breaks start new lines.</param>
    /// <param name="fontSize">The font size in points.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <param name="kind">
    /// <see cref="F:PdfViewerLite.Core.Annotations.AnnotationKind.TextBox" /> or <see cref="F:PdfViewerLite.Core.Annotations.AnnotationKind.Signature" />.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    internal static int AddText(HyperPdfAnnotations annotationState, int pageIndex, PagePoint location, string text, float fontSize, uint color, AnnotationKind kind)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrWhiteSpace(text))
        {
            return -1;
        }

        var signature = kind == AnnotationKind.Signature;
        var font = signature ? AppearanceFont.TimesItalic : AppearanceFont.Helvetica;
        lock (annotationState.Gate)
        {
            if (HyperPdfAnnotationReading.GetPage(annotationState, pageIndex) is not { } page)
            {
                return -1;
            }

            var origin = HyperPdfAnnotationReading.ToUser(page, location);
            var bounds = MeasureLines(font, fontSize, text, origin);
            if (!bounds.IsSet)
            {
                return -1;
            }

            var rectangle = bounds.ToRectangle(0);
            var stamp = PdfAnnotations.Create(annotationState.Store, KnownName.Stamp, rectangle);
            if (!signature)
            {
                PdfAnnotations.SetText(stamp, annotationState.Names.Text, text);
                PdfAnnotations.SetNumberText(stamp, annotationState.Names.FontSize, fontSize);
            }

            SetTextAppearance(annotationState, stamp, rectangle, new(font, fontSize), color, text, origin);
            return HyperPdfAnnotationReading.Add(annotationState, pageIndex, page, stamp, color, string.Empty, signature
                ? HyperPdfAnnotationKinds.GetSignatureSubject()
                : HyperPdfAnnotationKinds.GetTextBoxSubject());
        }
    }

    /// <summary>Places a stamp: a framed word such as "APPROVED".</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="location">The top-left corner of the stamp.</param>
    /// <param name="label">The word on the stamp.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    internal static int AddStamp(HyperPdfAnnotations annotationState, int pageIndex, PagePoint location, string label, uint color)
    {
        ArgumentNullException.ThrowIfNull(label);
        if (string.IsNullOrWhiteSpace(label))
        {
            return -1;
        }

        lock (annotationState.Gate)
        {
            if (HyperPdfAnnotationReading.GetPage(annotationState, pageIndex) is not { } page)
            {
                return -1;
            }

            var origin = HyperPdfAnnotationReading.ToUser(page, new(location.X + StampPadding + StampFrame, location.Y + StampPadding + StampFrame));
            var ink = MeasureLines(AppearanceFont.Helvetica, StampFontSize, label, origin);
            if (!ink.IsSet)
            {
                return -1;
            }

            var rectangle = ink.ToRectangle(StampPadding + StampFrame);
            var stamp = PdfAnnotations.Create(annotationState.Store, KnownName.Stamp, rectangle);
            var builder = default(PdfContentBuilder);
            try
            {
                builder.SaveState();
                PdfAppearances.SetColors(ref builder, color);
                var box = ink.ToRectangle(StampPadding);
                builder.SetLineWidth(StampFrame);
                builder.Rectangle(box.Left, box.Bottom, box.Width, box.Height);
                builder.Stroke();
                WriteLines(ref builder, new(AppearanceFont.Helvetica, StampFontSize), label, origin);
                builder.RestoreState();
                _ = PdfAnnotations.SetNormalAppearance(
                    annotationState.Store,
                    stamp,
                    builder.ToFormXObject(
                        annotationState.Store,
                        rectangle,
                        CreateFontResources(annotationState, AppearanceFont.Helvetica)));
            }
            finally
            {
                builder.Dispose();
            }

            return HyperPdfAnnotationReading.Add(annotationState, pageIndex, page, stamp, color, label, HyperPdfAnnotationKinds.GetStampSubject());
        }
    }

    /// <summary>Changes the text size of a text box or callout written by this viewer.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The annotation index.</param>
    /// <param name="fontSize">The font size in points.</param>
    /// <returns>
    /// <see langword="true" /> when changed.</returns>
    internal static bool SetFontSize(HyperPdfAnnotations annotationState, int pageIndex, int index, float fontSize)
    {
        if (fontSize <= 0)
        {
            return false;
        }

        lock (annotationState.Gate)
        {
            if (!HyperPdfAnnotationReading.TryEdit(annotationState, pageIndex, index, out var page, out var annotation) || !CanLayOutAgain(annotationState, annotation, out var kind))
            {
                return false;
            }

            var text = PdfAnnotations.GetText(annotation, annotationState.Names.Text);
            var color = HyperPdfAnnotationKinds.GetColor(annotationState, annotation, kind);
            if (kind == AnnotationKind.Callout)
            {
                HyperPdfAnnotationCallouts.RebuildCallout(annotationState, annotation, text, fontSize, color);
            }
            else
            {
                RelayText(annotationState, annotation, text, fontSize, color);
            }

            HyperPdfAnnotationReading.SetModified(annotationState, annotation);
            return HyperPdfAnnotationReading.Commit(annotationState, pageIndex, page, index, annotation);
        }
    }

    /// <summary>Measures the ink of lines of text, the first line's top at a corner.</summary>
    /// <param name="font">The font.</param>
    /// <param name="size">The size.</param>
    /// <param name="text">The text; line breaks start new lines.</param>
    /// <param name="origin">The first line's top-left corner.</param>
    /// <returns>The bounds, unset when no line has a character the font can show.</returns>
    internal static UserBounds MeasureLines(AppearanceFont font, float size, ReadOnlySpan<char> text, Vector2 origin)
    {
        var bounds = default(UserBounds);
        var builder = default(PdfContentBuilder);
        try
        {
            VisitLines(ref builder, new(font, size), text, origin, ref bounds, false);
        }
        finally
        {
            builder.Dispose();
        }

        return bounds;
    }

    /// <summary>Writes lines of text, the first line's top at a corner.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="style">The font and size.</param>
    /// <param name="text">The text.</param>
    /// <param name="origin">The first line's top-left corner.</param>
    internal static void WriteLines(ref PdfContentBuilder builder, TextLook style, ReadOnlySpan<char> text, Vector2 origin)
    {
        var bounds = default(UserBounds);
        VisitLines(ref builder, style, text, origin, ref bounds, true);
    }

    /// <summary>Measures, and optionally writes, each line of text.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="style">The font and size.</param>
    /// <param name="text">The text.</param>
    /// <param name="origin">The first line's top-left corner.</param>
    /// <param name="bounds">The ink bounds so far.</param>
    /// <param name="write">Whether to write the lines.</param>
    internal static void VisitLines(ref PdfContentBuilder builder, TextLook style, ReadOnlySpan<char> text, Vector2 origin, ref UserBounds bounds, bool write)
    {
        var count = 0;
        foreach (var line in text.EnumerateLines())
        {
            if (count == MaxLines)
            {
                return;
            }

            var baseline = origin.Y - (style.Size * Ascent) - (count * style.Size * LineHeight);
            VisitLine(ref builder, style, line, new(origin.X, baseline), ref bounds, write);
            count++;
        }
    }

    /// <summary>Measures, and optionally writes, one line of text in WinAnsi codes.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="style">The font and size.</param>
    /// <param name="line">The line.</param>
    /// <param name="start">Where the baseline starts.</param>
    /// <param name="bounds">The ink bounds so far.</param>
    /// <param name="write">Whether to write the line.</param>
    internal static void VisitLine(ref PdfContentBuilder builder, TextLook style, ReadOnlySpan<char> line, Vector2 start, ref UserBounds bounds, bool write)
    {
        // The codes go to the content builder, which may keep a span it is given, so they live in a pooled array.
        var rented = ArrayPool<byte>.Shared.Rent(Math.Max(line.Length, 1));
        var codes = rented.AsSpan();
        try
        {
            var count = AppearanceFontMetrics.Encode(line, codes);
            if (count == 0)
            {
                return;
            }

            var ink = AppearanceFontMetrics.MeasureInk(style.Font, codes[..count], style.Size);
            bounds.AddRectangle(new(start.X + ink.Left, start.Y + ink.Bottom, start.X + ink.Right, start.Y + ink.Top));
            if (!write)
            {
                return;
            }

            builder.BeginText();
            builder.SetFont(GetFontResource(), style.Size);
            builder.SetTextMatrix(1, 0, 0, 1, start.X, start.Y);
            builder.ShowText(codes[..count]);
            builder.EndText();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>Creates resources holding one built in font as <c>/F1</c>.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="font">The font.</param>
    /// <returns>The resources.</returns>
    internal static PdfDictionary CreateFontResources(HyperPdfAnnotations annotationState, AppearanceFont font)
    {
        var fonts = new PdfDictionary(annotationState.Store, 1);
        fonts.Set(annotationState.Store.Names.Intern(GetFontResource()), PdfValue.FromDictionary(AppearanceFontMetrics.CreateFontDictionary(annotationState.Store, font)));
        var resources = new PdfDictionary(annotationState.Store, 1);
        resources.Set(KnownName.Font, PdfValue.FromDictionary(fonts));
        return resources;
    }

    /// <summary>Determines whether an annotation is a text box or callout written here with a built in font, which can be laid out again.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="kind">Its kind.</param>
    /// <returns><see langword="true"/> when it can.</returns>
    internal static bool CanLayOutAgain(HyperPdfAnnotations annotationState, PdfDictionary annotation, out AnnotationKind kind)
    {
        kind = HyperPdfAnnotationKinds.GetKind(annotationState, annotation) ?? AnnotationKind.Other;
        var shape = annotation.IsName(KnownName.Subtype, KnownName.Stamp) || (kind == AnnotationKind.Callout && annotation.IsName(KnownName.Subtype, KnownName.FreeText));

        // Formatted text boxes are laid out again by writing them anew, so their fonts and settings are kept.
        return shape && kind is AnnotationKind.TextBox or AnnotationKind.Callout && !HyperPdfAnnotationKinds.IsRemoved(annotationState, annotation)
            && HyperPdfAnnotationEditing.IsWrittenHere(annotationState, annotation) && !PdfAnnotations.HasText(annotation, annotationState.Names.Format);
    }

    /// <summary>Lays a text box out again at a new size, keeping its top-left corner where its ink starts.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="annotation">The text box.</param>
    /// <param name="text">The text.</param>
    /// <param name="fontSize">The new size.</param>
    /// <param name="color">The colour.</param>
    internal static void RelayText(HyperPdfAnnotations annotationState, PdfDictionary annotation, string text, float fontSize, uint color)
    {
        var old = PdfAnnotations.GetRectangle(annotation);
        var corner = new Vector2(old.Left, old.Top);
        var ink = MeasureLines(AppearanceFont.Helvetica, fontSize, text, corner).ToRectangle(0);
        var origin = corner + new Vector2(corner.X - ink.Left, corner.Y - ink.Top);
        var rectangle = new PdfRectangle(old.Left, old.Top - ink.Height, old.Left + ink.Width, old.Top);
        PdfAnnotations.SetRectangle(annotation, rectangle);
        PdfAnnotations.SetNumberText(annotation, annotationState.Names.FontSize, fontSize);
        SetTextAppearance(annotationState, annotation, rectangle, new(AppearanceFont.Helvetica, fontSize), color, text, origin);
    }

    /// <summary>Gives an annotation an appearance of lines of text.</summary>
    /// <param name="annotationState">The annotation state.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="rectangle">Its rectangle, the appearance's box.</param>
    /// <param name="style">The font and size.</param>
    /// <param name="color">The colour.</param>
    /// <param name="text">The text.</param>
    /// <param name="origin">The first line's top-left corner.</param>
    internal static void SetTextAppearance(HyperPdfAnnotations annotationState, PdfDictionary annotation, PdfRectangle rectangle, TextLook style, uint color, string text, Vector2 origin)
    {
        var builder = default(PdfContentBuilder);
        try
        {
            builder.SaveState();
            PdfAppearances.SetColors(ref builder, color);
            WriteLines(ref builder, style, text, origin);
            builder.RestoreState();
            _ = PdfAnnotations.SetNormalAppearance(annotationState.Store, annotation, builder.ToFormXObject(annotationState.Store, rectangle, CreateFontResources(annotationState, style.Font)));
        }
        finally
        {
            builder.Dispose();
        }
    }
}
