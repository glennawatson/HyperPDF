// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>The width of one column of a <see cref="PdfGrid"/>.</summary>
/// <param name="Value">The width in points for a fixed column, or the weight for a star column.</param>
/// <param name="Kind">How <paramref name="Value"/> is read.</param>
[DebuggerDisplay("PdfGridColumn: {Value} {Kind}")]
public readonly record struct PdfGridColumn(float Value, PdfColumnWidthKind Kind)
{
    /// <summary>Makes a column of a fixed width.</summary>
    /// <param name="points">The width in points.</param>
    /// <returns>The column.</returns>
    public static PdfGridColumn Fixed(float points) => new(points, PdfColumnWidthKind.Fixed);

    /// <summary>Makes a column that takes a share of the free width.</summary>
    /// <param name="weight">The share, relative to the other star columns.</param>
    /// <returns>The column.</returns>
    public static PdfGridColumn Star(float weight) => new(weight, PdfColumnWidthKind.Star);
}
