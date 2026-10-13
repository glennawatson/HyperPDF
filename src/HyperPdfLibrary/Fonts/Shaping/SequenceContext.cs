// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>
/// Contextual lookups (OpenType, "Sequence Context Format 1, 2 and 3" and "Chained Sequence Context Format 1, 2 and 3"):
/// GSUB lookup types 5 and 6 and GPOS lookup types 7 and 8 match a sequence of glyphs by glyph, class or coverage, and
/// then run other lookups at positions of the matched sequence.
/// </summary>
internal static class SequenceContext
{
    /// <summary>The most glyphs of a matched input sequence that are tracked.</summary>
    private const int MaxInput = 64;

    /// <summary>The format that matches glyph indexes.</summary>
    private const int GlyphFormat = 1;

    /// <summary>The format that matches classes.</summary>
    private const int ClassFormat = 2;

    /// <summary>The format that matches coverage tables.</summary>
    private const int CoverageFormat = 3;

    /// <summary>The offset of the coverage offset in a format 1 or 2 subtable.</summary>
    private const int CoverageField = 2;

    /// <summary>The bytes of a sequence lookup record: sequence index and lookup index.</summary>
    private const int RecordBytes = 4;

    /// <summary>The offset of the lookup index in a sequence lookup record.</summary>
    private const int RecordLookup = 2;

    /// <summary>Applies a sequence context subtable.</summary>
    /// <param name="run">The engine.</param>
    /// <param name="subtable">The offset of the subtable.</param>
    /// <param name="index">The index of the first glyph.</param>
    /// <param name="next">Receives the index to continue from.</param>
    /// <returns><see langword="true"/> when a rule matched and its lookups ran.</returns>
    internal static bool ApplyContext(LayoutEngine run, int subtable, int index, out int next)
    {
        next = index + 1;
        Span<int> positions = stackalloc int[MaxInput];
        var table = run.Table;
        var glyph = run.Buffer.Items[index].Glyph;
        positions[0] = index;
        var matched = FontBytes.U16(table, subtable) switch
        {
            GlyphFormat => ContextFormat1(run, subtable, glyph, positions),
            ClassFormat => ContextFormat2(run, subtable, glyph, positions),
            CoverageFormat => ContextFormat3(run, subtable, glyph, positions),
            _ => null,
        };
        return matched is { } rule && Finish(run, rule, positions, out next);
    }

    /// <summary>Applies a chained sequence context subtable.</summary>
    /// <param name="run">The engine.</param>
    /// <param name="subtable">The offset of the subtable.</param>
    /// <param name="index">The index of the first glyph.</param>
    /// <param name="next">Receives the index to continue from.</param>
    /// <returns><see langword="true"/> when a rule matched and its lookups ran.</returns>
    internal static bool ApplyChain(LayoutEngine run, int subtable, int index, out int next)
    {
        next = index + 1;
        Span<int> positions = stackalloc int[MaxInput];
        var table = run.Table;
        var glyph = run.Buffer.Items[index].Glyph;
        positions[0] = index;
        var matched = FontBytes.U16(table, subtable) switch
        {
            GlyphFormat => ChainFormat1(run, subtable, glyph, positions),
            ClassFormat => ChainFormat2(run, subtable, glyph, positions),
            CoverageFormat => ChainFormat3(run, subtable, glyph, positions),
            _ => null,
        };
        return matched is { } rule && Finish(run, rule, positions, out next);
    }

