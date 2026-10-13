// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Fonts;

namespace HyperPdfLibrary.Text;

/// <summary>
/// Consecutive glyphs shown with one font, size, render mode and text matrix, advancing along one baseline. Glyph
/// positions are in the run's space, which maps to user space through <see cref="Matrix"/>.
/// </summary>
/// <param name="Font">The font.</param>
/// <param name="FontSize">The font size set by the content.</param>
/// <param name="RenderMode">The text render mode.</param>
/// <param name="Matrix">The matrix from the run's space (text space units, horizontal scaling applied) to user space, with its translation at the first glyph's pen.</param>
/// <param name="FirstGlyph">The index of the run's first glyph in the device's glyph list.</param>
/// <param name="GlyphCount">The number of glyphs.</param>
/// <param name="Marks">The replacement text that covers the run.</param>
[DebuggerDisplay("TextRun: {GlyphCount} glyphs at {Matrix.Translation}")]
internal readonly record struct TextRun(
    PdfFont Font,
    float FontSize,
    int RenderMode,
    Matrix3x2 Matrix,
    int FirstGlyph,
    int GlyphCount,
    TextMarks Marks);
