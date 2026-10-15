// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Creation;

/// <summary>
/// Paginates and draws a <see cref="RichTableGrid"/> across one or more pages in a <see cref="FlowRun"/>, handling merged
/// cells (<see cref="PdfTableCell.ColumnSpan"/> and <see cref="PdfTableCell.RowSpan"/>), per-cell fonts and colours, rich text
/// runs, inline images, and embedded code blocks.
/// </summary>
internal static class PdfRichTableFlow
{
    /// <summary>One half, for centring.</summary>
    private const float Half = 0.5F;

    /// <summary>The maximum fraction of a page that repeated headers may occupy.</summary>
    private const float MaxRepeatedFraction = 0.5F;

    /// <summary>Tolerance for floating-point top-of-page and height comparisons.</summary>
    private const float Tolerance = 0.01F;

    /// <summary>Writes a table using rich cell layout and span resolution.</summary>
    /// <param name="run">The active flow run.</param>
    /// <param name="table">The table to write.</param>
    internal static void Write(ref FlowRun run, PdfFlowTable table)
    {
        if (RichTableGrid.Build(table, run.Options, run.TextWidth) is not { } grid)
        {
            return;
        }

        MeasureHeaders(grid);
        var firstRowHeight = grid.HeaderRows < grid.Rows ? grid.RowHeights[grid.HeaderRows] : grid.Inset + grid.Leading;
        _ = run.Reserve(Math.Min(grid.HeaderHeight + firstRowHeight, run.TextHeight));
        run.AddSpaceBefore(grid.Size);
        CreateStructureTree(ref run, grid);
        for (var r = 0; r < grid.Rows; r++)
        {
            run.CancellationToken.ThrowIfCancellationRequested();
            WriteRow(ref run, grid, r, r < grid.HeaderRows);
        }

        run.CloseContent();
        run.AddSpaceAfter(grid.Size);
    }

    /// <summary>Measures the combined height of the header rows and decides whether they repeat on continuation pages.</summary>
    /// <param name="grid">The resolved table grid.</param>
    private static void MeasureHeaders(RichTableGrid grid)
    {
        var height = 0F;
        for (var r = 0; r < grid.HeaderRows; r++)
        {
            height += grid.RowHeights[r];
        }

        grid.HeaderHeight = height;
        grid.RepeatHeaders = grid.HeaderRows > 0
            && grid.HeaderRows < grid.Rows
            && height < (grid.PageTop - grid.Margin) * MaxRepeatedFraction;
    }

    /// <summary>Creates the <c>Table</c>, <c>TR</c>, <c>TH</c> and <c>TD</c> structure elements when the document is tagged.</summary>
    /// <param name="run">The active flow run.</param>
    /// <param name="grid">The resolved table grid.</param>
    private static void CreateStructureTree(ref FlowRun run, RichTableGrid grid)
    {
        if (run.Creator.Tags is not { } tags)
        {
            return;
        }

        var tableElement = tags.AddElement(tags.Root, "Table");
        grid.TableElement = tableElement;
        var rowElements = new PdfTagElement[grid.Rows];
        for (var r = 0; r < grid.Rows; r++)
        {
            rowElements[r] = tags.AddElement(tableElement, "TR");
        }

        for (var i = 0; i < grid.Cells.Count; i++)
        {
            var cell = grid.Cells[i];
            var scope = cell.IsHeader ? PdfTableScope.Column : PdfTableScope.None;
            var options = new PdfTagOptions { TableScope = scope, RowSpan = cell.RowSpan, ColumnSpan = cell.ColumnSpan };
            cell.TagElement = tags.AddElement(rowElements[cell.StartRow], cell.IsHeader ? "TH" : "TD", options);
        }
    }

