// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Fonts;
using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>A glyph to draw from a mask.</summary>
/// <param name="Font">The font.</param>
/// <param name="Code">The character code.</param>
/// <param name="Path">The glyph's outline in its own space.</param>
/// <param name="ToDevice">The matrix from the outline's space to device pixels.</param>
internal readonly record struct GlyphMaskGlyph(PdfFont Font, int Code, SKPath Path, Matrix3x2 ToDevice);
