// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// Places a table's cells on a grid, honouring row and column spans, and links each cell to the header cells that
/// label it. A cell's <c>/Headers</c> ids win; otherwise header cells label the cells below them (column scope) or to
/// their right (row scope). A header cell with no <c>/Scope</c> labels its column when it is in the table head or the
/// first row, its row when it is in the first column, and its column otherwise, as Acrobat infers it.
/// </summary>
internal static class TableHeaderResolver
{
    /// <summary>The most cells inferred for; larger tables keep only their explicit headers.</summary>
    private const int MaxInferredCells = 4096;

    /// <summary>The most grid slots a table may fill, guarding against huge spans.</summary>
    private const int MaxSlots = 1 << 18;

    /// <summary>The bits a row index is shifted by in a grid key.</summary>
    private const int RowShift = 32;

    /// <summary>Places a table's cells and links them to their headers.</summary>
    /// <param name="table">The table node.</param>
    internal static void Resolve(PdfSemanticNode table)
    {
        var rows = new List<PdfSemanticNode>();
        var headRows = new HashSet<PdfSemanticNode>(ReferenceEqualityComparer.Instance);
        CollectRows(table, rows, headRows, false);
        var cells = new List<PdfSemanticNode>();
        Place(rows, cells);
        Dictionary<string, PdfSemanticNode>? ids = null;
        foreach (var cell in cells)
        {
            InferScope(cell, cell.Parent is { } row && headRows.Contains(row));
            if (cell.Element?.ElementId is not { } id)
            {
                continue;
            }

            ids ??= [with(StringComparer.Ordinal)];
            _ = ids.TryAdd(id, cell);
        }

        foreach (var cell in cells)
        {
            LinkHeaders(cell, cells, ids);
        }
    }

    /// <summary>Collects a table's rows, looking inside its head, body and foot groups.</summary>
    /// <param name="node">The table or row group.</param>
    /// <param name="rows">Receives the rows in order.</param>
    /// <param name="headRows">Receives the rows in the table head.</param>
    /// <param name="inHead">Whether the node is the table head.</param>
    private static void CollectRows(PdfSemanticNode node, List<PdfSemanticNode> rows, HashSet<PdfSemanticNode> headRows, bool inHead)
    {
        foreach (var child in node.Children)
        {
            if (child.Role == PdfSemanticRole.TableRowGroup)
            {
                CollectRows(child, rows, headRows, child.Element?.Type == PdfStructureType.TableHead);
                continue;
            }

            if (child.Role != PdfSemanticRole.TableRow)
            {
                continue;
            }

            rows.Add(child);
            if (inHead)
            {
                _ = headRows.Add(child);
            }
        }
    }

    /// <summary>Gives each cell its row, column and spans, skipping grid slots that earlier cells span into.</summary>
    /// <param name="rows">The rows.</param>
    /// <param name="cells">Receives the cells in order.</param>
    private static void Place(List<PdfSemanticNode> rows, List<PdfSemanticNode> cells)
    {
        HashSet<long>? taken = null;
        for (var row = 0; row < rows.Count; row++)
        {
            var column = 0;
            foreach (var cell in rows[row].Children)
            {
                if (cell.Role is not (PdfSemanticRole.TableCell or PdfSemanticRole.TableHeaderCell))
                {
                    continue;
                }

                while (column < int.MaxValue && taken is not null && taken.Contains(Key(row, column)))
                {
                    column++;
                }

                PlaceCell(cell, row, column, rows.Count, ref taken);
                cells.Add(cell);
                column = (int)Math.Min((long)column + cell.ColumnSpan, int.MaxValue);
            }
        }
    }

    /// <summary>Places one cell and marks the slots it spans.</summary>
    /// <param name="cell">The cell.</param>
    /// <param name="row">Its row.</param>
    /// <param name="column">Its column.</param>
    /// <param name="rowCount">The number of rows actually present.</param>
    /// <param name="taken">The slots already filled.</param>
    private static void PlaceCell(PdfSemanticNode cell, int row, int column, int rowCount, ref HashSet<long>? taken)
    {
        var attributes = cell.Element?.Attributes ?? PdfStructureAttributes.None;
        cell.Row = row;
        cell.Column = column;
        cell.RowSpan = attributes.RowSpan;
        cell.ColumnSpan = attributes.ColumnSpan;
        var rowsToMark = Math.Min(cell.RowSpan, rowCount - row);
        if (rowsToMark <= 1)
        {
            return;
        }

        taken ??= [];
        var columnsToMark = Math.Min((long)cell.ColumnSpan, (long)int.MaxValue - column + 1);

        // Cells advance along their own row; only later, existing rows need occupancy markers.
        for (var r = 1; r < rowsToMark && taken.Count < MaxSlots; r++)
        {
            for (var c = 0; c < columnsToMark && taken.Count < MaxSlots; c++)
            {
                _ = taken.Add(Key(row + r, column + c));
            }
        }
    }