    /// <summary>Writes one row of the grid, paginating across page boundaries when needed.</summary>
    /// <param name="run">The active flow run.</param>
    /// <param name="grid">The resolved table grid.</param>
    /// <param name="rowIndex">The zero-based row index.</param>
    /// <param name="header">Whether the row is a header row.</param>
    private static void WriteRow(ref FlowRun run, RichTableGrid grid, int rowIndex, bool header)
    {
        var remainingHeight = EnsureEndingCellsFit(grid, rowIndex, grid.RowHeights[rowIndex]);
        if (ShouldBreakBeforeRow(ref run, grid, rowIndex, remainingHeight, header))
        {
            StartContinuation(ref run, grid, header);
        }

        var sliceIndex = 0;
        var isTopOfPageSlice = run.Top >= grid.FreshTop - Tolerance;
        while (remainingHeight > Tolerance || HasRemainingEndingItems(grid, rowIndex))
        {
            remainingHeight = EnsureEndingCellsFit(grid, rowIndex, remainingHeight);
            var available = run.Top - grid.Margin;
            if (sliceIndex > 0 || (remainingHeight > available + Tolerance && !CanFitAnySlice(grid, rowIndex, available) && run.Top < grid.FreshTop - Tolerance))
            {
                StartContinuation(ref run, grid, header);
                available = run.Top - grid.Margin;
                isTopOfPageSlice = true;
            }

            var sliceHeight = PlanSliceHeight(grid, rowIndex, remainingHeight, available);
            var breaksAfter = sliceHeight < remainingHeight - Tolerance
                || WillBreakBeforeNextRow(grid, rowIndex, run.Top - sliceHeight);
            DrawRowSlice(ref run, grid, rowIndex, sliceHeight, isTopOfPageSlice, breaksAfter);
            remainingHeight = Math.Max(0F, remainingHeight - sliceHeight);
            sliceIndex++;
            isTopOfPageSlice = false;
        }
    }

    /// <summary>Ensures the remaining height of a row is large enough to hold all remaining items of cells ending at that row.</summary>
    /// <param name="grid">The table grid.</param>
    /// <param name="rowIndex">The row index.</param>
    /// <param name="currentHeight">The current remaining height of the row.</param>
    /// <returns>The updated remaining height.</returns>
    private static float EnsureEndingCellsFit(RichTableGrid grid, int rowIndex, float currentHeight)
    {
        var needed = currentHeight;
        for (var c = 0; c < grid.Columns; c++)
        {
            if (grid.GetCell(rowIndex, c) is { } cell && cell.StartCol == c && cell.EndRow == rowIndex && cell.NextItemIndex < cell.Items.Count)
            {
                needed = Math.Max(needed, grid.Inset + cell.RemainingContentHeight);
            }
        }

        grid.RowHeights[rowIndex] = Math.Max(grid.RowHeights[rowIndex], needed);
        return needed;
    }

