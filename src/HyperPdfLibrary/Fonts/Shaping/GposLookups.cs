// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>The glyph positioning lookups of the GPOS table (OpenType, "GPOS": Lookup types 1 to 8).</summary>
internal static class GposLookups
{
    /// <summary>Lookup type 1: single adjustment.</summary>
    private const int SingleType = 1;

    /// <summary>Lookup type 2: pair adjustment.</summary>
    private const int PairType = 2;

    /// <summary>Lookup type 3: cursive attachment.</summary>
    private const int CursiveType = 3;

    /// <summary>Lookup type 4: mark-to-base attachment.</summary>
    private const int MarkToBaseType = 4;

    /// <summary>Lookup type 5: mark-to-ligature attachment.</summary>
    private const int MarkToLigatureType = 5;

    /// <summary>Lookup type 6: mark-to-mark attachment.</summary>
    private const int MarkToMarkType = 6;

    /// <summary>Lookup type 7: contextual positioning.</summary>
    private const int ContextType = 7;

    /// <summary>Lookup type 8: chaining contextual positioning.</summary>
    private const int ChainType = 8;

    /// <summary>The offset of the coverage offset in most subtables.</summary>
    private const int CoverageField = 2;

    /// <summary>The offset of the value format in single and pair subtables.</summary>
    private const int ValueFormatField = 4;

    /// <summary>The value format bits that hold a value rather than a device table offset.</summary>
    private const int ValueBits = 0x000F;

    /// <summary>The value format bit for a horizontal placement.</summary>
    private const int XPlacement = 0x0001;

    /// <summary>The value format bit for a vertical placement.</summary>
    private const int YPlacement = 0x0002;

    /// <summary>The value format bit for a horizontal advance.</summary>
    private const int XAdvance = 0x0004;

    /// <summary>The value format bits for device tables, which are ignored but take room in a value record.</summary>
    private const int DeviceBits = 0x00F0;

    /// <summary>The format of a single adjustment that gives every glyph the same value.</summary>
    private const int SingleValueFormat = 1;

    /// <summary>The format of a pair adjustment that lists the second glyphs of each first glyph.</summary>
    private const int PairListFormat = 1;

    /// <summary>The offset of the second value format in a pair subtable.</summary>
    private const int SecondFormatField = 6;

    /// <summary>The offset of the pair set count in a format 1 pair subtable.</summary>
    private const int PairSetCountField = 8;

    /// <summary>The offset of the pair set offsets in a format 1 pair subtable.</summary>
    private const int PairSetOffsetsField = 10;

    /// <summary>The offset of the first class definition in a format 2 pair subtable.</summary>
    private const int FirstClassDefField = 8;

    /// <summary>The offset of the second class definition in a format 2 pair subtable.</summary>
    private const int SecondClassDefField = 10;

    /// <summary>The offset of the first class count in a format 2 pair subtable.</summary>
    private const int FirstClassCountField = 12;

    /// <summary>The offset of the second class count in a format 2 pair subtable.</summary>
    private const int SecondClassCountField = 14;

    /// <summary>The offset of the class records in a format 2 pair subtable.</summary>
    private const int ClassRecordsField = 16;

    /// <summary>The offset of the entry and exit anchor records in a cursive subtable.</summary>
    private const int CursiveRecordsField = 6;

    /// <summary>The bytes of an entry and exit anchor record.</summary>
    private const int CursiveRecordBytes = 4;

    /// <summary>The offset of the horizontal coordinate in an Anchor table.</summary>
    private const int AnchorX = 2;

    /// <summary>The offset of the vertical coordinate in an Anchor table.</summary>
    private const int AnchorY = 4;

    /// <summary>The offset of the count of entry and exit anchor records in a cursive subtable.</summary>
    private const int CursiveCountField = 4;

