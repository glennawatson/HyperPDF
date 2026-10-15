// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Fonts.CMaps;

namespace HyperPdfLibrary.Fonts;

/// <summary>Everything a loaded composite font needs to show its codes.</summary>
/// <param name="CMap">The CMap.</param>
/// <param name="Source">Where the glyphs come from.</param>
/// <param name="GlyphRoute">How a code finds its glyph.</param>
/// <param name="Widths">The /W widths.</param>
/// <param name="DefaultWidth">The /DW in glyph units.</param>
/// <param name="Vertical">The /W2 vertical metrics.</param>
/// <param name="DefaultVertical">The /DW2 vertical origin y and advance, in glyph units.</param>
/// <param name="ToUnicode">The /ToUnicode map, or <see langword="null"/>.</param>
/// <param name="CidToUnicode">The CID-to-Unicode table of the font's character collection, or <see langword="null"/>.</param>
/// <param name="Metrics">The font-wide metrics and flags.</param>
[DebuggerDisplay("CompositeFontData: {Source}")]
internal readonly record struct CompositeFontData(
    CompositeCMap CMap,
    GlyphSource Source,
    CidGlyphRoute GlyphRoute,
    CidMetricsTable Widths,
    float DefaultWidth,
    CidMetricsTable Vertical,
    VerticalDefault DefaultVertical,
    ToUnicodeMap? ToUnicode,
    CidToUnicodeTable? CidToUnicode,
    FontMetrics Metrics);
