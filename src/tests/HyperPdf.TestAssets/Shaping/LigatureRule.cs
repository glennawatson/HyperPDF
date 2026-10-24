// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.TestAssets.Shaping;

/// <summary>A ligature: the glyph that replaces a glyph and the glyphs that follow it.</summary>
/// <param name="Glyph">The ligature glyph.</param>
/// <param name="Rest">The component glyphs after the first.</param>
public readonly record struct LigatureRule(int Glyph, int[] Rest);
