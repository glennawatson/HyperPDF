// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>A line, or the piece of a line between two column gaps: a run of items in the builder's array.</summary>
/// <param name="Start">The position of the first item.</param>
/// <param name="Count">The number of items, left to right once the line is finished.</param>
/// <param name="Bounds">The box around the items.</param>
[DebuggerDisplay("LayoutLine: {Count} items from {Start}")]
internal readonly record struct LayoutLine(int Start, int Count, PdfViewerRect Bounds);
