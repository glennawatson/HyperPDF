// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>
/// Shapes Khmer. A syllable is a base consonant or independent vowel with its subscript consonants (a coeng sign and a
/// consonant), vowel signs and other signs. Before the font's features run, the pair of a coeng and the letter ro moves to
/// the front of its syllable and takes the pre-base form, and a vowel sign written before the base moves there too. Subscript
/// consonants and the signs after the base are marked so the font's below-base, above-base and post-base forms apply to them.
/// </summary>
internal static class KhmerShaper
{
    /// <summary>The first consonant.</summary>
    private const int FirstConsonant = 0x1780;

    /// <summary>The last independent vowel; the consonants and independent vowels are the bases.</summary>
    private const int LastBase = 0x17B3;

    /// <summary>The last of the signs and vowel signs.</summary>
    private const int LastSign = 0x17D3;

    /// <summary>The sign that makes the next consonant a subscript.</summary>
    private const int Coeng = 0x17D2;

    /// <summary>The letter ro, whose subscript is written before the base.</summary>
    private const int Ro = 0x179A;

    /// <summary>The first vowel sign written before the base.</summary>
    private const int FirstPreVowel = 0x17C1;

    /// <summary>The last vowel sign written before the base.</summary>
    private const int LastPreVowel = 0x17C3;

    /// <summary>The sign for a bundle of dependent marks the script ends with.</summary>
    private const int Atthacan = 0x17DD;

    /// <summary>The zero width joiner.</summary>
    private const int ZeroWidthJoiner = 0x200D;

    /// <summary>The zero width non-joiner.</summary>
    private const int ZeroWidthNonJoiner = 0x200C;

    /// <summary>The most coeng signs of a syllable that are looked at for the ro rule.</summary>
    private const int MostCoengs = 2;

    /// <summary>The length of a coeng and the letter after it.</summary>
    private const int CoengLength = 2;

    /// <summary>The marks the below-base, above-base and post-base forms apply to.</summary>
    private const uint SubscriptMasks = ShapeMasks.Blwf | ShapeMasks.Abvf | ShapeMasks.Pstf;

    /// <summary>Splits a vowel sign written around the base into the sign before it and the sign itself.</summary>
    /// <param name="codePoint">The character.</param>
    /// <param name="first">Receives the sign that goes before the base.</param>
    /// <param name="second">Receives the sign that stays after it.</param>
    /// <returns><see langword="true"/> when the character is such a vowel sign.</returns>
    internal static bool TrySplitVowel(int codePoint, out int first, out int second)
    {
        first = FirstPreVowel;
        second = codePoint;
        return codePoint is 0x17BE or 0x17BF or 0x17C0 or 0x17C4 or 0x17C5;
    }

    /// <summary>Puts each syllable's glyphs in the order the font's features expect and marks them.</summary>
    /// <param name="buffer">The glyphs.</param>
    internal static void Reorder(GlyphBuffer buffer)
    {
        var start = 0;
        while (start < buffer.Count)
        {
            var end = SyllableEnd(buffer.Items, start);
            if (end - start > 1)
            {
                ReorderSyllable(buffer, start, end);
            }

            start = end;
        }
    }

    /// <summary>Tells whether a character is a consonant or independent vowel, which a syllable starts with.</summary>
    /// <param name="codePoint">The character.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsBase(int codePoint) => codePoint is >= FirstConsonant and <= LastBase;

    /// <summary>Tells whether a character goes on a syllable after its base.</summary>
    /// <param name="codePoint">The character.</param>
    /// <returns><see langword="true"/> when it is a sign, a vowel sign or a joiner.</returns>
    private static bool IsDependent(int codePoint) =>
        codePoint is > LastBase and <= LastSign or Atthacan or ZeroWidthJoiner or ZeroWidthNonJoiner;

    /// <summary>Tells whether a character is a vowel sign written before the base.</summary>
    /// <param name="codePoint">The character.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsPreVowel(int codePoint) => codePoint is >= FirstPreVowel and <= LastPreVowel;

    /// <summary>Finds where a syllable ends.</summary>
    /// <param name="items">The glyphs.</param>
    /// <param name="start">The syllable's first glyph.</param>
    /// <returns>The index after its last glyph.</returns>
    private static int SyllableEnd(Span<GlyphRecord> items, int start)
    {
        if (!IsBase(items[start].CodePoint))
        {
            return start + 1;
        }

        var i = start + 1;
        while (i < items.Length)
        {
            var codePoint = items[i].CodePoint;
            if (codePoint == Coeng)
            {
                // A coeng takes the letter after it as a subscript.
                i += i + 1 < items.Length && IsBase(items[i + 1].CodePoint) ? CoengLength : 1;
            }
            else if (IsDependent(codePoint))
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

    /// <summary>Moves a syllable's coeng and ro to its front, moves a vowel sign written before the base there too, and marks the rest.</summary>
    /// <param name="buffer">The glyphs.</param>
    /// <param name="start">The syllable's first glyph.</param>
    /// <param name="end">The index after its last glyph.</param>
    private static void ReorderSyllable(GlyphBuffer buffer, int start, int end)
    {
        var items = buffer.Items;
        for (var i = start + 1; i < end; i++)
        {
            items[i].Mask |= SubscriptMasks;
        }

        var coengs = 0;
        for (var i = start + 1; i < end; i++)
        {
            var codePoint = items[i].CodePoint;
            if (codePoint == Coeng && coengs <= MostCoengs && i + 1 < end)
            {
                coengs++;
                if (items[i + 1].CodePoint == Ro)
                {
                    MoveRoToFront(buffer, start, i, end);
                    coengs = MostCoengs;
                }
            }
            else if (IsPreVowel(codePoint))
            {
                buffer.Move(i, start);
            }
        }
    }

    /// <summary>Moves a coeng and the ro after it to the front of the syllable and marks them as the pre-base form.</summary>
    /// <param name="buffer">The glyphs.</param>
    /// <param name="start">The syllable's first glyph.</param>
    /// <param name="coeng">The index of the coeng.</param>
    /// <param name="end">The index after the syllable's last glyph.</param>
    private static void MoveRoToFront(GlyphBuffer buffer, int start, int coeng, int end)
    {
        var items = buffer.Items;
        items[coeng].Mask |= ShapeMasks.Pref;
        items[coeng + 1].Mask |= ShapeMasks.Pref;
        buffer.Move(coeng, start);
        buffer.Move(coeng + 1, start + 1);

        // What follows the pair tells "base, coeng ro, coeng other" from "base, coeng other, coeng ro" to the font.
        for (var j = coeng + CoengLength; j < end; j++)
        {
            items[j].Mask |= ShapeMasks.Cfar;
        }
    }
}
