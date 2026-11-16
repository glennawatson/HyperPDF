// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>
/// The steps for scripts that follow the Universal Shaping Engine model: Sinhala, Tibetan and Mongolian. Their fonts name a
/// script and expect the features of that model in order. Sinhala also needs its vowel signs written before the consonant
/// moved to the front of the syllable, and its vowel signs that wrap the consonant split into a sign before it and a sign
/// after it. Tibetan stacks and Mongolian joined forms need no reordering.
/// </summary>
internal static class UniversalShaper
{
    /// <summary>The first Sinhala consonant.</summary>
    private const int FirstConsonant = 0x0D9A;

    /// <summary>The last Sinhala consonant.</summary>
    private const int LastConsonant = 0x0DC6;

    /// <summary>The first independent vowel.</summary>
    private const int FirstVowel = 0x0D85;

    /// <summary>The last independent vowel.</summary>
    private const int LastVowel = 0x0D96;

    /// <summary>The virama, which stacks a consonant on the one before it.</summary>
    private const int Virama = 0x0DCA;

    /// <summary>The first vowel sign.</summary>
    private const int FirstVowelSign = 0x0DCF;

    /// <summary>The last vowel sign.</summary>
    private const int LastVowelSign = 0x0DDF;

    /// <summary>The first of the signs after a syllable: candrabindu, anusvara and visarga.</summary>
    private const int FirstSign = 0x0D81;

    /// <summary>The last of the signs after a syllable.</summary>
    private const int LastSign = 0x0D83;

    /// <summary>The vowel sign kombuva, written before the consonant.</summary>
    private const int Kombuva = 0x0DD9;

    /// <summary>The vowel sign kombu deka, written before the consonant.</summary>
    private const int KombuDeka = 0x0DDB;

    /// <summary>The zero width joiner.</summary>
    private const int ZeroWidthJoiner = 0x200D;

    /// <summary>The zero width non-joiner.</summary>
    private const int ZeroWidthNonJoiner = 0x200C;

    /// <summary>Moves the vowel signs written before the consonant of each Sinhala syllable to the front of it.</summary>
    /// <param name="buffer">The glyphs.</param>
    internal static void ReorderSinhala(GlyphBuffer buffer)
    {
        var start = 0;
        while (start < buffer.Count)
        {
            var end = SinhalaSyllableEnd(buffer.Items, start);
            for (var i = start + 1; i < end; i++)
            {
                if (buffer.Items[i].CodePoint is Kombuva or KombuDeka)
                {
                    buffer.Move(i, start);
                }
            }

            start = end;
        }
    }

    /// <summary>Tells whether a character is a consonant or an independent vowel, which a syllable starts with.</summary>
    /// <param name="codePoint">The character.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsBase(int codePoint) => codePoint is >= FirstConsonant and <= LastConsonant or >= FirstVowel and <= LastVowel;

    /// <summary>Tells whether a character is a consonant.</summary>
    /// <param name="codePoint">The character.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsConsonant(int codePoint) => codePoint is >= FirstConsonant and <= LastConsonant;

    /// <summary>Tells whether a character goes on a syllable after its base.</summary>
    /// <param name="codePoint">The character.</param>
    /// <returns><see langword="true"/> when it is a vowel sign, a sign or a joiner.</returns>
    private static bool IsFollower(int codePoint) =>
        codePoint is >= FirstVowelSign and <= LastVowelSign or >= FirstSign and <= LastSign or 0x0DF2 or 0x0DF3 or ZeroWidthJoiner or ZeroWidthNonJoiner;

    /// <summary>Finds where a Sinhala syllable ends.</summary>
    /// <param name="items">The glyphs.</param>
    /// <param name="start">The syllable's first glyph.</param>
    /// <returns>The index after its last glyph.</returns>
    private static int SinhalaSyllableEnd(Span<GlyphRecord> items, int start)
    {
        if (!IsBase(items[start].CodePoint))
        {
            return start + 1;
        }

        var i = start + 1;
        while (i < items.Length)
        {
            if (items[i].CodePoint == Virama)
            {
                i = StackedEnd(items, i);
            }
            else if (IsFollower(items[i].CodePoint))
            {
                i++;
            }
            else
            {
                break;
            }
        }

        return i;
    }

    /// <summary>Finds the end of a virama, the joiner that may follow it, and the consonant it stacks.</summary>
    /// <param name="items">The glyphs.</param>
    /// <param name="virama">The index of the virama.</param>
    /// <returns>The index after the stacked consonant, or after the virama and joiner when no consonant follows.</returns>
    private static int StackedEnd(Span<GlyphRecord> items, int virama)
    {
        var i = virama + 1;
        if (i < items.Length && items[i].CodePoint is ZeroWidthJoiner or ZeroWidthNonJoiner)
        {
            i++;
        }

        return i < items.Length && IsConsonant(items[i].CodePoint) ? i + 1 : i;
    }
}
