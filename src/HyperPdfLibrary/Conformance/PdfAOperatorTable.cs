// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>Maps content operators to how the PDF/A content rules treat them.</summary>
internal static class PdfAOperatorTable
{
    /// <summary>The table size: above every <see cref="ContentOperator"/> value.</summary>
    private const int TableSize = 128;

    /// <summary>The traits of each operator, indexed by its value.</summary>
    private static readonly PdfAOperatorTraits[] Traits = Build();

    /// <summary>Gets how the rules treat an operator.</summary>
    /// <param name="op">The operator.</param>
    /// <returns>Its traits.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfAOperatorTraits Get(ContentOperator op) => (uint)op < TableSize ? Traits[(int)op] : default;

    /// <summary>Builds the table.</summary>
    /// <returns>The traits by operator value.</returns>
    private static PdfAOperatorTraits[] Build()
    {
        var traits = new PdfAOperatorTraits[TableSize];
        Set(traits, ContentOperator.Save, PdfAOperatorRole.Save);
        Set(traits, ContentOperator.Restore, PdfAOperatorRole.Restore);
        Set(traits, ContentOperator.SetFont, PdfAOperatorRole.Font);
        Set(traits, ContentOperator.ShowText, PdfAOperatorRole.Text);
        Set(traits, ContentOperator.ShowTextArray, PdfAOperatorRole.Text);
        Set(traits, ContentOperator.NextLineShowText, PdfAOperatorRole.Text);
        Set(traits, ContentOperator.SetSpacingNextLineShowText, PdfAOperatorRole.Text);
        Set(traits, ContentOperator.SetGlyphWidthAndBounds, PdfAOperatorRole.GlyphBounds);
        traits[(int)ContentOperator.SetFillGray] = new(PdfAOperatorRole.DeviceColor, false, KnownName.DeviceGray);
        traits[(int)ContentOperator.SetStrokeGray] = new(PdfAOperatorRole.DeviceColor, true, KnownName.DeviceGray);
        traits[(int)ContentOperator.SetFillRgb] = new(PdfAOperatorRole.DeviceColor, false, KnownName.DeviceRGB);
        traits[(int)ContentOperator.SetStrokeRgb] = new(PdfAOperatorRole.DeviceColor, true, KnownName.DeviceRGB);
        traits[(int)ContentOperator.SetFillCmyk] = new(PdfAOperatorRole.DeviceColor, false, KnownName.DeviceCMYK);
        traits[(int)ContentOperator.SetStrokeCmyk] = new(PdfAOperatorRole.DeviceColor, true, KnownName.DeviceCMYK);
        traits[(int)ContentOperator.SetFillColorSpace] = new(PdfAOperatorRole.ColorSpace, false, KnownName.None);
        traits[(int)ContentOperator.SetStrokeColorSpace] = new(PdfAOperatorRole.ColorSpace, true, KnownName.None);
        traits[(int)ContentOperator.SetFillColorN] = new(PdfAOperatorRole.PatternColor, false, KnownName.None);
        traits[(int)ContentOperator.SetStrokeColorN] = new(PdfAOperatorRole.PatternColor, true, KnownName.None);
        Set(traits, ContentOperator.PaintShading, PdfAOperatorRole.Shading);
        Set(traits, ContentOperator.SetGraphicsState, PdfAOperatorRole.GraphicsState);
        Set(traits, ContentOperator.PaintXObject, PdfAOperatorRole.XObject);
        Set(traits, ContentOperator.BeginMarkedContentProperties, PdfAOperatorRole.Properties);
        Set(traits, ContentOperator.MarkPointProperties, PdfAOperatorRole.Properties);

        // veraPDF reads ID and EI only inside an inline image, so a stray one is an undefined operator.
        Set(traits, ContentOperator.Unknown, PdfAOperatorRole.Undefined);
        Set(traits, ContentOperator.InlineImageData, PdfAOperatorRole.Undefined);
        Set(traits, ContentOperator.EndInlineImage, PdfAOperatorRole.Undefined);
        return traits;
    }

    /// <summary>Sets an operator's role.</summary>
    /// <param name="traits">The table.</param>
    /// <param name="op">The operator.</param>
    /// <param name="role">Its role.</param>
    private static void Set(PdfAOperatorTraits[] traits, ContentOperator op, PdfAOperatorRole role) => traits[(int)op] = new(role, false, KnownName.None);
}
