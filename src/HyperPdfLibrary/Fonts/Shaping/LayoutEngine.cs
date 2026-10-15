// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>
/// Runs the lookups of one GSUB or GPOS table over a glyph buffer (OpenType, "Lookup Processing"): each lookup in turn
/// scans the glyphs, skips the ones its lookup flags ignore (OpenType, "Lookup table": lookupFlag) and applies the first
/// subtable that matches.
/// </summary>
[DebuggerDisplay("LayoutEngine: {Buffer.Count} glyphs")]
internal sealed class LayoutEngine
{
    /// <summary>The lookup flag bit that makes a mark glyph set decide which marks are ignored.</summary>
    internal const int UseMarkFilteringSet = 0x0010;

    /// <summary>The lookup flag bit that ignores base glyphs.</summary>
    private const int IgnoreBaseGlyphs = 0x0002;

    /// <summary>The lookup flag bit that ignores ligature glyphs.</summary>
    private const int IgnoreLigatures = 0x0004;

    /// <summary>The lookup flag bit that ignores mark glyphs.</summary>
    private const int IgnoreMarks = 0x0008;

    /// <summary>The lookup flag bits that name a mark attachment class.</summary>
    private const int MarkAttachmentMask = 0xFF00;

    /// <summary>The shift of the mark attachment class in the lookup flags.</summary>
    private const int MarkAttachmentShift = 8;

    /// <summary>The GDEF class of ligature glyphs.</summary>
    private const int LigatureClass = 2;

    /// <summary>The GDEF class of base glyphs.</summary>
    private const int BaseClass = 1;

    /// <summary>The GDEF class of mark glyphs.</summary>
    private const int MarkClass = 3;

    /// <summary>The offset of the lookup flags in a Lookup table.</summary>
    private const int FlagOffset = 2;

    /// <summary>The offset of the subtable count in a Lookup table.</summary>
    private const int SubtableCountOffset = 4;

    /// <summary>The offset of the subtable offsets in a Lookup table.</summary>
    private const int SubtableOffsetsOffset = 6;

    /// <summary>The offset of the type of the lookup an extension subtable wraps.</summary>
    private const int ExtensionTypeOffset = 2;

    /// <summary>The offset of the 32-bit offset of the lookup an extension subtable wraps.</summary>
    private const int ExtensionOffsetOffset = 4;

    /// <summary>The deepest a lookup may be nested inside other lookups' contextual rules.</summary>
    private const int MaxDepth = 6;

    /// <summary>The lookup type of a GSUB extension subtable.</summary>
    private const int GsubExtension = 7;

    /// <summary>The lookup type of a GPOS extension subtable.</summary>
    private const int GposExtension = 9;

    /// <summary>The lookup type of a GSUB reverse chaining contextual single substitution.</summary>
    private const int GsubReverseChaining = 8;

    /// <summary>The lookup flags of the lookup being applied.</summary>
    private int _flags;

    /// <summary>The mark glyph set of the lookup being applied.</summary>
    private int _markSet;

    /// <summary>How many lookups are being applied inside one another.</summary>
    private int _depth;

    /// <summary>Initializes a new instance of the <see cref="LayoutEngine"/> class.</summary>
    /// <param name="layout">The GSUB or GPOS table.</param>
    /// <param name="gdef">The font's glyph definitions.</param>
    /// <param name="buffer">The glyphs.</param>
    /// <param name="positioning">Whether the table is GPOS.</param>
    /// <param name="rightToLeft">Whether the run reads right to left.</param>
    internal LayoutEngine(OtLayoutTable layout, OtGdef gdef, GlyphBuffer buffer, bool positioning, bool rightToLeft)
    {
        Layout = layout;
        Gdef = gdef;
        Buffer = buffer;
        Begin(layout, gdef, positioning, rightToLeft);
    }

    /// <summary>Gets the table.</summary>
    internal OtLayoutTable Layout { get; private set; }

    /// <summary>Gets the font's glyph definitions.</summary>
    internal OtGdef Gdef { get; private set; }

    /// <summary>Gets the glyphs.</summary>
    internal GlyphBuffer Buffer { get; }

