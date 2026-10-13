// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>
/// Turns text into positioned glyphs of one font. The library's own shaper, <see cref="PdfOpenTypeShaper"/>, applies the
/// font's GSUB and GPOS tables, joining, reordering and bidirectional text, and is used unless a host supplies another,
/// such as one built on HarfBuzz. The glyph numbers must be those of the font file the shaper's
/// <see cref="PdfFontSource"/> was opened from.
/// </summary>
public interface IPdfGlyphShaper
{
    /// <summary>Shapes one run of text without line breaks.</summary>
    /// <param name="text">The text in logical order.</param>
    /// <param name="options">What to do with the text.</param>
    /// <param name="output">Receives the glyphs in the order they are drawn, which for right-to-left text is the reverse of reading order. The list is not cleared.</param>
    /// <returns><see langword="true"/> when the run reads right to left, so its glyphs are in visual order.</returns>
    bool Shape(ReadOnlySpan<char> text, PdfShapingOptions options, List<PdfShapedGlyph> output);
}