    /// <summary>Applies a GPOS subtable at a glyph.</summary>
    /// <param name="run">The engine.</param>
    /// <param name="type">The lookup type.</param>
    /// <param name="subtable">The offset of the subtable.</param>
    /// <param name="index">The glyph's index.</param>
    /// <param name="next">Receives the index to continue from.</param>
    /// <returns><see langword="true"/> when the subtable adjusted the glyphs.</returns>
    internal static bool Apply(LayoutEngine run, int type, int subtable, int index, out int next)
    {
        next = index + 1;
        return type switch
        {
            SingleType => Single(run, subtable, index),
            PairType => Pair(run, subtable, index, out next),
            CursiveType => Cursive(run, subtable, index),
            MarkToBaseType or MarkToLigatureType or MarkToMarkType => MarkAttachment.Apply(run, type, subtable, index),
            ContextType => SequenceContext.ApplyContext(run, subtable, index, out next),
            ChainType => SequenceContext.ApplyChain(run, subtable, index, out next),
            _ => false,
        };
    }

    /// <summary>Reads an anchor point.</summary>
    /// <param name="table">The table.</param>
    /// <param name="offset">The offset of the Anchor table, or zero for none.</param>
    /// <param name="x">Receives the horizontal coordinate.</param>
    /// <param name="y">Receives the vertical coordinate.</param>
    /// <returns><see langword="true"/> when the anchor exists.</returns>
    internal static bool TryReadAnchor(ReadOnlySpan<byte> table, int offset, out int x, out int y)
    {
        x = 0;
        y = 0;
        if (offset <= 0)
        {
            return false;
        }

        x = FontBytes.S16(table, offset + AnchorX);
        y = FontBytes.S16(table, offset + AnchorY);
        return true;
    }

    /// <summary>Gets the bytes of a value record.</summary>
    /// <param name="format">The value format.</param>
    /// <returns>The size in bytes.</returns>
    private static int ValueSize(int format) => BitOperations.PopCount((uint)(format & (ValueBits | DeviceBits))) * FontBytes.U16Size;

    /// <summary>Adds a value record to a glyph's position.</summary>
    /// <param name="table">The table.</param>
    /// <param name="offset">The offset of the value record.</param>
    /// <param name="format">The value format.</param>
    /// <param name="position">The glyph's position.</param>
    private static void ApplyValue(ReadOnlySpan<byte> table, int offset, int format, ref GlyphPosition position)
    {
        var at = offset;
        if ((format & XPlacement) != 0)
        {
            position.XOffset += FontBytes.S16(table, at);
            at += FontBytes.U16Size;
        }

        if ((format & YPlacement) != 0)
        {
            position.YOffset += FontBytes.S16(table, at);
            at += FontBytes.U16Size;
        }

        if ((format & XAdvance) != 0)
        {
            position.XAdvance += FontBytes.S16(table, at);
        }

        // The vertical advance and the device tables are not used: the run is laid out along one axis and rendered at one size.
    }

    /// <summary>Applies a single adjustment (OpenType, "GPOS lookup type 1").</summary>
    /// <param name="run">The engine.</param>
    /// <param name="subtable">The subtable.</param>
    /// <param name="index">The glyph's index.</param>
    /// <returns><see langword="true"/> when the glyph is covered.</returns>
    private static bool Single(LayoutEngine run, int subtable, int index)
    {
        var table = run.Table;
        var covered = OtCoverage.IndexOf(table, subtable + FontBytes.U16(table, subtable + CoverageField), run.Buffer.Items[index].Glyph);
        if (covered < 0)
        {
            return false;
        }

        var format = FontBytes.U16(table, subtable + ValueFormatField);
        var records = subtable + ValueFormatField + FontBytes.U16Size;
        var at = FontBytes.U16(table, subtable) == SingleValueFormat
            ? records
            : records + FontBytes.U16Size + (covered * ValueSize(format));
        ApplyValue(table, at, format, ref run.Buffer.Positions[index]);
        return true;
    }

