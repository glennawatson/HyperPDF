// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;

namespace HyperPdfLibrary.Rendering;

/// <summary>A glyph to draw from a mask.</summary>
/// <typeparam name="TSource">What the mask is drawn from when it is made.</typeparam>
/// <param name="Owner">What the mask is made from: the font whose glyph it is, or the image mask itself.</param>
/// <param name="Code">The character code; 0 for an image mask.</param>
/// <param name="Source">The glyph's outline, or the image mask, in its own space.</param>
/// <param name="ToDevice">The matrix from the source's space to device pixels.</param>
internal readonly record struct GlyphMaskGlyph<TSource>(object Owner, int Code, TSource Source, Matrix3x2 ToDevice)
    where TSource : struct, IGlyphMaskSource;
