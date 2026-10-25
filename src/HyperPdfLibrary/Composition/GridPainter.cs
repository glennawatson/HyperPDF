// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>Draws a group of rows of a grid: cell shading, cell content and cell borders.</summary>
internal static class GridPainter
{
    /// <summary>Half, for centring.</summary>
    private const float Half = 0.5F;

    /// <summary>Draws the rows of a group.</summary>
    /// <param name="sink">Where the commands go.</param>
    /// <param name="layout">The laid out grid.</param>
    /// <param name="group">The rows to draw.</param>
    /// <param name="left">The x of the table's left edge.</param>
    /// <param name="top">The y of the top of the group.</param>
    /// <param name="artifact">The kind of artifact that shading and borders are drawn as.</param>
    internal static void Draw(List<DrawCommand> sink, GridLayout layout, RowGroup group, float left, float top, PdfArtifactKind artifact)
    {
        foreach (var cell in layout.Cells)
        {
            if (cell.Slot.Row >= group.Start && cell.Slot.Row < group.End)
            {
                DrawCell(sink, layout, group, cell, left, top, artifact);
            }
        }
    }

    /// <summary>Draws one cell.</summary>
    /// <param name="sink">Where the commands go.</param>
    /// <param name="layout">The laid out grid.</param>
    /// <param name="group">The group the cell is in.</param>
    /// <param name="cell">The cell.</param>
    /// <param name="left">The x of the table's left edge.</param>
    /// <param name="top">The y of the top of the group.</param>
    /// <param name="artifact">The kind of artifact that shading and borders are drawn as.</param>
    private static void DrawCell(List<DrawCommand> sink, GridLayout layout, RowGroup group, GridCellLayout cell, float left, float top, PdfArtifactKind artifact)
    {
        var cellTop = top - SumHeights(layout.RowHeights, group.Start, cell.Slot.Row);
        var height = SumHeights(layout.RowHeights, cell.Slot.Row, Math.Min(cell.Slot.Row + cell.Slot.RowSpan, group.End));
        var box = new PdfCanvasBox(left + cell.X, cellTop - height, cell.Width, height);
        if (cell.Shading is { } shading)
        {
            sink.Add(new FillCommand(null, PdfArtifactKind.Background, box, shading));
        }

        var borders = cell.BorderInsets;
        var free = MathF.Max(0F, height - cell.NeededHeight);
        var shift = cell.Vertical switch
        {
            PdfVerticalAlignment.Middle => free * Half,
            PdfVerticalAlignment.Bottom => free,
            _ => 0F,
        };
        sink.AddRange(cell.Content.PlaceAt(box.X + borders.Left + cell.Padding.Left, cellTop - borders.Top - cell.Padding.Top - shift));
        BorderDrawer.Draw(sink, cell.Borders, box, artifact);
    }

    /// <summary>Adds up the heights of some rows.</summary>
    /// <param name="heights">The height of each row.</param>
    /// <param name="from">The first row.</param>
    /// <param name="to">The place after the last row.</param>
    /// <returns>The height in points.</returns>
    private static float SumHeights(float[] heights, int from, int to)
    {
        var height = 0F;
        for (var r = from; r < to; r++)
        {
            height += heights[r];
        }

        return height;
    }
}
