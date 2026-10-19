// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Fonts;

namespace HyperPdfLibrary.Text;

/// <summary>How a character is drawn: its colours, direction, matrix, font and decorations.</summary>
/// <param name="FillColor">The fill colour as 0xRRGGBB; 0 when the text is painted with a pattern.</param>
/// <param name="StrokeColor">The stroke colour as 0xRRGGBB; 0 when the text is stroked with a pattern.</param>
/// <param name="Angle">The angle of the baseline in degrees, counter-clockwise from the positive x axis of user space.</param>
/// <param name="Matrix">The matrix that maps the text's own space to user space, including horizontal scaling.</param>
/// <param name="FontName">The font's name without its subset tag, or empty.</param>
/// <param name="FontWeight">The font's weight from 100 to 900, 400 for regular and 700 for bold.</param>
/// <param name="FontFlags">The font descriptor flags, with italic added when the font's name or angle says so.</param>
/// <param name="Decorations">The underline, strikeout or highlight the page draws on the character.</param>
[DebuggerDisplay("PdfTextCharStyle: {FontName} {FontWeight}, fill {FillColor:X6}, angle {Angle}")]
public readonly record struct PdfTextCharStyle(
    uint FillColor,
    uint StrokeColor,
    float Angle,
    Matrix3x2 Matrix,
    string FontName,
    int FontWeight,
    FontFlags FontFlags,
    PdfTextDecorations Decorations)
{
    /// <summary>The weight from which a font counts as bold.</summary>
    private const int BoldWeight = 700;

    /// <summary>Gets a value indicating whether the font is bold: a weight of 700 or more.</summary>
    public bool IsBold => FontWeight >= BoldWeight;

    /// <summary>Gets a value indicating whether the font is italic.</summary>
    public bool IsItalic => (FontFlags & FontFlags.Italic) != 0;
}
