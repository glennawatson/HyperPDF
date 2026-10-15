// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Creation;

/// <summary>
/// Resolves a <see cref="PdfFlowTable"/> that uses cell spans, rich text runs, per-cell font sizes, inline images or code
/// blocks onto a two-dimensional grid of <see cref="PlacedCell"/> instances, computing column edges and wrapped row heights.
/// </summary>
internal sealed class RichTableGrid
{
    /// <summary>The narrowest content width a column keeps, in points.</summary>
    private const float MinContent = 1F;

    /// <summary>Extra width in points that keeps a column sized to its content from wrapping due to floating-point rounding.</summary>
    private const float RoundingSlack = 0.01F;

    /// <summary>The flat grid of placed cells indexed by <c>(row * Columns) + column</c>.</summary>
    private readonly PlacedCell?[] _slots;

    /// <summary>Initializes a new instance of the <see cref="RichTableGrid"/> class.</summary>
    /// <param name="table">The table definition.</param>
    /// <param name="options">The flow options.</param>
    /// <param name="columns">The column count.</param>
    /// <param name="slots">The flat grid slot array.</param>
    /// <param name="cells">The placed cells in row-major order.</param>
    /// <param name="lefts">The column left edges array.</param>
    /// <param name="rowHeights">The row heights array.</param>
    private RichTableGrid(
        PdfFlowTable table,
        PdfFlowOptions options,
        int columns,
        PlacedCell?[] slots,
        List<PlacedCell> cells,
        float[] lefts,
        float[] rowHeights)
    {
        var size = table.FontSize ?? options.BodySize;
        var bodyFont = table.BodyFont ?? options.BodyFont;
        var headerFont = table.HeaderFont ?? options.HeadingFont ?? bodyFont;
        Table = table;
        Columns = columns;
        Rows = table.Rows.Count;
        HeaderRows = Math.Clamp(table.HeaderRows, 0, table.Rows.Count);
        Padding = table.CellPadding;
        Inset = table.CellPadding + table.CellPadding;
        Size = size;
        LineSpacing = options.LineSpacing;
        Leading = size * options.LineSpacing;
        Margin = options.Margin;
        PageTop = options.PageHeight - options.Margin;
        BodyFont = bodyFont;
        HeaderFont = headerFont;
        _slots = slots;
        Cells = cells;
        Lefts = lefts;
        RowHeights = rowHeights;
        FreshTop = PageTop;
    }

    /// <summary>Gets the table definition.</summary>
    internal PdfFlowTable Table { get; }

    /// <summary>Gets the number of columns in the grid.</summary>
    internal int Columns { get; }

    /// <summary>Gets the number of rows in the grid.</summary>
    internal int Rows { get; }

    /// <summary>Gets the number of header rows.</summary>
    internal int HeaderRows { get; }

    /// <summary>Gets the padding on one side of a cell.</summary>
    internal float Padding { get; }

    /// <summary>Gets the padding on two opposite sides of a cell together.</summary>
    internal float Inset { get; }

    /// <summary>Gets the default font size of the table.</summary>
    internal float Size { get; }

    /// <summary>Gets the line spacing multiplier of the flow.</summary>
    internal float LineSpacing { get; }

    /// <summary>Gets the default single-line pitch of the table.</summary>
    internal float Leading { get; }

    /// <summary>Gets the bottom page margin.</summary>
    internal float Margin { get; }

    /// <summary>Gets the top of the text area of a page.</summary>
    internal float PageTop { get; }

    /// <summary>Gets the default font for body rows.</summary>
    internal PdfCanvasFont BodyFont { get; }

    /// <summary>Gets the default font for header rows.</summary>
    internal PdfCanvasFont HeaderFont { get; }

    /// <summary>Gets all placed cells in row-major order.</summary>
    internal List<PlacedCell> Cells { get; }

    /// <summary>Gets the x coordinate of each column's left edge, followed by the right edge of the last column.</summary>
    internal float[] Lefts { get; }

    /// <summary>Gets the resolved height of each row in points.</summary>
    internal float[] RowHeights { get; }

    /// <summary>Gets or sets the <c>Table</c> structure element when tagging is enabled.</summary>
    internal PdfTagElement? TableElement { get; set; }

    /// <summary>Gets or sets the combined height of the header rows.</summary>
    internal float HeaderHeight { get; set; }

    /// <summary>Gets or sets a value indicating whether header rows repeat on continuation pages.</summary>
    internal bool RepeatHeaders { get; set; }

    /// <summary>Gets or sets the top coordinate of the first body row on the current page.</summary>
    internal float FreshTop { get; set; }