    /// <summary>Applies a pair adjustment (OpenType, "GPOS lookup type 2"), which kerns two glyphs.</summary>
    /// <param name="run">The engine.</param>
    /// <param name="subtable">The subtable.</param>
    /// <param name="index">The index of the first glyph.</param>
    /// <param name="next">Receives the index to continue from.</param>
    /// <returns><see langword="true"/> when the pair is adjusted.</returns>
    private static bool Pair(LayoutEngine run, int subtable, int index, out int next)
    {
        next = index + 1;
        var table = run.Table;
        var items = run.Buffer.Items;
        var second = run.NextUnskipped(index);
        var covered = OtCoverage.IndexOf(table, subtable + FontBytes.U16(table, subtable + CoverageField), items[index].Glyph);
        if (second < 0 || covered < 0)
        {
            return false;
        }

        var first = FontBytes.U16(table, subtable + ValueFormatField);
        var firstSize = ValueSize(first);
        var secondFormat = FontBytes.U16(table, subtable + SecondFormatField);
        var secondSize = ValueSize(secondFormat);
        var record = FontBytes.U16(table, subtable) == PairListFormat
            ? FindPair(table, subtable, covered, items[second].Glyph, firstSize + secondSize)
            : FindClassPair(table, subtable, items[index].Glyph, items[second].Glyph, firstSize + secondSize);
        if (record < 0)
        {
            return false;
        }

        var positions = run.Buffer.Positions;
        ApplyValue(table, record, first, ref positions[index]);
        if (secondFormat != 0)
        {
            ApplyValue(table, record + firstSize, secondFormat, ref positions[second]);
            next = second + 1;
        }

        return true;
    }