    /// <summary>Makes a grid slot's key.</summary>
    /// <param name="row">The row.</param>
    /// <param name="column">The column.</param>
    /// <returns>The key.</returns>
    private static long Key(int row, int column) => ((long)row << RowShift) | (uint)column;

    /// <summary>Gives a header cell its scope: the one it states, or one inferred from its place.</summary>
    /// <param name="cell">The cell.</param>
    /// <param name="inHead">Whether its row is in the table head.</param>
    private static void InferScope(PdfSemanticNode cell, bool inHead)
    {
        if (cell.Role != PdfSemanticRole.TableHeaderCell)
        {
            return;
        }

        var stated = cell.Element?.Attributes.Scope ?? PdfTableScope.None;
        if (stated != PdfTableScope.None)
        {
            cell.Scope = stated;
            return;
        }

        cell.Scope = !inHead && cell.Row > 0 && cell.Column == 0 ? PdfTableScope.Row : PdfTableScope.Column;
    }

    /// <summary>Links a cell to its header cells.</summary>
    /// <param name="cell">The cell.</param>
    /// <param name="cells">Every cell of the table.</param>
    /// <param name="ids">The cells by element id.</param>
    private static void LinkHeaders(PdfSemanticNode cell, List<PdfSemanticNode> cells, Dictionary<string, PdfSemanticNode>? ids)
    {
        var stated = cell.Element?.Attributes.Headers ?? [];
        if (stated.Length > 0)
        {
            LinkDeclaredHeaders(cell, stated, ids);
            return;
        }

        if (cells.Count > MaxInferredCells)
        {
            return;
        }

        foreach (var header in cells)
        {
            if (Labels(header, cell))
            {
                cell.AddHeader(header);
            }
        }
    }

    /// <summary>Links only the header IDs that the cell explicitly declares.</summary>
    /// <param name="cell">The cell.</param>
    /// <param name="stated">Its header IDs.</param>
    /// <param name="ids">The table's cells by ID, or null when none have IDs.</param>
    private static void LinkDeclaredHeaders(PdfSemanticNode cell, string[] stated, Dictionary<string, PdfSemanticNode>? ids)
    {
        if (ids is null)
        {
            return;
        }

        foreach (var id in stated)
        {
            if (ids.TryGetValue(id, out var header) && !ReferenceEquals(header, cell))
            {
                cell.AddHeader(header);
            }
        }
    }

    /// <summary>Determines whether a header cell labels a cell by its scope: above it in its columns, or before it in its rows.</summary>
    /// <param name="header">The candidate header cell.</param>
    /// <param name="cell">The cell.</param>
    /// <returns><see langword="true"/> when it labels the cell.</returns>
    private static bool Labels(PdfSemanticNode header, PdfSemanticNode cell)
    {
        if (header.Role != PdfSemanticRole.TableHeaderCell || ReferenceEquals(header, cell))
        {
            return false;
        }

        return LabelsColumn(header, cell) || LabelsRow(header, cell);
    }

    /// <summary>Determines whether a column-scoped header cell is above a cell in its columns.</summary>
    /// <param name="header">The header cell.</param>
    /// <param name="cell">The cell.</param>
    /// <returns><see langword="true"/> when it labels the cell.</returns>
    private static bool LabelsColumn(PdfSemanticNode header, PdfSemanticNode cell) =>
        header.Scope is PdfTableScope.Column or PdfTableScope.Both
        && header.Row < cell.Row
        && header.Column < (long)cell.Column + cell.ColumnSpan
        && cell.Column < (long)header.Column + header.ColumnSpan;

    /// <summary>Determines whether a row-scoped header cell is before a cell in its rows.</summary>
    /// <param name="header">The header cell.</param>
    /// <param name="cell">The cell.</param>
    /// <returns><see langword="true"/> when it labels the cell.</returns>
    private static bool LabelsRow(PdfSemanticNode header, PdfSemanticNode cell) =>
        header.Scope is PdfTableScope.Row or PdfTableScope.Both
        && header.Column < cell.Column
        && header.Row < (long)cell.Row + cell.RowSpan
        && cell.Row < (long)header.Row + header.RowSpan;
}
