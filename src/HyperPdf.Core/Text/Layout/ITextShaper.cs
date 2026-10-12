// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Text.Layout;

/// <summary>Turns text in one font into positioned glyphs and gives the font's vertical metrics.</summary>
public interface ITextShaper
{
    /// <summary>Gets the height above the baseline, as a share of an em.</summary>
    float Ascent { get; }

    /// <summary>Gets the depth below the baseline, as a negative share of an em.</summary>
    float Descent { get; }

    /// <summary>Gets where an underline sits, as a negative share of an em below the baseline.</summary>
    float UnderlinePosition { get; }

    /// <summary>Gets how thick an underline is, as a share of an em.</summary>
    float UnderlineThickness { get; }

    /// <summary>Shapes one paragraph: a run of text without line breaks.</summary>
    /// <param name="text">The text.</param>
    /// <param name="output">Receives the glyphs in reading (logical) order; the list is not cleared.</param>
    /// <returns><see langword="true"/> when the paragraph reads right to left.</returns>
    bool Shape(ReadOnlySpan<char> text, List<ShapedGlyph> output);

    /// <summary>Determines whether the font can show a character.</summary>
    /// <param name="codePoint">The Unicode code point.</param>
    /// <returns><see langword="true"/> when the font has a glyph for it.</returns>
    bool Covers(int codePoint);
}
