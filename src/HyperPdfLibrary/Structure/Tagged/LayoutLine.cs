// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// A line, or the piece of a line between two column gaps: a run of items in the builder's array. A table or an image
/// takes part in the reading order as a line of its own with no items, so the page is cut around it like text.
/// </summary>
/// <param name="Start">The position of the first item.</param>
/// <param name="Count">The number of items, left to right once the line is finished.</param>
/// <param name="Bounds">The box around the items.</param>
/// <param name="Kind">What the line stands for.</param>
/// <param name="Slot">For a table or an image, its place in the builder's list of them; otherwise -1.</param>
[DebuggerDisplay("LayoutLine: {Kind} {Count} items from {Start}")]
internal readonly record struct LayoutLine(int Start, int Count, PdfViewerRect Bounds, LayoutLineKind Kind, int Slot)
{
    /// <summary>Initializes a new instance of the <see cref="LayoutLine"/> struct that is a line of text.</summary>
    /// <param name="start">The position of the first item.</param>
    /// <param name="count">The number of items.</param>
    /// <param name="bounds">The box around the items.</param>
    internal LayoutLine(int start, int count, PdfViewerRect bounds)
        : this(start, count, bounds, LayoutLineKind.Text, -1)
    {
    }
}
