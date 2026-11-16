// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>
/// Shapes Myanmar. A syllable is a base with an optional kinzi in front (nga, ra or Mon nga with an asat and a virama),
/// stacked consonants, medials, vowel signs and tone marks. Before the font's features run, each syllable's glyphs are put
/// in the order the font expects: the vowel sign written before the base and the medial ra go first, the base follows,
/// and the kinzi, signs and vowels below the base go after it. The font then forms the kinzi, the subscripts and the
/// ligatures from that order.
/// </summary>
internal static class MyanmarShaper
{
    /// <summary>The first character of the Myanmar block.</summary>
    private const int BlockStart = 0x1000;

    /// <summary>The last character of the Myanmar block.</summary>
    private const int BlockEnd = 0x109F;

    /// <summary>The length of a kinzi: the consonant, an asat and a virama.</summary>
    private const int KinziLength = 3;

    /// <summary>The length of a virama and the consonant it stacks.</summary>
    private const int StackLength = 2;

    /// <summary>The place of the asat after a kinzi's first letter.</summary>
    private const int AsatOffset = 1;

    /// <summary>The place of the virama after a kinzi's first letter.</summary>
    private const int ViramaOffset = 2;

    /// <summary>The first variation selector.</summary>
    private const int FirstVariation = 0xFE00;

    /// <summary>The last variation selector.</summary>
    private const int LastVariation = 0xFE0F;

    /// <summary>The dotted circle.</summary>
    private const int DottedCircle = 0x25CC;

    /// <summary>The zero width joiner.</summary>
    private const int ZeroWidthJoiner = 0x200D;

    /// <summary>The zero width non-joiner.</summary>
    private const int ZeroWidthNonJoiner = 0x200C;

    /// <summary>What each range of the block does, from the broadest to the most particular; a later range takes over from an earlier one.</summary>
    private static readonly CategoryRange[] Ranges =
    [
        new(0x1000, 0x1020, MyanmarCategory.Consonant),
        new(0x103F, 0x103F, MyanmarCategory.Consonant),
        new(0x1050, 0x1051, MyanmarCategory.Consonant),
        new(0x105B, 0x105D, MyanmarCategory.Consonant),
        new(0x1061, 0x1061, MyanmarCategory.Consonant),
        new(0x1065, 0x1066, MyanmarCategory.Consonant),
        new(0x106E, 0x1070, MyanmarCategory.Consonant),
        new(0x1075, 0x1081, MyanmarCategory.Consonant),
        new(0x108E, 0x108E, MyanmarCategory.Consonant),
        new(0x1004, 0x1004, MyanmarCategory.Ra),
        new(0x101B, 0x101B, MyanmarCategory.Ra),
        new(0x105A, 0x105A, MyanmarCategory.Ra),
        new(0x1021, 0x102A, MyanmarCategory.Vowel),
        new(0x1052, 0x1055, MyanmarCategory.Vowel),
        new(0x1040, 0x1049, MyanmarCategory.GenericBase),
        new(0x1090, 0x1099, MyanmarCategory.GenericBase),
        new(0x102B, 0x1038, MyanmarCategory.Dependent),
        new(0x103B, 0x103B, MyanmarCategory.Dependent),
        new(0x103D, 0x103E, MyanmarCategory.Dependent),
        new(0x1056, 0x1057, MyanmarCategory.Dependent),
        new(0x105E, 0x1060, MyanmarCategory.Dependent),
        new(0x1062, 0x1064, MyanmarCategory.Dependent),
        new(0x1067, 0x106D, MyanmarCategory.Dependent),
        new(0x1071, 0x1074, MyanmarCategory.Dependent),
        new(0x1082, 0x1083, MyanmarCategory.Dependent),
        new(0x1085, 0x108D, MyanmarCategory.Dependent),
        new(0x108F, 0x108F, MyanmarCategory.Dependent),
        new(0x1039, 0x1039, MyanmarCategory.Virama),
        new(0x103A, 0x103A, MyanmarCategory.Asat),
        new(0x103C, 0x103C, MyanmarCategory.MedialRa),
        new(0x1031, 0x1031, MyanmarCategory.VowelPre),
        new(0x1084, 0x1084, MyanmarCategory.VowelPre),
        new(0x102F, 0x1030, MyanmarCategory.VowelBelow),
        new(0x1058, 0x1059, MyanmarCategory.VowelBelow),
        new(0x1036, 0x1038, MyanmarCategory.Sign),
    ];

    /// <summary>The category of each character of the block.</summary>
    private static readonly MyanmarCategory[] Block = BuildBlock();

    /// <summary>Tells what a character does in a syllable.</summary>
    /// <param name="codePoint">The character.</param>
    /// <returns>The category.</returns>
    internal static MyanmarCategory CategoryOf(int codePoint)
    {
        if (codePoint is >= FirstVariation and <= LastVariation)
        {
            return MyanmarCategory.Variation;
        }

        if (codePoint is ZeroWidthJoiner or ZeroWidthNonJoiner)
        {
            return MyanmarCategory.Joiner;
        }

        if (codePoint == DottedCircle)
        {
            return MyanmarCategory.GenericBase;
        }

        return codePoint is >= BlockStart and <= BlockEnd ? CategoryInBlock(codePoint) : MyanmarCategory.Other;
    }

