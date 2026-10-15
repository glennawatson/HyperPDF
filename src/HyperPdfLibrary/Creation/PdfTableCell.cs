// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>One cell of a table row. Line breaks in the text start new lines; the text wraps to the column's width.</summary>
/// <param name="Text">The cell's text.</param>
[DebuggerDisplay("PdfTableCell: {Text}")]
public sealed record PdfTableCell(string Text)
{
    /// <summary>Gets how the text lines up in this cell, or <see langword="null"/> to use the column's alignment.</summary>
    public PdfTableAlignment? Alignment { get; init; }
}
