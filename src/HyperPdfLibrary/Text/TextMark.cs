// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Text;

/// <summary>A small filled or stroked shape the page draws, kept to find underlines, strikeouts and highlights near text.</summary>
/// <param name="Rect">The shape's bounds in user space; a stroked line is as thick as its line width.</param>
/// <param name="Color">The colour as 0xRRGGBB.</param>
[DebuggerDisplay("TextMark: {Rect} {Color:X6}")]
internal readonly record struct TextMark(PdfRectangle Rect, uint Color);
