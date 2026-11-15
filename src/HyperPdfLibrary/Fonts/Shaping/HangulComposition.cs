// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Fonts.Embedding;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>Joins the leading consonant, vowel and trailing consonant jamo of a Hangul syllable into the syllable's own character, as the Unicode standard describes.</summary>
internal static class HangulComposition
{
    /// <summary>How many characters a leading consonant and a vowel make.</summary>
    private const int JamoPair = 2;

    /// <summary>The first leading consonant jamo.</summary>
    private const int LeadBase = 0x1100;

    /// <summary>The first vowel jamo.</summary>
    private const int VowelBase = 0x1161;

    /// <summary>The jamo before the first trailing consonant; the trailing consonants start one after it.</summary>
    private const int TrailBase = 0x11A7;

    /// <summary>The first precomposed syllable.</summary>
    private const int SyllableBase = 0xAC00;

    /// <summary>The number of leading consonants.</summary>
    private const int LeadCount = 19;

    /// <summary>The number of vowels.</summary>
    private const int VowelCount = 21;

    /// <summary>The number of trailing consonants, counting none as one.</summary>
    private const int TrailCount = 28;

    /// <summary>The number of syllables that share one leading consonant.</summary>
    private const int VowelTrailCount = VowelCount * TrailCount;

    /// <summary>The number of precomposed syllables.</summary>
    private const int SyllableCount = LeadCount * VowelTrailCount;

    /// <summary>Tries to join the jamo at a place into one syllable the font has a glyph for.</summary>
    /// <param name="source">The font.</param>
    /// <param name="codePoints">The characters of the run.</param>
    /// <param name="at">The place of the first jamo.</param>
    /// <param name="syllable">Receives the syllable's character.</param>
    /// <param name="used">Receives how many characters were joined.</param>
    /// <returns><see langword="true"/> when characters were joined.</returns>
    internal static bool TryCompose(PdfFontSource source, ReadOnlySpan<int> codePoints, int at, out int syllable, out int used)
    {
        syllable = 0;
        if (!TryStart(codePoints, at, out var start, out used))
        {
            used = 0;
            return false;
        }

        // A trailing consonant can follow, whether the syllable began as jamo or as a syllable that has none.
        var next = at + used;
        if (next < codePoints.Length && IsTrail(codePoints[next]))
        {
            start += codePoints[next] - TrailBase;
            used++;
        }

        // A syllable that is already one character with nothing to join to it stays as it is.
        if (used < JamoPair)
        {
            used = 0;
            return false;
        }

        syllable = start;
        if (source.GetGlyph(syllable) != 0)
        {
            return true;
        }

        used = 0;
        return false;
    }

    /// <summary>Tells whether a character is a precomposed syllable without a trailing consonant.</summary>
    /// <param name="codePoint">The character.</param>
    /// <returns><see langword="true"/> for such a syllable.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsLeadVowel(int codePoint) => (uint)(codePoint - SyllableBase) < SyllableCount && (codePoint - SyllableBase) % TrailCount == 0;

    /// <summary>Tells whether a character is a trailing consonant jamo.</summary>
    /// <param name="codePoint">The character.</param>
    /// <returns><see langword="true"/> for a trailing consonant.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsTrail(int codePoint) => (uint)(codePoint - TrailBase - 1) < TrailCount - 1;

    /// <summary>Finds the syllable a leading consonant and a vowel make, or the syllable already there.</summary>
    /// <param name="codePoints">The characters of the run.</param>
    /// <param name="at">The place of the first character.</param>
    /// <param name="start">Receives the syllable without a trailing consonant.</param>
    /// <param name="used">Receives how many characters make it.</param>
    /// <returns><see langword="true"/> when the characters begin a syllable.</returns>
    private static bool TryStart(ReadOnlySpan<int> codePoints, int at, out int start, out int used)
    {
        var first = codePoints[at];
        start = first;
        used = 1;
        if (IsLeadVowel(first))
        {
            return true;
        }

        if ((uint)(first - LeadBase) >= LeadCount || at + 1 >= codePoints.Length || (uint)(codePoints[at + 1] - VowelBase) >= VowelCount)
        {
            return false;
        }

        start = SyllableBase + ((((first - LeadBase) * VowelCount) + (codePoints[at + 1] - VowelBase)) * TrailCount);
        used = JamoPair;
        return true;
    }
}
