// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>Tests whether a glyph fits one position of a contextual rule.</summary>
/// <typeparam name="TSelf">The matcher's own type.</typeparam>
internal interface IGlyphMatcher<TSelf>
    where TSelf : struct, IGlyphMatcher<TSelf>
{
    /// <summary>Makes a matcher for the array of a rule.</summary>
    /// <param name="arrayOffset">The offset of the rule's array.</param>
    /// <returns>The matcher.</returns>
    TSelf At(int arrayOffset);

    /// <summary>Determines whether a glyph fits a position of the rule.</summary>
    /// <param name="table">The GSUB or GPOS table.</param>
    /// <param name="glyph">The glyph.</param>
    /// <param name="position">The position's index in the rule's array.</param>
    /// <returns><see langword="true"/> when the glyph fits.</returns>
    bool Matches(ReadOnlySpan<byte> table, int glyph, int position);
}
