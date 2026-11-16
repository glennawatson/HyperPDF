// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>The model the shaper uses for a script.</summary>
internal enum ShapingKind
{
    /// <summary>The features apply in lookup order and glyphs keep their text order, as for Latin, Greek, Cyrillic, Hebrew, Thai and Han.</summary>
    Default = 0,

    /// <summary>Letters join their neighbours and take initial, medial, final or isolated forms.</summary>
    Arabic = 1,

    /// <summary>Consonants, virama and vowel signs form syllables that are reordered, as in Devanagari and its relatives.</summary>
    Indic = 2,

    /// <summary>Khmer: a base with subscript consonants, signs and vowel signs, where the subscript ro and the vowel signs written before the base move to the front.</summary>
    Khmer = 3,

    /// <summary>Myanmar: a base with a kinzi, stacked consonants, medials and vowel signs, put in a fixed order before the font's features run.</summary>
    Myanmar = 4,

    /// <summary>Scripts that follow the Universal Shaping Engine model, such as Sinhala and Tibetan, which have no letters that join.</summary>
    Universal = 5,

    /// <summary>Scripts that follow the Universal Shaping Engine model and also join their letters, as Mongolian does.</summary>
    UniversalJoining = 6,
}
