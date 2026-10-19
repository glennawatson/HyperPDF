// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Fonts;

namespace HyperPdfLibrary.Text;

/// <summary>A run of glyphs that share a font, size, matrix, render mode, replacement text and colours.</summary>
/// <param name="Font">The run's font.</param>
/// <param name="FontSize">The font size.</param>
/// <param name="RenderMode">The text render mode.</param>
/// <param name="Matrix">The matrix that maps the run's text space to user space.</param>
/// <param name="FirstGlyph">The index of the run's first glyph.</param>
/// <param name="GlyphCount">The number of glyphs.</param>
/// <param name="Marks">The replacement text that covers the run.</param>
/// <param name="FillColor">The fill colour as 0xRRGGBB; 0 for a pattern fill.</param>
/// <param name="StrokeColor">The stroke colour as 0xRRGGBB; 0 for a pattern stroke.</param>
[DebuggerDisplay("TextRun: {GlyphCount} glyphs at {Matrix.Translation}")]
internal readonly record struct TextRun(
    PdfFont Font,
    float FontSize,
    int RenderMode,
    Matrix3x2 Matrix,
    int FirstGlyph,
    int GlyphCount,
    TextMarks Marks,
    uint FillColor,
    uint StrokeColor);