    /// <summary>Puts each syllable's glyphs in order.</summary>
    /// <param name="buffer">The glyphs.</param>
    internal static void Reorder(GlyphBuffer buffer)
    {
        var start = 0;
        while (start < buffer.Count)
        {
            var end = SyllableEnd(buffer.Items, start);
            if (end - start > 1)
            {
                ReorderSyllable(buffer.Items, start, end);
            }

            start = end;
        }
    }

    /// <summary>Tells what a character in the Myanmar block does in a syllable.</summary>
    /// <param name="codePoint">The character.</param>
    /// <returns>The category.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MyanmarCategory CategoryInBlock(int codePoint) => Block[codePoint - BlockStart];

    /// <summary>Builds the category of each character of the block from the ranges, the later ranges taking over from the earlier.</summary>
    /// <returns>The categories, by place in the block.</returns>
    private static MyanmarCategory[] BuildBlock()
    {
        var table = new MyanmarCategory[BlockEnd - BlockStart + 1];
        foreach (var range in Ranges)
        {
            Array.Fill(table, range.Category, range.Start - BlockStart, range.End - range.Start + 1);
        }

        return table;
    }

    /// <summary>Tells whether a category can be the base of a syllable.</summary>
    /// <param name="category">The category.</param>
    /// <returns><see langword="true"/> when it can.</returns>
    private static bool IsBase(MyanmarCategory category) =>
        category is MyanmarCategory.Consonant or MyanmarCategory.Ra or MyanmarCategory.Vowel or MyanmarCategory.GenericBase;

    /// <summary>Tells whether a category goes on a syllable after its base.</summary>
    /// <param name="category">The category.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool IsFollower(MyanmarCategory category) =>
        category is MyanmarCategory.Asat or MyanmarCategory.MedialRa or MyanmarCategory.VowelPre or MyanmarCategory.VowelBelow
            or MyanmarCategory.Sign or MyanmarCategory.Variation or MyanmarCategory.Joiner or MyanmarCategory.Dependent;

    /// <summary>Tells whether a syllable starts with a kinzi.</summary>
    /// <param name="items">The glyphs.</param>
    /// <param name="start">The index to look at.</param>
    /// <returns><see langword="true"/> when a nga, ra or Mon nga, an asat, a virama and a base follow in that order.</returns>
    private static bool StartsWithKinzi(Span<GlyphRecord> items, int start) =>
        start + KinziLength < items.Length
        && CategoryOf(items[start].CodePoint) == MyanmarCategory.Ra
        && CategoryOf(items[start + AsatOffset].CodePoint) == MyanmarCategory.Asat
        && CategoryOf(items[start + ViramaOffset].CodePoint) == MyanmarCategory.Virama
        && IsBase(CategoryOf(items[start + KinziLength].CodePoint));

