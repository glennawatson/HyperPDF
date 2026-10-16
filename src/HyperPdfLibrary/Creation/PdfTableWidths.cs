// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace HyperPdfLibrary.Creation;

/// <summary>Works out the width of each column of a table: explicit widths are kept, the other columns are sized to their text, and the lot is scaled to fit the page.</summary>
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
        var total = 0F;
        foreach (var width in widths)
        {
            total += width;
        }

        if (total <= textWidth)
        {
            return;
        }

        var scale = textWidth / total;
        for (var c = 0; c < widths.Length; c++)
        {
            widths[c] = Math.Max(widths[c] * scale, minimum);
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
        var fixedTotal = 0F;
        var automatic = 0;
        for (var c = 0; c < widths.Length; c++)
        {
            var requested = c < layout.Table.ColumnWidths.Count ? layout.Table.ColumnWidths[c] : 0F;
            if (requested > 0F)
            {
                widths[c] = Math.Max(requested, minimum);
                fixedTotal += widths[c];
            }
            else
            {
                automatic++;
            }
        }

        if (automatic > 0)
        {
            MeasureNatural(ref layout, widths, natural, minimum);
            Share(widths, natural, Math.Max(textWidth - fixedTotal, automatic * minimum));
        }

        FitToText(widths, textWidth, minimum);
    }

    /// <summary>Measures the width each unsized column wants: its widest line of unwrapped text plus padding.</summary>
    /// <param name="layout">The table's layout.</param>
    /// <param name="widths">The column widths; a zero marks a column that is not sized yet.</param>
    /// <param name="natural">Receives the wanted width of each column; entries for sized columns are not used.</param>
    /// <param name="minimum">The narrowest a column may be.</param>
    private static void MeasureNatural(ref TableLayout layout, scoped ReadOnlySpan<float> widths, scoped Span<float> natural, float minimum)
    {
        var rows = layout.Table.Rows;
        for (var r = 0; r < rows.Count; r++)
        {
            var font = layout.GetFont(r < layout.HeaderRows);
            var cells = rows[r].Cells;
            for (var c = 0; c < cells.Count && c < widths.Length; c++)
            {
                if (widths[c] > 0F)
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
}
