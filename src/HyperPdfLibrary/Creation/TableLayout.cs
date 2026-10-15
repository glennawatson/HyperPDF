// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace HyperPdfLibrary.Creation;

/// <summary>
/// The state of one table being laid out: its resolved columns, the wrapped lines of the row being drawn and where the pages stand.
/// The per-column arrays are rented from the shared pool; dispose the layout to return them. The arrays may be longer than
/// <see cref="Columns"/>.
/// </summary>
[DebuggerDisplay("TableLayout: {Columns} columns, {HeaderRows} header rows")]
internal ref struct TableLayout
{
    /// <summary>The wrapped lines of the row being drawn.</summary>
    private PooledLines _lines;

    /// <summary>Initializes a new instance of the <see cref="TableLayout"/> struct.</summary>
    /// <param name="table">The table.</param>
    /// <param name="options">The flow's options.</param>
    /// <param name="columns">The number of columns.</param>
    /// <param name="left">The left edge of the table.</param>
    internal TableLayout(PdfFlowTable table, PdfFlowOptions options, int columns, float left)
    {
        var size = table.FontSize ?? options.BodySize;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        var bodyFont = table.BodyFont ?? options.BodyFont;
        var headerFont = table.HeaderFont ?? options.HeadingFont ?? bodyFont;
        Table = table;
        Columns = columns;
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
        Lefts = ArrayPool<float>.Shared.Rent(columns + 1);
        Lefts[0] = left;
        Starts = ArrayPool<int>.Shared.Rent(columns);
        Counts = ArrayPool<int>.Shared.Rent(columns);
        Cells = ArrayPool<PdfTagElement?>.Shared.Rent(columns);
        Array.Clear(Cells);
        _lines = default;
        TableElement = null;
        HeaderHeight = 0F;
        RepeatHeaders = false;
        FreshTop = PageTop;
        InArtifact = false;
    }

    /// <summary>Gets the table.</summary>
    internal readonly PdfFlowTable Table { get; }

    /// <summary>Gets the number of columns.</summary>
    internal readonly int Columns { get; }

    /// <summary>Gets the number of header rows.</summary>
    internal readonly int HeaderRows { get; }

    /// <summary>Gets the padding inside a cell.</summary>
    internal readonly float Padding { get; }

    /// <summary>Gets the padding on both sides of a cell together.</summary>
    internal readonly float Inset { get; }

    /// <summary>Gets the default font size of the table.</summary>
    internal readonly float Size { get; }

    /// <summary>Gets the line spacing multiplier of the flow.</summary>
    internal readonly float LineSpacing { get; }

    /// <summary>Gets the default line pitch of the table.</summary>
    internal readonly float Leading { get; }

    /// <summary>Gets the page margin, the lowest a row may reach.</summary>
    internal readonly float Margin { get; }

    /// <summary>Gets the top of the text area of a page.</summary>
    internal readonly float PageTop { get; }

    /// <summary>Gets the default font of body rows.</summary>
    internal readonly PdfCanvasFont BodyFont { get; }

    /// <summary>Gets the default font of header rows.</summary>
    internal readonly PdfCanvasFont HeaderFont { get; }

    /// <summary>Gets the x of each column's left edge, then the right edge of the last column.</summary>
    internal readonly float[] Lefts { get; }

    /// <summary>Gets the index in <see cref="Lines"/> of each cell's first line, for the row being drawn.</summary>
    internal readonly int[] Starts { get; }

    /// <summary>Gets the number of lines of each cell, for the row being drawn.</summary>
    internal readonly int[] Counts { get; }

    /// <summary>Gets the structure element of each cell of the row being drawn, or <see langword="null"/> when nothing is tagged.</summary>
    internal readonly PdfTagElement?[] Cells { get; }

    /// <summary>Gets the wrapped lines of the row being drawn.</summary>
    [UnscopedRef]
    internal ref PooledLines Lines => ref _lines;

    /// <summary>Gets or sets the <c>Table</c> element, or <see langword="null"/> when nothing is tagged.</summary>
    internal PdfTagElement? TableElement { get; set; }

    /// <summary>Gets or sets the height of all the header rows.</summary>
    internal float HeaderHeight { get; set; }

    /// <summary>Gets or sets a value indicating whether the header rows repeat at the top of each page the table continues onto.</summary>
    internal bool RepeatHeaders { get; set; }

    /// <summary>Gets or sets the top a row would start at on a page that has only the table's repeated headers on it.</summary>
    internal float FreshTop { get; set; }

    /// <summary>Gets or sets a value indicating whether an artifact is open around the content being drawn.</summary>
    internal bool InArtifact { get; set; }

    /// <summary>Gets the default font of a row.</summary>
    /// <param name="header">Whether the row is a header row.</param>
    /// <returns>The font.</returns>
    internal readonly PdfCanvasFont GetFont(bool header) => header ? HeaderFont : BodyFont;

    /// <summary>Gets the font of a specific cell, falling back to the row's default font.</summary>
    /// <param name="row">The row.</param>
    /// <param name="column">The column.</param>
    /// <param name="header">Whether the row is a header row.</param>
    /// <returns>The font.</returns>
    internal readonly PdfCanvasFont GetCellFont(PdfTableRow row, int column, bool header) =>
        (column < row.Cells.Count ? row.Cells[column].Font : null) ?? GetFont(header);

    /// <summary>Gets the text colour of a specific cell, falling back to the table's default text colour.</summary>
    /// <param name="row">The row.</param>
    /// <param name="column">The column.</param>
    /// <returns>The RGB colour.</returns>
    internal readonly PdfRgbColor GetCellColor(PdfTableRow row, int column) =>
        (column < row.Cells.Count ? row.Cells[column].TextColor : null) ?? Table.TextColor;

    /// <summary>Gets the width of a column.</summary>
    /// <param name="column">The column.</param>
    /// <returns>The width including padding.</returns>
    internal readonly float GetWidth(int column) => Lefts[column + 1] - Lefts[column];

    /// <summary>Gets the combined width of a range of columns.</summary>
    /// <param name="column">The start column.</param>
    /// <param name="span">The number of spanned columns.</param>
    /// <returns>The combined width including padding.</returns>
    internal readonly float GetSpanWidth(int column, int span) => Lefts[column + span] - Lefts[column];

    /// <summary>Gets how the text of a cell lines up.</summary>
    /// <param name="row">The row.</param>
    /// <param name="column">The column.</param>
    /// <returns>The alignment.</returns>
    internal readonly PdfTableAlignment GetAlignment(PdfTableRow row, int column)
    {
        if (column < row.Cells.Count && row.Cells[column].Alignment is { } own)
        {
            return own;
        }

        return column < Table.ColumnAlignments.Count ? Table.ColumnAlignments[column] : PdfTableAlignment.Left;
    }

    /// <summary>Gets how a specific cell's content lines up.</summary>
    /// <param name="cell">The cell, or <see langword="null"/>.</param>
    /// <param name="column">The starting column of the cell.</param>
    /// <returns>The alignment.</returns>
    internal readonly PdfTableAlignment GetCellAlignment(PdfTableCell? cell, int column)
    {
        if (cell?.Alignment is { } own)
        {
            return own;
        }

        return column < Table.ColumnAlignments.Count ? Table.ColumnAlignments[column] : PdfTableAlignment.Left;
    }

    /// <summary>Gets the height of a stretch of lines in a row, with the padding above and below.</summary>
    /// <param name="lines">The number of lines.</param>
    /// <returns>The height.</returns>
    internal readonly float GetHeight(int lines) => Inset + (lines * Leading);

    /// <summary>Returns the rented arrays to the pool.</summary>
    internal void Dispose()
    {
        _lines.Dispose();
        ArrayPool<float>.Shared.Return(Lefts);
        ArrayPool<int>.Shared.Return(Starts);
        ArrayPool<int>.Shared.Return(Counts);
        ArrayPool<PdfTagElement?>.Shared.Return(Cells, true);
    }
}
