// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Fonts.CMaps;

namespace HyperPdfLibrary.Fonts;

/// <summary>Everything a loaded simple font needs to show its codes.</summary>
/// <param name="Source">Where the glyphs come from.</param>
/// <param name="Glyphs">The glyph id of each code, or -1.</param>
/// <param name="Texts">The Unicode text of each code from its glyph name, or <see langword="null"/>.</param>
/// <param name="Widths">The advance of each code in text space units.</param>
/// <param name="ToUnicode">The /ToUnicode map, or <see langword="null"/>.</param>
/// <param name="Metrics">The font-wide metrics and flags.</param>
/// <param name="Remap">The encoding private-use text is mapped through when extracted.</param>
[DebuggerDisplay("SimpleFontData: {Source}")]
internal readonly record struct SimpleFontData(GlyphSource Source, int[] Glyphs, string?[] Texts, float[] Widths, ToUnicodeMap? ToUnicode, FontMetrics Metrics, PuaRemap Remap);
