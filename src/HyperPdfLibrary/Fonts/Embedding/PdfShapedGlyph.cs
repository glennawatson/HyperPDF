// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>One glyph from shaping, sized for a font size of one: lengths are in ems.</summary>
/// <param name="Glyph">The glyph index in the font file.</param>
/// <param name="Cluster">
/// The UTF-16 index in the shaped text of the first character the glyph shows. Glyphs that share a cluster show the same
/// characters together, as a ligature or a reordered Indic syllable does.
/// </param>
/// <param name="Advance">How far the pen moves along the writing direction after the glyph, kerning included; downward for vertical writing.</param>
/// <param name="OffsetX">How far the glyph is drawn to the right of the pen.</param>
/// <param name="OffsetY">How far the glyph is drawn above the pen.</param>
[DebuggerDisplay("PdfShapedGlyph: {Glyph} at cluster {Cluster}")]
public readonly record struct PdfShapedGlyph(int Glyph, int Cluster, float Advance, float OffsetX, float OffsetY);
