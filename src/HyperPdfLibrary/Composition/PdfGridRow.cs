// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>A row of a <see cref="PdfGrid"/>. A row stays whole on one page unless it is taller than a page.</summary>
/// <param name="Cells">The row's cells, left to right; cells covered by a cell spanning down from a row above are left out.</param>
[DebuggerDisplay("PdfGridRow: {Cells.Count} cells")]
public sealed record PdfGridRow(IReadOnlyList<PdfGridCell> Cells)
{
    /// <summary>Gets the least height of the row in points.</summary>
    public float MinHeight { get; init; }

    /// <summary>Gets the colour behind the cells of the row that have none of their own, or <see langword="null"/> for none.</summary>
    public PdfCanvasColor? Shading { get; init; }

    /// <summary>Makes a row of cells of plain text.</summary>
    /// <param name="cells">The text of each cell.</param>
    /// <returns>The row.</returns>
    public static PdfGridRow Of(params string[] cells)
    {
        var result = new PdfGridCell[cells.Length];
        for (var i = 0; i < cells.Length; i++)
        {
            result[i] = PdfGridCell.Of(cells[i]);
        }

        return new(result);
    }
}
