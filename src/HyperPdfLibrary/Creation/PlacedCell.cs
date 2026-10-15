// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>A cell placed at a specific row and column in a <see cref="RichTableGrid"/>.</summary>
[DebuggerDisplay("PlacedCell: ({StartRow},{StartCol}) span {ColumnSpan}x{RowSpan}")]
internal sealed class PlacedCell
{
    /// <summary>Initializes a new instance of the <see cref="PlacedCell"/> class.</summary>
    /// <param name="cell">The cell definition.</param>
    /// <param name="startRow">The starting row.</param>
    /// <param name="startCol">The starting column.</param>
    /// <param name="rowSpan">The row span.</param>
    /// <param name="columnSpan">The column span.</param>
    /// <param name="isHeader">Whether the cell is in a header row.</param>
    internal PlacedCell(PdfTableCell cell, int startRow, int startCol, int rowSpan, int columnSpan, bool isHeader)
    {
        Cell = cell;
        StartRow = startRow;
        StartCol = startCol;
        RowSpan = rowSpan;
        ColumnSpan = columnSpan;
        IsHeader = isHeader;
    }

    /// <summary>Gets the cell definition.</summary>
    internal PdfTableCell Cell { get; }

    /// <summary>Gets the zero-based starting row index.</summary>
    internal int StartRow { get; }

    /// <summary>Gets the zero-based starting column index.</summary>
    internal int StartCol { get; }

    /// <summary>Gets the number of rows the cell spans.</summary>
    internal int RowSpan { get; }

    /// <summary>Gets the number of columns the cell spans.</summary>
    internal int ColumnSpan { get; }

    /// <summary>Gets the zero-based last row index covered by the cell.</summary>
    internal int EndRow => StartRow + RowSpan - 1;

    /// <summary>Gets a value indicating whether the cell is in a header row.</summary>
    internal bool IsHeader { get; }

    /// <summary>Gets the wrapped visual items of the cell.</summary>
    internal List<CellVisualItem> Items { get; } = [];

    /// <summary>Gets or sets the structure element for this cell when tagging is enabled.</summary>
    internal PdfTagElement? TagElement { get; set; }

    /// <summary>Gets or sets the index of the next item to draw when paginating.</summary>
    internal int NextItemIndex { get; set; }

    /// <summary>Gets or sets the page number on which this cell was most recently drawn.</summary>
    internal int LastDrawnPage { get; set; } = -1;

    /// <summary>Gets the total vertical height of all items in the cell, without cell padding.</summary>
    internal float ContentHeight
    {
        get
        {
            var sum = 0F;
            for (var i = 0; i < Items.Count; i++)
            {
                sum += Items[i].Height;
            }

            return sum;
        }
    }

    /// <summary>Gets the vertical height of the items not yet drawn, without cell padding.</summary>
    internal float RemainingContentHeight
    {
        get
        {
            var sum = 0F;
            for (var i = NextItemIndex; i < Items.Count; i++)
            {
                sum += Items[i].Height;
            }

            return sum;
        }
    }
}
