// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Text.Layout;

/// <summary>A line of a text box.</summary>
/// <param name="GlyphStart">The index of the line's first glyph.</param>
/// <param name="GlyphCount">The number of glyphs, trailing spaces included.</param>
/// <param name="Baseline">The baseline, in points down from the box's top edge.</param>
/// <param name="Left">Where the line's ink starts, in points from the box's left edge.</param>
/// <param name="Width">The line's width without trailing spaces, in points.</param>
/// <param name="TextStart">The index of the line's first character in the whole text.</param>
/// <param name="TextLength">The number of characters the line shows, trailing spaces included.</param>
[DebuggerDisplay("LaidLine: {GlyphCount} glyphs at {Baseline}")]
public readonly record struct LaidLine(int GlyphStart, int GlyphCount, float Baseline, float Left, float Width, int TextStart, int TextLength);
