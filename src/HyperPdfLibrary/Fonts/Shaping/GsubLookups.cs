// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>The glyph substitution lookups of the GSUB table (OpenType, "GSUB": Lookup types 1 to 8).</summary>
internal static class GsubLookups
{
    /// <summary>Lookup type 1: single substitution.</summary>
    private const int SingleType = 1;

    /// <summary>Lookup type 2: multiple substitution.</summary>
    private const int MultipleType = 2;

    /// <summary>Lookup type 3: alternate substitution.</summary>
    private const int AlternateType = 3;

    /// <summary>Lookup type 4: ligature substitution.</summary>
    private const int LigatureType = 4;

    /// <summary>Lookup type 5: contextual substitution.</summary>
    private const int ContextType = 5;

    /// <summary>Lookup type 6: chaining contextual substitution.</summary>
    private const int ChainType = 6;

    /// <summary>Lookup type 8: reverse chaining contextual single substitution.</summary>
    private const int ReverseChainType = 8;

    /// <summary>The offset of the coverage offset in most subtables.</summary>
    private const int CoverageField = 2;

    /// <summary>The offset of the delta or the glyph count in a single substitution subtable.</summary>
    private const int SingleField = 4;

    /// <summary>The offset of the substitute glyphs in a format 2 single substitution subtable.</summary>
    private const int SingleArray = 6;

    /// <summary>The offset of the set count in multiple, alternate and ligature subtables.</summary>
    private const int SetCountField = 4;

    /// <summary>The offset of the set offsets in multiple, alternate and ligature subtables.</summary>
    private const int SetOffsetsField = 6;

    /// <summary>The offset of the glyphs in a sequence or alternate set.</summary>
    private const int SetGlyphs = 2;

    /// <summary>The offset of the components in a Ligature table.</summary>
    private const int LigatureComponents = 4;

    /// <summary>The offset of the component count in a Ligature table.</summary>
    private const int LigatureCountField = 2;

    /// <summary>The format of the single substitution that adds a delta.</summary>
    private const int DeltaFormat = 1;

    /// <summary>The mask of a glyph index.</summary>
    private const int GlyphMask = 0xFFFF;

    /// <summary>The GDEF class of ligature glyphs.</summary>
    private const int LigatureClass = 2;

    /// <summary>The most glyphs a multiple substitution may produce.</summary>
    private const int MaxSequence = 64;

    /// <summary>The most glyphs a ligature may skip over between its components.</summary>
    private const int MaxKept = 64;

    /// <summary>The largest identifier a ligature gets before they wrap.</summary>
    private const int MaxLigatureId = 0xFFFFFF;

    /// <summary>The identifier of the next ligature made.</summary>
    private static int _ligatureIds;

    /// <summary>Applies a GSUB subtable at a glyph.</summary>
    /// <param name="run">The engine.</param>
    /// <param name="type">The lookup type.</param>
    /// <param name="subtable">The offset of the subtable.</param>
    /// <param name="index">The glyph's index.</param>
    /// <param name="next">Receives the index to continue from.</param>
    /// <returns><see langword="true"/> when the subtable changed the glyphs.</returns>
    internal static bool Apply(LayoutEngine run, int type, int subtable, int index, out int next)
    {
        next = index + 1;
        switch (type)
        {
            case SingleType:
            {
                return Single(run, subtable, index);
            }

            case MultipleType:
            {
                return Multiple(run, subtable, index, out next);
            }

            case AlternateType:
            {
                return Alternate(run, subtable, index);
            }

            case LigatureType:
            {
                return Ligature(run, subtable, index, out next);
            }

            case ContextType:
            {
                return SequenceContext.ApplyContext(run, subtable, index, out next);
            }

            case ChainType:
            {
                return SequenceContext.ApplyChain(run, subtable, index, out next);
            }

            case ReverseChainType:
            {
                return ReverseChain(run, subtable, index);
            }

            default:
            {
                return false;
            }
        }
    }

    /// <summary>Gets the next ligature identifier, which ties marks to the ligature they follow.</summary>
    /// <returns>A non-zero identifier.</returns>
    private static int NextLigatureId() => (Interlocked.Increment(ref _ligatureIds) & MaxLigatureId) + 1;

