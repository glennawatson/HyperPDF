// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>
/// A table. The flow measures the cells, wraps their text to the columns, starts new pages when a row does not fit and
/// repeats the header rows on every page the table runs onto. A row that is taller than a page continues on the next page.
/// In a tagged document the table becomes <c>Table</c>, <c>TR</c>, <c>TH</c> and <c>TD</c>; the repeated headers and the
/// borders are artifacts.
/// </summary>
/// <param name="Rows">The rows from top to bottom; the first <see cref="HeaderRows"/> are header rows.</param>
[DebuggerDisplay("PdfFlowTable: {Rows.Count} rows, {HeaderRows} header rows")]
public sealed record PdfFlowTable(IReadOnlyList<PdfTableRow> Rows) : PdfFlowBlock
{
    /// <summary>The default cell padding in points.</summary>
    private const float DefaultPadding = 4F;

    /// <summary>The default border width in points.</summary>
    private const float DefaultBorderWidth = 0.5F;

    /// <summary>Gets the number of leading rows that are header rows: they are tagged <c>TH</c> and repeat on every page. The default is one.</summary>
    public int HeaderRows { get; init; } = 1;

    /// <summary>
    /// Gets the width of each column in points, from the first. A column with no entry, or an entry of zero or less, is sized to its
    /// text and shares what the explicit widths leave. When the columns are wider than the page's text they are scaled to fit.
    /// </summary>
    public IReadOnlyList<float> ColumnWidths { get; init; } = [];

    /// <summary>Gets how the text of each column lines up, from the first; a column with no entry is left aligned.</summary>
    public IReadOnlyList<PdfTableAlignment> ColumnAlignments { get; init; } = [];

    /// <summary>Gets the space between a cell's edge and its text, on every side, in points.</summary>
    public float CellPadding { get; init; } = DefaultPadding;

    /// <summary>Gets the stroke of the cell borders, or <see langword="null"/> for a table with no borders.</summary>
    public PdfStroke? Border { get; init; } = PdfStroke.Black(DefaultBorderWidth);

    /// <summary>Gets the default font for body cells, or <see langword="null"/> to use <see cref="PdfFlowOptions.BodyFont"/>.</summary>
    public PdfCanvasFont? BodyFont { get; init; }

    /// <summary>Gets the default font for header cells, or <see langword="null"/> to use <see cref="PdfFlowOptions.HeadingFont"/> (or the body font).</summary>
    public PdfCanvasFont? HeaderFont { get; init; }

    /// <summary>Gets the default font size in points for cells in this table, or <see langword="null"/> to use <see cref="PdfFlowOptions.BodySize"/>.</summary>
    public float? FontSize { get; init; }

    /// <summary>Gets the default text colour for cells in this table; the default is black.</summary>
    public PdfRgbColor TextColor { get; init; } = PdfRgbColor.Black;
}
