// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>
/// Finds syllables and their base consonants in Indic text (OpenType specification, "Developing OpenType Fonts for Devanagari
/// Script": syllable structure and base consonant identification).
/// </summary>
internal static class IndicSyllables
{
    /// <summary>The characters of the ra and virama that may become a reph.</summary>
    internal const int RephLength = 2;

    /// <summary>Finds where the syllable that starts at an index ends.</summary>
    /// <param name="categories">The categories of the characters.</param>
    /// <param name="start">The first character of the syllable.</param>
    /// <returns>The index after the syllable's last character.</returns>
    internal static int End(ReadOnlySpan<IndicCategory> categories, int start)
    {
        if (categories[start] is not (IndicCategory.Consonant or IndicCategory.Vowel))
        {
            return start + 1;
        }

        var i = ConsonantCluster(categories, start);
        while (i < categories.Length && categories[i] is IndicCategory.Matra or IndicCategory.MatraBefore or IndicCategory.Nukta)
        {
            i++;
        }

        return SkipModifiers(categories, i);
    }

    /// <summary>Finds where the run of glyphs of one syllable that starts at an index ends.</summary>
    /// <param name="buffer">The glyphs.</param>
    /// <param name="start">The first glyph of the run.</param>
    /// <returns>The index after the run.</returns>
    internal static int RunEnd(GlyphBuffer buffer, int start)
    {
        var items = buffer.Items;
        var end = start + 1;
        while (end < items.Length && items[end].Syllable == items[start].Syllable)
        {
            end++;
        }

        return end;
    }

    /// <summary>Determines whether a syllable starts with a ra and a virama that can form a reph.</summary>
    /// <param name="items">The glyphs.</param>
    /// <param name="start">The first glyph of the syllable.</param>
    /// <param name="end">The index after the last glyph.</param>
    /// <returns><see langword="true"/> when the syllable starts with ra and a virama that a joiner does not follow.</returns>
    internal static bool IsRephCandidate(Span<GlyphRecord> items, int start, int end)
    {
        if (end - start <= RephLength || !IndicData.IsRa(items[start].CodePoint) || (items[start].Flags & GlyphFlags.Consonant) == 0)
        {
            return false;
        }

        return (items[start + 1].Flags & GlyphFlags.Halant) != 0 && (items[start + RephLength].Flags & (GlyphFlags.Joiner | GlyphFlags.NonJoiner)) == 0;
    }

    /// <summary>Finds the base consonant of a syllable: the last consonant, unless the script puts later consonants below the first, or the last is a ra or ya that takes a subjoined form.</summary>
    /// <param name="items">The glyphs.</param>
    /// <param name="chain">The index of the first glyph after the reph candidate.</param>
    /// <param name="end">The index after the syllable.</param>
    /// <returns>The base's index; -1 when the syllable has no consonant, as an independent vowel has none.</returns>
    internal static int FindBase(Span<GlyphRecord> items, int chain, int end)
    {
        var first = -1;
        var last = -1;
        for (var i = chain; i < end; i++)
        {
            if ((items[i].Flags & GlyphFlags.Consonant) == 0)
            {
                continue;
            }

            first = first < 0 ? i : first;
            last = i;
        }

        if (first < 0 || IndicData.BaseIsFirstConsonant(items[first].CodePoint))
        {
            return first;
        }

        var candidate = last;
        while (candidate > first && IndicData.IsSubjoiningConsonant(items[candidate].CodePoint) && FollowsHalant(items, candidate, first))
        {
            candidate = PreviousConsonant(items, candidate, first);
        }

        return candidate;
    }

    /// <summary>Skips the vowel modifiers, accents and joiners that end a syllable.</summary>
    /// <param name="categories">The categories of the characters.</param>
    /// <param name="index">The index to start at.</param>
    /// <returns>The index after them.</returns>
    private static int SkipModifiers(ReadOnlySpan<IndicCategory> categories, int index)
    {
        var i = index;
        while (i < categories.Length && categories[i] is IndicCategory.Modifier or IndicCategory.Joiner or IndicCategory.NonJoiner)
        {
            i++;
        }

        return i;
    }

    /// <summary>Reads the consonants of a syllable joined by viramas: a consonant or vowel, a nukta, then a virama and the next consonant.</summary>
    /// <param name="categories">The categories of the characters.</param>
    /// <param name="start">The first character of the syllable.</param>
    /// <returns>The index after the last virama or nukta of the cluster.</returns>
    private static int ConsonantCluster(ReadOnlySpan<IndicCategory> categories, int start)
    {
        var i = start + 1;
        while (true)
        {
            while (i < categories.Length && categories[i] == IndicCategory.Nukta)
            {
                i++;
            }

            var afterHalant = SkipHalant(categories, i);
            if (afterHalant == i || afterHalant >= categories.Length || categories[afterHalant] != IndicCategory.Consonant)
            {
                return afterHalant;
            }

            i = afterHalant + 1;
        }
    }

    /// <summary>Skips a virama and the joiner or non-joiner that may follow it.</summary>
    /// <param name="categories">The categories of the characters.</param>
    /// <param name="index">The index to start at.</param>
    /// <returns>The index after the virama; <paramref name="index"/> when there is none.</returns>
    private static int SkipHalant(ReadOnlySpan<IndicCategory> categories, int index)
    {
        if (index >= categories.Length || categories[index] != IndicCategory.Halant)
        {
            return index;
        }

        var next = index + 1;
        return next < categories.Length && categories[next] is IndicCategory.Joiner or IndicCategory.NonJoiner ? next + 1 : next;
    }

    /// <summary>Determines whether a virama comes straight before a consonant, ignoring joiners.</summary>
    /// <param name="items">The glyphs.</param>
    /// <param name="index">The consonant's index.</param>
    /// <param name="floor">The first index to look at.</param>
    /// <returns><see langword="true"/> when a virama precedes the consonant.</returns>
    private static bool FollowsHalant(Span<GlyphRecord> items, int index, int floor)
    {
        for (var i = index - 1; i >= floor; i--)
        {
            if ((items[i].Flags & GlyphFlags.Halant) != 0)
            {
                return true;
            }

            if ((items[i].Flags & (GlyphFlags.Joiner | GlyphFlags.NonJoiner)) == 0)
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>Finds the consonant before another.</summary>
    /// <param name="items">The glyphs.</param>
    /// <param name="index">The consonant's index.</param>
    /// <param name="floor">The first index to look at.</param>
    /// <returns>The index of the previous consonant, or <paramref name="floor"/>.</returns>
    private static int PreviousConsonant(Span<GlyphRecord> items, int index, int floor)
    {
        for (var i = index - 1; i > floor; i--)
        {
            if ((items[i].Flags & GlyphFlags.Consonant) != 0)
            {
                return i;
            }
        }

        return floor;
    }
}
