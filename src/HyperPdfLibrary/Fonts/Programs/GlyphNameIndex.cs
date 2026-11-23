// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>
/// Finds glyphs by name through an open-addressing hash table of glyph ids, built in one pass over the glyphs. A name
/// listed for several glyphs finds the lowest id. The table holds ids only; names are read from the program.
/// </summary>
internal static class GlyphNameIndex
{
    /// <summary>A slot that holds no glyph.</summary>
    private const int EmptySlot = -1;

    /// <summary>How many slots the table has for each named glyph, which keeps probe runs short.</summary>
    private const int SlotsPerGlyph = 2;

    /// <summary>Builds the table of a program's glyph names.</summary>
    /// <param name="program">The font program.</param>
    /// <returns>The slots: glyph ids, or <see cref="EmptySlot"/>.</returns>
    internal static int[] Build(FontProgram program)
    {
        var count = program.GlyphCount;
        var slots = new int[BitOperations.RoundUpToPowerOf2((uint)Math.Max(count * SlotsPerGlyph, SlotsPerGlyph))];
        Array.Fill(slots, EmptySlot);
        var mask = slots.Length - 1;

        // Ascending ids, so the first glyph placed under a name is the lowest one with it.
        for (var glyph = 0; glyph < count; glyph++)
        {
            var name = program.GetGlyphName(glyph);
            if (!name.IsEmpty)
            {
                Place(program, slots, mask, glyph, name);
            }
        }

        return slots;
    }

    /// <summary>Finds the lowest glyph id with a name.</summary>
    /// <param name="program">The font program.</param>
    /// <param name="slots">The table from <see cref="Build"/>.</param>
    /// <param name="name">The name.</param>
    /// <returns>The glyph id, or -1.</returns>
    internal static int Find(FontProgram program, int[] slots, ReadOnlySpan<byte> name)
    {
        var mask = slots.Length - 1;
        var slot = Hash(name) & mask;
        var glyph = slots[slot];

        // The table is never more than half full, so a run of filled slots always ends.
        while (glyph != EmptySlot && !program.GetGlyphName(glyph).SequenceEqual(name))
        {
            slot = (slot + 1) & mask;
            glyph = slots[slot];
        }

        return glyph;
    }

    /// <summary>Puts a glyph in the table unless a lower glyph already has its name.</summary>
    /// <param name="program">The font program.</param>
    /// <param name="slots">The table.</param>
    /// <param name="mask">The slot count less one.</param>
    /// <param name="glyph">The glyph id.</param>
    /// <param name="name">Its name.</param>
    private static void Place(FontProgram program, int[] slots, int mask, int glyph, ReadOnlySpan<byte> name)
    {
        var slot = Hash(name) & mask;
        while (slots[slot] != EmptySlot)
        {
            if (program.GetGlyphName(slots[slot]).SequenceEqual(name))
            {
                return;
            }

            slot = (slot + 1) & mask;
        }

        slots[slot] = glyph;
    }

    /// <summary>Hashes a name for its first slot.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The hash, never negative once masked.</returns>
    private static int Hash(ReadOnlySpan<byte> name)
    {
        HashCode hash = default;
        hash.AddBytes(name);
        return hash.ToHashCode();
    }
}
