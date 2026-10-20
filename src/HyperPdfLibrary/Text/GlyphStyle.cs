// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Fonts;

namespace HyperPdfLibrary.Text;

/// <summary>What a glyph is drawn with, which decides whether it continues the open run of the text device.</summary>
/// <param name="Font">The font.</param>
/// <param name="FontSize">The font size.</param>
/// <param name="Matrix">The glyph's run matrix, mapping text space to user space.</param>
/// <param name="RenderMode">The text render mode.</param>
/// <param name="FillColor">The fill colour as 0xRRGGBB; 0 for a pattern fill.</param>
/// <param name="StrokeColor">The stroke colour as 0xRRGGBB; 0 for a pattern stroke.</param>
[DebuggerDisplay("GlyphStyle: size {FontSize}, mode {RenderMode}, fill {FillColor:X6}")]
internal readonly record struct GlyphStyle(
    PdfFont? Font,
    float FontSize,
    Matrix3x2 Matrix,
    int RenderMode,
    uint FillColor,
    uint StrokeColor);
