// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.TestAssets.Shaping;

/// <summary>Writes GPOS lookup subtables (OpenType, "GPOS": lookup types 1 to 6), for building test fonts.</summary>
public static class GposSubtables
{
    /// <summary>Writes a single adjustment that gives every covered glyph the same adjustment (lookup type 1, format 1).</summary>
    /// <param name="glyphs">The glyphs covered.</param>
    /// <param name="value">The adjustment.</param>
    /// <returns>The subtable.</returns>
    public static byte[] Single(int[] glyphs, ValueAdjustment value)
    {
        var format = value.Format();
        var buffer = new ByteBuffer();
        buffer.Write(1);
        var coverage = buffer.Reserve();
        buffer.Write(format);
        value.Write(buffer, format);
        buffer.AttachChild(coverage, CommonTables.Coverage(glyphs));
        return buffer.ToArray();
    }

    /// <summary>Writes a pair adjustment for pairs that start with one glyph (lookup type 2, format 1).</summary>
    /// <param name="first">The first glyph.</param>
    /// <param name="rules">The pairs, in ascending order of second glyph.</param>
    /// <returns>The subtable.</returns>
    public static byte[] PairGlyphs(int first, params PairRule[] rules)
    {
        var firstFormat = 0;
        var secondFormat = 0;
        foreach (var rule in rules)
        {
            firstFormat |= rule.First.Format();
            secondFormat |= rule.Next.Format();
        }

        var buffer = new ByteBuffer();
        buffer.Write(1);
        var coverage = buffer.Reserve();
        buffer.Write(firstFormat, secondFormat, 1);
        var set = buffer.Reserve();
        buffer.AttachChild(coverage, CommonTables.Coverage([first]));
        var pairSet = new ByteBuffer();
        pairSet.Write(rules.Length);
        foreach (var rule in rules)
        {
            pairSet.Write(rule.Second);
            rule.First.Write(pairSet, firstFormat);
            rule.Next.Write(pairSet, secondFormat);
        }

        buffer.AttachChild(set, pairSet.ToArray());
        return buffer.ToArray();
    }

    /// <summary>Writes a class-based pair adjustment (lookup type 2, format 2).</summary>
    /// <param name="covered">The first glyphs the subtable covers.</param>
    /// <param name="firstClasses">The class of each first glyph.</param>
    /// <param name="secondClasses">The class of each second glyph.</param>
    /// <param name="adjustments">The adjustment of the first glyph for each pair of classes, indexed by the first glyph's class and then the second's.</param>
    /// <returns>The subtable.</returns>
    public static byte[] PairClasses(int[] covered, IReadOnlyDictionary<int, int> firstClasses, IReadOnlyDictionary<int, int> secondClasses, ValueAdjustment[][] adjustments)
    {
        var format = 0;
        foreach (var adjustment in adjustments.SelectMany(static row => row))
        {
            format |= adjustment.Format();
        }

        var buffer = new ByteBuffer();
        buffer.Write(0x2);
        var coverage = buffer.Reserve();
        buffer.Write(format, 0);
        var firstDef = buffer.Reserve();
        var secondDef = buffer.Reserve();
        buffer.Write(adjustments.Length, adjustments[0].Length);
        foreach (var adjustment in adjustments.SelectMany(static row => row))
        {
            adjustment.Write(buffer, format);
        }

        buffer.AttachChild(coverage, CommonTables.Coverage(covered));
        buffer.AttachChild(firstDef, CommonTables.ClassDef(firstClasses));
        buffer.AttachChild(secondDef, CommonTables.ClassDef(secondClasses));
        return buffer.ToArray();
    }

    /// <summary>Writes a cursive attachment (lookup type 3).</summary>
    /// <param name="anchors">The entry and exit anchor of each glyph, in that order; either may be missing.</param>
    /// <returns>The subtable.</returns>
    public static byte[] Cursive(IReadOnlyDictionary<int, Anchor?[]> anchors)
    {
        var glyphs = anchors.Keys.Order().ToArray();
        var buffer = new ByteBuffer();
        buffer.Write(1);
        var coverage = buffer.Reserve();
        buffer.Write(glyphs.Length);
        var slots = new List<int>();
        var children = new List<Anchor?>();
        foreach (var anchor in glyphs.SelectMany(glyph => anchors[glyph]))
        {
            slots.Add(buffer.Reserve());
            children.Add(anchor);
        }

        buffer.AttachChild(coverage, CommonTables.Coverage(glyphs));
        AttachAnchors(buffer, slots, children);
        return buffer.ToArray();
    }

    /// <summary>Writes a mark-to-base attachment, or a mark-to-mark attachment, which has the same layout (lookup types 4 and 6).</summary>
    /// <param name="marks">The class and anchor of each attaching mark.</param>
    /// <param name="bases">The anchor of each base glyph, or second mark, for each mark class.</param>
    /// <param name="classCount">The number of mark classes.</param>
    /// <returns>The subtable.</returns>
    public static byte[] MarkToBase(IReadOnlyDictionary<int, MarkAnchor> marks, IReadOnlyDictionary<int, Anchor?[]> bases, int classCount)
    {
        var buffer = new ByteBuffer();
        buffer.Write(1);
        var markCoverage = buffer.Reserve();
        var baseCoverage = buffer.Reserve();
        buffer.Write(classCount);
        var markArray = buffer.Reserve();
        var baseArray = buffer.Reserve();
        buffer.AttachChild(markCoverage, CommonTables.Coverage(marks.Keys));
        buffer.AttachChild(baseCoverage, CommonTables.Coverage(bases.Keys));
        buffer.AttachChild(markArray, MarkArray(marks));
        buffer.AttachChild(baseArray, AnchorArray(bases));
        return buffer.ToArray();
    }

