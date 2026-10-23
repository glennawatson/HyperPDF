// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.TestAssets.Shaping;

/// <summary>Writes GSUB lookup subtables (OpenType, "GSUB": lookup types 1 to 8), for building test fonts.</summary>
public static class GsubSubtables
{
    /// <summary>Writes a single substitution that adds a delta to each covered glyph (lookup type 1, format 1).</summary>
    /// <param name="glyphs">The glyphs covered.</param>
    /// <param name="delta">The number added to each glyph.</param>
    /// <returns>The subtable.</returns>
    public static byte[] SingleDelta(int[] glyphs, int delta) =>
        ByteBuffer.Join(ByteBuffer.Words(1, 0x6, delta & 0xFFFF), CommonTables.Coverage(glyphs));

    /// <summary>Writes a single substitution that lists the substitute of each covered glyph (lookup type 1, format 2).</summary>
    /// <param name="glyphs">The glyphs covered, in coverage order.</param>
    /// <param name="substitutes">The substitute of each glyph.</param>
    /// <returns>The subtable.</returns>
    public static byte[] SingleList(int[] glyphs, int[] substitutes) =>
        ByteBuffer.Join(ByteBuffer.Words([0x2, 0x6 + (substitutes.Length * 0x2), substitutes.Length, .. substitutes]), CommonTables.Coverage(glyphs));

    /// <summary>Writes a multiple substitution (lookup type 2).</summary>
    /// <param name="sequences">The sequence each glyph becomes.</param>
    /// <returns>The subtable.</returns>
    public static byte[] Multiple(IReadOnlyDictionary<int, int[]> sequences) =>
        Sets(sequences.Keys.Order().ToArray(), glyph => GlyphList(sequences[glyph]));

    /// <summary>Writes an alternate substitution (lookup type 3).</summary>
    /// <param name="alternates">The alternates of each glyph.</param>
    /// <returns>The subtable.</returns>
    public static byte[] Alternate(IReadOnlyDictionary<int, int[]> alternates) =>
        Sets(alternates.Keys.Order().ToArray(), glyph => GlyphList(alternates[glyph]));

    /// <summary>Writes a ligature substitution for ligatures that start with one glyph (lookup type 4).</summary>
    /// <param name="first">The first component glyph.</param>
    /// <param name="rules">The ligatures, in the order they are tried.</param>
    /// <returns>The subtable.</returns>
    public static byte[] Ligature(int first, params LigatureRule[] rules) => Sets([first], _ => LigatureSet(rules));

    /// <summary>Writes a glyph-based contextual substitution with one rule (lookup type 5, format 1).</summary>
    /// <param name="first">The first glyph.</param>
    /// <param name="rest">The glyphs after the first.</param>
    /// <param name="records">The lookups to run.</param>
    /// <returns>The subtable.</returns>
    public static byte[] ContextGlyphs(int first, int[] rest, params LookupRecord[] records) =>
        Sets([first], _ => RuleSet(ContextRule(rest, records)));

    /// <summary>Writes a class-based contextual substitution with one rule (lookup type 5, format 2).</summary>
    /// <param name="covered">The glyphs the subtable covers.</param>
    /// <param name="classes">The class of each glyph.</param>
    /// <param name="firstClass">The class of the first glyph of the rule.</param>
    /// <param name="restClasses">The classes of the glyphs after the first.</param>
    /// <param name="records">The lookups to run.</param>
    /// <returns>The subtable.</returns>
    public static byte[] ContextClasses(int[] covered, IReadOnlyDictionary<int, int> classes, int firstClass, int[] restClasses, params LookupRecord[] records)
    {
        var buffer = new ByteBuffer();
        buffer.Write(0x2);
        var coverage = buffer.Reserve();
        var classDef = buffer.Reserve();
        buffer.Write(firstClass + 1);
        var sets = new int[firstClass + 1];
        for (var i = 0; i < sets.Length; i++)
        {
            sets[i] = buffer.Reserve();
        }

        buffer.AttachChild(coverage, CommonTables.Coverage(covered));
        buffer.AttachChild(classDef, CommonTables.ClassDef(classes));
        buffer.AttachChild(sets[firstClass], RuleSet(ContextRule(restClasses, records)));
        return buffer.ToArray();
    }

    /// <summary>Writes a coverage-based contextual substitution (lookup type 5, format 3).</summary>
    /// <param name="sequence">The glyphs each position of the rule covers.</param>
    /// <param name="records">The lookups to run.</param>
    /// <returns>The subtable.</returns>
    public static byte[] ContextCoverage(int[][] sequence, params LookupRecord[] records)
    {
        var buffer = new ByteBuffer();
        buffer.Write(0x3, sequence.Length, records.Length);
        var offsets = sequence.Select(_ => buffer.Reserve()).ToArray();
        WriteRecords(buffer, records);
        for (var i = 0; i < sequence.Length; i++)
        {
            buffer.AttachChild(offsets[i], CommonTables.Coverage(sequence[i]));
        }

        return buffer.ToArray();
    }

