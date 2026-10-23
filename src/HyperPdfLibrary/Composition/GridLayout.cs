// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Composition;

/// <summary>The first rows of a grid laid out in fixed column widths: every cell's content, the height of every row and the groups of rows that stay together.</summary>
[DebuggerDisplay("GridLayout: {Cells.Count} cells, {RowHeights.Length} rows")]
internal sealed class GridLayout
{
    /// <summary>Initializes a new instance of the <see cref="GridLayout"/> class.</summary>
    /// <param name="width">The width of the table.</param>
    /// <param name="rowHeights">The height of each row.</param>
    /// <param name="cells">The cells.</param>
    /// <param name="groups">The groups of rows.</param>
    private GridLayout(float width, float[] rowHeights, List<GridCellLayout> cells, List<RowGroup> groups)
    {
        Width = width;
        RowHeights = rowHeights;
        Cells = cells;
        Groups = groups;
    }

    /// <summary>Gets the width of the table in points.</summary>
    internal float Width { get; }

    /// <summary>Gets the height of each row.</summary>
    internal float[] RowHeights { get; }

    /// <summary>Gets the cells.</summary>
    internal List<GridCellLayout> Cells { get; }

    /// <summary>Gets the groups of rows, top to bottom.</summary>
    internal List<RowGroup> Groups { get; }

    /// <summary>Lays out the first rows of a grid.</summary>
    /// <param name="env">The document and its styles.</param>
    /// <param name="grid">The grid.</param>
    /// <param name="widths">The width of each column.</param>
    /// <param name="rowCount">The number of rows to lay out.</param>
    /// <param name="tableNode">The structure node of the table, or <see langword="null"/> when the grid is not part of the structure.</param>
    /// <param name="artifact">The kind of artifact content without a structure node is drawn as.</param>
    /// <returns>The layout.</returns>
    internal static GridLayout Build(LayoutEnv env, PdfGrid grid, float[] widths, int rowCount, StructNode? tableNode, PdfArtifactKind artifact)
    {
        var starts = GetStarts(widths);
        var rowNodes = new StructNode?[rowCount];
        for (var r = 0; r < rowCount; r++)
        {
            rowNodes[r] = env.CreateNode("TR", tableNode);
        }

        var cells = new List<GridCellLayout>();
        foreach (var slot in GridPlacer.Place(grid, rowCount, widths.Length, env.Warnings))
        {
            cells.Add(LayoutCell(env, grid, slot, widths, starts[slot.Column], rowNodes[slot.Row], artifact));
        }

        var heights = ResolveHeights(grid, cells, rowCount);
        return new(starts[^1], heights, cells, Group(cells, heights));
    }

    /// <summary>Works out where each column starts, with the table's width last.</summary>
    /// <param name="widths">The width of each column.</param>
    /// <returns>The x of each column's left edge from the table's left edge, then the total width.</returns>
    private static float[] GetStarts(float[] widths)
    {
        var starts = new float[widths.Length + 1];
        for (var i = 0; i < widths.Length; i++)
        {
            starts[i + 1] = starts[i] + widths[i];
        }

        return starts;
    }

    /// <summary>Lays out one cell.</summary>
    /// <param name="env">The document and its styles.</param>
    /// <param name="grid">The grid.</param>
    /// <param name="slot">Where the cell sits.</param>
    /// <param name="widths">The width of each column.</param>
    /// <param name="x">The distance from the table's left edge to the cell's left edge.</param>
    /// <param name="rowNode">The structure node of the cell's row, or <see langword="null"/>.</param>
    /// <param name="artifact">The kind of artifact content without a structure node is drawn as.</param>
    /// <returns>The laid out cell.</returns>
    private static GridCellLayout LayoutCell(LayoutEnv env, PdfGrid grid, GridCellSlot slot, float[] widths, float x, StructNode? rowNode, PdfArtifactKind artifact)
    {
        var cell = slot.Cell;
        var width = 0F;
        for (var c = slot.Column; c < slot.Column + slot.ColumnSpan; c++)
        {
            width += widths[c];
        }

        var borders = cell.Borders ?? (grid.Lines is { } line ? PdfBorders.Box(line) : PdfBorders.None);
        var padding = cell.Padding ?? grid.CellPadding;
        var shading = cell.Shading ?? grid.Rows[slot.Row].Shading;
        var vertical = cell.VerticalAlignment ?? grid.VerticalAlignment;
        var isHeader = slot.Row < grid.HeaderRows;
        var options = PdfTagOptions.Default with
        {
            TableScope = isHeader ? PdfTableScope.Column : PdfTableScope.None,
            RowSpan = slot.RowSpan,
            ColumnSpan = slot.ColumnSpan,
        };
        var node = env.CreateNode(isHeader ? "TH" : "TD", rowNode, options);
        var empty = new GridCellLayout(slot, x, width, borders, padding, shading, vertical, new(0F, []));
        var inner = MathF.Max(1F, width - padding.Horizontal - empty.BorderInsets.Horizontal);
        var style = isHeader ? PdfStyleNames.TableHeader : PdfStyleNames.TableText;
        var content = StackLayout.Build(env, cell.Blocks, inner, artifact, new(0F, node, style), CancellationToken.None);
        return empty with { Content = content };
    }

