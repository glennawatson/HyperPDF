// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Embedding;

namespace HyperPdfLibrary.Tests.Fonts.Shaping;

/// <summary>The glyphs a shaper made from some text.</summary>
/// <param name="RightToLeft">Whether the text reads right to left.</param>
/// <param name="Glyphs">The glyphs in drawing order.</param>
/// <param name="Fixture">The fixture whose font shaped the text, which names the glyphs.</param>
internal sealed record ShapedText(bool RightToLeft, PdfShapedGlyph[] Glyphs, ShapingFixture Fixture)
{
    /// <summary>The number of thousandths in one em.</summary>
    private const float Thousandths = 1000F;

    /// <summary>Gets the glyphs as the letters they are shown as, in drawing order.</summary>
    internal string Letters => string.Concat(Glyphs.Select(glyph => Fixture.NameOf(glyph.Glyph)));

    /// <summary>Gets the cluster of each glyph, separated by commas.</summary>
    internal string Clusters => string.Join(',', Glyphs.Select(static glyph => glyph.Cluster));

    /// <summary>Gets the advance of each glyph in thousandths of an em, rounded, separated by commas.</summary>
    internal string Advances => string.Join(',', Glyphs.Select(static glyph => (int)Math.Round(glyph.Advance * Thousandths)));

    /// <summary>Gets the horizontal offset of each glyph in thousandths of an em, rounded, separated by commas.</summary>
    internal string OffsetsX => string.Join(',', Glyphs.Select(static glyph => (int)Math.Round(glyph.OffsetX * Thousandths)));

    /// <summary>Gets the vertical offset of each glyph in thousandths of an em, rounded, separated by commas.</summary>
    internal string OffsetsY => string.Join(',', Glyphs.Select(static glyph => (int)Math.Round(glyph.OffsetY * Thousandths)));
}