    /// <summary>Writes a mark-to-ligature attachment (lookup type 5).</summary>
    /// <param name="marks">The class and anchor of each attaching mark.</param>
    /// <param name="ligatures">For each ligature glyph, the anchors for each mark class of each component.</param>
    /// <param name="classCount">The number of mark classes.</param>
    /// <returns>The subtable.</returns>
    public static byte[] MarkToLigature(IReadOnlyDictionary<int, MarkAnchor> marks, IReadOnlyDictionary<int, Anchor?[][]> ligatures, int classCount)
    {
        var buffer = new ByteBuffer();
        buffer.Write(1);
        var markCoverage = buffer.Reserve();
        var ligatureCoverage = buffer.Reserve();
        buffer.Write(classCount);
        var markArray = buffer.Reserve();
        var ligatureArray = buffer.Reserve();
        buffer.AttachChild(markCoverage, CommonTables.Coverage(marks.Keys));
        buffer.AttachChild(ligatureCoverage, CommonTables.Coverage(ligatures.Keys));
        buffer.AttachChild(markArray, MarkArray(marks));
        buffer.AttachChild(ligatureArray, LigatureArray(ligatures));
        return buffer.ToArray();
    }

    /// <summary>Writes an Anchor table (format 1).</summary>
    /// <param name="anchor">The anchor.</param>
    /// <returns>The table.</returns>
    private static byte[] AnchorTable(Anchor anchor) => ByteBuffer.Words(1, anchor.X & 0xFFFF, anchor.Y & 0xFFFF);

    /// <summary>Writes the anchors that go with offset placeholders, leaving the offset zero where there is no anchor.</summary>
    /// <param name="buffer">The table the placeholders are in.</param>
    /// <param name="slots">The placeholders.</param>
    /// <param name="anchors">The anchor for each placeholder.</param>
    private static void AttachAnchors(ByteBuffer buffer, List<int> slots, List<Anchor?> anchors)
    {
        for (var i = 0; i < slots.Count; i++)
        {
            if (anchors[i] is { } anchor)
            {
                buffer.AttachChild(slots[i], AnchorTable(anchor));
            }
        }
    }

    /// <summary>Writes a Mark Array table.</summary>
    /// <param name="marks">The class and anchor of each mark.</param>
    /// <returns>The table.</returns>
    private static byte[] MarkArray(IReadOnlyDictionary<int, MarkAnchor> marks)
    {
        var glyphs = marks.Keys.Order().ToArray();
        var buffer = new ByteBuffer();
        buffer.Write(glyphs.Length);
        var slots = new List<int>();
        var anchors = new List<Anchor?>();
        foreach (var glyph in glyphs)
        {
            buffer.Write(marks[glyph].Class);
            slots.Add(buffer.Reserve());
            anchors.Add(marks[glyph].Anchor);
        }

        AttachAnchors(buffer, slots, anchors);
        return buffer.ToArray();
    }

    /// <summary>Writes a Base Array table, or a Mark2 Array table.</summary>
    /// <param name="anchors">The anchors of each glyph for each mark class.</param>
    /// <returns>The table.</returns>
    private static byte[] AnchorArray(IReadOnlyDictionary<int, Anchor?[]> anchors)
    {
        var glyphs = anchors.Keys.Order().ToArray();
        var buffer = new ByteBuffer();
        buffer.Write(glyphs.Length);
        var slots = new List<int>();
        var children = new List<Anchor?>();
        foreach (var anchor in glyphs.SelectMany(glyph => anchors[glyph]))
        {
            slots.Add(buffer.Reserve());
            children.Add(anchor);
        }

        AttachAnchors(buffer, slots, children);
        return buffer.ToArray();
    }

    /// <summary>Writes a Ligature Array table.</summary>
    /// <param name="ligatures">For each ligature glyph, the anchors for each mark class of each component.</param>
    /// <returns>The table.</returns>
    private static byte[] LigatureArray(IReadOnlyDictionary<int, Anchor?[][]> ligatures)
    {
        var glyphs = ligatures.Keys.Order().ToArray();
        var buffer = new ByteBuffer();
        buffer.Write(glyphs.Length);
        var slots = glyphs.Select(_ => buffer.Reserve()).ToArray();
        for (var i = 0; i < glyphs.Length; i++)
        {
            var attach = new ByteBuffer();
            attach.Write(ligatures[glyphs[i]].Length);
            var attachSlots = new List<int>();
            var attachAnchors = new List<Anchor?>();
            foreach (var anchor in ligatures[glyphs[i]].SelectMany(static component => component))
            {
                attachSlots.Add(attach.Reserve());
                attachAnchors.Add(anchor);
            }

            AttachAnchors(attach, attachSlots, attachAnchors);
            buffer.AttachChild(slots[i], attach.ToArray());
        }

        return buffer.ToArray();
    }
}
