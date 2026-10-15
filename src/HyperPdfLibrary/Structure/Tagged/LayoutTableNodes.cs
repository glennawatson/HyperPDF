// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// Makes the nodes of an inferred table: Table, TR, and TH or TD for each cell (ISO 32000-2 14.8.4.3.2). The cells of a
/// header row are header cells for their columns, and every other cell lists the header cells above it.
/// </summary>
internal static class LayoutTableNodes
{
    /// <summary>Makes a table's nodes.</summary>
    /// <param name="nodes">Makes the nodes.</param>
    /// <param name="text">The items' text.</param>
    /// <param name="table">The table.</param>
    /// <param name="buffer">Room for a cell's text.</param>
    /// <returns>The table node.</returns>
    internal static PdfSemanticNode Make(LayoutNodes nodes, string text, LayoutTable table, char[] buffer)
    {
        var node = nodes.MakeGroup(PdfSemanticRole.Table);
        node.Bounds = table.Bounds;
        var headers = new List<PdfSemanticNode>();
        PdfSemanticNode? row = null;
        var rowIndex = -1;
        foreach (var cell in table.Cells)
        {
            if (cell.Row != rowIndex)
            {
                rowIndex = cell.Row;
                row = nodes.MakeGroup(PdfSemanticRole.TableRow);
                node.Add(row);
            }

            var isHeader = table.HasHeaderRow && cell.Row == 0;
            var cellNode = MakeCell(nodes, text, table, cell, isHeader, buffer);
            if (isHeader)
            {
                headers.Add(cellNode);
            }
            else
            {
                Label(cellNode, headers);
            }

            LayoutNodes.Adopt(row!, cellNode);
        }

        return node;
    }

    /// <summary>Makes a cell's node.</summary>
    /// <param name="nodes">Makes the nodes.</param>
    /// <param name="text">The items' text.</param>
    /// <param name="table">The table.</param>
    /// <param name="cell">The cell.</param>
    /// <param name="isHeader">Whether the cell is a header cell.</param>
    /// <param name="buffer">Room for the cell's text.</param>
    /// <returns>The cell node.</returns>
    private static PdfSemanticNode MakeCell(LayoutNodes nodes, string text, LayoutTable table, in LayoutCell cell, bool isHeader, char[] buffer)
    {
        var indices = new int[cell.ItemCount];
        var bounds = default(PdfViewerRect);
        for (var i = 0; i < cell.ItemCount; i++)
        {
            var item = table.Items[cell.ItemStart + i];
            indices[i] = item.Index;
            bounds = bounds.Union(item.Bounds);
        }

        var length = 0;
        LayoutText.AppendRows(text, table.Items, cell.ItemStart, cell.ItemCount, buffer, ref length);
        var node = nodes.Fill(isHeader ? PdfSemanticRole.TableHeaderCell : PdfSemanticRole.TableCell, new(indices, bounds, 0, length), buffer);
        node.Row = cell.Row;
        node.Column = cell.Column;
        node.RowSpan = cell.RowSpan;
        node.ColumnSpan = cell.ColumnSpan;
        node.Scope = isHeader ? PdfTableScope.Column : PdfTableScope.None;
        return node;
    }

    /// <summary>Lists the header cells above a cell as the ones that label it.</summary>
    /// <param name="cell">The cell.</param>
    /// <param name="headers">The header cells.</param>
    private static void Label(PdfSemanticNode cell, List<PdfSemanticNode> headers)
    {
        foreach (var header in headers)
        {
            var overlaps = header.Column < cell.Column + cell.ColumnSpan && cell.Column < header.Column + header.ColumnSpan;
            if (overlaps)
            {
                cell.AddHeader(header);
            }
        }
    }
}
