// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>
/// Mark attachment lookups of the GPOS table (OpenType, "GPOS lookup types 4, 5 and 6"): a mark glyph is moved so that
/// its mark anchor meets an anchor of the base glyph, ligature component or other mark it follows.
/// </summary>
internal static class MarkAttachment
{
    /// <summary>Lookup type 5: mark-to-ligature attachment.</summary>
    private const int MarkToLigature = 5;

    /// <summary>Lookup type 6: mark-to-mark attachment.</summary>
    private const int MarkToMark = 6;

    /// <summary>The GDEF class of mark glyphs.</summary>
    private const int MarkClass = 3;

    /// <summary>The offset of the mark coverage in the subtable.</summary>
    private const int MarkCoverageField = 2;

    /// <summary>The offset of the base, ligature or second mark coverage in the subtable.</summary>
    private const int BaseCoverageField = 4;

    /// <summary>The offset of the mark class count in the subtable.</summary>
    private const int ClassCountField = 6;

    /// <summary>The offset of the mark array in the subtable.</summary>
    private const int MarkArrayField = 8;

    /// <summary>The offset of the base, ligature or second mark array in the subtable.</summary>
    private const int BaseArrayField = 10;

    /// <summary>The bytes of a mark record: class and anchor offset.</summary>
    private const int MarkRecordBytes = 4;

    /// <summary>The offset of the anchor in a mark record.</summary>
    private const int MarkRecordAnchor = 2;

    /// <summary>The offset of the first record in an array that starts with a count.</summary>
    private const int RecordsOffset = 2;

    /// <summary>Applies a mark attachment subtable at a mark glyph.</summary>
    /// <param name="run">The engine.</param>
    /// <param name="type">The lookup type: 4, 5 or 6.</param>
    /// <param name="subtable">The subtable.</param>
    /// <param name="index">The mark glyph's index.</param>
    /// <returns><see langword="true"/> when the mark was attached.</returns>
    internal static bool Apply(LayoutEngine run, int type, int subtable, int index)
    {
        var table = run.Table;
        var markCoverage = OtCoverage.IndexOf(table, subtable + FontBytes.U16(table, subtable + MarkCoverageField), run.Buffer.Items[index].Glyph);
        var target = markCoverage < 0 ? -1 : FindTarget(run, type, index);
        if (target < 0)
        {
            return false;
        }

        var targetCoverage = OtCoverage.IndexOf(table, subtable + FontBytes.U16(table, subtable + BaseCoverageField), run.Buffer.Items[target].Glyph);
        if (targetCoverage < 0 || !ReadMark(table, subtable, markCoverage, out var markClass, out var markX, out var markY))
        {
            return false;
        }

        var anchor = type == MarkToLigature
            ? LigatureAnchor(table, subtable, targetCoverage, markClass, run.Buffer.Items[target].LigatureId, run.Buffer.Items[index])
            : TargetAnchor(table, subtable, targetCoverage, markClass);
        if (!GposLookups.TryReadAnchor(table, anchor, out var targetX, out var targetY))
        {
            return false;
        }

        ref var position = ref run.Buffer.Positions[index];
        position.Attach = AttachKind.Mark;
        position.AttachTo = target;
        position.AttachX = targetX - markX;
        position.AttachY = targetY - markY;
        return true;
    }

    /// <summary>Finds the glyph a mark attaches to: the base or ligature before it, or for mark-to-mark the mark before it.</summary>
    /// <param name="run">The engine.</param>
    /// <param name="type">The lookup type.</param>
    /// <param name="index">The mark's index.</param>
    /// <returns>The glyph's index, or -1.</returns>
    private static int FindTarget(LayoutEngine run, int type, int index)
    {
        if (type != MarkToMark)
        {
            return PreviousNonMark(run, index);
        }

        var previous = run.PreviousUnskipped(index);
        var items = run.Buffer.Items;
        return previous >= 0 && CanStackMarks(items[previous], items[index]) ? previous : -1;
    }

    /// <summary>Finds the glyph before a mark that is not itself a mark and not ignored by the lookup.</summary>
    /// <param name="run">The engine.</param>
    /// <param name="index">The mark's index.</param>
    /// <returns>The glyph's index, or -1.</returns>
    private static int PreviousNonMark(LayoutEngine run, int index)
    {
        var items = run.Buffer.Items;
        for (var j = index - 1; j >= 0; j--)
        {
            if (items[j].GlyphClass != MarkClass && !run.IsSkipped(j))
            {
                return j;
            }
        }

        return -1;
    }

