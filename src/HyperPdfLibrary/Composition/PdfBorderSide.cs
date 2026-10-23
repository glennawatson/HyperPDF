// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>One edge of a border: a line of one width, colour and style.</summary>
/// <param name="Width">The line width in points.</param>
/// <param name="Color">The line colour.</param>
/// <param name="Style">How the line is drawn.</param>
[DebuggerDisplay("PdfBorderSide: {Width} pt {Color} {Style}")]
public readonly record struct PdfBorderSide(float Width, PdfCanvasColor Color, PdfLineStyle Style)
{
    /// <summary>Makes a solid border edge.</summary>
    /// <param name="width">The line width in points.</param>
    /// <param name="color">The line colour.</param>
    /// <returns>The edge.</returns>
    public static PdfBorderSide Solid(float width, PdfCanvasColor color) => new(width, color, PdfLineStyle.Solid);
}