    /// <summary>Gets or sets a value indicating whether drawing is currently inside a layout artifact.</summary>
    internal bool InArtifact { get; set; }

    /// <summary>Validates a table's rows and cells.</summary>
    /// <param name="table">The table to validate.</param>
    internal static void ValidateTable(PdfFlowTable table)
    {
        for (var r = 0; r < table.Rows.Count; r++)
        {
            var row = table.Rows[r];
            ArgumentNullException.ThrowIfNull(row);
            ArgumentNullException.ThrowIfNull(row.Cells);
            for (var c = 0; c < row.Cells.Count; c++)
            {
                ValidateCell(row.Cells[c]);
            }
        }
    }

    /// <summary>Builds and resolves the grid for a table, returning <see langword="null"/> when the table has no columns.</summary>
    /// <param name="table">The table.</param>
    /// <param name="options">The flow options.</param>
    /// <param name="textWidth">The page's available text width.</param>
    /// <returns>The resolved grid, or <see langword="null"/> when the table is empty.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The table font size is zero or negative.</exception>
    internal static RichTableGrid? Build(PdfFlowTable table, PdfFlowOptions options, float textWidth)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(table.FontSize ?? options.BodySize);
        ValidateTable(table);
        if (table.Rows.Count == 0)
        {
            return null;
        }

        var headerRows = Math.Clamp(table.HeaderRows, 0, table.Rows.Count);
        var cells = PlaceCells(table, headerRows, out var columns);
        if (columns == 0)
        {
            return null;
        }

        var slots = FillSlots(table.Rows.Count, columns, cells);
        var lefts = new float[columns + 1];
        lefts[0] = options.Margin;
        var rowHeights = new float[table.Rows.Count];
        var grid = new RichTableGrid(table, options, columns, slots, cells, lefts, rowHeights);
        var lines = default(PooledLines);
        try
        {
            grid.ResolveColumnWidths(textWidth, ref lines);
            grid.WrapAndMeasureRows(ref lines);
        }
        finally
        {
            lines.Dispose();
        }

