// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Charts;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>A chart on its own line. A tagged document describes it with the chart's own description unless the block has alternative text.</summary>
/// <param name="Chart">The chart.</param>
/// <param name="Height">The height in points.</param>
[DebuggerDisplay("PdfChartBlock: {Chart}, {Height} pt")]
public sealed record PdfChartBlock(PdfChart Chart, float Height) : PdfBlock
{
    /// <summary>Gets the width in points, or <see langword="null"/> for the width of the text area.</summary>
    public float? Width { get; init; }

    /// <summary>Gets the font, colours and options, or <see langword="null"/> to use the Normal style's typeface at 9 points.</summary>
    public PdfChartStyle? Style { get; init; }

    /// <summary>Gets the description for people who cannot see the chart, or <see langword="null"/> to use <see cref="PdfChart.Describe"/>.</summary>
    public string? AlternativeText { get; init; }

    /// <summary>Gets where the chart sits between the left and right edges when it is narrower than the text area.</summary>
    public PdfTextAlignment Alignment { get; init; }

    /// <summary>Gets the space above the chart in points.</summary>
    public float SpaceBefore { get; init; }

    /// <summary>Gets the space below the chart in points.</summary>
    public float SpaceAfter { get; init; }
}
