// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Draws push button appearances: the background and border, the icon (<c>/MK /I</c>) and the caption (<c>/MK /CA</c>), laid
/// out by the text position <c>/MK /TP</c> and the icon fit <c>/MK /IF</c> (ISO 32000-2 12.5.6.19, Tables 189 and 190).
/// </summary>
internal static class FormPushButtonAppearance
{
    /// <summary>The text position that shows only the caption.</summary>
    private const int CaptionOnly = 0;

    /// <summary>The text position that shows only the icon.</summary>
    private const int IconOnly = 1;

    /// <summary>The text position that puts the caption below the icon.</summary>
    private const int CaptionBelow = 2;

    /// <summary>The text position that puts the caption above the icon.</summary>
    private const int CaptionAbove = 3;

    /// <summary>The text position that puts the caption to the right of the icon.</summary>
    private const int CaptionRight = 4;

    /// <summary>The text position that puts the caption to the left of the icon.</summary>
    private const int CaptionLeft = 5;

    /// <summary>The text position that draws the caption over the icon.</summary>
    private const int CaptionOverlaid = 6;

    /// <summary>The smallest size a caption is fitted to.</summary>
    private const float MinimumFitSize = 4;

    /// <summary>The largest size a caption is fitted to.</summary>
    private const float MaximumFitSize = 12;

    /// <summary>One thousand: font measurements are in thousandths of an em.</summary>
    private const float GlyphUnits = 1000;

    /// <summary>Half, to centre a caption.</summary>
    private const float Half = 0.5F;

    /// <summary>The numbers of a matrix.</summary>
    private const int MatrixNumbers = 6;

    /// <summary>The position of the x translation in a matrix.</summary>
    private const int TranslateX = 4;

    /// <summary>The position of the y translation in a matrix.</summary>
    private const int TranslateY = 5;