    /// <summary>Finds the value records of a pair in a format 1 pair set.</summary>
    /// <param name="table">The table.</param>
    /// <param name="subtable">The subtable.</param>
    /// <param name="covered">The first glyph's coverage index.</param>
    /// <param name="glyph">The second glyph.</param>
    /// <param name="valueBytes">The bytes of both value records.</param>
    /// <returns>The offset of the first value record, or -1 when the pair is not listed.</returns>
    private static int FindPair(ReadOnlySpan<byte> table, int subtable, int covered, int glyph, int valueBytes)
    {
        if (covered >= FontBytes.U16(table, subtable + PairSetCountField))
        {
            return -1;
        }

        var set = subtable + FontBytes.U16(table, subtable + PairSetOffsetsField + (covered * FontBytes.U16Size));
        var stride = FontBytes.U16Size + valueBytes;
        var low = 0;
        var high = FontBytes.U16(table, set) - 1;
        while (low <= high)
        {
            var middle = (low + high) >>> 1;
            var record = set + FontBytes.U16Size + (middle * stride);
            var candidate = FontBytes.U16(table, record);
            if (candidate == glyph)
            {
                return record + FontBytes.U16Size;
            }

            if (candidate < glyph)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return -1;
    }

    /// <summary>Finds the value records of a pair in a format 2 class pair table.</summary>
    /// <param name="table">The table.</param>
    /// <param name="subtable">The subtable.</param>
    /// <param name="first">The first glyph.</param>
    /// <param name="second">The second glyph.</param>
    /// <param name="valueBytes">The bytes of both value records.</param>
    /// <returns>The offset of the first value record, or -1 when the classes are out of range.</returns>
    private static int FindClassPair(ReadOnlySpan<byte> table, int subtable, int first, int second, int valueBytes)
    {
        var firstClass = OtClassDef.ClassOf(table, ResolveOffset(table, subtable, subtable + FirstClassDefField), first);
        var secondClass = OtClassDef.ClassOf(table, ResolveOffset(table, subtable, subtable + SecondClassDefField), second);
        var secondCount = FontBytes.U16(table, subtable + SecondClassCountField);
        var inRange = firstClass < FontBytes.U16(table, subtable + FirstClassCountField) && secondClass < secondCount;
        return inRange ? subtable + ClassRecordsField + (((firstClass * secondCount) + secondClass) * valueBytes) : -1;
    }

    /// <summary>Gets an absolute offset from an offset field, where zero means none.</summary>
    /// <param name="table">The table.</param>
    /// <param name="baseOffset">The offset the field's value is measured from.</param>
    /// <param name="field">The offset of the field.</param>
    /// <returns>The absolute offset, or zero.</returns>
    private static int ResolveOffset(ReadOnlySpan<byte> table, int baseOffset, int field)
    {
        var relative = FontBytes.U16(table, field);
        return relative == 0 ? 0 : baseOffset + relative;
    }

    /// <summary>Applies a cursive attachment (OpenType, "GPOS lookup type 3"): a glyph joins the one before it where its entry anchor meets that glyph's exit anchor.</summary>
    /// <param name="run">The engine.</param>
    /// <param name="subtable">The subtable.</param>
    /// <param name="index">The glyph's index.</param>
    /// <returns><see langword="true"/> when the glyph was joined to the previous one.</returns>
    private static bool Cursive(LayoutEngine run, int subtable, int index)
    {
        var previous = run.PreviousUnskipped(index);
        if (previous < 0 || !TryCursiveAnchors(run.Table, subtable, run.Buffer.Items[previous].Glyph, run.Buffer.Items[index].Glyph, out var anchors))
        {
            return false;
        }

        var positions = run.Buffer.Positions;
        if (run.RightToLeft)
        {
            // Drawn right to left, the later glyph sits to the left, so its entry anchor meets the earlier glyph's exit anchor from the other side.
            positions[index].XAdvance = anchors.EntryX + positions[index].XOffset;
            positions[previous].XAdvance -= anchors.ExitX;
            positions[previous].XOffset -= anchors.ExitX;
        }
        else
        {
            positions[previous].XAdvance = anchors.ExitX + positions[previous].XOffset;
            positions[index].XAdvance -= anchors.EntryX;
            positions[index].XOffset -= anchors.EntryX;
        }

        positions[index].Attach = AttachKind.Cursive;
        positions[index].AttachTo = previous;
        positions[index].AttachY = anchors.ExitY - anchors.EntryY;
        return true;
    }

    /// <summary>Reads the exit anchor of the earlier glyph and the entry anchor of the later one.</summary>
    /// <param name="table">The table.</param>
    /// <param name="subtable">The subtable.</param>
    /// <param name="earlier">The earlier glyph.</param>
    /// <param name="later">The later glyph.</param>
    /// <param name="anchors">Receives the two anchors.</param>
    /// <returns><see langword="true"/> when both anchors exist.</returns>
    private static bool TryCursiveAnchors(ReadOnlySpan<byte> table, int subtable, int earlier, int later, out CursiveAnchors anchors)
    {
        anchors = default;
        var coverage = subtable + FontBytes.U16(table, subtable + CoverageField);
        var exitIndex = OtCoverage.IndexOf(table, coverage, earlier);
        var entryIndex = OtCoverage.IndexOf(table, coverage, later);
        var count = FontBytes.U16(table, subtable + CursiveCountField);
        if (exitIndex < 0 || entryIndex < 0 || exitIndex >= count || entryIndex >= count)
        {
            return false;
        }

        var exit = ResolveOffset(table, subtable, subtable + CursiveRecordsField + (exitIndex * CursiveRecordBytes) + FontBytes.U16Size);
        var entry = ResolveOffset(table, subtable, subtable + CursiveRecordsField + (entryIndex * CursiveRecordBytes));
        if (!TryReadAnchor(table, exit, out var exitX, out var exitY) || !TryReadAnchor(table, entry, out var entryX, out var entryY))
        {
            return false;
        }

        anchors = new(exitX, exitY, entryX, entryY);
        return true;
    }

    /// <summary>The anchors where two cursive glyphs join.</summary>
    /// <param name="ExitX">The horizontal coordinate of the earlier glyph's exit anchor.</param>
    /// <param name="ExitY">The vertical coordinate of the earlier glyph's exit anchor.</param>
    /// <param name="EntryX">The horizontal coordinate of the later glyph's entry anchor.</param>
    /// <param name="EntryY">The vertical coordinate of the later glyph's entry anchor.</param>
    private readonly record struct CursiveAnchors(int ExitX, int ExitY, int EntryX, int EntryY);
}
