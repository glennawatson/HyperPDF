// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Viewer.Text;

/// <summary>One glyph of a laid out piece of text.</summary>
/// <param name="Font">The font the glyph comes from.</param>
/// <param name="Glyph">The glyph.</param>
/// <param name="X">How far the glyph's origin is from the start of the text, in pixels.</param>
/// <param name="Rise">How far the glyph is lifted above the baseline, in pixels.</param>
[DebuggerDisplay("PlacedGlyph: {Glyph} at {X}")]
internal readonly record struct PlacedGlyph(UiFont Font, int Glyph, float X, float Rise);
