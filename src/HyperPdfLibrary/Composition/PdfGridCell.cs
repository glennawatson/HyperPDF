// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>A cell of a <see cref="PdfGrid"/>: blocks of content, which can span columns and rows, with their own borders, padding, shading and alignment.</summary>
/// <param name="Blocks">The cell's content.</param>
[DebuggerDisplay("PdfGridCell: {Blocks.Count} blocks, span {ColumnSpan} x {RowSpan}")]
public sealed record PdfGridCell(IReadOnlyList<PdfBlock> Blocks)
{
    /// <summary>Gets the number of columns the cell spans.</summary>
    public int ColumnSpan { get; init; } = 1;

    /// <summary>Gets the number of rows the cell spans.</summary>
    public int RowSpan { get; init; } = 1;

    /// <summary>Gets the cell's borders, or <see langword="null"/> to use the grid's lines.</summary>
    public PdfBorders? Borders { get; init; }

    /// <summary>Gets the space between the cell's border and its content, or <see langword="null"/> to use the grid's padding.</summary>
    public PdfInsets? Padding { get; init; }

    /// <summary>Gets the colour behind the cell, or <see langword="null"/> to use the row's shading, or none.</summary>
    public PdfCanvasColor? Shading { get; init; }

    /// <summary>Gets how the content sits between the top and bottom of the cell, or <see langword="null"/> to use the grid's.</summary>
    public PdfVerticalAlignment? VerticalAlignment { get; init; }

    /// <summary>Makes a cell of one paragraph of plain text.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The cell.</returns>
    public static PdfGridCell Of(string text) => new([PdfParagraph.Of(text)]);
}
