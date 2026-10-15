// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>A cell of an inferred table, at the top left of the grid squares it covers.</summary>
/// <param name="Row">The row, from zero.</param>
/// <param name="Column">The column, from zero.</param>
/// <param name="RowSpan">The rows it covers.</param>
/// <param name="ColumnSpan">The columns it covers.</param>
/// <param name="ItemStart">The position of its first item in the table's items.</param>
/// <param name="ItemCount">The number of items; zero for an empty cell.</param>
[DebuggerDisplay("LayoutCell: row {Row} column {Column} span {RowSpan}x{ColumnSpan}, {ItemCount} items")]
internal readonly record struct LayoutCell(int Row, int Column, int RowSpan, int ColumnSpan, int ItemStart, int ItemCount);