    /// <summary>Matches the glyphs before an index against a rule's backtrack sequence, nearest first.</summary>
    /// <typeparam name="T">The kind of matcher.</typeparam>
    /// <param name="run">The engine.</param>
    /// <param name="matcher">The matcher.</param>
    /// <param name="count">The number of glyphs in the sequence.</param>
    /// <param name="index">The index of the first input glyph.</param>
    /// <returns><see langword="true"/> when every glyph fits.</returns>
    internal static bool MatchBacktrack<T>(LayoutEngine run, in T matcher, int count, int index)
        where T : struct, IGlyphMatcher<T>
    {
        var at = index;
        for (var k = 0; k < count; k++)
        {
            at = run.PreviousUnskipped(at);
            if (at < 0 || !matcher.Matches(run.Table, run.Buffer.Items[at].Glyph, k))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Matches the glyphs after an index against a rule's lookahead sequence.</summary>
    /// <typeparam name="T">The kind of matcher.</typeparam>
    /// <param name="run">The engine.</param>
    /// <param name="matcher">The matcher.</param>
    /// <param name="count">The number of glyphs in the sequence.</param>
    /// <param name="index">The index of the last input glyph.</param>
    /// <returns><see langword="true"/> when every glyph fits.</returns>
    internal static bool MatchLookahead<T>(LayoutEngine run, in T matcher, int count, int index)
        where T : struct, IGlyphMatcher<T>
    {
        var at = index;
        for (var k = 0; k < count; k++)
        {
            at = run.NextUnskipped(at);
            if (at < 0 || !matcher.Matches(run.Table, run.Buffer.Items[at].Glyph, k))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Matches the input glyphs after the first against a rule.</summary>
    /// <typeparam name="T">The kind of matcher.</typeparam>
    /// <param name="run">The engine.</param>
    /// <param name="matcher">The matcher.</param>
    /// <param name="count">The number of input glyphs, the first included.</param>
    /// <param name="bias">Added to the glyph's place in the input to get its index in the rule's array: -1 when the array leaves out the first glyph.</param>
    /// <param name="positions">Holds the first glyph's index; receives the indexes of the others.</param>
    /// <returns><see langword="true"/> when every glyph fits.</returns>
    private static bool MatchInput<T>(LayoutEngine run, in T matcher, int count, int bias, Span<int> positions)
        where T : struct, IGlyphMatcher<T>
    {
        if (count < 1 || count > positions.Length)
        {
            return false;
        }

        for (var k = 1; k < count; k++)
        {
            var at = run.NextUnskipped(positions[k - 1]);
            if (at < 0 || !matcher.Matches(run.Table, run.Buffer.Items[at].Glyph, k + bias))
            {
                return false;
            }

            positions[k] = at;
        }

        return true;
    }

    /// <summary>Gets an absolute offset from an offset field, where zero means none.</summary>
    /// <param name="table">The table.</param>
    /// <param name="baseOffset">The offset the field's value is measured from.</param>
    /// <param name="field">The offset of the field.</param>
    /// <returns>The absolute offset, or zero.</returns>
    private static int Resolve(ReadOnlySpan<byte> table, int baseOffset, int field)
    {
        var relative = FontBytes.U16(table, field);
        return relative == 0 ? 0 : baseOffset + relative;
    }

    /// <summary>Runs the lookup records of a matched rule.</summary>
    /// <param name="run">The engine.</param>
    /// <param name="rule">The matched rule.</param>
    /// <param name="positions">The indexes of the matched input glyphs.</param>
    /// <param name="next">Receives the index to continue from.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    private static bool Finish(LayoutEngine run, in MatchedRule rule, Span<int> positions, out int next)
    {
        var table = run.Table;
        for (var r = 0; r < rule.RecordCount; r++)
        {
            var record = rule.Records + (r * RecordBytes);
            var sequence = FontBytes.U16(table, record);
            if (sequence >= rule.InputCount)
            {
                continue;
            }

            var before = run.Buffer.Count;
            _ = run.ApplyNested(FontBytes.U16(table, record + RecordLookup), positions[sequence]);
            ShiftAfter(positions, sequence, rule.InputCount, run.Buffer.Count - before);
        }

        next = Math.Max(positions[rule.InputCount - 1], positions[0]) + 1;
        return true;
    }

    /// <summary>Moves the indexes of the input glyphs after one when a lookup added or removed glyphs.</summary>
    /// <param name="positions">The indexes.</param>
    /// <param name="sequence">The place of the glyph the lookup ran at.</param>
    /// <param name="count">The number of input glyphs.</param>
    /// <param name="change">The number of glyphs added, or removed when negative.</param>
    private static void ShiftAfter(Span<int> positions, int sequence, int count, int change)
    {
        if (change == 0)
        {
            return;
        }

        for (var k = sequence + 1; k < count; k++)
        {
            positions[k] = Math.Max(positions[k] + change, positions[sequence]);
        }
    }

    /// <summary>Matches the rules of a format 1 sequence context.</summary>
    /// <param name="run">The engine.</param>
    /// <param name="subtable">The subtable.</param>
    /// <param name="glyph">The first glyph.</param>
    /// <param name="positions">Receives the input positions.</param>
    /// <returns>The rule, or <see langword="null"/>.</returns>
    private static MatchedRule? ContextFormat1(LayoutEngine run, int subtable, int glyph, Span<int> positions)
    {
        var set = RuleSet(run.Table, subtable, glyph);
        return set < 0 ? null : FirstContextRule(run, set, positions, new GlyphIdMatcher(0));
    }

    /// <summary>Matches the rules of a format 2 sequence context.</summary>
    /// <param name="run">The engine.</param>
    /// <param name="subtable">The subtable.</param>
    /// <param name="glyph">The first glyph.</param>
    /// <param name="positions">Receives the input positions.</param>
    /// <returns>The rule, or <see langword="null"/>.</returns>
    private static MatchedRule? ContextFormat2(LayoutEngine run, int subtable, int glyph, Span<int> positions)
    {
        var table = run.Table;
        if (OtCoverage.IndexOf(table, subtable + FontBytes.U16(table, subtable + CoverageField), glyph) < 0)
        {
            return null;
        }

        var classDef = Resolve(table, subtable, subtable + RecordBytes);
        var firstClass = OtClassDef.ClassOf(table, classDef, glyph);
        var setCount = FontBytes.U16(table, subtable + RecordBytes + FontBytes.U16Size);
        var set = firstClass < setCount ? Resolve(table, subtable, subtable + RecordBytes + RecordBytes + (firstClass * FontBytes.U16Size)) : 0;
        return set == 0 ? null : FirstContextRule(run, set, positions, new ClassMatcher(classDef, 0));
    }

    /// <summary>Matches a format 3 sequence context.</summary>
    /// <param name="run">The engine.</param>
    /// <param name="subtable">The subtable.</param>
    /// <param name="glyph">The first glyph.</param>
    /// <param name="positions">Receives the input positions.</param>
    /// <returns>The rule, or <see langword="null"/>.</returns>
    private static MatchedRule? ContextFormat3(LayoutEngine run, int subtable, int glyph, Span<int> positions)
    {
        var table = run.Table;
        var count = FontBytes.U16(table, subtable + CoverageField);
        var arrayOffset = subtable + CoverageField + RecordBytes;
        if (OtCoverage.IndexOf(table, subtable + FontBytes.U16(table, arrayOffset), glyph) < 0
            || !MatchInput(run, new CoverageMatcher(subtable, arrayOffset), count, 0, positions))
        {
            return null;
        }

        var records = arrayOffset + (count * FontBytes.U16Size);
        return new(count, FontBytes.U16(table, subtable + CoverageField + FontBytes.U16Size), records);
    }

    /// <summary>Finds the rule set of a format 1 subtable for a first glyph.</summary>
    /// <param name="table">The table.</param>
    /// <param name="subtable">The subtable.</param>
    /// <param name="glyph">The first glyph.</param>
    /// <returns>The offset of the rule set, or -1.</returns>
    private static int RuleSet(ReadOnlySpan<byte> table, int subtable, int glyph)
    {
        var coverage = OtCoverage.IndexOf(table, subtable + FontBytes.U16(table, subtable + CoverageField), glyph);
        if (coverage < 0 || coverage >= FontBytes.U16(table, subtable + RecordBytes))
        {
            return -1;
        }

        var set = Resolve(table, subtable, subtable + RecordBytes + FontBytes.U16Size + (coverage * FontBytes.U16Size));
        return set == 0 ? -1 : set;
    }

    /// <summary>Tries the rules of a rule set in order and returns the first that matches.</summary>
    /// <typeparam name="TMatcher">The kind of matcher the rules use.</typeparam>
    /// <param name="run">The engine.</param>
    /// <param name="set">The offset of the rule set.</param>
    /// <param name="positions">Receives the input positions.</param>
    /// <param name="template">The matcher, to be pointed at each rule's array.</param>
    /// <returns>The matching rule, or <see langword="null"/>.</returns>
    private static MatchedRule? FirstContextRule<TMatcher>(LayoutEngine run, int set, Span<int> positions, in TMatcher template)
        where TMatcher : struct, IGlyphMatcher<TMatcher>
    {
        var table = run.Table;
        var count = FontBytes.U16(table, set);
        for (var r = 0; r < count; r++)
        {
            var rule = Resolve(table, set, set + FontBytes.U16Size + (r * FontBytes.U16Size));
            var glyphs = FontBytes.U16(table, rule);
            if (rule == 0 || !MatchInput(run, template.At(rule + RecordBytes), glyphs, -1, positions))
            {
                continue;
            }

            var records = rule + RecordBytes + (Math.Max(glyphs - 1, 0) * FontBytes.U16Size);
            return new(glyphs, FontBytes.U16(table, rule + FontBytes.U16Size), records);
        }

        return null;
    }

    /// <summary>Matches the rules of a format 1 chained sequence context.</summary>
    /// <param name="run">The engine.</param>
    /// <param name="subtable">The subtable.</param>
    /// <param name="glyph">The first glyph.</param>
    /// <param name="positions">Receives the input positions.</param>
    /// <returns>The rule, or <see langword="null"/>.</returns>
    private static MatchedRule? ChainFormat1(LayoutEngine run, int subtable, int glyph, Span<int> positions)
    {
        var set = RuleSet(run.Table, subtable, glyph);
        var template = new GlyphIdMatcher(0);
        return set < 0 ? null : FirstChainRule(run, set, positions, template, template, template);
    }

    /// <summary>Matches the rules of a format 2 chained sequence context.</summary>
    /// <param name="run">The engine.</param>
    /// <param name="subtable">The subtable.</param>
    /// <param name="glyph">The first glyph.</param>
    /// <param name="positions">Receives the input positions.</param>
    /// <returns>The rule, or <see langword="null"/>.</returns>
    private static MatchedRule? ChainFormat2(LayoutEngine run, int subtable, int glyph, Span<int> positions)
    {
        var table = run.Table;
        if (OtCoverage.IndexOf(table, subtable + FontBytes.U16(table, subtable + CoverageField), glyph) < 0)
        {
            return null;
        }

        var backDef = Resolve(table, subtable, subtable + RecordBytes);
        var inputDef = Resolve(table, subtable, subtable + RecordBytes + FontBytes.U16Size);
        var lookDef = Resolve(table, subtable, subtable + RecordBytes + RecordBytes);
        var firstClass = OtClassDef.ClassOf(table, inputDef, glyph);
        var setCount = FontBytes.U16(table, subtable + RecordBytes + RecordBytes + FontBytes.U16Size);
        var set = firstClass < setCount ? Resolve(table, subtable, subtable + RecordBytes + RecordBytes + RecordBytes + (firstClass * FontBytes.U16Size)) : 0;
        ClassMatcher back = new(backDef, 0);
        ClassMatcher input = new(inputDef, 0);
        ClassMatcher look = new(lookDef, 0);
        return set == 0 ? null : FirstChainRule(run, set, positions, back, input, look);
    }

    /// <summary>Matches a format 3 chained sequence context.</summary>
    /// <param name="run">The engine.</param>
    /// <param name="subtable">The subtable.</param>
    /// <param name="glyph">The first glyph.</param>
    /// <param name="positions">Receives the input positions.</param>
    /// <returns>The rule, or <see langword="null"/>.</returns>
    private static MatchedRule? ChainFormat3(LayoutEngine run, int subtable, int glyph, Span<int> positions)
    {
        var table = run.Table;
        var layout = ReadChain(table, subtable + CoverageField, true);
        CoverageMatcher input = new(subtable, layout.InputArray);
        if (OtCoverage.IndexOf(table, subtable + FontBytes.U16(table, layout.InputArray), glyph) < 0 || !MatchInput(run, input, layout.InputCount, 0, positions))
        {
            return null;
        }

        CoverageMatcher back = new(subtable, layout.BacktrackArray);
        CoverageMatcher look = new(subtable, layout.LookaheadArray);
        return MatchSurroundings(run, layout, back, look, positions);
    }

    /// <summary>Tries the rules of a chain rule set in order and returns the first that matches.</summary>
    /// <typeparam name="TMatcher">The kind of matcher the rules use.</typeparam>
    /// <param name="run">The engine.</param>
    /// <param name="set">The offset of the rule set.</param>
    /// <param name="positions">Receives the input positions.</param>
    /// <param name="back">The backtrack matcher, to be pointed at each rule's array.</param>
    /// <param name="input">The input matcher, to be pointed at each rule's array.</param>
    /// <param name="look">The lookahead matcher, to be pointed at each rule's array.</param>
    /// <returns>The matching rule, or <see langword="null"/>.</returns>
    private static MatchedRule? FirstChainRule<TMatcher>(LayoutEngine run, int set, Span<int> positions, in TMatcher back, in TMatcher input, in TMatcher look)
        where TMatcher : struct, IGlyphMatcher<TMatcher>
    {
        var table = run.Table;
        var count = FontBytes.U16(table, set);
        for (var r = 0; r < count; r++)
        {
            var rule = Resolve(table, set, set + FontBytes.U16Size + (r * FontBytes.U16Size));
            if (rule == 0)
            {
                continue;
            }

            var layout = ReadChain(table, rule, false);
            if (MatchInput(run, input.At(layout.InputArray), layout.InputCount, -1, positions)
                && MatchSurroundings(run, layout, back.At(layout.BacktrackArray), look.At(layout.LookaheadArray), positions) is { } matched)
            {
                return matched;
            }
        }

        return null;
    }

    /// <summary>Matches the backtrack and lookahead sequences around matched input.</summary>
    /// <typeparam name="T">The kind of matcher.</typeparam>
    /// <param name="run">The engine.</param>
    /// <param name="layout">The rule's layout.</param>
    /// <param name="back">The backtrack matcher.</param>
    /// <param name="look">The lookahead matcher.</param>
    /// <param name="positions">The input positions.</param>
    /// <returns>The matched rule, or <see langword="null"/>.</returns>
    private static MatchedRule? MatchSurroundings<T>(LayoutEngine run, in ChainLayout layout, in T back, in T look, Span<int> positions)
        where T : struct, IGlyphMatcher<T>
    {
        var last = positions[Math.Max(layout.InputCount - 1, 0)];
        var fits = MatchBacktrack(run, back, layout.BacktrackCount, positions[0]) && MatchLookahead(run, look, layout.LookaheadCount, last);
        return fits ? new MatchedRule(layout.InputCount, layout.RecordCount, layout.Records) : null;
    }

    /// <summary>Reads the layout of a chained rule: the backtrack, input and lookahead arrays and the lookup records.</summary>
    /// <param name="table">The table.</param>
    /// <param name="start">The offset of the rule, or for format 3 of the backtrack count.</param>
    /// <param name="coverageFormat">Whether the arrays hold coverage offsets, so the input array includes its first glyph.</param>
    /// <returns>The layout.</returns>
    private static ChainLayout ReadChain(ReadOnlySpan<byte> table, int start, bool coverageFormat)
    {
        var backCount = FontBytes.U16(table, start);
        var backArray = start + FontBytes.U16Size;
        var at = backArray + (backCount * FontBytes.U16Size);
        var inputCount = FontBytes.U16(table, at);
        var inputArray = at + FontBytes.U16Size;
        at = inputArray + (Math.Max(inputCount - (coverageFormat ? 0 : 1), 0) * FontBytes.U16Size);
        var lookCount = FontBytes.U16(table, at);
        var lookArray = at + FontBytes.U16Size;
        at = lookArray + (lookCount * FontBytes.U16Size);
        return new(backCount, backArray, inputCount, inputArray, lookCount, lookArray, FontBytes.U16(table, at), at + FontBytes.U16Size);
    }

    /// <summary>The arrays of a chained rule.</summary>
    /// <param name="BacktrackCount">The number of backtrack entries.</param>
    /// <param name="BacktrackArray">The offset of the backtrack array.</param>
    /// <param name="InputCount">The number of input glyphs.</param>
    /// <param name="InputArray">The offset of the input array.</param>
    /// <param name="LookaheadCount">The number of lookahead entries.</param>
    /// <param name="LookaheadArray">The offset of the lookahead array.</param>
    /// <param name="RecordCount">The number of lookup records.</param>
    /// <param name="Records">The offset of the lookup records.</param>
    private readonly record struct ChainLayout(int BacktrackCount, int BacktrackArray, int InputCount, int InputArray, int LookaheadCount, int LookaheadArray, int RecordCount, int Records);

    /// <summary>A rule that matched.</summary>
    /// <param name="InputCount">The number of input glyphs.</param>
    /// <param name="RecordCount">The number of lookup records.</param>
    /// <param name="Records">The offset of the lookup records.</param>
    private readonly record struct MatchedRule(int InputCount, int RecordCount, int Records);
}