    /// <summary>Applies a single substitution (OpenType, "GSUB lookup type 1").</summary>
    /// <param name="run">The engine.</param>
    /// <param name="subtable">The subtable.</param>
    /// <param name="index">The glyph's index.</param>
    /// <returns><see langword="true"/> when the glyph was replaced.</returns>
    private static bool Single(LayoutEngine run, int subtable, int index)
    {
        var table = run.Table;
        ref var record = ref run.Buffer.Items[index];
        var covered = OtCoverage.IndexOf(table, subtable + FontBytes.U16(table, subtable + CoverageField), record.Glyph);
        if (covered < 0)
        {
            return false;
        }

        if (FontBytes.U16(table, subtable) == DeltaFormat)
        {
            record.Glyph = (record.Glyph + FontBytes.S16(table, subtable + SingleField)) & GlyphMask;
        }
        else if (covered < FontBytes.U16(table, subtable + SingleField))
        {
            record.Glyph = FontBytes.U16(table, subtable + SingleArray + (covered * FontBytes.U16Size));
        }
        else
        {
            return false;
        }

        record.GlyphClass = ClassOfSubstitute(run, record.Glyph, record.GlyphClass);
        return true;
    }

    /// <summary>Gets the class of a glyph that a substitution produced.</summary>
    /// <param name="run">The engine.</param>
    /// <param name="glyph">The new glyph.</param>
    /// <param name="previous">The class of the glyph it replaced.</param>
    /// <returns>The class from GDEF, or the previous class when the font has no glyph classes.</returns>
    private static int ClassOfSubstitute(LayoutEngine run, int glyph, int previous) =>
        run.Gdef.HasGlyphClasses ? run.Gdef.GlyphClass(glyph) : previous;

    /// <summary>Applies a multiple substitution (OpenType, "GSUB lookup type 2").</summary>
    /// <param name="run">The engine.</param>
    /// <param name="subtable">The subtable.</param>
    /// <param name="index">The glyph's index.</param>
    /// <param name="next">Receives the index after the new glyphs.</param>
    /// <returns><see langword="true"/> when the glyph was replaced.</returns>
    private static bool Multiple(LayoutEngine run, int subtable, int index, out int next)
    {
        next = index + 1;
        var table = run.Table;
        var original = run.Buffer.Items[index];
        var sequence = SetFor(table, subtable, original.Glyph);
        if (sequence < 0)
        {
            return false;
        }

        var count = FontBytes.U16(table, sequence);
        if (count > MaxSequence)
        {
            return false;
        }

        Span<GlyphRecord> produced = stackalloc GlyphRecord[MaxSequence];
        produced = produced[..count];
        for (var i = 0; i < count; i++)
        {
            var glyph = FontBytes.U16(table, sequence + SetGlyphs + (i * FontBytes.U16Size));
            produced[i] = original with { Glyph = glyph, GlyphClass = ClassOfSubstitute(run, glyph, original.GlyphClass), Flags = original.Flags | GlyphFlags.Multiplied };
        }

        run.Buffer.Replace(index, 1, produced);
        next = index + count;
        return true;
    }

    /// <summary>Applies an alternate substitution (OpenType, "GSUB lookup type 3"): the glyph becomes the alternate the feature's value picks.</summary>
    /// <param name="run">The engine.</param>
    /// <param name="subtable">The subtable.</param>
    /// <param name="index">The glyph's index.</param>
    /// <returns><see langword="true"/> when the glyph was replaced.</returns>
    private static bool Alternate(LayoutEngine run, int subtable, int index)
    {
        var table = run.Table;
        ref var record = ref run.Buffer.Items[index];
        var set = SetFor(table, subtable, record.Glyph);
        var count = set < 0 ? 0 : FontBytes.U16(table, set);
        var pick = run.AlternateValue - 1;
        if ((uint)pick >= (uint)count)
        {
            return false;
        }

        record.Glyph = FontBytes.U16(table, set + SetGlyphs + (pick * FontBytes.U16Size));
        record.GlyphClass = ClassOfSubstitute(run, record.Glyph, record.GlyphClass);
        return true;
    }

    /// <summary>Finds the sequence, alternate set or ligature set a glyph's coverage index selects.</summary>
    /// <param name="table">The table.</param>
    /// <param name="subtable">The subtable.</param>
    /// <param name="glyph">The glyph.</param>
    /// <returns>The offset of the set, or -1 when the glyph has none.</returns>
    private static int SetFor(ReadOnlySpan<byte> table, int subtable, int glyph)
    {
        var covered = OtCoverage.IndexOf(table, subtable + FontBytes.U16(table, subtable + CoverageField), glyph);
        if (covered < 0 || covered >= FontBytes.U16(table, subtable + SetCountField))
        {
            return -1;
        }

        var offset = FontBytes.U16(table, subtable + SetOffsetsField + (covered * FontBytes.U16Size));
        return offset == 0 ? -1 : subtable + offset;
    }