    /// <summary>Finds where a syllable ends.</summary>
    /// <param name="items">The glyphs.</param>
    /// <param name="start">The syllable's first glyph.</param>
    /// <returns>The index after its last glyph.</returns>
    private static int SyllableEnd(Span<GlyphRecord> items, int start)
    {
        var i = StartsWithKinzi(items, start) ? start + KinziLength : start;
        if (!IsBase(CategoryOf(items[i].CodePoint)))
        {
            return start + 1;
        }

        i++;
        while (i < items.Length)
        {
            var category = CategoryOf(items[i].CodePoint);
            if (category == MyanmarCategory.Virama)
            {
                // A virama stacks the consonant after it under the one before.
                i += i + 1 < items.Length && IsBase(CategoryOf(items[i + 1].CodePoint)) ? StackLength : 1;
            }
            else if (IsFollower(category))
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

    /// <summary>Orders one syllable's glyphs by where each goes.</summary>
    /// <param name="items">The glyphs.</param>
    /// <param name="start">The syllable's first glyph.</param>
    /// <param name="end">The index after its last glyph.</param>
    private static void ReorderSyllable(Span<GlyphRecord> items, int start, int end)
    {
        var length = end - start;
        var rented = ArrayPool<MyanmarPosition>.Shared.Rent(length);
        try
        {
            var positions = rented.AsSpan(0, length);
            AssignPositions(items.Slice(start, length), positions);
            SortStable(items.Slice(start, length), positions);
            FlipLeftVowels(items.Slice(start, length), positions);
        }
        finally
        {
            ArrayPool<MyanmarPosition>.Shared.Return(rented);
        }
    }

    /// <summary>Finds the base of a syllable.</summary>
    /// <param name="syllable">The syllable's glyphs.</param>
    /// <param name="from">The index to start looking at, after any kinzi.</param>
    /// <returns>The index of the first glyph that can be a base, or <paramref name="from"/> when none can.</returns>
    private static int FindBase(Span<GlyphRecord> syllable, int from)
    {
        for (var i = from; i < syllable.Length; i++)
        {
            if (IsBase(CategoryOf(syllable[i].CodePoint)))
            {
                return i;
            }
        }

        return from;
    }

    /// <summary>Works out where each glyph of a syllable goes.</summary>
    /// <param name="syllable">The syllable's glyphs.</param>
    /// <param name="positions">Receives the position of each.</param>
    private static void AssignPositions(Span<GlyphRecord> syllable, Span<MyanmarPosition> positions)
    {
        var hasKinzi = StartsWithKinzi(syllable, 0);
        var baseIndex = FindBase(syllable, hasKinzi ? KinziLength : 0);
        for (var i = 0; i < baseIndex; i++)
        {
            positions[i] = hasKinzi && i < KinziLength ? MyanmarPosition.AfterMain : MyanmarPosition.PreConsonant;
        }

        positions[baseIndex] = MyanmarPosition.Base;
        AssignAfterBase(syllable, positions, baseIndex + 1);
    }

    /// <summary>Works out where each glyph after the base goes.</summary>
    /// <param name="syllable">The syllable's glyphs.</param>
    /// <param name="positions">The positions, filled in up to the base.</param>
    /// <param name="first">The index after the base.</param>
    private static void AssignAfterBase(Span<GlyphRecord> syllable, Span<MyanmarPosition> positions, int first)
    {
        var running = MyanmarPosition.AfterMain;
        for (var i = first; i < syllable.Length; i++)
        {
            var category = CategoryOf(syllable[i].CodePoint);
            if (category == MyanmarCategory.MedialRa)
            {
                positions[i] = MyanmarPosition.PreConsonant;
            }
            else if (category == MyanmarCategory.VowelPre)
            {
                positions[i] = MyanmarPosition.PreMatra;
            }
            else if (category == MyanmarCategory.Variation)
            {
                positions[i] = positions[i - 1];
            }
            else
            {
                running = NextRunning(running, category);
                positions[i] = running == MyanmarPosition.Below && category == MyanmarCategory.Sign ? MyanmarPosition.BeforeSubjoined : running;
            }
        }
    }

    /// <summary>Works out the position the glyphs after a vowel sign below the base are in.</summary>
    /// <param name="running">The position so far.</param>
    /// <param name="category">The category of the glyph.</param>
    /// <returns>The position the glyph and those after it are in.</returns>
    private static MyanmarPosition NextRunning(MyanmarPosition running, MyanmarCategory category) => running switch
    {
        MyanmarPosition.AfterMain when category == MyanmarCategory.VowelBelow => MyanmarPosition.Below,
        MyanmarPosition.Below when category is not (MyanmarCategory.Sign or MyanmarCategory.VowelBelow) => MyanmarPosition.AfterSubjoined,
        _ => running,
    };

    /// <summary>Sorts a syllable's glyphs by position, keeping the order of glyphs in the same position.</summary>
    /// <param name="syllable">The syllable's glyphs.</param>
    /// <param name="positions">The position of each, moved with the glyphs.</param>
    private static void SortStable(Span<GlyphRecord> syllable, Span<MyanmarPosition> positions)
    {
        for (var i = 1; i < syllable.Length; i++)
        {
            var glyph = syllable[i];
            var position = positions[i];
            var j = i - 1;
            while (j >= 0 && positions[j] > position)
            {
                syllable[j + 1] = syllable[j];
                positions[j + 1] = positions[j];
                j--;
            }

            syllable[j + 1] = glyph;
            positions[j + 1] = position;
        }
    }

    /// <summary>Reverses the glyphs of two or more vowel signs written before the base, then each sign's own run, so the signs keep their typed order.</summary>
    /// <param name="syllable">The syllable's glyphs, sorted.</param>
    /// <param name="positions">The position of each.</param>
    private static void FlipLeftVowels(Span<GlyphRecord> syllable, ReadOnlySpan<MyanmarPosition> positions)
    {
        var first = -1;
        var last = -1;
        for (var i = 0; i < positions.Length; i++)
        {
            if (positions[i] != MyanmarPosition.PreMatra)
            {
                continue;
            }

            first = first < 0 ? i : first;
            last = i;
        }

        if (first >= last)
        {
            return;
        }

        syllable.Slice(first, last - first + 1).Reverse();
        var run = first;
        for (var j = first; j <= last; j++)
        {
            if (CategoryOf(syllable[j].CodePoint) != MyanmarCategory.VowelPre)
            {
                continue;
            }

            syllable.Slice(run, j - run + 1).Reverse();
            run = j + 1;
        }
    }

    /// <summary>A range of characters of one category.</summary>
    /// <param name="Start">The first character.</param>
    /// <param name="End">The last character.</param>
    /// <param name="Category">What the characters do in a syllable.</param>
    private readonly record struct CategoryRange(int Start, int End, MyanmarCategory Category);
}