    /// <summary>Determines whether a mark may sit on another mark: they must belong to the same ligature component.</summary>
    /// <param name="below">The mark underneath.</param>
    /// <param name="above">The mark that attaches.</param>
    /// <returns><see langword="true"/> when the first is a mark of the same component.</returns>
    private static bool CanStackMarks(GlyphRecord below, GlyphRecord above) =>
        below.GlyphClass == MarkClass && below.LigatureId == above.LigatureId && below.LigatureComponent == above.LigatureComponent;

    /// <summary>Reads the class and anchor of a mark.</summary>
    /// <param name="table">The table.</param>
    /// <param name="subtable">The subtable.</param>
    /// <param name="markIndex">The mark's coverage index.</param>
    /// <param name="markClass">Receives the mark's class.</param>
    /// <param name="x">Receives the anchor's horizontal coordinate.</param>
    /// <param name="y">Receives the anchor's vertical coordinate.</param>
    /// <returns><see langword="true"/> when the mark has an anchor.</returns>
    private static bool ReadMark(ReadOnlySpan<byte> table, int subtable, int markIndex, out int markClass, out int x, out int y)
    {
        markClass = 0;
        x = 0;
        y = 0;
        var array = subtable + FontBytes.U16(table, subtable + MarkArrayField);
        if (markIndex >= FontBytes.U16(table, array))
        {
            return false;
        }

        var record = array + RecordsOffset + (markIndex * MarkRecordBytes);
        markClass = FontBytes.U16(table, record);
        var anchor = FontBytes.U16(table, record + MarkRecordAnchor);
        return markClass < FontBytes.U16(table, subtable + ClassCountField) && GposLookups.TryReadAnchor(table, anchor == 0 ? 0 : array + anchor, out x, out y);
    }

    /// <summary>Gets the offset of the anchor a base glyph or second mark offers for a mark class.</summary>
    /// <param name="table">The table.</param>
    /// <param name="subtable">The subtable.</param>
    /// <param name="targetIndex">The target's coverage index.</param>
    /// <param name="markClass">The mark's class.</param>
    /// <returns>The offset of the Anchor table, or zero.</returns>
    private static int TargetAnchor(ReadOnlySpan<byte> table, int subtable, int targetIndex, int markClass)
    {
        var array = subtable + FontBytes.U16(table, subtable + BaseArrayField);
        var classes = FontBytes.U16(table, subtable + ClassCountField);
        if (targetIndex >= FontBytes.U16(table, array))
        {
            return 0;
        }

        var record = array + RecordsOffset + (targetIndex * classes * FontBytes.U16Size);
        var anchor = FontBytes.U16(table, record + (markClass * FontBytes.U16Size));
        return anchor == 0 ? 0 : array + anchor;
    }

    /// <summary>Gets the offset of the anchor a ligature offers for a mark class on the component the mark follows.</summary>
    /// <param name="table">The table.</param>
    /// <param name="subtable">The subtable.</param>
    /// <param name="ligatureIndex">The ligature's coverage index.</param>
    /// <param name="markClass">The mark's class.</param>
    /// <param name="ligatureId">The identifier of the ligature glyph.</param>
    /// <param name="mark">The mark glyph.</param>
    /// <returns>The offset of the Anchor table, or zero.</returns>
    private static int LigatureAnchor(ReadOnlySpan<byte> table, int subtable, int ligatureIndex, int markClass, int ligatureId, GlyphRecord mark)
    {
        var array = subtable + FontBytes.U16(table, subtable + BaseArrayField);
        var classes = FontBytes.U16(table, subtable + ClassCountField);
        if (ligatureIndex >= FontBytes.U16(table, array))
        {
            return 0;
        }

        var attach = array + FontBytes.U16(table, array + RecordsOffset + (ligatureIndex * FontBytes.U16Size));
        var components = FontBytes.U16(table, attach);
        if (components == 0)
        {
            return 0;
        }

        // A mark that followed a component stays on it; a mark added after shaping went on the last one.
        var component = mark.LigatureId == ligatureId && mark.LigatureComponent > 0 ? Math.Min(mark.LigatureComponent, components) : components;
        var record = attach + RecordsOffset + ((component - 1) * classes * FontBytes.U16Size);
        var anchor = FontBytes.U16(table, record + (markClass * FontBytes.U16Size));
        return anchor == 0 ? 0 : attach + anchor;
    }
}
