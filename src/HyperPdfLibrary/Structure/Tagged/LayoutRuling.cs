// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>A straight horizontal or vertical line a page draws, such as a table border.</summary>
/// <param name="Bounds">The line as a box in viewer space; a horizontal line is as tall as its thickness.</param>
/// <param name="IsHorizontal">Whether the line runs across the page rather than down it.</param>
[DebuggerDisplay("LayoutRuling: {Bounds} horizontal={IsHorizontal}")]
internal readonly record struct LayoutRuling(PdfViewerRect Bounds, bool IsHorizontal);
