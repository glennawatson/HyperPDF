// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Viewer.Chrome;

/// <summary>How the focus mode column is laid out, worked out from the settings and the room.</summary>
/// <param name="TextSize">The text size in device pixels.</param>
/// <param name="RowHeight">The height of a line of text in device pixels.</param>
/// <param name="GapHeight">The height of the space between paragraphs in device pixels.</param>
/// <param name="Width">The width of the column in device pixels.</param>
/// <param name="Left">The left edge of the column.</param>
/// <param name="Serif">Whether the text is set in the serif typeface.</param>
/// <param name="Tone">The colour of the page and of the text on it.</param>
[DebuggerDisplay("FocusLayout: {Width} px wide, text {TextSize} px")]
internal readonly record struct FocusLayout(int TextSize, int RowHeight, int GapHeight, int Width, int Left, bool Serif, FocusTone Tone);
