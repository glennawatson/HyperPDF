// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>Matches glyphs against a rule that lists glyph indexes (sequence context format 1).</summary>
/// <param name="ArrayOffset">The offset of the rule's array of glyphs.</param>
internal readonly record struct GlyphIdMatcher(int ArrayOffset) : IGlyphMatcher<GlyphIdMatcher>
{
    /// <inheritdoc/>
    public GlyphIdMatcher At(int arrayOffset) => new(arrayOffset);

    /// <inheritdoc/>
    public bool Matches(ReadOnlySpan<byte> table, int glyph, int position) =>
        FontBytes.U16(table, ArrayOffset + (position * FontBytes.U16Size)) == glyph;
}
