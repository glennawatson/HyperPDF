// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Text;

/// <summary>One character of a text page, in user space.</summary>
/// <param name="Unicode">The character; the hyphen itself for a line-end hyphen, and 0 for a glyph with neither text nor a code.</param>
/// <param name="Kind">Where the character came from.</param>
/// <param name="Code">The character code, or -1 for generated and /ActualText characters.</param>
/// <param name="Origin">The glyph origin.</param>
/// <param name="Box">The glyph's outline box; a point for generated characters.</param>
/// <param name="LooseBox">The em cell: the glyph's advance by the font's descent to ascent, joined with <paramref name="Box"/>.</param>
/// <param name="FontSize">The font size set by the content, before any scaling; 1 for characters without a font.</param>
/// <param name="RenderMode">The text render mode; 3 is invisible text, such as an OCR layer.</param>
/// <param name="IsBold">Whether the font is bold.</param>
[DebuggerDisplay("PdfTextChar: {Unicode} {Kind} {Box}")]
public readonly record struct PdfTextChar(
    char Unicode,
    PdfTextCharKind Kind,
    int Code,
    Vector2 Origin,
    PdfRectangle Box,
    PdfRectangle LooseBox,
    float FontSize,
    int RenderMode,
    bool IsBold)
{
    /// <summary>Gets a value indicating whether the extractor inserted the character, as it does for spaces and line breaks.</summary>
    public bool IsGenerated => Kind == PdfTextCharKind.Generated;
}
