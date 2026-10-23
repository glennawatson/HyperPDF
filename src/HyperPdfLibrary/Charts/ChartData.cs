// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Charts;

/// <summary>Works out what a chart's data needs from its axes.</summary>
internal static class ChartData
{
    /// <summary>Makes a value axis that covers the data and zero.</summary>
    /// <param name="chart">The chart.</param>
    /// <returns>The axis.</returns>
    internal static ChartScale GetScale(PdfChart chart)
    {
        var lowest = 0D;
        var highest = 0D;
        var stacked = chart.Kind == PdfChartKind.Area;
        for (var category = 0; category < chart.Categories.Length; category++)
        {
            var total = 0D;
            foreach (var series in chart.Series.Span)
            {
                var value = series.Values.Span[category];
                total += value;
                lowest = Math.Min(lowest, stacked ? total : value);
                highest = Math.Max(highest, stacked ? total : value);
            }
        }

        return ChartScale.Create(lowest, highest);
    }

    /// <summary>Gets the names the legend lists: the series, or the slices of a pie.</summary>
    /// <param name="chart">The chart.</param>
    /// <returns>The names.</returns>
    internal static string[] GetLegendNames(PdfChart chart)
    {
        if (chart.Kind == PdfChartKind.Pie)
        {
            return chart.Categories.ToArray();
        }

        var names = new string[chart.Series.Length];
        for (var i = 0; i < names.Length; i++)
        {
            names[i] = chart.Series.Span[i].Name;
        }

        return names;
    }
}
