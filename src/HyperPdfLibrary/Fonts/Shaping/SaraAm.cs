// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>Splits the Thai and Lao vowel sign "am" into the nikhahit mark and the long a it is made of, for fonts that have no glyph for the sign as a whole.</summary>
internal static class SaraAm
{
    /// <summary>The Thai vowel sign am.</summary>
    private const int ThaiSaraAm = 0x0E33;

    /// <summary>The Thai mark nikhahit.</summary>
    private const int ThaiNikhahit = 0x0E4D;

    /// <summary>The Thai vowel sign long a.</summary>
    private const int ThaiSaraAa = 0x0E32;

    /// <summary>The Lao vowel sign am.</summary>
    private const int LaoAm = 0x0EB3;

    /// <summary>The Lao niggahita mark.</summary>
    private const int LaoNiggahita = 0x0ECD;

    /// <summary>The Lao vowel sign long a.</summary>
    private const int LaoAa = 0x0EB2;

    /// <summary>Splits an "am" vowel sign.</summary>
    /// <param name="codePoint">The character.</param>
    /// <param name="mark">Receives the mark that goes above the consonant.</param>
    /// <param name="vowel">Receives the long a that follows.</param>
    /// <returns><see langword="true"/> when the character is an "am" sign.</returns>
    internal static bool TrySplit(int codePoint, out int mark, out int vowel)
    {
        switch (codePoint)
        {
            case ThaiSaraAm:
            {
                mark = ThaiNikhahit;
                vowel = ThaiSaraAa;
                return true;
            }

            case LaoAm:
            {
                mark = LaoNiggahita;
                vowel = LaoAa;
                return true;
            }

            default:
            {
                mark = 0;
                vowel = 0;
                return false;
            }
        }
    }
}