    /// <summary>Writes a glyph-based chaining contextual substitution with one rule (lookup type 6, format 1).</summary>
    /// <param name="first">The first input glyph.</param>
    /// <param name="back">The glyphs before, nearest first.</param>
    /// <param name="rest">The input glyphs after the first.</param>
    /// <param name="look">The glyphs after the input.</param>
    /// <param name="records">The lookups to run.</param>
    /// <returns>The subtable.</returns>
    public static byte[] ChainGlyphs(int first, int[] back, int[] rest, int[] look, params LookupRecord[] records) =>
        Sets([first], _ => RuleSet(ChainRule(back, rest, look, records)));

    /// <summary>Writes a class-based chaining contextual substitution with one rule (lookup type 6, format 2).</summary>
    /// <param name="covered">The glyphs the subtable covers.</param>
    /// <param name="classes">The class of each glyph, for the backtrack, input and lookahead alike.</param>
    /// <param name="firstClass">The class of the first input glyph.</param>
    /// <param name="back">The classes before, nearest first.</param>
    /// <param name="rest">The classes of the input after the first.</param>
    /// <param name="look">The classes after the input.</param>
    /// <param name="records">The lookups to run.</param>
    /// <returns>The subtable.</returns>
    public static byte[] ChainClasses(int[] covered, IReadOnlyDictionary<int, int> classes, int firstClass, int[] back, int[] rest, int[] look, params LookupRecord[] records)
    {
        var buffer = new ByteBuffer();
        buffer.Write(0x2);
        var coverage = buffer.Reserve();
        var backDef = buffer.Reserve();
        var inputDef = buffer.Reserve();
        var lookDef = buffer.Reserve();
        buffer.Write(firstClass + 1);
        var sets = new int[firstClass + 1];
        for (var i = 0; i < sets.Length; i++)
        {
            sets[i] = buffer.Reserve();
        }

        buffer.AttachChild(coverage, CommonTables.Coverage(covered));
        var classDef = CommonTables.ClassDef(classes);
        buffer.AttachChild(backDef, classDef);
        buffer.AttachChild(inputDef, classDef);
        buffer.AttachChild(lookDef, classDef);
        buffer.AttachChild(sets[firstClass], RuleSet(ChainRule(back, rest, look, records)));
        return buffer.ToArray();
    }

    /// <summary>Writes a coverage-based chaining contextual substitution (lookup type 6, format 3).</summary>
    /// <param name="back">The glyphs each backtrack position covers, nearest first.</param>
    /// <param name="input">The glyphs each input position covers.</param>
    /// <param name="look">The glyphs each lookahead position covers.</param>
    /// <param name="records">The lookups to run.</param>
    /// <returns>The subtable.</returns>
    public static byte[] ChainCoverage(int[][] back, int[][] input, int[][] look, params LookupRecord[] records)
    {
        var buffer = new ByteBuffer();
        buffer.Write(0x3);
        var offsets = new List<int>();
        var children = new List<byte[]>();
        foreach (var group in new[] { back, input, look })
        {
            buffer.Write(group.Length);
            foreach (var coverage in group)
            {
                offsets.Add(buffer.Reserve());
                children.Add(CommonTables.Coverage(coverage));
            }
        }

        buffer.Write(records.Length);
        WriteRecords(buffer, records);
        for (var i = 0; i < offsets.Count; i++)
        {
            buffer.AttachChild(offsets[i], children[i]);
        }

        return buffer.ToArray();
    }

    /// <summary>Writes a reverse chaining contextual single substitution (lookup type 8).</summary>
    /// <param name="covered">The glyphs replaced.</param>
    /// <param name="back">The glyphs each backtrack position covers, nearest first.</param>
    /// <param name="look">The glyphs each lookahead position covers.</param>
    /// <param name="substitutes">The substitute of each covered glyph, in coverage order.</param>
    /// <returns>The subtable.</returns>
    public static byte[] ReverseChain(int[] covered, int[][] back, int[][] look, int[] substitutes)
    {
        var buffer = new ByteBuffer();
        buffer.Write(1);
        var coverage = buffer.Reserve();
        var offsets = new List<int>();
        var children = new List<byte[]>();
        buffer.Write(back.Length);
        foreach (var set in back)
        {
            offsets.Add(buffer.Reserve());
            children.Add(CommonTables.Coverage(set));
        }

        buffer.Write(look.Length);
        foreach (var set in look)
        {
            offsets.Add(buffer.Reserve());
            children.Add(CommonTables.Coverage(set));
        }

        buffer.Write(substitutes.Length);
        buffer.Write(substitutes);
        buffer.AttachChild(coverage, CommonTables.Coverage(covered));
        for (var i = 0; i < offsets.Count; i++)
        {
            buffer.AttachChild(offsets[i], children[i]);
        }

        return buffer.ToArray();
    }

