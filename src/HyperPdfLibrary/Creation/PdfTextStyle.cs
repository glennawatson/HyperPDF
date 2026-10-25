// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>The look of drawn text: its font, size and colour, and how it is laid out in a box.</summary>
/// <param name="Font">The font.</param>
/// <param name="Size">The font size in points.</param>
/// <param name="Color">The text colour.</param>
[DebuggerDisplay("PdfTextStyle: {Font} {Size} pt {Color}")]
public sealed record PdfTextStyle(PdfCanvasFont Font, float Size, PdfCanvasColor Color)
{
    /// <summary>Gets how the text is laid out in a box; the default is left aligned, wrapped text.</summary>
    public PdfTextFormat Format { get; init; } = PdfTextFormat.Default;
}
