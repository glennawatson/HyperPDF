// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Composition;

/// <summary>Finds the row and column of every cell of a grid, leaving out the places that cells spanning down from above already cover.</summary>
internal static class GridPlacer
{
    /// <summary>Places the cells of the first rows of a grid.</summary>
    /// <param name="grid">The grid.</param>
    /// <param name="rowCount">The number of rows to place.</param>
    /// <param name="columnCount">The number of columns.</param>
    /// <param name="warnings">Receives notes about spans that were cut short.</param>
    /// <returns>The slots, in reading order.</returns>
    internal static List<GridCellSlot> Place(PdfGrid grid, int rowCount, int columnCount, List<string> warnings)
    {
        var covered = new bool[rowCount][];
        for (var r = 0; r < rowCount; r++)
        {
            covered[r] = new bool[columnCount];
        }

        var slots = new List<GridCellSlot>();
        for (var row = 0; row < rowCount; row++)
        {
            var column = 0;
            foreach (var cell in grid.Rows[row].Cells)
            {
                while (column < columnCount && covered[row][column])
                {
                    column++;
                }

                if (column >= columnCount)
                {
                    warnings.Add($"Row {row + 1} of a grid has more cells than the grid has columns; the extra cells were left out.");
                    break;
                }

                var slot = Clip(cell, row, column, rowCount, columnCount, warnings);
                Cover(covered, slot);
                slots.Add(slot);
                column += slot.ColumnSpan;
            }
        }

        return slots;
    }

    /// <summary>Cuts a cell's spans to the grid.</summary>
    /// <param name="cell">The cell.</param>
    /// <param name="row">The cell's row.</param>
    /// <param name="column">The cell's column.</param>
    /// <param name="rowCount">The number of rows.</param>
    /// <param name="columnCount">The number of columns.</param>
    /// <param name="warnings">Receives a note when a span is cut.</param>
    /// <returns>The slot.</returns>
    private static GridCellSlot Clip(PdfGridCell cell, int row, int column, int rowCount, int columnCount, List<string> warnings)
    {
        var rowSpan = Math.Clamp(cell.RowSpan, 1, rowCount - row);
        var columnSpan = Math.Clamp(cell.ColumnSpan, 1, columnCount - column);
        if (rowSpan != cell.RowSpan || columnSpan != cell.ColumnSpan)
        {
            warnings.Add($"A cell at row {row + 1}, column {column + 1} of a grid spans past the grid; its span was cut.");
        }

        return new(cell, row, column, rowSpan, columnSpan);
    }

    /// <summary>Marks the places a cell covers.</summary>
    /// <param name="covered">The covered places, by row then column.</param>
    /// <param name="slot">The cell.</param>
    private static void Cover(bool[][] covered, GridCellSlot slot)
    {
        for (var r = slot.Row; r < slot.Row + slot.RowSpan; r++)
        {
            for (var c = slot.Column; c < slot.Column + slot.ColumnSpan; c++)
            {
                covered[r][c] = true;
            }
        }
    }
}