    /// <summary>Wraps a subtable in an extension subtable (lookup type 7).</summary>
    /// <param name="type">The lookup type of the wrapped subtable.</param>
    /// <param name="subtable">The subtable.</param>
    /// <returns>The extension subtable.</returns>
    public static byte[] Extension(int type, byte[] subtable) => ByteBuffer.Join(ByteBuffer.Words(1, type, 0, 0x8), subtable);

    /// <summary>Writes a subtable of format 1 that has a coverage table and one child table for each covered glyph.</summary>
    /// <param name="keys">The covered glyphs, sorted.</param>
    /// <param name="child">Makes the child table for a glyph.</param>
    /// <returns>The subtable.</returns>
    private static byte[] Sets(int[] keys, Func<int, byte[]> child)
    {
        var buffer = new ByteBuffer();
        buffer.Write(1);
        var coverage = buffer.Reserve();
        buffer.Write(keys.Length);
        var offsets = keys.Select(_ => buffer.Reserve()).ToArray();
        buffer.AttachChild(coverage, CommonTables.Coverage(keys));
        for (var i = 0; i < keys.Length; i++)
        {
            buffer.AttachChild(offsets[i], child(keys[i]));
        }

        return buffer.ToArray();
    }

    /// <summary>Writes a glyph count and glyphs.</summary>
    /// <param name="glyphs">The glyphs.</param>
    /// <returns>The table.</returns>
    private static byte[] GlyphList(int[] glyphs) => ByteBuffer.Words([glyphs.Length, .. glyphs]);

    /// <summary>Writes a Ligature Set table.</summary>
    /// <param name="rules">The ligatures.</param>
    /// <returns>The table.</returns>
    private static byte[] LigatureSet(LigatureRule[] rules)
    {
        var buffer = new ByteBuffer();
        buffer.Write(rules.Length);
        var offsets = rules.Select(_ => buffer.Reserve()).ToArray();
        for (var i = 0; i < rules.Length; i++)
        {
            buffer.AttachChild(offsets[i], ByteBuffer.Words([rules[i].Glyph, rules[i].Rest.Length + 1, .. rules[i].Rest]));
        }

        return buffer.ToArray();
    }

    /// <summary>Writes a rule set holding one rule.</summary>
    /// <param name="rule">The rule.</param>
    /// <returns>The table.</returns>
    private static byte[] RuleSet(byte[] rule)
    {
        var buffer = new ByteBuffer();
        buffer.Write(1);
        var offset = buffer.Reserve();
        buffer.AttachChild(offset, rule);
        return buffer.ToArray();
    }

    /// <summary>Writes a rule of a sequence context.</summary>
    /// <param name="rest">The input glyphs or classes after the first.</param>
    /// <param name="records">The lookups to run.</param>
    /// <returns>The rule.</returns>
    private static byte[] ContextRule(int[] rest, LookupRecord[] records)
    {
        var buffer = new ByteBuffer();
        buffer.Write([rest.Length + 1, records.Length, .. rest]);
        WriteRecords(buffer, records);
        return buffer.ToArray();
    }

    /// <summary>Writes a rule of a chained sequence context.</summary>
    /// <param name="back">The backtrack glyphs or classes.</param>
    /// <param name="rest">The input glyphs or classes after the first.</param>
    /// <param name="look">The lookahead glyphs or classes.</param>
    /// <param name="records">The lookups to run.</param>
    /// <returns>The rule.</returns>
    private static byte[] ChainRule(int[] back, int[] rest, int[] look, LookupRecord[] records)
    {
        var buffer = new ByteBuffer();
        buffer.Write([back.Length, .. back, rest.Length + 1, .. rest, look.Length, .. look, records.Length]);
        WriteRecords(buffer, records);
        return buffer.ToArray();
    }

    /// <summary>Writes sequence lookup records.</summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="records">The records.</param>
    private static void WriteRecords(ByteBuffer buffer, LookupRecord[] records)
    {
        foreach (var record in records)
        {
            buffer.Write(record.SequenceIndex, record.LookupIndex);
        }
    }
}
