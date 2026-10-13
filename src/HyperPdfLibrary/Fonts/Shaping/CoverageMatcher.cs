// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>Matches glyphs against coverage tables (sequence context format 3).</summary>
/// <param name="Base">The offset the coverage offsets are measured from.</param>
/// <param name="ArrayOffset">The offset of the array of coverage offsets.</param>
internal readonly record struct CoverageMatcher(int Base, int ArrayOffset) : IGlyphMatcher<CoverageMatcher>
{
    /// <inheritdoc/>
    public CoverageMatcher At(int arrayOffset) => this with { ArrayOffset = arrayOffset };

    /// <inheritdoc/>
    public bool Matches(ReadOnlySpan<byte> table, int glyph, int position) =>
        OtCoverage.IndexOf(table, Base + FontBytes.U16(table, ArrayOffset + (position * FontBytes.U16Size)), glyph) >= 0;
}
