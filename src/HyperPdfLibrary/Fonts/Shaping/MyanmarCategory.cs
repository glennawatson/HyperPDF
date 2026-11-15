// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>What a Myanmar character does in a syllable.</summary>
internal enum MyanmarCategory
{
    /// <summary>A character that is not part of a Myanmar syllable.</summary>
    Other = 0,

    /// <summary>A consonant.</summary>
    Consonant = 1,

    /// <summary>A consonant that can be the first part of a kinzi: nga, ra or Mon nga.</summary>
    Ra = 2,

    /// <summary>An independent vowel.</summary>
    Vowel = 3,

    /// <summary>The dotted circle, which stands for a missing base.</summary>
    GenericBase = 4,

    /// <summary>The virama, which stacks the consonant after it under the one before.</summary>
    Virama = 5,

    /// <summary>The asat, which kills a consonant's vowel.</summary>
    Asat = 6,

    /// <summary>The medial ra, which is written before the base.</summary>
    MedialRa = 7,

    /// <summary>A vowel sign written before the base.</summary>
    VowelPre = 8,

    /// <summary>A vowel sign written below the base.</summary>
    VowelBelow = 9,

    /// <summary>A tone mark or sign written over or after the base.</summary>
    Sign = 10,

    /// <summary>A variation selector.</summary>
    Variation = 11,

    /// <summary>The zero width joiner or non-joiner.</summary>
    Joiner = 12,

    /// <summary>A medial, vowel sign or other mark that goes on a syllable after its base.</summary>
    Dependent = 13,
}
