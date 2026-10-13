// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>Some consecutive glyphs of a run and the characters they show together.</summary>
/// <param name="FirstGlyph">The index of the first glyph.</param>
/// <param name="GlyphCount">The number of glyphs.</param>
/// <param name="TextStart">The UTF-16 index of the first character they show.</param>
/// <param name="TextLength">The number of UTF-16 code units they show.</param>
/// <param name="Mark">Whether the glyphs are wrapped in <c>/ActualText</c> marked content.</param>
[DebuggerDisplay("ClusterGroup: {GlyphCount} glyphs for {TextLength} characters, mark {Mark}")]
internal readonly record struct ClusterGroup(int FirstGlyph, int GlyphCount, int TextStart, int TextLength, bool Mark);
