// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Editing;

/// <summary>Checks the settings of a watermark, adds its shared objects and works out where it sits on a page.</summary>
internal static class PdfWatermarkForms
{
    /// <summary>Degrees in a half turn.</summary>
    private const float HalfTurnDegrees = 180;

    /// <summary>Splits a size into two halves.</summary>
    private const float Half = 2;

    /// <summary>Gets the name of the constant opacity key for fills.</summary>
    private static ReadOnlySpan<byte> FillOpacityKey => "ca"u8;

    /// <summary>Gets the name of the constant opacity key for strokes.</summary>
    private static ReadOnlySpan<byte> StrokeOpacityKey => "CA"u8;

    /// <summary>Checks the style of a text watermark.</summary>
    /// <param name="style">The style.</param>
    /// <exception cref="ArgumentOutOfRangeException">The size or a colour level is out of range.</exception>
    internal static void ValidateStyle(PdfTextWatermarkStyle style)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(style.FontSize);
        if (!float.IsFinite(style.FontSize))
        {
            throw new ArgumentOutOfRangeException(nameof(style), style.FontSize, "The font size must be finite.");
        }

        foreach (var level in (ReadOnlySpan<float>)[style.Red, style.Green, style.Blue])
        {
            ArgumentOutOfRangeException.ThrowIfNegative(level);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(level, 1F);
        }
    }

    /// <summary>Checks the options of a watermark.</summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentOutOfRangeException">A number is out of range.</exception>
    internal static void ValidateOptions(PdfWatermarkOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Opacity);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.Opacity, 1F);
        ArgumentOutOfRangeException.ThrowIfNegative(options.Margin);
        if (!float.IsFinite(options.RotationDegrees) || !float.IsFinite(options.Margin))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The rotation and margin must be finite.");
        }
    }

    /// <summary>Refuses a watermark that would break the PDF/A claim the document makes.</summary>
    /// <param name="document">The document.</param>
    /// <param name="options">The options.</param>
    /// <param name="content">What is drawn.</param>
    /// <exception cref="InvalidOperationException">The watermark cannot keep the claim.</exception>
    internal static void RequireConformance(PdfDocument document, PdfWatermarkOptions options, PdfWatermarkContent content)
    {
        if (options.LayerName is not null)
        {
            PdfStampConformance.RequireLayersAllowed(document);
        }

        if (options.Opacity < 1F || content.HasTransparency)
        {
            PdfStampConformance.RequireTransparencyAllowed(document);
        }

        if (content.UsesUnembeddedFont)
        {
            PdfStampConformance.RequireUnembeddedFontAllowed(document);
        }
    }

    /// <summary>Adds a graphics state that draws at an opacity.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="opacity">The opacity from 0 to 1.</param>
    /// <returns>The graphics state, or an invalid id for a solid watermark.</returns>
    internal static PdfObjectId AddOpacity(PdfDocument document, PdfEditTransaction transaction, float opacity)
    {
        if (opacity >= 1F)
        {
            return default;
        }

        var names = document.Objects.Names;
        var state = new PdfDictionary(document.Objects);
        state.Set(KnownName.Type, PdfValue.FromName(KnownName.ExtGState));
        state.Set(names.Intern(FillOpacityKey), PdfValue.FromReal(opacity));
        state.Set(names.Intern(StrokeOpacityKey), PdfValue.FromReal(opacity));
        return transaction.Add(PdfValue.FromDictionary(state));
    }

    /// <summary>Adds a form to the document, under the control of an optional content group when there is one.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="form">The form stream.</param>
    /// <param name="group">The group, or an invalid id.</param>
    /// <returns>The form's id.</returns>
    internal static PdfObjectId AddForm(PdfEditTransaction transaction, PdfStream form, PdfObjectId group)
    {
        if (group.IsValid)
        {
            form.Dictionary.Set(KnownName.OC, PdfValue.FromReference(group));
        }

        return transaction.Add(PdfValue.FromStream(form));
    }

    /// <summary>Works out the matrix that places a watermark on a page.</summary>
    /// <param name="page">The page.</param>
    /// <param name="width">The watermark's width in points.</param>
    /// <param name="height">The watermark's height in points.</param>
    /// <param name="options">Where it sits and how it is turned.</param>
    /// <returns>The matrix from the watermark form's space to the page's user space.</returns>
    internal static Matrix3x2 GetPlacement(PdfPage page, float width, float height, PdfWatermarkOptions options)
    {
        PdfPageImporter.GetDisplayedSize(page.CropBox, page.Rotation, out var displayedWidth, out var displayedHeight);
        var radians = options.RotationDegrees * MathF.PI / HalfTurnDegrees;
        var cos = MathF.Abs(MathF.Cos(radians));
        var sin = MathF.Abs(MathF.Sin(radians));
        var bounds = new Vector2((width * cos) + (height * sin), (width * sin) + (height * cos));
        var pageSize = new Vector2(displayedWidth, displayedHeight) * page.UserUnit;
        var centre = GetCentre(options.Anchor, pageSize, bounds, options.Margin);
        var points = Matrix3x2.CreateTranslation(-width / Half, -height / Half) * Matrix3x2.CreateRotation(radians) * Matrix3x2.CreateTranslation(centre);
        return PdfPageStamping.ToUserSpace(points * Matrix3x2.CreateScale(1 / page.UserUnit), page);
    }

    /// <summary>Gets where the centre of a watermark sits on a page.</summary>
    /// <param name="anchor">The anchor.</param>
    /// <param name="page">The page's displayed size in points.</param>
    /// <param name="bounds">The size of the box around the turned watermark.</param>
    /// <param name="margin">The margin in points.</param>
    /// <returns>The centre in points, from the page's lower left.</returns>
    private static Vector2 GetCentre(PdfWatermarkAnchor anchor, Vector2 page, Vector2 bounds, float margin)
    {
        var left = margin + (bounds.X / Half);
        var right = page.X - margin - (bounds.X / Half);
        var bottom = margin + (bounds.Y / Half);
        var top = page.Y - margin - (bounds.Y / Half);
        return anchor switch
        {
            PdfWatermarkAnchor.TopLeft => new(left, top),
            PdfWatermarkAnchor.TopRight => new(right, top),
            PdfWatermarkAnchor.BottomLeft => new(left, bottom),
            PdfWatermarkAnchor.BottomRight => new(right, bottom),
            _ => page / Half,
        };
    }
}
