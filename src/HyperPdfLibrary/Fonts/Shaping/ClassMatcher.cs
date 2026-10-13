// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>Matches glyphs against a rule that lists glyph classes (sequence context format 2).</summary>
/// <param name="ClassDef">The offset of the Class Definition table, or zero when every glyph is in class zero.</param>
/// <param name="ArrayOffset">The offset of the rule's array of classes.</param>
internal readonly record struct ClassMatcher(int ClassDef, int ArrayOffset) : IGlyphMatcher<ClassMatcher>
{
    /// <inheritdoc/>
    public ClassMatcher At(int arrayOffset) => this with { ArrayOffset = arrayOffset };

    /// <inheritdoc/>
    public bool Matches(ReadOnlySpan<byte> table, int glyph, int position) =>
        OtClassDef.ClassOf(table, ClassDef, glyph) == FontBytes.U16(table, ArrayOffset + (position * FontBytes.U16Size));
}
