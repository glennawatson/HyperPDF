// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Charts;

/// <summary>How a chart looks: its font, colours and which parts are shown.</summary>
/// <param name="Font">The font for the title, labels and legend.</param>
/// <param name="FontSize">The size of the labels and legend in points; the title is larger.</param>
[DebuggerDisplay("PdfChartStyle: {Font} {FontSize} pt")]
public sealed record PdfChartStyle(PdfCanvasFont Font, float FontSize)
{
    /// <summary>The grey level of the axis lines.</summary>
    private const float AxisGray = 0.35F;

    /// <summary>The grey level of the grid lines.</summary>
    private const float GridGray = 0.85F;

    /// <summary>Gets the colour of the text.</summary>
    public PdfCanvasColor TextColor { get; init; } = PdfCanvasColor.Black;

    /// <summary>Gets the colour of the axis lines.</summary>
    public PdfCanvasColor AxisColor { get; init; } = PdfCanvasColor.Gray(AxisGray);

    /// <summary>Gets the colour of the grid lines.</summary>
    public PdfCanvasColor GridColor { get; init; } = PdfCanvasColor.Gray(GridGray);

    /// <summary>Gets the colours of the series, used in turn and repeated when there are more series than colours.</summary>
    public ReadOnlyMemory<PdfCanvasColor> Palette { get; init; } = PdfChartPalette.Accessible;

    /// <summary>Gets the width of the lines in a line chart, in points.</summary>
    public float LineWidth { get; init; } = 2F;

    /// <summary>Gets a value indicating whether the legend is drawn.</summary>
    public bool ShowLegend { get; init; } = true;

    /// <summary>Gets a value indicating whether each value is written on its bar, line point or slice.</summary>
    public bool ShowValues { get; init; }
}