    /// <summary>Works out the height of each row: the tallest cell that ends in it, with cells that span rows sharing their height out to the last row they cover.</summary>
    /// <param name="grid">The grid.</param>
    /// <param name="cells">The laid out cells.</param>
    /// <param name="rowCount">The number of rows.</param>
    /// <returns>The height of each row.</returns>
    private static float[] ResolveHeights(PdfGrid grid, List<GridCellLayout> cells, int rowCount)
    {
        var heights = new float[rowCount];
        for (var r = 0; r < rowCount; r++)
        {
            heights[r] = grid.Rows[r].MinHeight;
        }

        foreach (var cell in cells)
        {
            if (cell.Slot.RowSpan == 1)
            {
                heights[cell.Slot.Row] = MathF.Max(heights[cell.Slot.Row], cell.NeededHeight);
            }
        }

        var ordered = new List<GridCellLayout>(cells);
        ordered.Sort(static (a, b) => a.Slot.RowSpan.CompareTo(b.Slot.RowSpan));
        foreach (var cell in ordered)
        {
            GrowForSpan(heights, cell);
        }

        return heights;
    }

    /// <summary>Makes the last row a cell covers taller when the cell needs more than the rows it covers hold.</summary>
    /// <param name="heights">The height of each row.</param>
    /// <param name="cell">The cell.</param>
    private static void GrowForSpan(float[] heights, GridCellLayout cell)
    {
        if (cell.Slot.RowSpan == 1)
        {
            return;
        }

        var last = cell.Slot.Row + cell.Slot.RowSpan - 1;
        var held = 0F;
        for (var r = cell.Slot.Row; r <= last; r++)
        {
            held += heights[r];
        }

        heights[last] += MathF.Max(0F, cell.NeededHeight - held);
    }

    /// <summary>Finds where a group of rows that stay together ends: after the last row any cell that starts in the group spans to.</summary>
    /// <param name="cells">The laid out cells.</param>
    /// <param name="start">The first row of the group.</param>
    /// <returns>The place after the group's last row.</returns>
    private static int FindGroupEnd(List<GridCellLayout> cells, int start)
    {
        var end = start + 1;
        var previous = -1;
        while (end != previous)
        {
            previous = end;
            foreach (var cell in cells)
            {
                var inGroup = cell.Slot.Row >= start && cell.Slot.Row < end;
                end = inGroup ? Math.Max(end, cell.Slot.Row + cell.Slot.RowSpan) : end;
            }
        }

        return end;
    }

    /// <summary>Joins rows into groups that stay together: rows that a cell spans across.</summary>
    /// <param name="cells">The laid out cells.</param>
    /// <param name="heights">The height of each row.</param>
    /// <returns>The groups, top to bottom.</returns>
    private static List<RowGroup> Group(List<GridCellLayout> cells, float[] heights)
    {
        var groups = new List<RowGroup>();
        var start = 0;
        while (start < heights.Length)
        {
            var end = FindGroupEnd(cells, start);
            var height = 0F;
            for (var r = start; r < end; r++)
            {
                height += heights[r];
            }

            groups.Add(new(start, end, height));
            start = end;
        }

        return groups;
    }
}
