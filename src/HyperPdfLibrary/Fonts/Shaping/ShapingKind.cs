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
}
