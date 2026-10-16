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
/// <remarks>
/// <para>
/// By default (<see cref="FillAvailableWidth"/> is <see langword="false"/> and no proportional columns are configured),
/// automatic columns shrink to fit their widest unwrapped line plus cell padding, only sharing or scaling down when the
/// table would exceed the page's available content width (<c>PageWidth - 2 * Margin</c>). Set
/// <see cref="FillAvailableWidth"/> to <see langword="true"/> or configure <see cref="ColumnSizing"/> with
/// <see cref="PdfTableColumnWidth.Proportional(float)"/> / <see cref="PdfTableColumnWidth.Relative(float)"/> weights so the
/// table expands across the full available content width.
/// </para>
/// <para>
/// Each row's height is at least its effective single-line pitch plus top and bottom <see cref="CellPadding"/>, taking into
/// account cell-level <see cref="PdfTableCell.FontSize"/> overrides and optional <see cref="PdfTableRow.Height"/> minimums.
/// </para>
/// </remarks>
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
    /// For mixed fixed, proportional and automatic column sizing, use <see cref="ColumnSizing"/>.
    /// </summary>
    public IReadOnlyList<float> ColumnWidths { get; init; } = [];

    /// <summary>
    /// Gets the sizing specification of each column (<see cref="PdfTableColumnWidth.Auto"/>,
    /// <see cref="PdfTableColumnWidth.Fixed(float)"/>, or <see cref="PdfTableColumnWidth.Proportional(float)"/>), from the
    /// first column. When an entry is present for a column index, it takes precedence over <see cref="ColumnWidths"/>.
    /// </summary>
    public IReadOnlyList<PdfTableColumnWidth> ColumnSizing { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether the table expands to fill the full available content width
    /// (<c>PageWidth - 2 * Margin</c>) even when its cells contain short text. When <see langword="false"/> (the default),
    /// automatic columns shrink to fit their content unless proportional columns are used or the content exceeds the page width.
    /// </summary>
    public bool FillAvailableWidth { get; init; }

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

    /// <summary>Gets the ordered fallback fonts for cells in this table, or <see langword="null"/> to inherit <see cref="PdfFlowOptions.FallbackFonts"/>.</summary>
    public IReadOnlyList<PdfCanvasFont>? FallbackFonts { get; init; }
}
