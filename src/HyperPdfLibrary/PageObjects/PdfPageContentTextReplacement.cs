// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.PageObjects;

/// <summary>Replaces the text of a text object with new text written in a standard font at the same place, size and colour.</summary>
public static class PdfPageContentTextReplacement
{
    /// <summary>The prefix of the font resource name the new text uses.</summary>
    private const string FontPrefix = "EdF";

    /// <summary>The longest line, in bytes, that can be written.</summary>
    private const int LineBytes = 4096;

    /// <summary>The components of a gray colour.</summary>
    private const int GrayComponents = 1;

    /// <summary>The components of an RGB colour.</summary>
    private const int RgbComponents = 3;

    /// <summary>The components of a CMYK colour.</summary>
    private const int CmykComponents = 4;

    /// <summary>The index of the second colour component.</summary>
    private const int SecondComponent = 1;

    /// <summary>The index of the third colour component.</summary>
    private const int ThirdComponent = 2;

    /// <summary>The index of the fourth colour component.</summary>
    private const int FourthComponent = 3;

    /// <summary>Removes the glyphs of a text object and writes new text where the first glyph was.</summary>
    /// <param name="content">The page content that holds the object.</param>
    /// <param name="text">The text object to replace.</param>
    /// <param name="replacement">The new text; empty only removes the old text. Characters Windows ANSI cannot hold are written as question marks.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static void Replace(PdfPageContent content, PdfTextObject text, string replacement)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(replacement);
        if (text.GlyphCount == 0)
        {
            return;
        }

        var origin = text.Glyphs[0].Origin;
        var size = GetUserSize(text);
        var fill = text.FillPaint;
        for (var i = 0; i < text.GlyphCount; i++)
        {
            _ = text.RemoveGlyph(i);
        }

        if (replacement.Length == 0)
        {
            return;
        }

        Write(content, origin, size, fill, replacement);
    }

    /// <summary>Gets the size the text is drawn at in user space.</summary>
    /// <param name="text">The text object.</param>
    /// <returns>The size in user space units.</returns>
    private static float GetUserSize(PdfTextObject text)
    {
        var matrix = text.TextMatrix * text.Matrix;
        var scale = MathF.Sqrt((matrix.M21 * matrix.M21) + (matrix.M22 * matrix.M22));
        return text.FontSize * (scale > 0 ? scale : 1);
    }

    /// <summary>Appends the new text to the page content.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="origin">Where the baseline starts, in user space.</param>
    /// <param name="size">The font size.</param>
    /// <param name="fill">The colour to write in.</param>
    /// <param name="replacement">The text.</param>
    private static void Write(PdfPageContent content, Vector2 origin, float size, PdfPaint fill, string replacement)
    {
        var name = PdfPageContentEditing.AllocateName(content, KnownName.Font, FontPrefix);
        PdfPageContentEditing.AddResource(content, KnownName.Font, name, PdfValue.FromDictionary(AppearanceFontMetrics.CreateFontDictionary(content.Document.Objects, AppearanceFont.Helvetica)));
        var codes = new byte[Math.Min(replacement.Length, LineBytes)];
        var count = AppearanceFontMetrics.Encode(replacement.AsSpan(0, codes.Length), codes);
        var builder = default(PdfContentBuilder);
        try
        {
            builder.SaveState();
            SetFill(ref builder, fill);
            builder.BeginText();
            builder.SetFont(content.Document.Objects.Names.GetSpelling(name), size);
            builder.MoveText(origin.X, origin.Y);
            builder.ShowText(codes.AsSpan(0, count));
            builder.EndText();
            builder.RestoreState();
            PdfPageContentEditing.Append(content, builder.WrittenSpan);
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Sets the fill colour from a device paint, or black when the paint is not a device colour.</summary>
    /// <param name="builder">The output.</param>
    /// <param name="paint">The paint.</param>
    private static void SetFill(ref PdfContentBuilder builder, PdfPaint paint)
    {
        var c = paint.Components;
        if (!paint.IsDeviceColor)
        {
            builder.SetFillGray(0);
        }
        else if (c.Length == GrayComponents)
        {
            builder.SetFillGray(c[0]);
        }
        else if (c.Length == RgbComponents)
        {
            builder.SetFillRgb(c[0], c[SecondComponent], c[ThirdComponent]);
        }
        else if (c.Length == CmykComponents)
        {
            builder.SetFillCmyk(c[0], c[SecondComponent], c[ThirdComponent], c[FourthComponent]);
        }
        else
        {
            builder.SetFillGray(0);
        }
    }
}
