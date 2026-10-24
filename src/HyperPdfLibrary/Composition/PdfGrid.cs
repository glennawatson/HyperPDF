// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>
/// A table: rows of cells in columns, with cells that span columns and rows, borders, padding, shading and vertical
/// alignment. Header rows repeat at the top of each page the table continues on, as artifacts, and a tagged document marks the
/// table up with header and data cells that name the rows and columns they span.
/// </summary>
/// <param name="Columns">The columns.</param>
/// <param name="Rows">The rows, top to bottom.</param>
[DebuggerDisplay("PdfGrid: {Columns.Count} columns, {Rows.Count} rows")]
public sealed record PdfGrid(IReadOnlyList<PdfGridColumn> Columns, IReadOnlyList<PdfGridRow> Rows) : PdfBlock
{
    /// <summary>The default horizontal padding of a cell in points.</summary>
    private const float DefaultPaddingX = 5F;

    /// <summary>The default vertical padding of a cell in points.</summary>
    private const float DefaultPaddingY = 3F;

    /// <summary>The default space above and below a table in points.</summary>
    private const float DefaultSpace = 6F;

    /// <summary>Gets the number of rows at the top that are headers, which repeat on every page the table continues on.</summary>
    public int HeaderRows { get; init; }

    /// <summary>Gets the line drawn on every edge of every cell that has no borders of its own, or <see langword="null"/> for none.</summary>
    public PdfBorderSide? Lines { get; init; }

    /// <summary>Gets the space between a cell's border and its content, for cells that name none.</summary>
    public PdfInsets CellPadding { get; init; } = PdfInsets.Symmetric(DefaultPaddingX, DefaultPaddingY);

    /// <summary>Gets how content sits between the top and bottom of a cell, for cells that name none.</summary>
    public PdfVerticalAlignment VerticalAlignment { get; init; }

    /// <summary>Gets the width of the table in points, or <see langword="null"/> for the width of the text area.</summary>
    public float? Width { get; init; }

    /// <summary>Gets where the table sits between the left and right edges when it is narrower than the text area.</summary>
    public PdfTextAlignment Alignment { get; init; }

    /// <summary>Gets the space above the table in points.</summary>
    public float SpaceBefore { get; init; } = DefaultSpace;

    /// <summary>Gets the space below the table in points.</summary>
    public float SpaceAfter { get; init; } = DefaultSpace;

    /// <summary>Gets a description of the table's purpose for people who cannot see it, used as its alternative text in a tagged document.</summary>
    public string? Summary { get; init; }
}
