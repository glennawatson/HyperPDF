// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>The part a character of an Indic script plays in a syllable.</summary>
internal enum IndicCategory
{
    /// <summary>A character outside syllables: a digit, danda, sign or a character of another script.</summary>
    Other = 0,

    /// <summary>A consonant.</summary>
    Consonant = 1,

    /// <summary>An independent vowel.</summary>
    Vowel = 2,

    /// <summary>A nukta, which modifies the consonant before it.</summary>
    Nukta = 3,

    /// <summary>A virama (halant), which removes a consonant's inherent vowel.</summary>
    Halant = 4,

    /// <summary>A dependent vowel sign written before the consonant it follows in the text.</summary>
    MatraBefore = 5,

    /// <summary>A dependent vowel sign written above, below or after the consonant.</summary>
    Matra = 6,

    /// <summary>A vowel modifier, accent or other sign written after the syllable.</summary>
    Modifier = 7,

    /// <summary>A zero width joiner.</summary>
    Joiner = 8,

    /// <summary>A zero width non-joiner.</summary>
    NonJoiner = 9,
}
