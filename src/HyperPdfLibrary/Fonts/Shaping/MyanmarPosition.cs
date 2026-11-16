// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>Where a glyph of a Myanmar syllable goes, in the order the glyphs are put in.</summary>
internal enum MyanmarPosition
{
    /// <summary>A vowel sign written before the base.</summary>
    PreMatra = 0,

    /// <summary>A consonant before the base, or a medial ra, which is written before it.</summary>
    PreConsonant = 1,

    /// <summary>The base consonant.</summary>
    Base = 2,

    /// <summary>Anything written after the base, and the kinzi.</summary>
    AfterMain = 3,

    /// <summary>A sign above the base that follows a vowel below it.</summary>
    BeforeSubjoined = 4,

    /// <summary>A vowel sign below the base.</summary>
    Below = 5,

    /// <summary>Anything after the vowel signs below the base.</summary>
    AfterSubjoined = 6,
}
