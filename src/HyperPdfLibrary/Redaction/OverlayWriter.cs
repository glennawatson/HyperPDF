// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.PageObjects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Redaction;

/// <summary>Paints the look of applied redactions on a page: the fill colour, the overlay text or the overlay form.</summary>
internal static class OverlayWriter
{
    /// <summary>The bits red is shifted left by in 0xRRGGBB.</summary>
    private const int RedShift = 16;

    /// <summary>The bits green is shifted left by in 0xRRGGBB.</summary>
    private const int GreenShift = 8;

    /// <summary>The mask of one channel.</summary>
    private const uint ChannelMask = 0xFF;

    /// <summary>The highest channel value as a float.</summary>
    private const float ChannelMax = 255F;

    /// <summary>The weight of red in perceived brightness.</summary>
    private const float RedWeight = 0.299F;

    /// <summary>The weight of green in perceived brightness.</summary>
    private const float GreenWeight = 0.587F;

    /// <summary>The weight of blue in perceived brightness.</summary>
    private const float BlueWeight = 0.114F;

    /// <summary>The brightness below which the fill counts as dark and the text turns white.</summary>
    private const float DarkBelow = 0.5F;

    /// <summary>The share of an area's height automatic text takes.</summary>
    private const float AutoShare = 0.8F;

    /// <summary>The largest automatic text size in points.</summary>
    private const float AutoMaximum = 14;

    /// <summary>The distance between lines of repeated text, as a multiple of the size.</summary>
    private const float LineSpacing = 1.2F;

    /// <summary>The share of the size the baseline sits above the bottom of a centred line.</summary>
    private const float BaselineLift = 0.22F;

    /// <summary>The bytes encoded for one line of overlay text.</summary>
    private const int LineBytes = 512;

    /// <summary>Half, for centring.</summary>
    private const float Half = 0.5F;

