// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>A row of a table. A row with fewer cells than the table has columns leaves the rest empty.</summary>
/// <param name="Cells">The cells, from the first column.</param>
[DebuggerDisplay("PdfTableRow: {Cells.Count} cells")]
public sealed record PdfTableRow(IReadOnlyList<PdfTableCell> Cells)
{
    /// <summary>Makes a row from the text of each cell.</summary>
    /// <param name="cells">The text of each cell, from the first column.</param>
    /// <returns>The row.</returns>
    public static PdfTableRow FromText(params ReadOnlySpan<string> cells)
    {
        var made = new PdfTableCell[cells.Length];
        for (var i = 0; i < made.Length; i++)
        {
            made[i] = new(cells[i]);
        }

        return new(made);
    }
}
