// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>A table found on a page: a regular grid of cells, each with the items it holds.</summary>
/// <param name="Bounds">The box around the table.</param>
/// <param name="Rows">The number of rows.</param>
/// <param name="Columns">The number of columns.</param>
/// <param name="Cells">The cells in row order, left to right, each at its top left square.</param>
/// <param name="Items">The items of all cells; a cell's items are a run of them, in reading order.</param>
/// <param name="HasHeaderRow">Whether the first row labels the columns.</param>
[DebuggerDisplay("LayoutTable: {Rows}x{Columns}")]
internal sealed record LayoutTable(PdfViewerRect Bounds, int Rows, int Columns, LayoutCell[] Cells, LayoutItem[] Items, bool HasHeaderRow);
