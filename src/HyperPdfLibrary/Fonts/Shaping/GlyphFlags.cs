// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>What the shaper has learned about a glyph while it works on a run.</summary>
[Flags]
internal enum GlyphFlags
{
    /// <summary>Nothing is known.</summary>
    None = 0,

    /// <summary>A ligature lookup made the glyph.</summary>
    Ligated = 1 << 0,

    /// <summary>A multiple substitution made the glyph.</summary>
    Multiplied = 1 << 1,

    /// <summary>The character is invisible (such as a joiner), so the glyph has no advance.</summary>
    Hidden = 1 << 2,

    /// <summary>The font has no glyph for the character, so the glyph is dropped when shaping ends.</summary>
    Virtual = 1 << 3,

    /// <summary>The glyph is, or was made from, the base consonant of an Indic syllable.</summary>
    Base = 1 << 4,

    /// <summary>The glyph is a consonant of an Indic syllable.</summary>
    Consonant = 1 << 5,

    /// <summary>The glyph is a vowel sign written before its consonant.</summary>
    PreBaseMatra = 1 << 6,

    /// <summary>The glyph is a reph, a ra that has become a mark written after the syllable.</summary>
    Reph = 1 << 7,

    /// <summary>The glyph is a virama (halant).</summary>
    Halant = 1 << 8,

    /// <summary>The glyph is a zero width joiner.</summary>
    Joiner = 1 << 9,

    /// <summary>The glyph is a zero width non-joiner.</summary>
    NonJoiner = 1 << 10,

    /// <summary>The glyph is a ra that may become a reph.</summary>
    RephCandidate = 1 << 11,

    /// <summary>The glyph is a vowel modifier or accent written after the syllable's base.</summary>
    Modifier = 1 << 12,
}