    /// <summary>Gets a value indicating whether the table is GPOS.</summary>
    internal bool IsPositioning { get; private set; }

    /// <summary>Gets a value indicating whether the run reads right to left.</summary>
    internal bool RightToLeft { get; private set; }

    /// <summary>Gets the table's bytes.</summary>
    internal ReadOnlySpan<byte> Table => Layout.Span;

    /// <summary>Gets the feature value that picks an alternate in an alternate substitution.</summary>
    internal int AlternateValue { get; private set; }

    /// <summary>Gets the lookup flags of the lookup being applied.</summary>
    internal int LookupFlags => _flags;

    /// <summary>Points the engine at a table for a new run, so one engine serves many runs.</summary>
    /// <param name="layout">The GSUB or GPOS table.</param>
    /// <param name="gdef">The font's glyph definitions.</param>
    /// <param name="positioning">Whether the table is GPOS.</param>
    /// <param name="rightToLeft">Whether the run reads right to left.</param>
    internal void Begin(OtLayoutTable layout, OtGdef gdef, bool positioning, bool rightToLeft)
    {
        Layout = layout;
        Gdef = gdef;
        IsPositioning = positioning;
        RightToLeft = rightToLeft;
        AlternateValue = 1;
        _flags = 0;
        _markSet = 0;
        _depth = 0;
    }

    /// <summary>Runs the lookups of a stage, each over the whole buffer.</summary>
    /// <param name="stage">The lookups, in lookup list order.</param>
    internal void RunStage(LookupEntry[] stage)
    {
        foreach (var entry in stage)
        {
            RunLookup(entry);
        }
    }

    /// <summary>Applies a lookup at one glyph, as a contextual rule does for each lookup record.</summary>
    /// <param name="lookup">The lookup's index.</param>
    /// <param name="index">The glyph's index.</param>
    /// <returns><see langword="true"/> when the lookup changed the glyph.</returns>
    internal bool ApplyNested(int lookup, int index)
    {
        var offset = Layout.LookupOffset(lookup);
        if (offset < 0 || _depth >= MaxDepth || (uint)index >= (uint)Buffer.Count)
        {
            return false;
        }

        var savedFlags = _flags;
        var savedSet = _markSet;
        _depth++;
        try
        {
            LoadFlags(offset);
            return !IsSkipped(index) && TryApply(offset, index, out _);
        }
        finally
        {
            _depth--;
            _flags = savedFlags;
            _markSet = savedSet;
        }
    }

    /// <summary>Finds the next glyph after an index that the lookup being applied does not ignore.</summary>
    /// <param name="index">The index to start after.</param>
    /// <returns>The glyph's index, or -1.</returns>
    internal int NextUnskipped(int index)
    {
        for (var j = index + 1; j < Buffer.Count; j++)
        {
            if (!IsSkipped(j))
            {
                return j;
            }
        }

        return -1;
    }

    /// <summary>Finds the last glyph before an index that the lookup being applied does not ignore.</summary>
    /// <param name="index">The index to start before.</param>
    /// <returns>The glyph's index, or -1.</returns>
    internal int PreviousUnskipped(int index)
    {
        for (var j = Math.Min(index, Buffer.Count) - 1; j >= 0; j--)
        {
            if (!IsSkipped(j))
            {
                return j;
            }
        }

        return -1;
    }

    /// <summary>Determines whether the lookup being applied ignores a glyph.</summary>
    /// <param name="index">The glyph's index.</param>
    /// <returns><see langword="true"/> when the glyph is skipped.</returns>
    internal bool IsSkipped(int index)
    {
        ref var glyph = ref Buffer.Items[index];
        var glyphClass = glyph.GlyphClass;
        if (((_flags & IgnoreBaseGlyphs) != 0 && glyphClass == BaseClass)
            || ((_flags & IgnoreLigatures) != 0 && glyphClass == LigatureClass)
            || ((_flags & IgnoreMarks) != 0 && glyphClass == MarkClass))
        {
            return true;
        }

        return glyphClass == MarkClass && SkipsMark(glyph.Glyph);
    }

