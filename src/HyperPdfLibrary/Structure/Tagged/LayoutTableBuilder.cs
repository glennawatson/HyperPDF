// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// Makes a <see cref="LayoutTable"/> from a grid of squares and the items placed in them. Squares that share an
/// anchor are one cell, which covers the rows and columns they span.
/// </summary>
internal static class LayoutTableBuilder
{
    /// <summary>The fewest rows a table with a header row has.</summary>
    private const int MinHeaderRows = 2;

    /// <summary>Makes a table.</summary>
    /// <param name="text">The items' text.</param>
    /// <param name="bounds">The box around the table.</param>
    /// <param name="rows">The number of rows in the grid.</param>
    /// <param name="columns">The number of columns in the grid.</param>
    /// <param name="anchors">For each square, row by row, the square that anchors the cell it belongs to; a square that is a cell on its own anchors itself.</param>
    /// <param name="placed">The items and the squares they sit in.</param>
    /// <returns>The table.</returns>
    internal static LayoutTable Create(string text, PdfViewerRect bounds, int rows, int columns, int[] anchors, ReadOnlySpan<PlacedItem> placed)
    {
        var squares = rows * columns;
        var rowEnd = ArrayPool<int>.Shared.Rent(squares);
        var columnEnd = ArrayPool<int>.Shared.Rent(squares);
        var counts = ArrayPool<int>.Shared.Rent(squares);
        try
        {
            rowEnd.AsSpan(0, squares).Clear();
            columnEnd.AsSpan(0, squares).Clear();
            counts.AsSpan(0, squares).Clear();
            Measure(anchors, columns, rowEnd, columnEnd);
            foreach (var item in placed)
            {
                counts[anchors[item.Square]]++;
            }

            var items = new LayoutItem[placed.Length];
            var cells = Cells(anchors, columns, rowEnd, columnEnd, counts);
            var filled = counts.AsSpan(0, cells.Length);
            filled.Clear();
            Fill(placed, anchors, cells, columns, items, filled);
            foreach (var cell in cells)
            {
                LayoutGeometry.SortReading(items.AsSpan(cell.ItemStart, cell.ItemCount));
            }

            return new(bounds, rows, columns, cells, items, HasHeader(text, rows, columns, cells, items));
        }
        finally
        {
            ArrayPool<int>.Shared.Return(rowEnd);
            ArrayPool<int>.Shared.Return(columnEnd);
            ArrayPool<int>.Shared.Return(counts);
        }
    }

    /// <summary>Finds the last row and column each cell reaches.</summary>
    /// <param name="anchors">The anchor of each square.</param>
    /// <param name="columns">The number of columns.</param>
    /// <param name="rowEnd">Receives, for each anchor, the last row it covers.</param>
    /// <param name="columnEnd">Receives, for each anchor, the last column it covers.</param>
    private static void Measure(int[] anchors, int columns, int[] rowEnd, int[] columnEnd)
    {
        for (var square = 0; square < anchors.Length; square++)
        {
            var anchor = anchors[square];
            rowEnd[anchor] = Math.Max(rowEnd[anchor], square / columns);
            columnEnd[anchor] = Math.Max(columnEnd[anchor], square % columns);
        }
    }

    /// <summary>Lists the cells in row order, with the place of each one's items.</summary>
    /// <param name="anchors">The anchor of each square.</param>
    /// <param name="columns">The number of columns.</param>
    /// <param name="rowEnd">The last row each anchor covers.</param>
    /// <param name="columnEnd">The last column each anchor covers.</param>
    /// <param name="counts">The items in each anchor's cell.</param>
    /// <returns>The cells.</returns>
    private static LayoutCell[] Cells(int[] anchors, int columns, int[] rowEnd, int[] columnEnd, int[] counts)
    {
        var cellCount = 0;
        for (var square = 0; square < anchors.Length; square++)
        {
            cellCount += anchors[square] == square ? 1 : 0;
        }

        var cells = new LayoutCell[cellCount];
        var next = 0;
        var itemStart = 0;
        for (var square = 0; square < anchors.Length; square++)
        {
            if (anchors[square] != square)
            {
                continue;
            }

            var row = square / columns;
            var column = square % columns;
            cells[next] = new(row, column, rowEnd[square] - row + 1, columnEnd[square] - column + 1, itemStart, counts[square]);
            itemStart += counts[square];
            next++;
        }

        return cells;
    }

    /// <summary>Puts each placed item in its cell's run.</summary>
    /// <param name="placed">The items and the squares they sit in.</param>
    /// <param name="anchors">The anchor of each square.</param>
    /// <param name="cells">The cells.</param>
    /// <param name="columns">The number of columns.</param>
    /// <param name="items">Receives the items.</param>
    /// <param name="filled">Cleared scratch tracking the items already placed in each cell.</param>
    private static void Fill(ReadOnlySpan<PlacedItem> placed, int[] anchors, LayoutCell[] cells, int columns, LayoutItem[] items, Span<int> filled)
    {
        foreach (var (item, square) in placed)
        {
            var anchor = anchors[square];
            var index = FindCell(cells, anchor / columns, anchor % columns);
            items[cells[index].ItemStart + filled[index]] = item;
            filled[index]++;
        }
    }

    /// <summary>Finds the cell at a row and column by binary search, since cells are in row order.</summary>
    /// <param name="cells">The cells.</param>
    /// <param name="row">The row.</param>
    /// <param name="column">The column.</param>
    /// <returns>The cell's position.</returns>
    private static int FindCell(LayoutCell[] cells, int row, int column)
    {
        var low = 0;
        var high = cells.Length - 1;
        while (low < high)
        {
            var middle = (low + high) / LayoutGeometry.Halves;
            var before = cells[middle].Row < row || (cells[middle].Row == row && cells[middle].Column < column);
            if (before)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    /// <summary>Decides whether the first row labels the columns: it has text, and not all of it is numbers.</summary>
    /// <param name="text">The items' text.</param>
    /// <param name="rows">The number of rows.</param>
    /// <param name="columns">The number of columns.</param>
    /// <param name="cells">The cells.</param>
    /// <param name="items">The items.</param>
    /// <returns><see langword="true"/> when the first row is a header row.</returns>
    private static bool HasHeader(string text, int rows, int columns, LayoutCell[] cells, LayoutItem[] items)
    {
        if (rows < MinHeaderRows || columns < 1)
        {
            return false;
        }

        var filled = 0;
        foreach (var cell in cells)
        {
            if (cell.Row != 0)
            {
                break;
            }

            if (cell.ItemCount == 0)
            {
                continue;
            }

            if (LayoutGeometry.IsNumeric(text, items.AsSpan(cell.ItemStart, cell.ItemCount)))
            {
                return false;
            }

            filled++;
        }

        return filled > 0;
    }
}