    /// <summary>Applies a ligature substitution (OpenType, "GSUB lookup type 4").</summary>
    /// <param name="run">The engine.</param>
    /// <param name="subtable">The subtable.</param>
    /// <param name="index">The index of the first component.</param>
    /// <param name="next">Receives the index after the ligature.</param>
    /// <returns><see langword="true"/> when a ligature was formed.</returns>
    private static bool Ligature(LayoutEngine run, int subtable, int index, out int next)
    {
        next = index + 1;
        var table = run.Table;
        var set = SetFor(table, subtable, run.Buffer.Items[index].Glyph);
        var count = set < 0 ? 0 : FontBytes.U16(table, set);
        for (var l = 0; l < count; l++)
        {
            var ligature = set + FontBytes.U16(table, set + SetGlyphs + (l * FontBytes.U16Size));
            if (!MatchComponents(run, ligature, index, out var last) || last - index > MaxKept)
            {
                continue;
            }

            Form(run, ligature, index, last);
            return true;
        }

        return false;
    }

    /// <summary>Matches the components of a Ligature table after the first glyph.</summary>
    /// <param name="run">The engine.</param>
    /// <param name="ligature">The offset of the Ligature table.</param>
    /// <param name="index">The index of the first component.</param>
    /// <param name="last">Receives the index of the last component.</param>
    /// <returns><see langword="true"/> when every component is present.</returns>
    private static bool MatchComponents(LayoutEngine run, int ligature, int index, out int last)
    {
        var table = run.Table;
        var components = FontBytes.U16(table, ligature + LigatureCountField);
        last = index;
        for (var c = 1; c < components; c++)
        {
            last = run.NextUnskipped(last);
            if (last < 0 || run.Buffer.Items[last].Glyph != FontBytes.U16(table, ligature + LigatureComponents + ((c - 1) * FontBytes.U16Size)))
            {
                return false;
            }
        }

        return components > 0;
    }

    /// <summary>Replaces the matched components with the ligature glyph, keeping the marks between them.</summary>
    /// <param name="run">The engine.</param>
    /// <param name="ligature">The offset of the Ligature table.</param>
    /// <param name="index">The index of the first component.</param>
    /// <param name="last">The index of the last component.</param>
    private static void Form(LayoutEngine run, int ligature, int index, int last)
    {
        var table = run.Table;
        var id = NextLigatureId();
        Span<GlyphRecord> kept = stackalloc GlyphRecord[MaxKept];
        var keptCount = 0;
        var flags = run.Buffer.Items[index].Flags;
        var component = 1;

        // The glyphs the lookup skipped between components stay, and each follows the component before it.
        for (var i = index + 1; i <= last; i++)
        {
            ref var glyph = ref run.Buffer.Items[i];
            if (!run.IsSkipped(i))
            {
                flags |= glyph.Flags;
                component++;
                continue;
            }

            glyph.LigatureId = id;
            glyph.LigatureComponent = component;
            kept[keptCount] = glyph;
            keptCount++;
        }

        ref var first = ref run.Buffer.Items[index];
        first.Glyph = FontBytes.U16(table, ligature);
        first.GlyphClass = run.Gdef.HasGlyphClasses ? run.Gdef.GlyphClass(first.Glyph) : LigatureClass;
        first.Flags = (flags | GlyphFlags.Ligated) & ~GlyphFlags.Multiplied;
        first.LigatureId = id;
        first.LigatureComponent = 0;
        first.LigatureComponents = FontBytes.U16(table, ligature + LigatureCountField);
        run.Buffer.Replace(index + 1, last - index, kept[..keptCount]);
    }

    /// <summary>Applies a reverse chaining contextual single substitution (OpenType, "GSUB lookup type 8").</summary>
    /// <param name="run">The engine.</param>
    /// <param name="subtable">The subtable.</param>
    /// <param name="index">The glyph's index.</param>
    /// <returns><see langword="true"/> when the glyph was replaced.</returns>
    private static bool ReverseChain(LayoutEngine run, int subtable, int index)
    {
        var table = run.Table;
        ref var record = ref run.Buffer.Items[index];
        var covered = OtCoverage.IndexOf(table, subtable + FontBytes.U16(table, subtable + CoverageField), record.Glyph);
        if (covered < 0)
        {
            return false;
        }

        var backCount = FontBytes.U16(table, subtable + SingleField);
        var backArray = subtable + SingleArray;
        var lookCountAt = backArray + (backCount * FontBytes.U16Size);
        var lookCount = FontBytes.U16(table, lookCountAt);
        var lookArray = lookCountAt + FontBytes.U16Size;
        var glyphCountAt = lookArray + (lookCount * FontBytes.U16Size);
        if (covered >= FontBytes.U16(table, glyphCountAt)
            || !SequenceContext.MatchBacktrack(run, new CoverageMatcher(subtable, backArray), backCount, index)
            || !SequenceContext.MatchLookahead(run, new CoverageMatcher(subtable, lookArray), lookCount, index))
        {
            return false;
        }

        record.Glyph = FontBytes.U16(table, glyphCountAt + FontBytes.U16Size + (covered * FontBytes.U16Size));
        record.GlyphClass = ClassOfSubstitute(run, record.Glyph, record.GlyphClass);
        return true;
    }
}