        return grid;
    }

    /// <summary>Gets the cell occupying a grid slot, or <see langword="null"/> when the slot is empty.</summary>
    /// <param name="row">The zero-based row.</param>
    /// <param name="column">The zero-based column.</param>
    /// <returns>The placed cell, or <see langword="null"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal PlacedCell? GetCell(int row, int column) => _slots[(row * Columns) + column];

    /// <summary>Gets the default font for a row.</summary>
    /// <param name="header">Whether the row is a header row.</param>
    /// <returns>The font.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal PdfCanvasFont GetFont(bool header) => header ? HeaderFont : BodyFont;

    /// <summary>Gets the combined width of a range of columns.</summary>
    /// <param name="startCol">The first column.</param>
    /// <param name="colSpan">The number of columns.</param>
    /// <returns>The width in points.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal float GetSpanWidth(int startCol, int colSpan) => Lefts[startCol + colSpan] - Lefts[startCol];

    /// <summary>Gets the horizontal alignment for a placed cell.</summary>
    /// <param name="cell">The placed cell.</param>
    /// <returns>The alignment.</returns>
    internal PdfTableAlignment GetAlignment(PlacedCell cell)
    {
        if (cell.Cell.Alignment is { } explicitAlignment)
        {
            return explicitAlignment;
        }

        return cell.StartCol < Table.ColumnAlignments.Count
            ? Table.ColumnAlignments[cell.StartCol]
            : PdfTableAlignment.Left;
    }

    /// <summary>Validates a single table cell and its nested content.</summary>
    /// <param name="cell">The cell to validate.</param>
    /// <exception cref="ArgumentNullException"><paramref name="cell"/> or a required property is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A span or font size is not positive.</exception>
    private static void ValidateCell(PdfTableCell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        ArgumentNullException.ThrowIfNull(cell.Text);
        ArgumentOutOfRangeException.ThrowIfLessThan(cell.ColumnSpan, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(cell.RowSpan, 1);
        if (cell.FontSize is { } fontSize)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fontSize);
        }

        ValidateCellRuns(cell.Runs);
        if (cell.Image is { } image)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(image.Image.Width);
        }

        if (cell.CodeBlock is { } code)
        {
            PdfCodeFlow.Validate(code);
        }

        ValidateCellBlocks(cell.Blocks);
    }

    /// <summary>Validates the styled text runs of a cell.</summary>
    /// <param name="runs">The styled runs.</param>
    /// <exception cref="ArgumentNullException"><paramref name="runs"/> or a run is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A run font size is not positive.</exception>
    private static void ValidateCellRuns(IReadOnlyList<PdfTextRun> runs)
    {
        ArgumentNullException.ThrowIfNull(runs);
        for (var i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            ArgumentNullException.ThrowIfNull(run);
            ArgumentNullException.ThrowIfNull(run.Text);
            if (run.FontSize is { } runSize)
            {
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(runSize);
            }
        }
    }

    /// <summary>Validates the nested flow blocks of a cell.</summary>
    /// <param name="blocks">The flow blocks.</param>
    /// <exception cref="ArgumentNullException"><paramref name="blocks"/> or a block is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An image width is not positive.</exception>
    private static void ValidateCellBlocks(IReadOnlyList<PdfFlowBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            ArgumentNullException.ThrowIfNull(block);
            if (block is PdfFlowCodeBlock code)
            {
                PdfCodeFlow.Validate(code);
            }
            else if (block is PdfFlowImage image)
            {
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(image.Image.Width);
            }
            else if (block is PdfFlowParagraph paragraph)
            {
                ArgumentNullException.ThrowIfNull(paragraph.Text);
            }
            else if (block is PdfFlowHeading heading)
            {
                ArgumentNullException.ThrowIfNull(heading.Text);
            }
        }
    }

    /// <summary>Places all cells onto rows and columns, validating row and column spans.</summary>
    /// <param name="table">The table.</param>
    /// <param name="headerRows">The clamped header row count.</param>
    /// <param name="columns">Receives the validated column count.</param>
    /// <returns>The placed cells.</returns>
    /// <exception cref="ArgumentException">A cell's span exceeds the table bounds or overlaps another span.</exception>
    private static List<PlacedCell> PlaceCells(PdfFlowTable table, int headerRows, out int columns)
    {
        var rowCount = table.Rows.Count;
        var occupied = new List<ColInterval>[rowCount];
        for (var r = 0; r < rowCount; r++)
        {
            occupied[r] = [];
        }

        var rowWidths = new int[rowCount];
        var rowHasColSpan = new bool[rowCount];
        var cells = new List<PlacedCell>();
        for (var r = 0; r < rowCount; r++)
        {
            PlaceRow(table, r, headerRows, occupied, rowWidths, rowHasColSpan, cells);
        }

        columns = ResolveColumnCount(table, rowWidths, rowHasColSpan);
        for (var r = 0; r < rowCount; r++)
        {
            if (rowHasColSpan[r] && rowWidths[r] > columns)
            {
                throw new ArgumentException("A cell's ColumnSpan exceeds the table's column count.", nameof(table));
            }
        }

        return cells;
    }

    /// <summary>Places the cells of one row around active row spans from earlier rows.</summary>
    /// <param name="table">The table.</param>
    /// <param name="rowIndex">The current row index.</param>
    /// <param name="headerRows">The clamped header row count.</param>
    /// <param name="occupied">The occupied intervals per row.</param>
    /// <param name="rowWidths">Receives the total column width of each row.</param>
    /// <param name="rowHasColSpan">Receives whether each row has a column span greater than 1.</param>
    /// <param name="cells">Receives the placed cells.</param>
    /// <exception cref="ArgumentException">A cell's row span or column span is invalid.</exception>
    private static void PlaceRow(
        PdfFlowTable table,
        int rowIndex,
        int headerRows,
        List<ColInterval>[] occupied,
        int[] rowWidths,
        bool[] rowHasColSpan,
        List<PlacedCell> cells)
    {
        var rowCells = table.Rows[rowIndex].Cells;
        var col = 0;
        for (var i = 0; i < rowCells.Count; i++)
        {
            var cell = rowCells[i];
            col = SkipOccupied(occupied[rowIndex], col);
            var cs = cell.ColumnSpan;
            var rs = cell.RowSpan;
            if (rowIndex + rs > table.Rows.Count)
            {
                throw new ArgumentException("A cell's RowSpan extends past the last row of the table.", nameof(table));
            }

            if (rowIndex < headerRows && rowIndex + rs > headerRows)
            {
                throw new ArgumentException("A header cell's RowSpan cannot extend into body rows.", nameof(table));
            }

            if (OverlapsOccupied(occupied[rowIndex], col, col + cs))
            {
                throw new ArgumentException("A cell's ColumnSpan overlaps a column occupied by an active RowSpan.", nameof(table));
            }

            for (var rr = rowIndex; rr < rowIndex + rs; rr++)
            {
                occupied[rr].Add(new(col, col + cs));
            }

            rowHasColSpan[rowIndex] |= cs > 1;
            cells.Add(new(cell, rowIndex, col, rs, cs, rowIndex < headerRows));
            col += cs;
        }

        rowWidths[rowIndex] = SkipOccupied(occupied[rowIndex], col);
    }

    /// <summary>Advances a column index past any intervals already occupied in the row.</summary>
    /// <param name="intervals">The occupied column intervals.</param>
    /// <param name="col">The starting column.</param>
    /// <returns>The first unoccupied column at or after <paramref name="col"/>.</returns>
    private static int SkipOccupied(List<ColInterval> intervals, int col)
    {
        var moved = true;
        while (moved)
        {
            moved = false;
            for (var i = 0; i < intervals.Count; i++)
            {
                var interval = intervals[i];
                if (col < interval.Start || col >= interval.End)
                {
                    continue;
                }

                col = interval.End;
                moved = true;
            }
        }

        return col;
    }

    /// <summary>Determines whether a column range overlaps any occupied interval in a row.</summary>
    /// <param name="intervals">The occupied column intervals.</param>
    /// <param name="start">The inclusive start column.</param>
    /// <param name="end">The exclusive end column.</param>
    /// <returns><see langword="true"/> when an overlap exists.</returns>
    private static bool OverlapsOccupied(List<ColInterval> intervals, int start, int end)
    {
        for (var i = 0; i < intervals.Count; i++)
        {
            var interval = intervals[i];
            if (start < interval.End && interval.Start < end)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines the table's established column count from explicit widths and unspanned or complementary rows.</summary>
    /// <param name="table">The table.</param>
    /// <param name="rowWidths">The total column width of each row.</param>
    /// <param name="rowHasColSpan">Whether each row contains any cell with <c>ColumnSpan &gt; 1</c>.</param>
    /// <returns>The established column count.</returns>
    private static int ResolveColumnCount(PdfFlowTable table, int[] rowWidths, bool[] rowHasColSpan)
    {
        var established = table.ColumnWidths.Count;
        for (var r = 0; r < rowWidths.Length; r++)
        {
            if (!rowHasColSpan[r])
            {
                established = Math.Max(established, rowWidths[r]);
            }
        }

        if (established > 0)
        {
            return established;
        }

        var consistent = rowWidths.Length > 1 && rowWidths[0] > 0;
        var multiCellRow = false;
        for (var r = 0; r < rowWidths.Length; r++)
        {
            consistent &= rowWidths[r] == rowWidths[0];
            multiCellRow |= table.Rows[r].Cells.Count > 1;
        }

        if (consistent && multiCellRow)
        {
            return rowWidths[0];
        }

        var maxCells = 0;
        for (var r = 0; r < table.Rows.Count; r++)
        {
            maxCells = Math.Max(maxCells, table.Rows[r].Cells.Count);
        }

        return maxCells;
    }

    /// <summary>Populates the flat slot array from the list of placed cells.</summary>
    /// <param name="rows">The row count.</param>
    /// <param name="columns">The column count.</param>
    /// <param name="cells">The placed cells.</param>
    /// <returns>The flat slot array.</returns>
    private static PlacedCell?[] FillSlots(int rows, int columns, List<PlacedCell> cells)
    {
        var slots = new PlacedCell?[rows * columns];
        for (var i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            for (var r = cell.StartRow; r < cell.StartRow + cell.RowSpan; r++)
            {
                for (var c = cell.StartCol; c < cell.StartCol + cell.ColumnSpan; c++)
                {
                    slots[(r * columns) + c] = cell;
                }
            }
        }

        return slots;
    }

    /// <summary>Resolves the width of every column and populates <see cref="Lefts"/>.</summary>
    /// <param name="textWidth">The page's available text width.</param>
    /// <param name="lines">Scratch pooled lines.</param>
    private void ResolveColumnWidths(float textWidth, ref PooledLines lines)
    {
        var minimum = Inset + MinContent;
        var widths = new float[Columns];
        var natural = new float[Columns];
        var fixedTotal = 0F;
        var automatic = 0;
        for (var c = 0; c < Columns; c++)
        {
            var requested = c < Table.ColumnWidths.Count ? Table.ColumnWidths[c] : 0F;
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
            MeasureNaturalWidths(widths, natural, minimum, ref lines);
            PdfTableWidths.Share(widths, natural, Math.Max(textWidth - fixedTotal, automatic * minimum));
        }

        PdfTableWidths.FitToText(widths, textWidth, minimum);
        for (var c = 0; c < Columns; c++)
        {
            Lefts[c + 1] = Lefts[c] + widths[c];
        }
    }

    /// <summary>Measures the natural width of single-column and multi-column cells for unsized columns.</summary>
    /// <param name="widths">The fixed column widths (zero for automatic columns).</param>
    /// <param name="natural">Receives the natural width of each automatic column.</param>
    /// <param name="minimum">The minimum column width.</param>
    /// <param name="lines">Scratch pooled lines.</param>
    private void MeasureNaturalWidths(float[] widths, float[] natural, float minimum, ref PooledLines lines)
    {
        for (var i = 0; i < Cells.Count; i++)
        {
            var placed = Cells[i];
            if (placed.ColumnSpan != 1 || widths[placed.StartCol] > 0F)
            {
                continue;
            }

            var want = RichCellWrapper.MeasureNaturalWidth(placed.Cell, GetFont(placed.IsHeader), Size, Table.TextColor, LineSpacing, ref lines)
                + Inset + RoundingSlack;
            natural[placed.StartCol] = Math.Max(natural[placed.StartCol], want);
        }

        for (var c = 0; c < Columns; c++)
        {
            natural[c] = Math.Max(natural[c], minimum);
        }

        for (var i = 0; i < Cells.Count; i++)
        {
            var placed = Cells[i];
            if (placed.ColumnSpan > 1)
            {
                DistributeSpanNaturalWidth(placed, widths, natural, ref lines);
            }
        }
    }

    /// <summary>Distributes any excess natural width of a column-spanning cell across the unsized columns it covers.</summary>
    /// <param name="placed">The spanning cell.</param>
    /// <param name="widths">The fixed column widths.</param>
    /// <param name="natural">The natural widths of automatic columns.</param>
    /// <param name="lines">Scratch pooled lines.</param>
    private void DistributeSpanNaturalWidth(PlacedCell placed, float[] widths, float[] natural, ref PooledLines lines)
    {
        var want = RichCellWrapper.MeasureNaturalWidth(placed.Cell, GetFont(placed.IsHeader), Size, Table.TextColor, LineSpacing, ref lines)
            + Inset + RoundingSlack;
        var current = 0F;
        var unsized = 0;
        for (var c = placed.StartCol; c < placed.StartCol + placed.ColumnSpan; c++)
        {
            current += widths[c] > 0F ? widths[c] : natural[c];
            unsized += widths[c] > 0F ? 0 : 1;
        }

        if (want <= current || unsized == 0)
        {
            return;
        }

        var extra = (want - current) / unsized;
        for (var c = placed.StartCol; c < placed.StartCol + placed.ColumnSpan; c++)
        {
            if (widths[c] <= 0F)
            {
                natural[c] += extra;
            }
        }
    }

    /// <summary>Wraps the content of every placed cell and computes the height of every row.</summary>
    /// <param name="lines">Scratch pooled lines.</param>
    private void WrapAndMeasureRows(ref PooledLines lines)
    {
        var maxImageHeight = Math.Max(Leading, PageTop - Margin - Inset);
        for (var r = 0; r < Rows; r++)
        {
            RowHeights[r] = Inset + Leading;
        }

        for (var i = 0; i < Cells.Count; i++)
        {
            var placed = Cells[i];
            var cell = placed.Cell;
            var innerWidth = Math.Max(MinContent, GetSpanWidth(placed.StartCol, placed.ColumnSpan) - Inset);
            var style = new CellStyleContext(
                cell.Font ?? GetFont(placed.IsHeader),
                cell.FontSize ?? Size,
                cell.TextColor ?? Table.TextColor,
                LineSpacing,
                innerWidth,
                maxImageHeight);
            RichCellWrapper.WrapCell(placed, in style, ref lines);
            if (placed.RowSpan == 1)
            {
                RowHeights[placed.StartRow] = Math.Max(RowHeights[placed.StartRow], Inset + Math.Max(placed.ContentHeight, Leading));
            }
        }

        ApplyRowSpanHeights();
    }

    /// <summary>Distributes any excess content height of row-spanning cells to the last row of their span.</summary>
    private void ApplyRowSpanHeights()
    {
        for (var i = 0; i < Cells.Count; i++)
        {
            var placed = Cells[i];
            if (placed.RowSpan <= 1)
            {
                continue;
            }

            var needed = Inset + Math.Max(placed.ContentHeight, Leading);
            var current = 0F;
            for (var r = placed.StartRow; r < placed.StartRow + placed.RowSpan; r++)
            {
                current += RowHeights[r];
            }

            if (needed > current)
            {
                RowHeights[placed.StartRow + placed.RowSpan - 1] += needed - current;
            }
        }
    }

    /// <summary>A half-open column interval <c>[Start, End)</c>.</summary>
    /// <param name="Start">The inclusive start column.</param>
    /// <param name="End">The exclusive end column.</param>
    private readonly record struct ColInterval(int Start, int End);
}
