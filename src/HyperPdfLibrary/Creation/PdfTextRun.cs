// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>A styled run of text inside a table cell. Unset properties inherit the cell's or table's defaults.</summary>
/// <param name="Text">The text of the run.</param>
[DebuggerDisplay("PdfTextRun: {Text}")]
public sealed record PdfTextRun(string Text)
{
    /// <summary>Gets the font of this run, or <see langword="null"/> to inherit the cell's font.</summary>
    public PdfCanvasFont? Font { get; init; }

    /// <summary>Gets the point size of this run, or <see langword="null"/> to inherit the cell's font size.</summary>
    public float? FontSize { get; init; }

    /// <summary>Gets the RGB colour of this run, or <see langword="null"/> to inherit the cell's text colour.</summary>
    public PdfRgbColor? Color { get; init; }
}
