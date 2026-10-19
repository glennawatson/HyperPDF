// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Optimizing;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Accessibility;

/// <summary>
/// Checks that a table is regular: every row has the same number of logical cells once <c>RowSpan</c> and <c>ColSpan</c> are counted, and
/// so is each row group, with no cell spanning out of its group (WTPDF 1.0, clause 8.2.5.26). Then it checks the header cells.
/// </summary>
internal static class WtpdfTableScan
{
    /// <summary>Checks a Table tag.</summary>
    /// <param name="table">The Table.</param>
    /// <param name="findings">Receives the finding.</param>
    internal static void Check(PdfStructureElement table, List<PdfWtpdfFinding> findings)
    {
        var rows = default(RentedList<PdfStructureElement>);
        var ends = default(RentedList<int>);
        try
        {
            CollectGroups(table, ref rows, ref ends);
            if (!IsRegular(rows.Span, ends.Span))
            {
                findings.Add(new(
                    PdfWtpdfProfile.Reuse,
                    "8.2.5.26",
                    "A table must be regular: every row needs the same number of cells after row and column spans, and no cell may span out of its row group.",
                    table.Id.Number));
                return;
            }

            WtpdfTableHeaderScan.Check(table, rows.Span, findings);
        }
        finally
        {
            rows.Dispose();
            ends.Dispose();
        }
    }

    /// <summary>Lists a table's rows in order and where each row group (THead, TBody, TFoot, or a run of rows outside any group) ends.</summary>
    /// <param name="table">The Table.</param>
    /// <param name="rows">Receives the rows.</param>
    /// <param name="ends">Receives, for each group, the index in <paramref name="rows"/> after its last row.</param>
    private static void CollectGroups(PdfStructureElement table, ref RentedList<PdfStructureElement> rows, ref RentedList<int> ends)
    {
        var runStart = 0;
        foreach (var kid in table.KidSpan)
        {
            if (kid.Element is not { } child)
            {
                continue;
            }

            if (child.MappedType == "TR")
            {
                rows.Add(child);
            }
            else if (child.MappedType is "THead" or "TBody" or "TFoot")
            {
                CloseGroup(rows.Count, runStart, ref ends);
                var groupStart = rows.Count;
                CollectRows(child, ref rows);
                CloseGroup(rows.Count, groupStart, ref ends);
                runStart = rows.Count;
            }
        }

        CloseGroup(rows.Count, runStart, ref ends);
    }

    /// <summary>Records the end of a group when it holds rows.</summary>
    /// <param name="end">The index after the group's last row.</param>
    /// <param name="start">The index of the group's first row.</param>
    /// <param name="ends">Receives the end.</param>
    private static void CloseGroup(int end, int start, ref RentedList<int> ends)
    {
        if (end <= start)
        {
            return;
        }

        ends.Add(end);
    }

    /// <summary>Determines whether the rows are regular as a whole and each group is regular and closed.</summary>
    /// <param name="rows">The table's rows.</param>
    /// <param name="ends">The index after the last row of each group.</param>
    /// <returns><see langword="true"/> when the table is regular.</returns>
    private static bool IsRegular(ReadOnlySpan<PdfStructureElement> rows, ReadOnlySpan<int> ends)
    {
        if (!IsRegular(rows, false))
        {
            return false;
        }

        var start = 0;
        foreach (var end in ends)
        {
            if (!IsRegular(rows[start..end], true))
            {
                return false;
            }

            start = end;
        }

        return true;
    }

    /// <summary>Determines whether rows all have the same width.</summary>
    /// <param name="rows">The rows.</param>
    /// <param name="closed">Whether a cell may not span past the last row.</param>
    /// <returns><see langword="true"/> when the rows are regular.</returns>
    private static bool IsRegular(ReadOnlySpan<PdfStructureElement> rows, bool closed)
    {
        var carried = default(RentedList<SpanCarry>);
        try
        {
            var width = -1;
            foreach (var row in rows)
            {
                var total = RowWidth(row, ref carried);
                if (width >= 0 && total != width)
                {
                    return false;
                }

                width = total;
            }

            return !closed || carried.Count == 0;
        }
        finally
        {
            carried.Dispose();
        }
    }

    /// <summary>Lists the rows under a row group in order, looking inside nested groups.</summary>
    /// <param name="parent">The row group.</param>
    /// <param name="rows">Receives the rows.</param>
    private static void CollectRows(PdfStructureElement parent, ref RentedList<PdfStructureElement> rows)
    {
        foreach (var kid in parent.KidSpan)
        {
            if (kid.Element is not { } child)
            {
                continue;
            }

            if (child.MappedType == "TR")
            {
                rows.Add(child);
            }
            else if (child.MappedType is "THead" or "TBody" or "TFoot")
            {
                CollectRows(child, ref rows);
            }
        }
    }

    /// <summary>Counts a row's logical cells, including cells carried down from rows above, and records the spans that continue.</summary>
    /// <param name="row">The row.</param>
    /// <param name="carried">The cells still spanning down; updated for the next row.</param>
    /// <returns>The row's width in columns.</returns>
    private static int RowWidth(PdfStructureElement row, ref RentedList<SpanCarry> carried)
    {
        var total = 0;
        var kept = 0;
        for (var i = 0; i < carried.Count; i++)
        {
            var carry = carried[i];
            total += carry.Columns;
            if (carry.Rows <= 1)
            {
                continue;
            }

            carried[kept] = carry with { Rows = carry.Rows - 1 };
            kept++;
        }

        carried.Count = kept;
        foreach (var kid in row.KidSpan)
        {
            if (kid.Element is not { } cell)
            {
                continue;
            }

            var attributes = cell.Attributes;
            total += attributes.ColumnSpan;
            if (attributes.RowSpan > 1)
            {
                carried.Add(new(attributes.RowSpan - 1, attributes.ColumnSpan));
            }
        }

        return total;
    }

    /// <summary>A cell that spans into the rows below.</summary>
    /// <param name="Rows">How many more rows it covers.</param>
    /// <param name="Columns">How many columns it covers.</param>
    private readonly record struct SpanCarry(int Rows, int Columns);
}