    /// <summary>Determines whether a mark glyph set or mark attachment class excludes a mark.</summary>
    /// <param name="glyph">The mark glyph.</param>
    /// <returns><see langword="true"/> when the mark is ignored.</returns>
    private bool SkipsMark(int glyph)
    {
        if ((_flags & UseMarkFilteringSet) != 0)
        {
            return !Gdef.IsInMarkSet(_markSet, glyph);
        }

        var attachment = (_flags & MarkAttachmentMask) >> MarkAttachmentShift;
        return attachment != 0 && Gdef.MarkAttachClass(glyph) != attachment;
    }

    /// <summary>Reads the lookup flags and the mark filtering set of a lookup.</summary>
    /// <param name="lookup">The offset of the Lookup table.</param>
    private void LoadFlags(int lookup)
    {
        var table = Table;
        _flags = FontBytes.U16(table, lookup + FlagOffset);
        var count = FontBytes.U16(table, lookup + SubtableCountOffset);
        _markSet = (_flags & UseMarkFilteringSet) != 0
            ? FontBytes.U16(table, lookup + SubtableOffsetsOffset + (count * FontBytes.U16Size))
            : 0;
    }

    /// <summary>Runs one lookup over the buffer.</summary>
    /// <param name="entry">The lookup.</param>
    private void RunLookup(in LookupEntry entry)
    {
        var offset = Layout.LookupOffset(entry.Lookup);
        if (offset < 0)
        {
            return;
        }

        LoadFlags(offset);
        AlternateValue = entry.Value;
        if (!IsPositioning && FontBytes.U16(Table, offset) == GsubReverseChaining)
        {
            RunReverse(offset, entry.Mask);
            return;
        }

        var i = 0;
        while (i < Buffer.Count)
        {
            var before = Buffer.Count;
            if ((Buffer.Items[i].Mask & entry.Mask) != 0 && !IsSkipped(i) && TryApply(offset, i, out var next))
            {
                // A lookup that removed a glyph has made progress even when it asks to continue at the same index.
                i = next > i || Buffer.Count < before ? next : i + 1;
                continue;
            }

            i++;
        }
    }

    /// <summary>Runs a reverse chaining lookup from the last glyph to the first.</summary>
    /// <param name="offset">The offset of the Lookup table.</param>
    /// <param name="mask">The feature mask bits.</param>
    private void RunReverse(int offset, uint mask)
    {
        for (var i = Buffer.Count - 1; i >= 0; i--)
        {
            if ((Buffer.Items[i].Mask & mask) != 0 && !IsSkipped(i))
            {
                _ = TryApply(offset, i, out _);
            }
        }
    }

    /// <summary>Applies the first subtable of a lookup that matches at a glyph.</summary>
    /// <param name="lookup">The offset of the Lookup table.</param>
    /// <param name="index">The glyph's index.</param>
    /// <param name="next">Receives the index to continue from.</param>
    /// <returns><see langword="true"/> when a subtable applied.</returns>
    private bool TryApply(int lookup, int index, out int next)
    {
        var table = Table;
        var type = FontBytes.U16(table, lookup);
        var count = FontBytes.U16(table, lookup + SubtableCountOffset);
        for (var s = 0; s < count; s++)
        {
            var subtable = lookup + FontBytes.U16(table, lookup + SubtableOffsetsOffset + (s * FontBytes.U16Size));
            var subtableType = type;
            if (type == (IsPositioning ? GposExtension : GsubExtension))
            {
                subtableType = FontBytes.U16(table, subtable + ExtensionTypeOffset);
                subtable += FontBytes.Offset32(table, subtable + ExtensionOffsetOffset);
            }

            if (Dispatch(subtableType, subtable, index, out next))
            {
                return true;
            }
        }

        next = index + 1;
        return false;
    }

    /// <summary>Applies a subtable of a type.</summary>
    /// <param name="type">The lookup type.</param>
    /// <param name="subtable">The offset of the subtable.</param>
    /// <param name="index">The glyph's index.</param>
    /// <param name="next">Receives the index to continue from.</param>
    /// <returns><see langword="true"/> when the subtable applied.</returns>
    private bool Dispatch(int type, int subtable, int index, out int next) => IsPositioning
        ? GposLookups.Apply(this, type, subtable, index, out next)
        : GsubLookups.Apply(this, type, subtable, index, out next);
}