    /// <summary>Determines whether any cell ending at a row still has undrawn items.</summary>
    /// <param name="grid">The table grid.</param>
    /// <param name="rowIndex">The row index.</param>
    /// <returns><see langword="true"/> when at least one cell ending at <paramref name="rowIndex"/> has undrawn items.</returns>
    private static bool HasRemainingEndingItems(RichTableGrid grid, int rowIndex)
    {
        for (var c = 0; c < grid.Columns; c++)
        {
            if (grid.GetCell(rowIndex, c) is { } cell && cell.StartCol == c && cell.EndRow == rowIndex && cell.NextItemIndex < cell.Items.Count)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether the flow should start a new page before drawing a row.</summary>
    /// <param name="run">The active flow run.</param>
    /// <param name="grid">The table grid.</param>
    /// <param name="rowIndex">The row index.</param>
    /// <param name="rowHeight">The row's height.</param>
    /// <param name="header">Whether the row is a header row.</param>
    /// <returns><see langword="true"/> when a new page should be started first.</returns>
    private static bool ShouldBreakBeforeRow(ref FlowRun run, RichTableGrid grid, int rowIndex, float rowHeight, bool header)
    {
        if (run.Top >= grid.FreshTop - Tolerance)
        {
            return false;
        }

        var available = run.Top - grid.Margin;
        var bandHeight = Math.Max(rowHeight, GetSpanBandHeight(grid, rowIndex));
        if (bandHeight <= available + Tolerance)
        {
            return false;
        }

        var nextPageSpace = header || !grid.RepeatHeaders ? grid.PageTop - grid.Margin : grid.PageTop - grid.Margin - grid.HeaderHeight;
        if (bandHeight <= nextPageSpace + Tolerance)
        {
            return true;
        }

        return !CanFitAnySlice(grid, rowIndex, available);
    }

    /// <summary>Determines whether a page break will occur immediately after <paramref name="rowIndex"/> when drawn ending at <paramref name="topAfterRow"/>.</summary>
    /// <param name="grid">The table grid.</param>
    /// <param name="rowIndex">The current row index.</param>
    /// <param name="topAfterRow">The page Y position after <paramref name="rowIndex"/> is drawn.</param>
    /// <returns><see langword="true"/> when the table ends or the next row will start a new page.</returns>
    private static bool WillBreakBeforeNextRow(RichTableGrid grid, int rowIndex, float topAfterRow)
    {
        var nextRow = rowIndex + 1;
        if (nextRow >= grid.Rows)
        {
            return true;
        }

        var available = topAfterRow - grid.Margin;
        var bandHeight = GetSpanBandHeight(grid, nextRow);
        if (bandHeight <= available + Tolerance)
        {
            return false;
        }

        var nextPageSpace = nextRow < grid.HeaderRows || !grid.RepeatHeaders
            ? grid.PageTop - grid.Margin
            : grid.PageTop - grid.Margin - grid.HeaderHeight;
        return bandHeight <= nextPageSpace + Tolerance || !CanFitAnySlice(grid, nextRow, available);
    }

    /// <summary>Gets the combined height of the band of rows covered by cells starting at <paramref name="rowIndex"/>.</summary>
    /// <param name="grid">The table grid.</param>
    /// <param name="rowIndex">The starting row index.</param>
    /// <returns>The combined height in points.</returns>
    private static float GetSpanBandHeight(RichTableGrid grid, int rowIndex)
    {
        var endRow = rowIndex;
        for (var c = 0; c < grid.Columns; c++)
        {
            if (grid.GetCell(rowIndex, c) is { } cell && cell.StartRow == rowIndex)
            {
                endRow = Math.Max(endRow, cell.EndRow);
            }
        }

        var height = 0F;
        for (var r = rowIndex; r <= endRow; r++)
        {
            height += grid.RowHeights[r];
        }

        return height;
    }

    /// <summary>Determines whether at least one visual item of every active cell in a row fits within <paramref name="available"/> height.</summary>
    /// <param name="grid">The table grid.</param>
    /// <param name="rowIndex">The row index.</param>
    /// <param name="available">The available vertical space on the page.</param>
    /// <returns><see langword="true"/> when a slice of the row can fit.</returns>
    private static bool CanFitAnySlice(RichTableGrid grid, int rowIndex, float available)
    {
        var innerAvailable = available - grid.Inset;
        if (innerAvailable < grid.Leading - Tolerance)
        {
            return false;
        }

        for (var c = 0; c < grid.Columns; c++)
        {
            if (grid.GetCell(rowIndex, c) is { } cell
                && cell.StartCol == c
                && cell.NextItemIndex < cell.Items.Count
                && cell.Items[cell.NextItemIndex].Height > innerAvailable + Tolerance)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Plans the height of the slice of <paramref name="rowIndex"/> to draw on the current page.</summary>
    /// <param name="grid">The table grid.</param>
    /// <param name="rowIndex">The row index.</param>
    /// <param name="remainingHeight">The remaining height of the row.</param>
    /// <param name="available">The available space on the current page.</param>
    /// <returns>The height of the slice to draw.</returns>
    private static float PlanSliceHeight(RichTableGrid grid, int rowIndex, float remainingHeight, float available)
    {
        if (remainingHeight <= available + Tolerance)
        {
            return remainingHeight;
        }

        var innerAvailable = Math.Max(grid.Leading, available - grid.Inset);
        var maxContent = 0F;
        for (var c = 0; c < grid.Columns; c++)
        {
            if (grid.GetCell(rowIndex, c) is { } cell && cell.StartCol == c && cell.NextItemIndex < cell.Items.Count)
            {
                maxContent = Math.Max(maxContent, MeasureFittingItems(cell, innerAvailable));
            }
        }

        return Math.Min(remainingHeight, grid.Inset + Math.Max(maxContent, grid.Leading));
    }

    /// <summary>Measures the height of the consecutive items in a cell that fit within <paramref name="innerAvailable"/>.</summary>
    /// <param name="cell">The placed cell.</param>
    /// <param name="innerAvailable">The available inner height.</param>
    /// <returns>The sum of fitting item heights.</returns>
    private static float MeasureFittingItems(PlacedCell cell, float innerAvailable)
    {
        var sum = 0F;
        for (var i = cell.NextItemIndex; i < cell.Items.Count; i++)
        {
            var itemHeight = cell.Items[i].Height;
            if (sum + itemHeight > innerAvailable + Tolerance && i > cell.NextItemIndex)
            {
                break;
            }

            sum += itemHeight;
            if (sum >= innerAvailable - Tolerance)
            {
                break;
            }
        }

        return sum;
    }

    /// <summary>Starts a continuation page and repeats the table's header rows as a layout artifact when configured.</summary>
    /// <param name="run">The active flow run.</param>
    /// <param name="grid">The table grid.</param>
    /// <param name="header">Whether a header row itself is being continued.</param>
    private static void StartContinuation(ref FlowRun run, RichTableGrid grid, bool header)
    {
        run.StartNewPage();
        grid.FreshTop = run.Top;
        if (header || !grid.RepeatHeaders)
        {
            return;
        }

        var tagged = run.Creator.Tags is not null;
        if (tagged)
        {
            run.Canvas.BeginArtifact(PdfArtifactKind.Layout);
            grid.InArtifact = true;
        }

        ResetHeaderCells(grid);
        for (var r = 0; r < grid.HeaderRows; r++)
        {
            var rowHeight = grid.RowHeights[r];
            DrawRowSlice(ref run, grid, r, rowHeight, isTopOfPageSlice: r == 0, isBottomOfPageSlice: r == grid.HeaderRows - 1);
        }

        if (tagged)
        {
            run.Canvas.EndArtifact();
            grid.InArtifact = false;
        }

        grid.FreshTop = run.Top;
    }

    /// <summary>Resets the item cursors of header cells before repeating the header rows on a new page.</summary>
    /// <param name="grid">The table grid.</param>
    private static void ResetHeaderCells(RichTableGrid grid)
    {
        for (var i = 0; i < grid.Cells.Count; i++)
        {
            var cell = grid.Cells[i];
            if (!cell.IsHeader)
            {
                continue;
            }

            cell.NextItemIndex = 0;
            cell.LastDrawnPage = -1;
        }
    }

    /// <summary>Draws one page slice of a row: cell backgrounds, cell items, and span-aware borders.</summary>
    /// <param name="run">The active flow run.</param>
    /// <param name="grid">The table grid.</param>
    /// <param name="rowIndex">The row index.</param>
    /// <param name="sliceHeight">The vertical height of this row slice.</param>
    /// <param name="isTopOfPageSlice">Whether this row slice sits at the top of the current page's table area.</param>
    /// <param name="isBottomOfPageSlice">Whether a page break or table end follows immediately after this row slice.</param>
    private static void DrawRowSlice(
        ref FlowRun run,
        RichTableGrid grid,
        int rowIndex,
        float sliceHeight,
        bool isTopOfPageSlice,
        bool isBottomOfPageSlice)
    {
        var top = run.Top;
        for (var c = 0; c < grid.Columns; c++)
        {
            if (grid.GetCell(rowIndex, c) is not { } cell || cell.StartCol != c)
            {
                continue;
            }

            if (cell.LastDrawnPage == run.PageNumber && !isTopOfPageSlice)
            {
                continue;
            }

            var cellSpanHeight = GetCellOnPageHeight(grid, cell, rowIndex, sliceHeight, top, isBottomOfPageSlice);
            DrawCellBackground(ref run, grid, cell, top, cellSpanHeight);
            DrawCellItems(ref run, grid, cell, top, cellSpanHeight);
            cell.LastDrawnPage = run.PageNumber;
        }

        run.CloseContent();
        DrawRowBorders(ref run, grid, rowIndex, top, sliceHeight, isTopOfPageSlice, isBottomOfPageSlice);
        run.Top = top - sliceHeight;
    }

    /// <summary>Computes the vertical height that a cell covers on the current page starting from <paramref name="rowIndex"/>.</summary>
    /// <param name="grid">The table grid.</param>
    /// <param name="cell">The placed cell.</param>
    /// <param name="rowIndex">The current row index.</param>
    /// <param name="sliceHeight">The current row slice height.</param>
    /// <param name="top">The page Y at the top of the current row slice.</param>
    /// <param name="isBottomOfPageSlice">Whether the page breaks after this row slice.</param>
    /// <returns>The vertical height covered on the current page.</returns>
    private static float GetCellOnPageHeight(
        RichTableGrid grid,
        PlacedCell cell,
        int rowIndex,
        float sliceHeight,
        float top,
        bool isBottomOfPageSlice)
    {
        var height = sliceHeight;
        if (isBottomOfPageSlice || cell.EndRow <= rowIndex)
        {
            return height;
        }

        for (var r = rowIndex + 1; r <= cell.EndRow; r++)
        {
            var nextHeight = grid.RowHeights[r];
            if (top - height - nextHeight < grid.Margin - Tolerance)
            {
                break;
            }

            height += nextHeight;
            if (WillBreakBeforeNextRow(grid, r, top - height))
            {
                break;
            }
        }

        return height;
    }

    /// <summary>Draws a cell's background colour rectangle when configured.</summary>
    /// <param name="run">The active flow run.</param>
    /// <param name="grid">The table grid.</param>
    /// <param name="cell">The placed cell.</param>
    /// <param name="top">The top Y of the cell box on this page.</param>
    /// <param name="height">The height of the cell box on this page.</param>
    private static void DrawCellBackground(ref FlowRun run, RichTableGrid grid, PlacedCell cell, float top, float height)
    {
        if (cell.Cell.BackgroundColor is not { } bg)
        {
            return;
        }

        run.CloseContent();
        var wrapArtifact = run.Creator.Tags is not null && !grid.InArtifact;
        if (wrapArtifact)
        {
            run.Canvas.BeginArtifact(PdfArtifactKind.Layout);
        }

        var left = grid.Lefts[cell.StartCol];
        var width = grid.GetSpanWidth(cell.StartCol, cell.ColumnSpan);
        run.Canvas.FillRectangle(bg.Red, bg.Green, bg.Blue, left, top - height, width, height);

        if (wrapArtifact)
        {
            run.Canvas.EndArtifact();
        }
    }

    /// <summary>Draws all items of a cell that fit within <paramref name="cellSpanHeight"/> on the current page.</summary>
    /// <param name="run">The active flow run.</param>
    /// <param name="grid">The table grid.</param>
    /// <param name="cell">The placed cell.</param>
    /// <param name="top">The top Y of the cell box on this page.</param>
    /// <param name="cellSpanHeight">The height of the cell box on this page.</param>
    private static void DrawCellItems(ref FlowRun run, RichTableGrid grid, PlacedCell cell, float top, float cellSpanHeight)
    {
        if (cell.NextItemIndex >= cell.Items.Count)
        {
            return;
        }

        var innerAvailable = Math.Max(0F, cellSpanHeight - grid.Inset);
        var cursorY = top - grid.Padding;
        var used = 0F;
        var alignment = grid.GetAlignment(cell);
        var index = cell.NextItemIndex;
        while (index < cell.Items.Count)
        {
            var item = cell.Items[index];
            if (used + item.Height > innerAvailable + Tolerance && index > cell.NextItemIndex)
            {
                break;
            }

            if (item.Kind == CellItemKind.Code)
            {
                var nextIndex = DrawCodeSlice(ref run, grid, cell, index, innerAvailable - used, ref cursorY, ref used);
                index = nextIndex;
                continue;
            }

            DrawSingleItem(ref run, grid, cell, item, alignment, ref cursorY);
            used += item.Height;
            index++;
        }

        cell.NextItemIndex = index;
    }

    /// <summary>Draws a single text line or inline image item inside a cell.</summary>
    /// <param name="run">The active flow run.</param>
    /// <param name="grid">The table grid.</param>
    /// <param name="cell">The placed cell.</param>
    /// <param name="item">The visual item to draw.</param>
    /// <param name="alignment">The horizontal alignment of the cell.</param>
    /// <param name="cursorY">The current vertical cursor, advanced downward by the item's height.</param>
    private static void DrawSingleItem(
        ref FlowRun run,
        RichTableGrid grid,
        PlacedCell cell,
        CellVisualItem item,
        PdfTableAlignment alignment,
        ref float cursorY)
    {
        var innerLeft = grid.Lefts[cell.StartCol] + grid.Padding;
        var innerWidth = grid.GetSpanWidth(cell.StartCol, cell.ColumnSpan) - grid.Inset;
        var slack = Math.Max(0F, innerWidth - item.Width);
        var x = alignment switch
        {
            PdfTableAlignment.Right => innerLeft + slack,
            PdfTableAlignment.Center => innerLeft + (slack * Half),
            _ => innerLeft,
        };

        if (item.Kind == CellItemKind.Text)
        {
            DrawTextLineItem(ref run, grid, cell, item, x, cursorY);
        }
        else if (item.Kind == CellItemKind.Image)
        {
            DrawImageItem(ref run, grid, cell, item, x, cursorY - item.Height);
        }

        cursorY -= item.Height;
    }

    /// <summary>Draws a wrapped line of styled text segments inside a cell.</summary>
    /// <param name="run">The active flow run.</param>
    /// <param name="grid">The table grid.</param>
    /// <param name="cell">The placed cell.</param>
    /// <param name="item">The text line item.</param>
    /// <param name="x">The left X of the line.</param>
    /// <param name="cursorY">The top Y of the line.</param>
    private static void DrawTextLineItem(ref FlowRun run, RichTableGrid grid, PlacedCell cell, CellVisualItem item, float x, float cursorY)
    {
        if (item.Segments.Length == 0)
        {
            return;
        }

        run.OpenContent(grid.InArtifact ? null : cell.TagElement);
        var offset = ((item.Height - (item.Ascent - item.Descent)) * Half) + item.Ascent;
        var baseline = cursorY - offset;
        var segX = x;
        for (var i = 0; i < item.Segments.Length; i++)
        {
            var seg = item.Segments[i];
            if (seg.Length > 0)
            {
                run.Canvas.DrawText(seg.Font, seg.Size, seg.Color, segX, baseline, seg.Source.AsSpan(seg.Start, seg.Length));
            }

            segX += seg.Width;
        }
    }

    /// <summary>Draws an inline image item inside a cell, tagging it as a <c>Figure</c> or layout artifact when tagged.</summary>
    /// <param name="run">The active flow run.</param>
    /// <param name="grid">The table grid.</param>
    /// <param name="cell">The placed cell.</param>
    /// <param name="item">The image visual item.</param>
    /// <param name="x">The left X of the image.</param>
    /// <param name="bottomY">The bottom Y of the image.</param>
    private static void DrawImageItem(
        ref FlowRun run,
        RichTableGrid grid,
        PlacedCell cell,
        CellVisualItem item,
        float x,
        float bottomY)
    {
        if (item.Image is not { } block)
        {
            return;
        }

        var tags = run.Creator.Tags;
        if (tags is null || grid.InArtifact)
        {
            run.Canvas.DrawImage(block.Image, x, bottomY, item.Width, item.Height);
            return;
        }

        run.CloseContent();
        if (string.IsNullOrWhiteSpace(block.AlternativeText) || cell.TagElement is null)
        {
            run.Canvas.BeginArtifact(PdfArtifactKind.Layout);
            run.Canvas.DrawImage(block.Image, x, bottomY, item.Width, item.Height);
            run.Canvas.EndArtifact();
            return;
        }

        var figure = tags.AddElement(cell.TagElement, "Figure", new PdfTagOptions { AlternativeText = block.AlternativeText });
        run.OpenContent(figure);
        run.Canvas.DrawImage(block.Image, x, bottomY, item.Width, item.Height);
        run.CloseContent();
    }

    /// <summary>Draws a contiguous slice of code block lines inside a cell.</summary>
    /// <param name="run">The active flow run.</param>
    /// <param name="grid">The table grid.</param>
    /// <param name="cell">The placed cell.</param>
    /// <param name="startIndex">The index of the first code line item.</param>
    /// <param name="remainingSpace">The remaining inner vertical space in the cell slice.</param>
    /// <param name="cursorY">The current vertical cursor, advanced downward by the slice height.</param>
    /// <param name="used">The vertical space used so far in the cell slice.</param>
    /// <returns>The item index immediately following the drawn code slice.</returns>
    private static int DrawCodeSlice(
        ref FlowRun run,
        RichTableGrid grid,
        PlacedCell cell,
        int startIndex,
        float remainingSpace,
        ref float cursorY,
        ref float used)
    {
        var firstItem = cell.Items[startIndex];
        var block = firstItem.CodeBlock!;
        var sliceHeight = 0F;
        var endIndex = startIndex;
        while (endIndex < cell.Items.Count && ReferenceEquals(cell.Items[endIndex].CodeBlock, block))
        {
            var h = cell.Items[endIndex].Height;
            if (sliceHeight + h > remainingSpace + Tolerance && endIndex > startIndex)
            {
                break;
            }

            sliceHeight += h;
            endIndex++;
        }

        var boxLeft = grid.Lefts[cell.StartCol] + grid.Padding + block.Indent;
        PdfCodeFlow.DrawBox(ref run, block, boxLeft, cursorY - sliceHeight, firstItem.Width, sliceHeight, grid.InArtifact);
        DrawCodeLines(ref run, grid, cell, startIndex, endIndex, boxLeft + block.Padding, cursorY);
        cursorY -= sliceHeight;
        used += sliceHeight;
        return endIndex;
    }

    /// <summary>Draws the text lines of a code block slice inside a cell.</summary>
    /// <param name="run">The active flow run.</param>
    /// <param name="grid">The table grid.</param>
    /// <param name="cell">The placed cell.</param>
    /// <param name="startIndex">The index of the first code line item.</param>
    /// <param name="endIndex">The exclusive end index of the code line slice.</param>
    /// <param name="textX">The left X of the code text.</param>
    /// <param name="topY">The top Y of the code box.</param>
    private static void DrawCodeLines(
        ref FlowRun run,
        RichTableGrid grid,
        PlacedCell cell,
        int startIndex,
        int endIndex,
        float textX,
        float topY)
    {
        var firstItem = cell.Items[startIndex];
        var block = firstItem.CodeBlock!;
        var font = firstItem.CodeFont!;
        var hasTopPad = firstItem.Height > firstItem.CodeLeading + Tolerance;
        var lineTop = topY - (hasTopPad ? block.Padding : 0F);
        var size = block.FontSize;
        var ascent = font.GetAscent(size);
        var offset = ((firstItem.CodeLeading - (ascent - font.GetDescent(size))) * Half) + ascent;
        run.OpenContent(grid.InArtifact ? null : cell.TagElement);
        for (var i = startIndex; i < endIndex; i++)
        {
            var item = cell.Items[i];
            if (item.CodeLine.Length > 0)
            {
                run.Canvas.DrawText(
                    font,
                    size,
                    block.TextColor,
                    textX,
                    lineTop - offset,
                    item.CodeText.AsSpan(item.CodeLine.Start, item.CodeLine.Length));
            }

            lineTop -= item.CodeLeading;
        }
    }

    /// <summary>Draws the horizontal and vertical border segments for a row slice, omitting interior lines inside spanned cells.</summary>
    /// <param name="run">The active flow run.</param>
    /// <param name="grid">The table grid.</param>
    /// <param name="rowIndex">The row index.</param>
    /// <param name="top">The top Y of the row slice.</param>
    /// <param name="height">The height of the row slice.</param>
    /// <param name="isTopOfPageSlice">Whether this row slice is at the top of a page.</param>
    /// <param name="isBottomOfPageSlice">Whether a page break or table end follows this row slice.</param>
    private static void DrawRowBorders(
        ref FlowRun run,
        RichTableGrid grid,
        int rowIndex,
        float top,
        float height,
        bool isTopOfPageSlice,
        bool isBottomOfPageSlice)
    {
        if (grid.Table.Border is not { } stroke)
        {
            return;
        }

        var wrapArtifact = run.Creator.Tags is not null && !grid.InArtifact;
        if (wrapArtifact)
        {
            run.Canvas.BeginArtifact(PdfArtifactKind.Layout);
        }

        var bottom = top - height;
        DrawHorizontalBorders(ref run.Canvas, grid, rowIndex, top, bottom, isTopOfPageSlice, isBottomOfPageSlice);
        DrawVerticalBorders(ref run.Canvas, grid, stroke, rowIndex, top, bottom);

        if (wrapArtifact)
        {
            run.Canvas.EndArtifact();
        }
    }

    /// <summary>Draws the top and bottom horizontal border segments of a row slice.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="grid">The table grid.</param>
    /// <param name="rowIndex">The row index.</param>
    /// <param name="top">The top Y of the row slice.</param>
    /// <param name="bottom">The bottom Y of the row slice.</param>
    /// <param name="isTopOfPageSlice">Whether this row slice is at the top of a page.</param>
    /// <param name="isBottomOfPageSlice">Whether a page break or table end follows this row slice.</param>
    private static void DrawHorizontalBorders(
        ref PdfPageCanvas canvas,
        RichTableGrid grid,
        int rowIndex,
        float top,
        float bottom,
        bool isTopOfPageSlice,
        bool isBottomOfPageSlice)
    {
        var stroke = grid.Table.Border!.Value;
        for (var c = 0; c < grid.Columns; c++)
        {
            var cell = grid.GetCell(rowIndex, c);
            if (rowIndex == 0 || isTopOfPageSlice || cell is null || cell.StartRow == rowIndex)
            {
                canvas.DrawLine(stroke, grid.Lefts[c], top, grid.Lefts[c + 1], top);
            }

            if (rowIndex == grid.Rows - 1 || isBottomOfPageSlice || cell is null || cell.EndRow == rowIndex)
            {
                canvas.DrawLine(stroke, grid.Lefts[c], bottom, grid.Lefts[c + 1], bottom);
            }
        }
    }

    /// <summary>Draws the vertical border segments of a row slice, omitting interior vertical lines inside column-spanning cells.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="grid">The table grid.</param>
    /// <param name="stroke">The border stroke.</param>
    /// <param name="rowIndex">The row index.</param>
    /// <param name="top">The top Y of the row slice.</param>
    /// <param name="bottom">The bottom Y of the row slice.</param>
    private static void DrawVerticalBorders(
        ref PdfPageCanvas canvas,
        RichTableGrid grid,
        PdfStroke stroke,
        int rowIndex,
        float top,
        float bottom)
    {
        for (var c = 0; c <= grid.Columns; c++)
        {
            if (c > 0 && c < grid.Columns)
            {
                var leftCell = grid.GetCell(rowIndex, c - 1);
                var rightCell = grid.GetCell(rowIndex, c);
                if (leftCell is not null && ReferenceEquals(leftCell, rightCell))
                {
                    continue;
                }
            }

            canvas.DrawLine(stroke, grid.Lefts[c], bottom, grid.Lefts[c], top);
        }
    }
}
