// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Charts;

/// <summary>What the painters of one chart share: the chart, its style and the text styles made from it.</summary>
/// <param name="Chart">The chart.</param>
/// <param name="Style">The style.</param>
/// <param name="Left">Left aligned label text.</param>
/// <param name="Center">Centred label text.</param>
/// <param name="Right">Right aligned label text.</param>
[DebuggerDisplay("ChartContext: {Chart}")]
internal sealed record ChartContext(PdfChart Chart, PdfChartStyle Style, PdfTextStyle Left, PdfTextStyle Center, PdfTextStyle Right)
{
    /// <summary>How tall a line of label text is, as a multiple of the font size.</summary>
    private const float LineScale = 1.4F;

    /// <summary>Gets the height of a line of label text.</summary>
    internal float LineHeight => Style.FontSize * LineScale;

    /// <summary>Makes the context for a chart.</summary>
    /// <param name="chart">The chart.</param>
    /// <param name="style">The style.</param>
    /// <returns>The context.</returns>
    internal static ChartContext Create(PdfChart chart, PdfChartStyle style)
    {
        var plain = new PdfTextStyle(style.Font, style.FontSize, style.TextColor);
        var format = PdfTextFormat.Default with { Vertical = PdfVerticalAlignment.Middle, Wrap = false };
        return new(
            chart,
            style,
            plain with { Format = format },
            plain with { Format = format with { Alignment = PdfTextAlignment.Center } },
            plain with { Format = format with { Alignment = PdfTextAlignment.Right } });
    }

    /// <summary>Gets the colour of a series or slice.</summary>
    /// <param name="index">The series or slice number.</param>
    /// <returns>The palette colour, repeating when there are more series than colours.</returns>
    internal PdfCanvasColor GetColor(int index)
    {
        var palette = Style.Palette.Span;
        return palette.IsEmpty ? PdfCanvasColor.Black : palette[index % palette.Length];
    }

    /// <summary>Measures a label.</summary>
    /// <param name="text">The label.</param>
    /// <returns>The width in points.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal float Measure(string text) => Style.Font.Measure(text, Style.FontSize);
}
