// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>
/// The syllable model of the Indic scripts (OpenType specification, "Developing OpenType Fonts for Devanagari Script", new
/// shaping model): text is split into syllables, the base consonant of each is found, a vowel sign written before the
/// consonant moves in front of the syllable, and a ra that formed a reph moves behind it. The substitutions in between are
/// the font's own lookups for the nukt, akhn, rphf, pref, blwf, half, pstf and later features.
/// </summary>
internal static class IndicShaper
{
    /// <summary>Splits the glyphs into syllables and records what each glyph is.</summary>
    /// <param name="buffer">The glyphs, one for each character.</param>
    internal static void Prepare(GlyphBuffer buffer)
    {
        var items = buffer.Items;
        var categories = ArrayPool<IndicCategory>.Shared.Rent(items.Length);
        try
        {
            for (var i = 0; i < items.Length; i++)
            {
                categories[i] = IndicData.CategoryOf(items[i].CodePoint);
                items[i].Flags |= FlagsOf(categories[i]);
            }

            var start = 0;
            while (start < items.Length)
            {
                var end = IndicSyllables.End(categories.AsSpan(0, items.Length), start);
                for (var i = start; i < end; i++)
                {
                    items[i].Syllable = start + 1;
                }

                start = end;
            }
        }
        finally
        {
            ArrayPool<IndicCategory>.Shared.Return(categories);
        }
    }

    /// <summary>Finds the base consonant of each syllable, marks the glyphs before it, and moves vowel signs that precede the consonant to the front.</summary>
    /// <param name="buffer">The glyphs.</param>
    internal static void ReorderInitially(GlyphBuffer buffer)
    {
        var start = 0;
        while (start < buffer.Count)
        {
            var end = IndicSyllables.RunEnd(buffer, start);
            ReorderSyllable(buffer, start, end);
            start = end;
        }
    }

    /// <summary>Records which ra glyphs became a reph, and sends the vowel sign back to the front of syllables where none did.</summary>
    /// <param name="buffer">The glyphs, after the rphf feature.</param>
    internal static void SettleReph(GlyphBuffer buffer)
    {
        var start = 0;
        while (start < buffer.Count)
        {
            var end = IndicSyllables.RunEnd(buffer, start);
            SettleSyllable(buffer, start, end);
            start = end;
        }
    }

    /// <summary>Moves each reph behind its syllable's base, matras and post-base signs, in front of the vowel modifiers.</summary>
    /// <param name="buffer">The glyphs, after the basic features.</param>
    internal static void ReorderFinally(GlyphBuffer buffer)
    {
        var start = 0;
        while (start < buffer.Count)
        {
            var end = IndicSyllables.RunEnd(buffer, start);
            PlaceReph(buffer, start, end);
            start = end;
        }
    }

    /// <summary>Gives every glyph of a syllable the cluster of the syllable's first glyph, so the syllable reads as one unit of text however its glyphs were reordered.</summary>
    /// <param name="buffer">The glyphs.</param>
    internal static void MergeClusters(GlyphBuffer buffer)
    {
        var start = 0;
        while (start < buffer.Count)
        {
            var end = IndicSyllables.RunEnd(buffer, start);
            var cluster = int.MaxValue;
            for (var i = start; i < end; i++)
            {
                cluster = Math.Min(cluster, buffer.Items[i].Cluster);
            }

            for (var i = start; i < end; i++)
            {
                buffer.Items[i].Cluster = cluster;
            }

            start = end;
        }
    }

    /// <summary>Gets the flags a category sets on a glyph.</summary>
    /// <param name="category">The category.</param>
    /// <returns>The flags.</returns>
    private static GlyphFlags FlagsOf(IndicCategory category) => category switch
    {
        IndicCategory.Consonant => GlyphFlags.Consonant,
        IndicCategory.Halant => GlyphFlags.Halant,
        IndicCategory.MatraBefore => GlyphFlags.PreBaseMatra,
        IndicCategory.Modifier => GlyphFlags.Modifier,
        IndicCategory.Joiner => GlyphFlags.Joiner | GlyphFlags.Hidden,
        IndicCategory.NonJoiner => GlyphFlags.NonJoiner | GlyphFlags.Hidden,
        _ => GlyphFlags.None,
    };

