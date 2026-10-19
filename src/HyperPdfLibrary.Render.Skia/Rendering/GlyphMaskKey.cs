// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Rendering;

/// <summary>Identifies one glyph mask: a glyph of a font, or an image mask, at a size and position within a pixel, as grey coverage or as per-channel coverage in one colour.</summary>
/// <param name="Owner">What the mask is made from: the font whose glyph it is, or the image mask itself.</param>
/// <param name="Code">The glyph's character code; 0 for an image mask.</param>
/// <param name="A">The horizontal scale of the glyph's matrix, as a whole number of 2^-<paramref name="Shift"/>.</param>
/// <param name="B">The matrix entry that moves a point down as it moves right, as a whole number of 2^-<paramref name="Shift"/>.</param>
/// <param name="C">The matrix entry that moves a point right as it moves down, as a whole number of 2^-<paramref name="Shift"/>.</param>
/// <param name="D">The vertical scale of the glyph's matrix, as a whole number of 2^-<paramref name="Shift"/>.</param>
/// <param name="Shift">The power of two the matrix entries are scaled by.</param>
/// <param name="SubX">How far the glyph's origin is right of its pixel, in quarter pixels.</param>
/// <param name="SubY">How far the glyph's origin is down its pixel, in quarter pixels.</param>
/// <param name="Edges">How the edges are drawn.</param>
/// <param name="Contrast">The contrast level, 0 to 3.</param>
/// <param name="Color">The text colour as 0xRRGGBB for a per-channel mask, and 0 for grey coverage, which any colour can tint.</param>
internal readonly record struct GlyphMaskKey(object Owner, int Code, int A, int B, int C, int D, int Shift, byte SubX, byte SubY, GlyphMaskEdges Edges, byte Contrast, uint Color);
