// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>A cell of a grid laid out: where it is, what is round its content and the content itself.</summary>
/// <param name="Slot">Where the cell sits in the grid.</param>
/// <param name="X">The distance from the left edge of the table to the cell's left edge.</param>
/// <param name="Width">The cell's width, over all the columns it spans.</param>
/// <param name="Borders">The cell's borders.</param>
/// <param name="Padding">The space between the borders and the content.</param>
/// <param name="Shading">The colour behind the cell, or <see langword="null"/> for none.</param>
/// <param name="Vertical">How the content sits between the top and bottom of the cell.</param>
/// <param name="Content">The content laid out in the cell's inner width.</param>
[DebuggerDisplay("GridCellLayout: row {Slot.Row}, column {Slot.Column}")]
internal sealed record GridCellLayout(
    GridCellSlot Slot,
    float X,
    float Width,
    PdfBorders Borders,
    PdfInsets Padding,
    PdfCanvasColor? Shading,
    PdfVerticalAlignment Vertical,
    StackResult Content)
{
    /// <summary>Gets the room the borders take on each side.</summary>
    internal PdfInsets BorderInsets => new(Borders.Left?.Width ?? 0F, Borders.Top?.Width ?? 0F, Borders.Right?.Width ?? 0F, Borders.Bottom?.Width ?? 0F);

    /// <summary>Gets the height the cell needs: its content, padding and borders.</summary>
    internal float NeededHeight => Content.Height + Padding.Vertical + BorderInsets.Vertical;
}
