// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Creation;

/// <summary>
/// Lays a <see cref="PdfFlowTable"/> out on the pages of a <see cref="FlowRun"/>. A row stays on one page when it fits on a
/// page of its own; a taller row is split between lines and continues on the next page. A table that runs onto another page
/// repeats its header rows there as an artifact, since the real header is in the structure once (ISO 32000-2 14.8.4.8).
/// </summary>
internal static class PdfTableFlow
{
    /// <summary>One half, for centring.</summary>
    private const float Half = 0.5F;

    /// <summary>The fraction of a page the repeated headers may take; taller headers are drawn once.</summary>
    private const float MaxRepeatedFraction = 0.5F;

    /// <summary>How far above a page's first row position a row may start and still count as at the top of the page.</summary>
    private const float TopTolerance = 0.01F;

    /// <summary>The options of a header cell: it labels the cells below it.</summary>
    private static readonly PdfTagOptions ColumnHeader = new() { TableScope = PdfTableScope.Column };

    /// <summary>Writes a table.</summary>
    /// <param name="run">The flow being written.</param>
    /// <param name="table">The table.</param>
    /// <exception cref="ArgumentOutOfRangeException">The padding or font size is invalid.</exception>
    internal static void Write(ref FlowRun run, PdfFlowTable table)
    {
        ArgumentNullException.ThrowIfNull(table.Rows);
        ArgumentNullException.ThrowIfNull(table.ColumnWidths);
        ArgumentNullException.ThrowIfNull(table.ColumnAlignments);
        ArgumentOutOfRangeException.ThrowIfNegative(table.CellPadding);
        RichTableGrid.ValidateTable(table);
        if (NeedsRichLayout(table.Rows))
        {
            PdfRichTableFlow.Write(ref run, table);
            return;
        }

        var columns = CountColumns(table.Rows);
        if (columns == 0)
        {
            return;
        }

        var layout = new TableLayout(table, run.Options, columns, run.Options.Margin);
        try
        {
            WriteRows(ref run, ref layout);
        }
        finally
        {
            layout.Dispose();
        }
    }

