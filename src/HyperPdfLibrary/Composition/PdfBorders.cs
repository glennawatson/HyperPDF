// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>The border lines round a paragraph, table cell or frame. An edge that is <see langword="null"/> has no line.</summary>
/// <param name="Left">The left edge.</param>
/// <param name="Top">The top edge.</param>
/// <param name="Right">The right edge.</param>
/// <param name="Bottom">The bottom edge.</param>
[DebuggerDisplay("PdfBorders: left {Left}, top {Top}, right {Right}, bottom {Bottom}")]
public readonly record struct PdfBorders(PdfBorderSide? Left, PdfBorderSide? Top, PdfBorderSide? Right, PdfBorderSide? Bottom)
{
    /// <summary>Gets borders with no lines.</summary>
    public static PdfBorders None => default;

    /// <summary>Gets a value indicating whether no edge has a line.</summary>
    public bool IsEmpty => Left is null && Top is null && Right is null && Bottom is null;

    /// <summary>Makes borders with the same line on every edge.</summary>
    /// <param name="side">The line.</param>
    /// <returns>The borders.</returns>
    public static PdfBorders Box(PdfBorderSide side) => new(side, side, side, side);

    /// <summary>Makes borders with a line on the top and bottom edges only.</summary>
    /// <param name="side">The line.</param>
    /// <returns>The borders.</returns>
    public static PdfBorders TopAndBottom(PdfBorderSide side) => new(null, side, null, side);
}
