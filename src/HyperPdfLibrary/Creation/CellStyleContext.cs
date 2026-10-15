// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>Resolved style defaults and dimensions for wrapping a table cell's content.</summary>
/// <param name="Font">The resolved cell font.</param>
/// <param name="Size">The resolved cell font size.</param>
/// <param name="Color">The resolved cell text colour.</param>
/// <param name="LineSpacing">The flow line spacing multiplier.</param>
/// <param name="InnerWidth">The available width inside the cell's padding.</param>
/// <param name="MaxImageHeight">The tallest an image may be on a single page.</param>
[DebuggerDisplay("CellStyleContext: {Size} pt, width {InnerWidth}")]
internal readonly record struct CellStyleContext(
    PdfCanvasFont Font,
    float Size,
    PdfRgbColor Color,
    float LineSpacing,
    float InnerWidth,
    float MaxImageHeight);