    /// <summary>Determines whether any cell in the table requires the rich layout engine.</summary>
    /// <param name="rows">The table rows.</param>
    /// <returns><see langword="true"/> when any cell uses spans, custom font size, runs, images, code blocks or flow blocks.</returns>
    private static bool NeedsRichLayout(IReadOnlyList<PdfTableRow> rows)
    {
        for (var r = 0; r < rows.Count; r++)
        {
            var cells = rows[r].Cells;
            for (var c = 0; c < cells.Count; c++)
            {
                if (cells[c].UsesRichLayout)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Resolves the columns, then writes every row.</summary>
    /// <param name="run">The flow being written.</param>
    /// <param name="layout">The table's layout.</param>
    private static void WriteRows(ref FlowRun run, ref TableLayout layout)
    {
        PdfTableWidths.Resolve(ref layout, run.TextWidth);
        MeasureHeaders(ref layout);
        _ = run.Reserve(Math.Min(layout.HeaderHeight + layout.GetHeight(1), run.TextHeight));
        run.AddSpaceBefore(layout.Size);
        layout.TableElement = run.Creator.Tags is { } tags ? tags.AddElement(tags.Root, "Table") : null;
        var rows = layout.Table.Rows;
        for (var r = 0; r < rows.Count; r++)
        {
            run.CancellationToken.ThrowIfCancellationRequested();
            WriteRow(ref run, ref layout, rows[r], r < layout.HeaderRows);
        }

        run.CloseContent();
        run.AddSpaceAfter(layout.Size);
    }

    /// <summary>Counts the columns: the most cells in any row.</summary>
    /// <param name="rows">The rows.</param>
    /// <returns>The number of columns.</returns>
    private static int CountColumns(IReadOnlyList<PdfTableRow> rows)
    {
        var columns = 0;
        for (var r = 0; r < rows.Count; r++)
        {
            columns = Math.Max(columns, rows[r].Cells.Count);
        }

        return columns;
    }

    /// <summary>Measures the header rows and decides whether they repeat.</summary>
    /// <param name="layout">The table's layout.</param>
    private static void MeasureHeaders(ref TableLayout layout)
    {
        var height = 0F;
        for (var r = 0; r < layout.HeaderRows; r++)
        {
            height += layout.GetHeight(Math.Max(WrapRow(ref layout, layout.Table.Rows[r], header: true), 1));
        }

        layout.HeaderHeight = height;
        layout.RepeatHeaders = layout.HeaderRows > 0 && layout.HeaderRows < layout.Table.Rows.Count && height < (layout.PageTop - layout.Margin) * MaxRepeatedFraction;
    }

    /// <summary>Wraps the text of every cell of a row to its column.</summary>
    /// <param name="layout">The table's layout; its lines and cell ranges receive the result.</param>
    /// <param name="row">The row.</param>
    /// <param name="header">Whether the row is a header row.</param>
    /// <returns>The most lines in any cell.</returns>
    private static int WrapRow(ref TableLayout layout, PdfTableRow row, bool header)
    {
        layout.Lines.Clear();
        var most = 0;
        for (var c = 0; c < layout.Columns; c++)
        {
            var text = c < row.Cells.Count ? row.Cells[c].Text : string.Empty;
            var font = layout.GetCellFont(row, c, header);
            layout.Starts[c] = layout.Lines.Count;
            PdfTextWrapper.Wrap(font, layout.Size, text, layout.GetWidth(c) - layout.Inset, ref layout.Lines);
            layout.Counts[c] = layout.Lines.Count - layout.Starts[c];
            most = Math.Max(most, layout.Counts[c]);
        }

        return most;
    }

    /// <summary>Writes one row, continuing it on the next page when it does not fit.</summary>
    /// <param name="run">The flow being written.</param>
    /// <param name="layout">The table's layout.</param>
    /// <param name="row">The row.</param>
    /// <param name="header">Whether it is a header row.</param>
    private static void WriteRow(ref FlowRun run, ref TableLayout layout, PdfTableRow row, bool header)
    {
        var total = Math.Max(WrapRow(ref layout, row, header), 1);
        CreateElements(ref run, ref layout, header);
        var done = 0;
        while (done < total)
        {
            var take = Plan(ref run, ref layout, total - done, done == 0, header);
            if (take == 0)
            {
                StartContinuation(ref run, ref layout, header);
                _ = WrapRow(ref layout, row, header);
                continue;
            }

            DrawSegment(ref run, ref layout, row, header, done, take);
            done += take;
        }
    }

    /// <summary>Creates the structure elements of a row, when the document is tagged.</summary>
    /// <param name="run">The flow being written.</param>
    /// <param name="layout">The table's layout; its cell elements receive the result.</param>
    /// <param name="header">Whether it is a header row.</param>
    private static void CreateElements(ref FlowRun run, ref TableLayout layout, bool header)
    {
        if (run.Creator.Tags is not { } tags || layout.TableElement is not { } table)
        {
            Array.Clear(layout.Cells);
            return;
        }

        var tableRow = tags.AddElement(table, "TR");
        for (var c = 0; c < layout.Columns; c++)
        {
            layout.Cells[c] = header ? tags.AddElement(tableRow, "TH", ColumnHeader) : tags.AddElement(tableRow, "TD");
        }
    }

    /// <summary>Decides how many of a row's remaining lines to draw on the open page.</summary>
    /// <param name="run">The flow being written.</param>
    /// <param name="layout">The table's layout.</param>
    /// <param name="remaining">The lines of the row not drawn yet.</param>
    /// <param name="first">Whether none of the row is drawn yet.</param>
    /// <param name="header">Whether it is a header row.</param>
    /// <returns>The lines to draw, or zero to continue on a new page.</returns>
    private static int Plan(ref FlowRun run, ref TableLayout layout, int remaining, bool first, bool header)
    {
        var fit = FitLines(ref layout, run.Top);
        if (fit >= remaining)
        {
            return remaining;
        }

        if (run.Top >= layout.FreshTop - TopTolerance)
        {
            return Math.Max(fit, 1);
        }

        var next = header || !layout.RepeatHeaders ? layout.PageTop : layout.PageTop - layout.HeaderHeight;
        if (first && remaining <= FitLines(ref layout, next))
        {
            return 0;
        }

        return fit >= 1 ? fit : 0;
    }

    /// <summary>Counts the lines that fit between a position and the bottom margin.</summary>
    /// <param name="layout">The table's layout.</param>
    /// <param name="top">The top of the space.</param>
    /// <returns>The number of lines, with room for the padding.</returns>
    private static int FitLines(ref TableLayout layout, float top) =>
        (int)Math.Max(0F, (top - layout.Margin - layout.Inset) / layout.Leading);

    /// <summary>Starts the next page of a table, repeating the header rows on it.</summary>
    /// <param name="run">The flow being written.</param>
    /// <param name="layout">The table's layout.</param>
    /// <param name="header">Whether the row being written is a header row, which has no header to repeat above it.</param>
    private static void StartContinuation(ref FlowRun run, ref TableLayout layout, bool header)
    {
        run.StartNewPage();
        layout.FreshTop = run.Top;
        if (header || !layout.RepeatHeaders)
        {
            return;
        }

        var tagged = run.Creator.Tags is not null;
        if (tagged)
        {
            run.Canvas.BeginArtifact(PdfArtifactKind.Layout);
            layout.InArtifact = true;
        }

        for (var r = 0; r < layout.HeaderRows; r++)
        {
            var row = layout.Table.Rows[r];
            var lines = Math.Max(WrapRow(ref layout, row, header: true), 1);
            DrawSegment(ref run, ref layout, row, header: true, 0, lines);
        }

        if (tagged)
        {
            run.Canvas.EndArtifact();
            layout.InArtifact = false;
        }

        layout.FreshTop = run.Top;
    }

    /// <summary>Draws a stretch of a row's lines in every cell, then its borders, and moves down.</summary>
    /// <param name="run">The flow being written.</param>
    /// <param name="layout">The table's layout.</param>
    /// <param name="row">The row.</param>
    /// <param name="header">Whether the row is a header row.</param>
    /// <param name="done">The lines already drawn on earlier pages.</param>
    /// <param name="take">The lines to draw.</param>
    private static void DrawSegment(ref FlowRun run, ref TableLayout layout, PdfTableRow row, bool header, int done, int take)
    {
        var top = run.Top;
        var height = layout.GetHeight(take);
        DrawBackgrounds(ref run, ref layout, row, top, height);
        for (var c = 0; c < layout.Columns; c++)
        {
            var last = Math.Min(done + take, layout.Counts[c]);
            if (last <= done)
            {
                continue;
            }

            run.OpenContent(layout.InArtifact ? null : layout.Cells[c]);
            DrawCell(ref run, ref layout, row, header, c, done, last);
            run.CloseContent();
        }

        DrawBorders(ref run, ref layout, top, height);
        run.Top = top - height;
    }

    /// <summary>Draws the background colour of any cells in the row that specify one.</summary>
    /// <param name="run">The flow being written.</param>
    /// <param name="layout">The table's layout.</param>
    /// <param name="row">The row.</param>
    /// <param name="top">The top of the row slice.</param>
    /// <param name="height">The height of the row slice.</param>
    private static void DrawBackgrounds(ref FlowRun run, ref TableLayout layout, PdfTableRow row, float top, float height)
    {
        for (var c = 0; c < layout.Columns && c < row.Cells.Count; c++)
        {
            if (row.Cells[c].BackgroundColor is not { } bg)
            {
                continue;
            }

            run.CloseContent();
            var wrap = run.Creator.Tags is not null && !layout.InArtifact;
            if (wrap)
            {
                run.Canvas.BeginArtifact(PdfArtifactKind.Layout);
            }

            run.Canvas.FillRectangle(bg.Red, bg.Green, bg.Blue, layout.Lefts[c], top - height, layout.GetWidth(c), height);
            if (wrap)
            {
                run.Canvas.EndArtifact();
            }
        }
    }

    /// <summary>Draws some lines of one cell.</summary>
    /// <param name="run">The flow being written.</param>
    /// <param name="layout">The table's layout.</param>
    /// <param name="row">The row.</param>
    /// <param name="header">Whether the row is a header row.</param>
    /// <param name="column">The column.</param>
    /// <param name="first">The first line to draw, counted from the cell's first.</param>
    /// <param name="last">The index after the last line to draw.</param>
    private static void DrawCell(ref FlowRun run, ref TableLayout layout, PdfTableRow row, bool header, int column, int first, int last)
    {
        var text = column < row.Cells.Count ? row.Cells[column].Text : string.Empty;
        var font = layout.GetCellFont(row, column, header);
        var color = layout.GetCellColor(row, column);
        var alignment = layout.GetAlignment(row, column);
        var ascent = font.GetAscent(layout.Size);
        var offset = ((layout.Leading - (ascent - font.GetDescent(layout.Size))) * Half) + ascent;
        var lineTop = run.Top - layout.Padding;
        for (var i = first; i < last; i++)
        {
            var line = layout.Lines[layout.Starts[column] + i];
            if (line.Length > 0)
            {
                var x = GetLeft(ref layout, column, alignment, line.Width);
                run.Canvas.DrawText(font, layout.Size, color, x, lineTop - offset, text.AsSpan(line.Start, line.Length));
            }

            lineTop -= layout.Leading;
        }
    }

    /// <summary>Finds where a line of a cell starts.</summary>
    /// <param name="layout">The table's layout.</param>
    /// <param name="column">The column.</param>
    /// <param name="alignment">How the text lines up.</param>
    /// <param name="width">The line's advance.</param>
    /// <returns>The x of the line's start.</returns>
    private static float GetLeft(ref TableLayout layout, int column, PdfTableAlignment alignment, float width)
    {
        var left = layout.Lefts[column] + layout.Padding;
        var space = layout.GetWidth(column) - layout.Inset - width;
        return alignment switch
        {
            PdfTableAlignment.Right => left + space,
            PdfTableAlignment.Center => left + (space * Half),
            _ => left,
        };
    }

    /// <summary>Strokes the outline of a stretch of a row and the lines between its columns. In a tagged document they are an artifact.</summary>
    /// <param name="run">The flow being written.</param>
    /// <param name="layout">The table's layout.</param>
    /// <param name="top">The top of the stretch.</param>
    /// <param name="height">The height of the stretch.</param>
    private static void DrawBorders(ref FlowRun run, ref TableLayout layout, float top, float height)
    {
        if (layout.Table.Border is not { } stroke)
        {
            return;
        }

        var wrap = run.Creator.Tags is not null && !layout.InArtifact;
        ref var canvas = ref run.Canvas;
        if (wrap)
        {
            canvas.BeginArtifact(PdfArtifactKind.Layout);
        }

        var left = layout.Lefts[0];
        var dividers = layout.Lefts.AsSpan(1, layout.Columns - 1);
        canvas.StrokeRectangleWithDividers(stroke, left, top, layout.Lefts[layout.Columns] - left, height, dividers);

        if (wrap)
        {
            canvas.EndArtifact();
        }
    }
}
