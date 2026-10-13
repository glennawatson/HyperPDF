// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Text;

/// <summary>One shown glyph, in the space of its <see cref="TextRun"/>. Lengths are text space units, already scaled by the font size.</summary>
/// <param name="Code">The character code.</param>
/// <param name="UnicodeStart">The start of the glyph's text in the device's character buffer.</param>
/// <param name="UnicodeLength">The length of the glyph's text; 0 when the font gives none.</param>
/// <param name="Pen">The text position where the glyph is shown, along the writing direction (the x axis, or the y axis for vertical writing).</param>
/// <param name="Advance">How far the text position moved after the glyph, including character and word spacing; negative for vertical writing.</param>
/// <param name="Origin">The glyph origin: the pen, or for vertical writing the pen moved by the glyph's vertical origin.</param>
/// <param name="Outline">The outline bounds, or an empty rectangle for a blank glyph.</param>
/// <param name="Cell">The glyph's em cell: its advance by the font's descent to ascent, or for vertical writing one em wide centred on the pen.</param>
[DebuggerDisplay("TextGlyph: {Code} at {Pen}")]
internal readonly record struct TextGlyph(
    int Code,
    int UnicodeStart,
    int UnicodeLength,
    float Pen,
    float Advance,
    Vector2 Origin,
    PdfRectangle Outline,
    PdfRectangle Cell);