    /// <summary>Reorders one syllable before the basic features.</summary>
    /// <param name="buffer">The glyphs.</param>
    /// <param name="start">The first glyph of the syllable.</param>
    /// <param name="end">The index after the last glyph.</param>
    private static void ReorderSyllable(GlyphBuffer buffer, int start, int end)
    {
        var items = buffer.Items;
        var chain = start;
        if (IndicSyllables.IsRephCandidate(items, start, end))
        {
            items[start].Flags |= GlyphFlags.RephCandidate;
            items[start].Mask |= ShapeMasks.Reph;
            chain = start + IndicSyllables.RephLength;
        }

        var baseIndex = IndicSyllables.FindBase(items, chain, end);
        if (baseIndex >= 0)
        {
            items[baseIndex].Flags |= GlyphFlags.Base;
            MarkBeforeBase(items, start, baseIndex);
        }

        MoveMatrasTo(buffer, start, end, chain);
    }

    /// <summary>Marks the glyphs before the base of a syllable, which may take half forms.</summary>
    /// <param name="items">The glyphs.</param>
    /// <param name="start">The first glyph of the syllable.</param>
    /// <param name="baseIndex">The base consonant's index.</param>
    private static void MarkBeforeBase(Span<GlyphRecord> items, int start, int baseIndex)
    {
        for (var i = start; i < baseIndex; i++)
        {
            items[i].Mask |= ShapeMasks.PreBase;
        }
    }

    /// <summary>Records whether the ra that starts a syllable became a reph.</summary>
    /// <param name="buffer">The glyphs.</param>
    /// <param name="start">The first glyph of the syllable.</param>
    /// <param name="end">The index after the last glyph.</param>
    private static void SettleSyllable(GlyphBuffer buffer, int start, int end)
    {
        ref var first = ref buffer.Items[start];
        if ((first.Flags & GlyphFlags.RephCandidate) == 0)
        {
            return;
        }

        first.Flags &= ~GlyphFlags.RephCandidate;
        if ((first.Flags & GlyphFlags.Ligated) != 0)
        {
            first.Flags |= GlyphFlags.Reph;
            return;
        }

        // Without a reph the ra stays a consonant, so the vowel sign goes in front of the whole syllable.
        MoveMatrasTo(buffer, start, end, start);
    }

    /// <summary>Moves the reph of a syllable, if it has one, behind everything but the vowel modifiers.</summary>
    /// <param name="buffer">The glyphs.</param>
    /// <param name="start">The first glyph of the syllable.</param>
    /// <param name="end">The index after the last glyph.</param>
    private static void PlaceReph(GlyphBuffer buffer, int start, int end)
    {
        if ((buffer.Items[start].Flags & GlyphFlags.Reph) == 0 || end - start < IndicSyllables.RephLength)
        {
            return;
        }

        var position = end - 1;
        while (position > start && (buffer.Items[position].Flags & GlyphFlags.Modifier) != 0)
        {
            position--;
        }

        buffer.Move(start, position);
    }

    /// <summary>Moves the vowel signs that precede the consonant to a place in the syllable, keeping their order.</summary>
    /// <param name="buffer">The glyphs.</param>
    /// <param name="start">The first glyph of the syllable.</param>
    /// <param name="end">The index after the last glyph.</param>
    /// <param name="target">The index the first sign moves to.</param>
    private static void MoveMatrasTo(GlyphBuffer buffer, int start, int end, int target)
    {
        var place = target;
        for (var i = Math.Max(place, start); i < end; i++)
        {
            if ((buffer.Items[i].Flags & GlyphFlags.PreBaseMatra) == 0)
            {
                continue;
            }

            buffer.Move(i, place);
            place++;
        }
    }
}