    /// <summary>Appends the look of every mark to a content.</summary>
    /// <param name="content">The page content; the overlay and its resources are added to it.</param>
    /// <param name="marks">The redact annotations being applied.</param>
    internal static void Write(PdfPageContent content, IReadOnlyList<RedactMark> marks)
    {
        var builder = default(PdfContentBuilder);
        try
        {
            var font = default(PdfName);
            foreach (var mark in marks)
            {
                font = WriteMark(ref builder, content, mark, font);
            }

            if (builder.Length > 0)
            {
                PdfPageContentEditing.Append(content, builder.WrittenSpan);
            }
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Gets the perceived brightness of a colour.</summary>
    /// <param name="rgb">The colour as 0xRRGGBB.</param>
    /// <returns>The brightness from 0 to 1.</returns>
    private static float Brightness(uint rgb) =>
        ((((rgb >> RedShift) & ChannelMask) * RedWeight) + (((rgb >> GreenShift) & ChannelMask) * GreenWeight) + ((rgb & ChannelMask) * BlueWeight)) / ChannelMax;

    /// <summary>Sets the fill colour from 0xRRGGBB.</summary>
    /// <param name="builder">The output.</param>
    /// <param name="rgb">The colour.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SetFill(ref PdfContentBuilder builder, uint rgb) =>
        builder.SetFillRgb(((rgb >> RedShift) & ChannelMask) / ChannelMax, ((rgb >> GreenShift) & ChannelMask) / ChannelMax, (rgb & ChannelMask) / ChannelMax);

    /// <summary>Gets the size overlay text is drawn at.</summary>
    /// <param name="appearance">The appearance.</param>
    /// <param name="height">The height of the area.</param>
    /// <returns>The size in points.</returns>
    private static float TextSize(PdfRedactionAppearance appearance, float height) =>
        appearance.FontSize > 0 ? appearance.FontSize : Math.Min(height * AutoShare, AutoMaximum);

    /// <summary>Gets the colour overlay text is drawn in.</summary>
    /// <param name="appearance">The appearance.</param>
    /// <returns>The colour as 0xRRGGBB.</returns>
    private static uint TextColor(PdfRedactionAppearance appearance) =>
        appearance.TextColor ?? (appearance.FillColor is { } fill && Brightness(fill) < DarkBelow ? 0xFFFFFFU : 0U);

    /// <summary>Encodes overlay text as WinAnsi codes, repeated when asked until a line is full.</summary>
    /// <param name="appearance">The appearance.</param>
    /// <param name="size">The text size.</param>
    /// <param name="width">The width of the area.</param>
    /// <returns>The codes of one line.</returns>
    private static byte[] EncodeLine(PdfRedactionAppearance appearance, float size, float width)
    {
        Span<byte> buffer = stackalloc byte[LineBytes];
        var count = AppearanceFontMetrics.Encode(appearance.OverlayText, buffer);
        var once = buffer[..count].ToArray();
        if (!appearance.Repeat || once.Length == 0)
        {
            return once;
        }

        var line = new List<byte>();
        var separated = new List<byte>(once) { (byte)' ' };
        while (AppearanceFontMetrics.MeasureAdvance(AppearanceFont.Helvetica, [.. line], size) < width && line.Count < LineBytes)
        {
            line.AddRange(separated);
        }

        return [.. line];
    }

    /// <summary>Writes one mark.</summary>
    /// <param name="builder">The output.</param>
    /// <param name="content">The page content.</param>
    /// <param name="mark">The mark.</param>
    /// <param name="font">The font resource name made so far, or none.</param>
    /// <returns>The font resource name, made when the mark needed one.</returns>
    private static PdfName WriteMark(ref PdfContentBuilder builder, PdfPageContent content, RedactMark mark, PdfName font)
    {
        var info = mark.Info;

        // The overlay is page decoration, not content to read, so it is an artifact (ISO 14289-1 7.1).
        builder.BeginMarkedContent("Artifact"u8);
        builder.SaveState();
        try
        {
            if (mark.Annotation.Get(mark.Annotation.Owner!.Names.Intern("RO"u8)).AsStream() is { } overlay)
            {
                WriteForm(ref builder, content, mark, overlay);
                return font;
            }

            if (info.Appearance.FillColor is { } fill)
            {
                SetFill(ref builder, fill);
                foreach (var region in info.Regions)
                {
                    builder.Rectangle(region.Left, region.Bottom, region.Width, region.Height);
                    builder.Fill();
                }
            }

            if (info.Appearance.OverlayText.Length == 0)
            {
                return font;
            }

            font = font.IsNone ? AddFont(content) : font;
            WriteText(ref builder, content, info, font);
            return font;
        }
        finally
        {
            builder.RestoreState();
            builder.EndMarkedContent();
        }
    }

    /// <summary>Adds the Helvetica font overlay text uses.</summary>
    /// <param name="content">The page content.</param>
    /// <returns>The font's resource name.</returns>
    private static PdfName AddFont(PdfPageContent content)
    {
        var name = PdfPageContentEditing.AllocateName(content, KnownName.Font, "RdF");
        PdfPageContentEditing.AddResource(content, KnownName.Font, name, PdfValue.FromDictionary(AppearanceFontMetrics.CreateFontDictionary(content.Document.Objects, AppearanceFont.Helvetica)));
        return name;
    }

    /// <summary>Draws the overlay form scaled to fill the annotation's rectangle.</summary>
    /// <param name="builder">The output.</param>
    /// <param name="content">The page content.</param>
    /// <param name="mark">The mark.</param>
    /// <param name="overlay">The overlay form.</param>
    private static void WriteForm(ref PdfContentBuilder builder, PdfPageContent content, RedactMark mark, PdfStream overlay)
    {
        var bounds = mark.Info.Bounds;
        var box = overlay.Dictionary.TryGetRectangle(KnownName.BBox, out var found) ? found : bounds;
        var scaleX = box.Width > 0 ? bounds.Width / box.Width : 1;
        var scaleY = box.Height > 0 ? bounds.Height / box.Height : 1;
        var name = PdfPageContentEditing.AllocateName(content, KnownName.XObject, "RdO");
        var raw = mark.Annotation.GetRaw(mark.Annotation.Owner!.Names.Intern("RO"u8));
        PdfPageContentEditing.AddResource(content, KnownName.XObject, name, raw.IsReference ? raw : PdfValue.FromStream(overlay));
        builder.Transform(scaleX, 0, 0, scaleY, bounds.Left - (box.Left * scaleX), bounds.Bottom - (box.Bottom * scaleY));
        builder.DrawXObject(content.Document.Objects.Names.GetSpelling(name));
    }

    /// <summary>Draws the overlay text in the first area, or in every area when it repeats.</summary>
    /// <param name="builder">The output.</param>
    /// <param name="content">The page content.</param>
    /// <param name="info">The redaction.</param>
    /// <param name="font">The font resource name.</param>
    private static void WriteText(ref PdfContentBuilder builder, PdfPageContent content, PdfRedaction info, PdfName font)
    {
        var appearance = info.Appearance;
        SetFill(ref builder, TextColor(appearance));
        var spelling = content.Document.Objects.Names.GetSpelling(font);
        foreach (var region in info.Regions)
        {
            WriteTextIn(ref builder, appearance, region, spelling);
            if (!appearance.Repeat)
            {
                break;
            }
        }
    }

    /// <summary>Draws overlay text clipped to one area.</summary>
    /// <param name="builder">The output.</param>
    /// <param name="appearance">The appearance.</param>
    /// <param name="region">The area.</param>
    /// <param name="font">The font resource name.</param>
    private static void WriteTextIn(ref PdfContentBuilder builder, PdfRedactionAppearance appearance, PdfRectangle region, ReadOnlySpan<byte> font)
    {
        var size = TextSize(appearance, region.Height);
        if (size <= 0 || region.Width <= 0)
        {
            return;
        }

        var line = EncodeLine(appearance, size, region.Width);
        var width = AppearanceFontMetrics.MeasureAdvance(AppearanceFont.Helvetica, line, size);
        var x = appearance.Alignment switch
        {
            PdfRedactionAlignment.Centre => region.Left + ((region.Width - width) * Half),
            PdfRedactionAlignment.Right => region.Right - width,
            _ => region.Left,
        };
        builder.SaveState();
        builder.Rectangle(region.Left, region.Bottom, region.Width, region.Height);
        builder.Clip();
        builder.EndPath();
        var rows = appearance.Repeat ? Math.Max(1, (int)(region.Height / (size * LineSpacing))) : 1;
        for (var row = 0; row < rows; row++)
        {
            var baseline = appearance.Repeat
                ? region.Top - (size * LineSpacing * (row + 1)) + (size * (LineSpacing - 1))
                : region.Bottom + ((region.Height - size) * Half) + (size * BaselineLift);
            builder.BeginText();
            builder.SetFont(font, size);
            builder.MoveText(appearance.Repeat ? region.Left : x, baseline);
            builder.ShowText(line);
            builder.EndText();
        }

        builder.RestoreState();
    }
}
