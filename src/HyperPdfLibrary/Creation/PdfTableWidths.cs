// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace HyperPdfLibrary.Creation;

/// <summary>
/// Works out the width of each column of a table: explicit fixed widths are kept, automatic columns are sized to their
/// content, proportional columns split the remaining space by weight, <see cref="PdfFlowTable.FillAvailableWidth"/> expands
/// narrower tables to the available width, and tables wider than the page are scaled down to fit.
/// </summary>
internal static class PdfTableWidths
{
    /// <summary>The narrowest text a column keeps, in points.</summary>
    private const float MinContent = 1F;

    /// <summary>Extra width, in points, that keeps a column sized to its text from wrapping it because the column edges are floating-point sums.</summary>
    private const float RoundingSlack = 0.01F;

    /// <summary>The most columns whose working space is taken from the stack; a wider table rents it.</summary>
    private const int StackColumns = 64;

    /// <summary>The working arrays used for each column: the widths and the wanted widths.</summary>
    private const int ScratchPerColumn = 2;

    /// <summary>Sets the column edges of a table.</summary>
    /// <param name="layout">The table's layout; its <see cref="TableLayout.Lefts"/> receive the edges.</param>
    /// <param name="textWidth">The width the page leaves for text.</param>
    internal static void Resolve(ref TableLayout layout, float textWidth)
    {
        var columns = layout.Columns;
        float[]? rented = null;
        Span<float> scratch = columns <= StackColumns
            ? stackalloc float[StackColumns * ScratchPerColumn]
            : (rented = ArrayPool<float>.Shared.Rent(columns * ScratchPerColumn));
        try
        {
            var widths = scratch[..columns];
            var natural = scratch.Slice(columns, columns);
            widths.Clear();
            natural.Clear();
            ResolveWidths(ref layout, textWidth, widths, natural);
            for (var c = 0; c < columns; c++)
            {
                layout.Lefts[c + 1] = layout.Lefts[c] + widths[c];
            }
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<float>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Gets the effective column width specification for a column index.</summary>
    /// <param name="table">The table.</param>
    /// <param name="column">The zero-based column index.</param>
    /// <returns>The effective <see cref="PdfTableColumnWidth"/>.</returns>
    internal static PdfTableColumnWidth GetColumnSpec(PdfFlowTable table, int column)
    {
        if (column < table.ColumnSizing.Count)
        {
            return table.ColumnSizing[column];
        }

        return column < table.ColumnWidths.Count && table.ColumnWidths[column] > 0F
            ? new(PdfTableColumnWidthKind.Fixed, table.ColumnWidths[column])
            : PdfTableColumnWidth.Auto;
    }

    /// <summary>Applies fixed column widths and counts the automatic columns.</summary>
    /// <param name="table">The table definition.</param>
    /// <param name="minimum">The minimum column width in points.</param>
    /// <param name="widths">Receives fixed column widths; non-fixed columns stay zero.</param>
    /// <param name="fixedTotal">Receives the sum of fixed column widths.</param>
    /// <param name="autoCount">Receives the number of automatic columns.</param>
    internal static void ApplyFixedWidths(
        PdfFlowTable table,
        float minimum,
        scoped Span<float> widths,
        out float fixedTotal,
        out int autoCount)
    {
        fixedTotal = 0F;
        autoCount = 0;
        for (var c = 0; c < widths.Length; c++)
        {
            var spec = GetColumnSpec(table, c);
            if (spec.Kind == PdfTableColumnWidthKind.Fixed)
            {
                widths[c] = Math.Max(spec.Value, minimum);
                fixedTotal += widths[c];
            }
            else if (spec.Kind == PdfTableColumnWidthKind.Auto)
            {
                autoCount++;
            }
        }
    }

    /// <summary>Completes column width resolution for automatic, proportional and fill-available-width columns.</summary>
    /// <param name="table">The table definition.</param>
    /// <param name="textWidth">The page's available text width.</param>
    /// <param name="minimum">The minimum column width.</param>
    /// <param name="fixedTotal">The sum of fixed column widths.</param>
    /// <param name="autoCount">The number of automatic columns.</param>
    /// <param name="widths">The column widths buffer.</param>
    /// <param name="natural">The natural widths of automatic columns.</param>
    internal static void CompleteWidths(
        PdfFlowTable table,
        float textWidth,
        float minimum,
        float fixedTotal,
        int autoCount,
        scoped Span<float> widths,
        scoped ReadOnlySpan<float> natural)
    {
        CountProportional(table, widths.Length, out var propCount, out var totalWeight);
        if (autoCount > 0)
        {
            var autoBudget = ComputeAutoBudget(textWidth, minimum, fixedTotal, autoCount, propCount, natural, table);
            ShareAuto(table, widths, natural, autoBudget, autoCount);
        }

        if (propCount > 0)
        {
            ApplyProportionalWidths(table, textWidth, minimum, totalWeight, propCount, widths);
        }
        else if (table.FillAvailableWidth)
        {
            ExpandToAvailableWidth(table, textWidth, autoCount, widths);
        }

        FitToText(widths, textWidth, minimum);
    }

    /// <summary>Finds the widest of a set of lines.</summary>
    /// <param name="lines">The lines.</param>
    /// <returns>The widest advance.</returns>
    internal static float WidestLine(ref PooledLines lines)
    {
        var widest = 0F;
        for (var i = 0; i < lines.Count; i++)
        {
            widest = Math.Max(widest, lines[i].Width);
        }

        return widest;
    }

    /// <summary>Gives the unsized columns the space left: a column that wants less than an equal share keeps what it wants and the rest split what remains.</summary>
    /// <param name="widths">The column widths; a zero marks a column to size.</param>
    /// <param name="natural">The wanted width of each column.</param>
    /// <param name="remaining">The space to share.</param>
    internal static void Share(scoped Span<float> widths, scoped ReadOnlySpan<float> natural, float remaining)
    {
        var pending = 0;
        foreach (var width in widths)
        {
            pending += width > 0F ? 0 : 1;
        }

        while (pending > 0)
        {
            var share = remaining / pending;
            var taken = TakeWanted(widths, natural, share, ref remaining);
            if (taken == 0)
            {
                SetRemaining(widths, share);
                return;
            }

            pending -= taken;
        }
    }

    /// <summary>Scales the columns down when they are wider than the page's text.</summary>
    /// <param name="widths">The column widths.</param>
    /// <param name="textWidth">The width the page leaves for text.</param>
    /// <param name="minimum">The narrowest a column may be.</param>
    internal static void FitToText(scoped Span<float> widths, float textWidth, float minimum)
    {
        var total = Sum(widths);
        if (total <= textWidth || widths.IsEmpty)
        {
            return;
        }

        var effectiveMin = Math.Min(minimum, textWidth / widths.Length);
        var scale = textWidth / total;
        var scaledTotal = 0F;
        for (var c = 0; c < widths.Length; c++)
        {
            widths[c] = Math.Max(widths[c] * scale, effectiveMin);
            scaledTotal += widths[c];
        }

        if (scaledTotal > textWidth)
        {
            TrimExcess(widths, effectiveMin, scaledTotal - textWidth);
        }
    }

    /// <summary>Fills in the width of every column.</summary>
    /// <param name="layout">The table's layout.</param>
    /// <param name="textWidth">The width the page leaves for text.</param>
    /// <param name="widths">Receives the widths; it starts zeroed.</param>
    /// <param name="natural">Working space for the wanted widths; it starts zeroed.</param>
    private static void ResolveWidths(ref TableLayout layout, float textWidth, scoped Span<float> widths, scoped Span<float> natural)
    {
        var minimum = layout.Inset + MinContent;
        ApplyFixedWidths(layout.Table, minimum, widths, out var fixedTotal, out var autoCount);
        if (autoCount > 0)
        {
            MeasureNatural(ref layout, natural, minimum);
        }

        CompleteWidths(layout.Table, textWidth, minimum, fixedTotal, autoCount, widths, natural);
    }

    /// <summary>Counts proportional columns and sums their weights.</summary>
    /// <param name="table">The table definition.</param>
    /// <param name="columns">The total number of columns.</param>
    /// <param name="propCount">Receives the count of proportional columns.</param>
    /// <param name="totalWeight">Receives the sum of proportional weights.</param>
    private static void CountProportional(PdfFlowTable table, int columns, out int propCount, out float totalWeight)
    {
        propCount = 0;
        totalWeight = 0F;
        for (var c = 0; c < columns; c++)
        {
            var spec = GetColumnSpec(table, c);
            if (spec.Kind != PdfTableColumnWidthKind.Proportional)
            {
                continue;
            }

            propCount++;
            totalWeight += spec.Value;
        }
    }

    /// <summary>Computes the width budget available to automatic columns before proportional columns are sized.</summary>
    /// <param name="textWidth">The page's available text width.</param>
    /// <param name="minimum">The minimum column width.</param>
    /// <param name="fixedTotal">The sum of fixed column widths.</param>
    /// <param name="autoCount">The number of automatic columns.</param>
    /// <param name="propCount">The number of proportional columns.</param>
    /// <param name="natural">The natural widths of automatic columns.</param>
    /// <param name="table">The table definition.</param>
    /// <returns>The width budget for automatic columns.</returns>
    private static float ComputeAutoBudget(
        float textWidth,
        float minimum,
        float fixedTotal,
        int autoCount,
        int propCount,
        scoped ReadOnlySpan<float> natural,
        PdfFlowTable table)
    {
        var afterFixed = Math.Max(textWidth - fixedTotal, (autoCount + propCount) * minimum);
        if (propCount == 0)
        {
            return afterFixed;
        }

        var naturalAutoSum = 0F;
        for (var c = 0; c < natural.Length; c++)
        {
            if (GetColumnSpec(table, c).Kind == PdfTableColumnWidthKind.Auto)
            {
                naturalAutoSum += natural[c];
            }
        }

        var maxBeforePropStarves = afterFixed * (autoCount / (float)(autoCount + propCount));
        return naturalAutoSum + (propCount * minimum) <= afterFixed
            ? naturalAutoSum
            : Math.Max(autoCount * minimum, maxBeforePropStarves);
    }

    /// <summary>Distributes the automatic column budget among <see cref="PdfTableColumnWidthKind.Auto"/> columns.</summary>
    /// <param name="table">The table definition.</param>
    /// <param name="widths">The column widths buffer.</param>
    /// <param name="natural">The natural widths of automatic columns.</param>
    /// <param name="remaining">The space available to automatic columns.</param>
    /// <param name="pending">The number of automatic columns.</param>
    private static void ShareAuto(
        PdfFlowTable table,
        scoped Span<float> widths,
        scoped ReadOnlySpan<float> natural,
        float remaining,
        int pending)
    {
        while (pending > 0)
        {
            var share = remaining / pending;
            var taken = TakeWantedAuto(table, widths, natural, share, ref remaining);
            if (taken == 0)
            {
                SetRemainingAuto(table, widths, share);
                return;
            }

            pending -= taken;
        }
    }

    /// <summary>Assigns widths to proportional columns according to their relative weights.</summary>
    /// <param name="table">The table definition.</param>
    /// <param name="textWidth">The page's available text width.</param>
    /// <param name="minimum">The minimum column width.</param>
    /// <param name="totalWeight">The sum of proportional weights.</param>
    /// <param name="propCount">The number of proportional columns.</param>
    /// <param name="widths">The column widths buffer.</param>
    private static void ApplyProportionalWidths(
        PdfFlowTable table,
        float textWidth,
        float minimum,
        float totalWeight,
        int propCount,
        scoped Span<float> widths)
    {
        var used = Sum(widths);
        var remaining = Math.Max(propCount * minimum, textWidth - used);
        for (var c = 0; c < widths.Length; c++)
        {
            var spec = GetColumnSpec(table, c);
            if (spec.Kind == PdfTableColumnWidthKind.Proportional)
            {
                widths[c] = Math.Max(minimum, remaining * (spec.Value / totalWeight));
            }
        }
    }

    /// <summary>Expands a table to <paramref name="textWidth"/> when <see cref="PdfFlowTable.FillAvailableWidth"/> is enabled.</summary>
    /// <param name="table">The table definition.</param>
    /// <param name="textWidth">The page's available text width.</param>
    /// <param name="autoCount">The number of automatic columns.</param>
    /// <param name="widths">The column widths buffer.</param>
    private static void ExpandToAvailableWidth(PdfFlowTable table, float textWidth, int autoCount, scoped Span<float> widths)
    {
        var total = Sum(widths);
        if (total >= textWidth || total <= 0F)
        {
            return;
        }

        if (autoCount > 0)
        {
            var extra = (textWidth - total) / autoCount;
            for (var c = 0; c < widths.Length; c++)
            {
                if (GetColumnSpec(table, c).Kind == PdfTableColumnWidthKind.Auto)
                {
                    widths[c] += extra;
                }
            }

            return;
        }

        var scale = textWidth / total;
        for (var c = 0; c < widths.Length; c++)
        {
            widths[c] *= scale;
        }
    }

    /// <summary>Measures the width each automatic column wants: its widest line of unwrapped text plus padding.</summary>
    /// <param name="layout">The table's layout.</param>
    /// <param name="natural">Receives the wanted width of each automatic column.</param>
    /// <param name="minimum">The narrowest a column may be.</param>
    private static void MeasureNatural(ref TableLayout layout, scoped Span<float> natural, float minimum)
    {
        var rows = layout.Table.Rows;
        for (var r = 0; r < rows.Count; r++)
        {
            var font = layout.GetFont(r < layout.HeaderRows);
            var cells = rows[r].Cells;
            for (var c = 0; c < cells.Count && c < natural.Length; c++)
            {
                if (GetColumnSpec(layout.Table, c).Kind != PdfTableColumnWidthKind.Auto)
                {
                    continue;
                }

                var cellFont = cells[c].Font ?? font;
                layout.Lines.Clear();
                PdfTextWrapper.Wrap(cellFont, layout.Size, cells[c].Text, float.MaxValue, ref layout.Lines);
                natural[c] = Math.Max(natural[c], WidestLine(ref layout.Lines) + layout.Inset + RoundingSlack);
            }
        }

        for (var c = 0; c < natural.Length; c++)
        {
            natural[c] = Math.Max(natural[c], minimum);
        }
    }

    /// <summary>Sizes the automatic columns that want no more than a share to what they want.</summary>
    /// <param name="table">The table definition.</param>
    /// <param name="widths">The column widths; a zero marks a column to size.</param>
    /// <param name="natural">The wanted width of each column.</param>
    /// <param name="share">An equal share of the space left.</param>
    /// <param name="remaining">The space left; the widths given out come off it.</param>
    /// <returns>The number of columns sized.</returns>
    private static int TakeWantedAuto(
        PdfFlowTable table,
        scoped Span<float> widths,
        scoped ReadOnlySpan<float> natural,
        float share,
        ref float remaining)
    {
        var taken = 0;
        for (var c = 0; c < widths.Length; c++)
        {
            if (widths[c] > 0F || GetColumnSpec(table, c).Kind != PdfTableColumnWidthKind.Auto || natural[c] > share)
            {
                continue;
            }

            widths[c] = natural[c];
            remaining -= natural[c];
            taken++;
        }

        return taken;
    }

    /// <summary>Gives every automatic column still unsized an equal share.</summary>
    /// <param name="table">The table definition.</param>
    /// <param name="widths">The column widths; a zero marks a column to size.</param>
    /// <param name="share">The share.</param>
    private static void SetRemainingAuto(PdfFlowTable table, scoped Span<float> widths, float share)
    {
        for (var c = 0; c < widths.Length; c++)
        {
            if (widths[c] <= 0F && GetColumnSpec(table, c).Kind == PdfTableColumnWidthKind.Auto)
            {
                widths[c] = share;
            }
        }
    }

    /// <summary>Sizes the unsized columns that want no more than a share to what they want.</summary>
    /// <param name="widths">The column widths; a zero marks a column to size.</param>
    /// <param name="natural">The wanted width of each column.</param>
    /// <param name="share">An equal share of the space left.</param>
    /// <param name="remaining">The space left; the widths given out come off it.</param>
    /// <returns>The number of columns sized.</returns>
    private static int TakeWanted(scoped Span<float> widths, scoped ReadOnlySpan<float> natural, float share, ref float remaining)
    {
        var taken = 0;
        for (var c = 0; c < widths.Length; c++)
        {
            if (widths[c] > 0F || natural[c] > share)
            {
                continue;
            }

            widths[c] = natural[c];
            remaining -= natural[c];
            taken++;
        }

        return taken;
    }

    /// <summary>Gives every column still unsized an equal share.</summary>
    /// <param name="widths">The column widths; a zero marks a column to size.</param>
    /// <param name="share">The share.</param>
    private static void SetRemaining(scoped Span<float> widths, float share)
    {
        for (var c = 0; c < widths.Length; c++)
        {
            widths[c] = widths[c] > 0F ? widths[c] : share;
        }
    }

    /// <summary>Sums a span of floats.</summary>
    /// <param name="values">The values to sum.</param>
    /// <returns>The sum.</returns>
    private static float Sum(scoped ReadOnlySpan<float> values)
    {
        var total = 0F;
        foreach (var value in values)
        {
            total += value;
        }

        return total;
    }

    /// <summary>Trims excess width from columns wider than <paramref name="effectiveMin"/> so the total does not exceed the page width.</summary>
    /// <param name="widths">The column widths.</param>
    /// <param name="effectiveMin">The floor width per column.</param>
    /// <param name="excess">The excess width to remove.</param>
    private static void TrimExcess(scoped Span<float> widths, float effectiveMin, float excess)
    {
        var reducible = 0F;
        for (var c = 0; c < widths.Length; c++)
        {
            reducible += Math.Max(0F, widths[c] - effectiveMin);
        }

        if (reducible <= 0F)
        {
            return;
        }

        for (var c = 0; c < widths.Length; c++)
        {
            var slack = Math.Max(0F, widths[c] - effectiveMin);
            if (slack > 0F)
            {
                widths[c] -= excess * (slack / reducible);
            }
        }
    }
}