    /// <summary>Creates the appearance of a push button.</summary>
    /// <param name="context">The widget.</param>
    /// <returns>The Form XObject.</returns>
    internal static PdfStream Create(in AppearanceContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        var frame = FormFrame.Create(context);
        var da = context.DefaultAppearance;
        var characteristics = context.Characteristics;
        var position = ReadPosition(characteristics, context.Store.Names);
        var icon = position == CaptionOnly ? default : characteristics?.GetRaw(KnownName.I) ?? default;
        var iconForm = StoreReading.Resolve(context.Store, icon).AsStream();
        var font = FormFont.Find(context.Store, da.FontName, context.Widget, context.Form, context.PageResources, context.Fonts);
        var caption = position == IconOnly ? string.Empty : characteristics?.GetText(KnownName.CA) ?? string.Empty;
        var parts = new FormPushParts(position, iconForm, font.Encode(caption));
        var builder = default(PdfContentBuilder);
        try
        {
            FormBorderAppearance.Paint(ref builder, frame, characteristics, false);
            WriteContent(ref builder, context, da, frame, parts, font);
            return FormAppearanceResources.Finish(ref builder, context, frame, caption.Length > 0 ? [font] : [], iconForm is null ? default : icon);
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Reads the text position (<c>/MK /TP</c>).</summary>
    /// <param name="characteristics">The widget's <c>/MK</c>, or <see langword="null"/>.</param>
    /// <param name="names">The document's names.</param>
    /// <returns>The position; caption only when missing or out of range.</returns>
    internal static int ReadPosition(PdfDictionary? characteristics, PdfNameTable names)
    {
        var position = characteristics?.GetInt32(names.Intern("TP"u8)) ?? CaptionOnly;
        return position is >= CaptionOnly and <= CaptionOverlaid ? position : CaptionOnly;
    }

    /// <summary>Writes the icon and caption, clipped to the inside of the border.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="context">The widget.</param>
    /// <param name="da">The default appearance.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="parts">The position, icon and caption.</param>
    /// <param name="font">The caption's font.</param>
    private static void WriteContent(ref PdfContentBuilder builder, in AppearanceContext context, DefaultAppearance da, in FormFrame frame, FormPushParts parts, FormFont font)
    {
        var border = frame.Border.Width;
        var inner = new FormBox(border, border, frame.InnerWidth, frame.InnerHeight);
        var icon = parts.Icon;
        var codes = parts.Codes;
        if (inner.Width <= 0 || inner.Height <= 0 || (icon is null && codes.Length == 0))
        {
            return;
        }

        var size = da.FontSize > 0 ? da.FontSize : ChooseSize(font, codes, inner);
        var captionWidth = codes.Measure(0, codes.Length) * size / GlyphUnits;
        var captionHeight = codes.Length == 0 ? 0 : (font.Ascent - font.Descent) * size / GlyphUnits;
        Split(parts.Position, inner, captionWidth, captionHeight, out var iconArea, out var captionArea);

        builder.SaveState();
        builder.Rectangle(inner.X, inner.Y, inner.Width, inner.Height);
        builder.Clip();
        builder.EndPath();
        if (icon is not null)
        {
            var fit = FormIconFit.Read(context.Characteristics, context.Store.Names);
            WriteIcon(ref builder, icon, fit, fit.FitToBounds ? new(0, 0, frame.Width, frame.Height) : iconArea);
        }

        WriteCaption(ref builder, da, font, codes, size, captionArea, captionWidth);
        builder.RestoreState();
    }

    /// <summary>Divides the space inside the border between the icon and the caption.</summary>
    /// <param name="position">The text position.</param>
    /// <param name="inner">The space inside the border.</param>
    /// <param name="captionWidth">The caption's width.</param>
    /// <param name="captionHeight">The caption's height.</param>
    /// <param name="iconArea">Receives the icon's space.</param>
    /// <param name="captionArea">Receives the caption's space.</param>
    private static void Split(int position, in FormBox inner, float captionWidth, float captionHeight, out FormBox iconArea, out FormBox captionArea)
    {
        var height = Math.Min(captionHeight, inner.Height);
        var width = Math.Min(captionWidth, inner.Width);
        switch (position)
        {
            case CaptionBelow:
            {
                captionArea = new(inner.X, inner.Y, inner.Width, height);
                iconArea = new(inner.X, inner.Y + height, inner.Width, inner.Height - height);
                break;
            }

            case CaptionAbove:
            {
                captionArea = new(inner.X, inner.Top - height, inner.Width, height);
                iconArea = new(inner.X, inner.Y, inner.Width, inner.Height - height);
                break;
            }

            case CaptionRight:
            {
                captionArea = new(inner.Right - width, inner.Y, width, inner.Height);
                iconArea = new(inner.X, inner.Y, inner.Width - width, inner.Height);
                break;
            }

            case CaptionLeft:
            {
                captionArea = new(inner.X, inner.Y, width, inner.Height);
                iconArea = new(inner.X + width, inner.Y, inner.Width - width, inner.Height);
                break;
            }

            default:
            {
                captionArea = inner;
                iconArea = inner;
                break;
            }
        }
    }

    /// <summary>Chooses the size of a caption that fits the space.</summary>
    /// <param name="font">The font.</param>
    /// <param name="codes">The caption's codes.</param>
    /// <param name="inner">The space inside the border.</param>
    /// <returns>The size.</returns>
    private static float ChooseSize(FormFont font, FormCodes codes, in FormBox inner)
    {
        var lineHeight = (font.Ascent - font.Descent) / GlyphUnits;
        var size = Math.Clamp(inner.Height / lineHeight, MinimumFitSize, MaximumFitSize);
        var width = codes.Measure(0, codes.Length) * size / GlyphUnits;
        return width > inner.Width && width > 0 ? Math.Max(MinimumFitSize, size * inner.Width / width) : size;
    }

    /// <summary>Draws the icon in its space.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="icon">The icon form.</param>
    /// <param name="fit">How the icon is fitted.</param>
    /// <param name="area">The icon's space.</param>
    private static void WriteIcon(ref PdfContentBuilder builder, PdfStream icon, FormIconFit fit, in FormBox area)
    {
        if (!TryMeasureIcon(icon.Dictionary, out var extent) || area.Width <= 0 || area.Height <= 0)
        {
            return;
        }

        var placement = fit.Place(extent.Width, extent.Height, area.Width, area.Height);
        builder.SaveState();
        builder.Transform(
            placement.ScaleX,
            0,
            0,
            placement.ScaleY,
            area.X + placement.OffsetX - (extent.Left * placement.ScaleX),
            area.Y + placement.OffsetY - (extent.Bottom * placement.ScaleY));
        builder.DrawXObject(FormAppearanceResources.IconName);
        builder.RestoreState();
    }

    /// <summary>Finds the box an icon form covers on the page: its <c>/BBox</c> run through its <c>/Matrix</c>.</summary>
    /// <param name="icon">The icon form's dictionary.</param>
    /// <param name="extent">Receives the box.</param>
    /// <returns><see langword="false"/> when the form has no usable <c>/BBox</c>.</returns>
    private static bool TryMeasureIcon(PdfDictionary icon, out PdfRectangle extent)
    {
        extent = default;
        if (!icon.TryGetRectangle(KnownName.BBox, out var box) || box.IsEmpty)
        {
            return false;
        }

        Span<float> matrix = [1, 0, 0, 1, 0, 0];
        if (icon.GetArray(KnownName.Matrix) is { Count: MatrixNumbers } numbers)
        {
            for (var i = 0; i < MatrixNumbers; i++)
            {
                matrix[i] = numbers.GetSingle(i);
            }
        }

        extent = Transform(box, matrix);
        return !extent.IsEmpty;
    }

    /// <summary>Finds the box round a transformed rectangle.</summary>
    /// <param name="box">The rectangle.</param>
    /// <param name="matrix">The six numbers of the matrix.</param>
    /// <returns>The box.</returns>
    private static PdfRectangle Transform(PdfRectangle box, ReadOnlySpan<float> matrix)
    {
        var x0 = MapX(matrix, box.Left, box.Bottom);
        var y0 = MapY(matrix, box.Left, box.Bottom);
        var x1 = MapX(matrix, box.Right, box.Bottom);
        var y1 = MapY(matrix, box.Right, box.Bottom);
        var x2 = MapX(matrix, box.Left, box.Top);
        var y2 = MapY(matrix, box.Left, box.Top);
        var x3 = MapX(matrix, box.Right, box.Top);
        var y3 = MapY(matrix, box.Right, box.Top);
        return new(
            Math.Min(Math.Min(x0, x1), Math.Min(x2, x3)),
            Math.Min(Math.Min(y0, y1), Math.Min(y2, y3)),
            Math.Max(Math.Max(x0, x1), Math.Max(x2, x3)),
            Math.Max(Math.Max(y0, y1), Math.Max(y2, y3)));
    }

    /// <summary>Maps the x of a point through a matrix.</summary>
    /// <param name="matrix">The six numbers of the matrix.</param>
    /// <param name="x">The point's x.</param>
    /// <param name="y">The point's y.</param>
    /// <returns>The new x.</returns>
    private static float MapX(ReadOnlySpan<float> matrix, float x, float y) => (matrix[0] * x) + (matrix[2] * y) + matrix[TranslateX];

    /// <summary>Maps the y of a point through a matrix.</summary>
    /// <param name="matrix">The six numbers of the matrix.</param>
    /// <param name="x">The point's x.</param>
    /// <param name="y">The point's y.</param>
    /// <returns>The new y.</returns>
    private static float MapY(ReadOnlySpan<float> matrix, float x, float y) => (matrix[1] * x) + (matrix[3] * y) + matrix[TranslateY];

    /// <summary>Writes the caption, centred in its space.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="da">The default appearance.</param>
    /// <param name="font">The font.</param>
    /// <param name="codes">The caption's codes.</param>
    /// <param name="size">The font size.</param>
    /// <param name="area">The caption's space.</param>
    /// <param name="width">The caption's width.</param>
    private static void WriteCaption(ref PdfContentBuilder builder, DefaultAppearance da, FormFont font, FormCodes codes, float size, in FormBox area, float width)
    {
        if (codes.Length == 0)
        {
            return;
        }

        var line = TextLayout.FirstLine(codes, size);
        builder.BeginMarkedContent("Tx"u8);
        builder.BeginText();
        builder.WriteRaw(Encoding.ASCII.GetBytes($"{da.Color}\n"));
        builder.SetFont(font.NameBytes, size);
        builder.MoveText(
            area.X + ((area.Width - Math.Min(width, line.Width)) * Half),
            area.Y + ((area.Height - ((font.Ascent - font.Descent) * size / GlyphUnits)) * Half) - (font.Descent * size / GlyphUnits));
        builder.ShowText(codes.Slice(line.Start, line.Length));
        builder.EndText();
        builder.EndMarkedContent();
    }
}
