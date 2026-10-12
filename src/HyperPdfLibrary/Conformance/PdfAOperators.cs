// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Content;

namespace HyperPdfLibrary.Conformance;

/// <summary>Maps content operators to what the PDF/A colour walk does for them.</summary>
internal static class PdfAOperators
{
    /// <summary>The group of each operator, indexed by <see cref="ContentOperator"/>.</summary>
    private static readonly PdfAOperatorGroup[] Groups = Build();

    /// <summary>Gets the group of an operator.</summary>
    /// <param name="op">The operator.</param>
    /// <returns>The group.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfAOperatorGroup Group(ContentOperator op) => (uint)op < (uint)Groups.Length ? Groups[(int)op] : PdfAOperatorGroup.None;

    /// <summary>Builds the table.</summary>
    /// <returns>The table.</returns>
    private static PdfAOperatorGroup[] Build()
    {
        var groups = new PdfAOperatorGroup[(int)ContentOperator.SetSpacingNextLineShowText + 1];
        groups[(int)ContentOperator.Save] = PdfAOperatorGroup.Save;
        groups[(int)ContentOperator.Restore] = PdfAOperatorGroup.Restore;
        groups[(int)ContentOperator.SetGraphicsState] = PdfAOperatorGroup.GraphicsState;
        groups[(int)ContentOperator.SetRenderingIntent] = PdfAOperatorGroup.Intent;
        groups[(int)ContentOperator.SetRenderMode] = PdfAOperatorGroup.RenderMode;
        groups[(int)ContentOperator.SetFont] = PdfAOperatorGroup.Font;
        groups[(int)ContentOperator.SetGlyphWidthAndBounds] = PdfAOperatorGroup.ShapeGlyph;
        groups[(int)ContentOperator.SetFillGray] = PdfAOperatorGroup.FillGray;
        groups[(int)ContentOperator.SetStrokeGray] = PdfAOperatorGroup.StrokeGray;
        groups[(int)ContentOperator.SetFillRgb] = PdfAOperatorGroup.FillRgb;
        groups[(int)ContentOperator.SetStrokeRgb] = PdfAOperatorGroup.StrokeRgb;
        groups[(int)ContentOperator.SetFillCmyk] = PdfAOperatorGroup.FillCmyk;
        groups[(int)ContentOperator.SetStrokeCmyk] = PdfAOperatorGroup.StrokeCmyk;
        groups[(int)ContentOperator.SetFillColorSpace] = PdfAOperatorGroup.FillSpace;
        groups[(int)ContentOperator.SetStrokeColorSpace] = PdfAOperatorGroup.StrokeSpace;
        groups[(int)ContentOperator.SetFillColorN] = PdfAOperatorGroup.FillPattern;
        groups[(int)ContentOperator.SetStrokeColorN] = PdfAOperatorGroup.StrokePattern;
        groups[(int)ContentOperator.Fill] = PdfAOperatorGroup.PaintFill;
        groups[(int)ContentOperator.FillEvenOdd] = PdfAOperatorGroup.PaintFill;
        groups[(int)ContentOperator.Stroke] = PdfAOperatorGroup.PaintStroke;
        groups[(int)ContentOperator.CloseStroke] = PdfAOperatorGroup.PaintStroke;
        groups[(int)ContentOperator.FillStroke] = PdfAOperatorGroup.PaintBoth;
        groups[(int)ContentOperator.FillStrokeEvenOdd] = PdfAOperatorGroup.PaintBoth;
        groups[(int)ContentOperator.CloseFillStroke] = PdfAOperatorGroup.PaintBoth;
        groups[(int)ContentOperator.CloseFillStrokeEvenOdd] = PdfAOperatorGroup.PaintBoth;
        groups[(int)ContentOperator.ShowText] = PdfAOperatorGroup.ShowText;
        groups[(int)ContentOperator.ShowTextArray] = PdfAOperatorGroup.ShowText;
        groups[(int)ContentOperator.NextLineShowText] = PdfAOperatorGroup.ShowText;
        groups[(int)ContentOperator.SetSpacingNextLineShowText] = PdfAOperatorGroup.ShowText;
        groups[(int)ContentOperator.BeginInlineImage] = PdfAOperatorGroup.InlineImage;
        groups[(int)ContentOperator.PaintXObject] = PdfAOperatorGroup.XObject;
        groups[(int)ContentOperator.PaintShading] = PdfAOperatorGroup.Shading;
        return groups;
    }
}
