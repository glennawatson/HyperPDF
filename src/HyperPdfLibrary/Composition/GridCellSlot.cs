// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>Where a cell of a grid sits: its first row and column and how far it spans.</summary>
/// <param name="Cell">The cell.</param>
/// <param name="Row">The first row, counted from 0.</param>
/// <param name="Column">The first column, counted from 0.</param>
/// <param name="RowSpan">The rows the cell covers, at least 1.</param>
/// <param name="ColumnSpan">The columns the cell covers, at least 1.</param>
[DebuggerDisplay("GridCellSlot: row {Row}, column {Column}, span {RowSpan} x {ColumnSpan}")]
internal readonly record struct GridCellSlot(PdfGridCell Cell, int Row, int Column, int RowSpan, int ColumnSpan);
