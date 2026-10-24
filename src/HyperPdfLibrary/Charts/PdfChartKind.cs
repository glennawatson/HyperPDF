// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Charts;

/// <summary>The kinds of chart <see cref="PdfChart"/> can be drawn as.</summary>
public enum PdfChartKind
{
    /// <summary>Vertical bars, side by side for each category.</summary>
    Column = 0,

    /// <summary>Horizontal bars, side by side for each category, with the first category at the top.</summary>
    Bar = 1,

    /// <summary>A line through each series' values, with a different dash and marker for each series.</summary>
    Line = 2,

    /// <summary>Series stacked on top of each other as filled areas. Values cannot be negative.</summary>
    Area = 3,

    /// <summary>One circle cut into slices by the first series. Values cannot be negative.</summary>
    Pie = 4,
}
