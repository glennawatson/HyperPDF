// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace HyperPdfLibrary.Charts;

/// <summary>
/// The data and kind of a chart. Draw it with <c>DrawChart</c> on a page canvas. In a tagged document, put the chart inside a
/// figure and use <see cref="Describe"/> for its alternative text, so that the numbers reach people who cannot see the chart.
/// </summary>
/// <param name="Kind">The kind of chart.</param>
/// <param name="Title">The title above the chart, or an empty string for none.</param>
/// <param name="Categories">The names of the categories along the axis, or the slices of a pie.</param>
/// <param name="Series">The series; each needs a value for every category. A pie uses only the first.</param>
[DebuggerDisplay("PdfChart: {Kind} {Title}")]
public sealed record PdfChart(PdfChartKind Kind, string Title, ReadOnlyMemory<string> Categories, ReadOnlyMemory<PdfChartSeries> Series)
{
    /// <summary>Writes the chart as plain text: its kind, title, categories and every value.</summary>
    /// <returns>The description, suitable as alternative text.</returns>
    public string Describe()
    {
        var text = new StringBuilder();
        _ = text.Append(Kind).Append(" chart");
        if (Title.Length > 0)
        {
            _ = text.Append(": ").Append(Title);
        }

        _ = text.Append(". ");
        var categories = Categories.Span;
        foreach (var series in Series.Span)
        {
            _ = text.Append(series.Name).Append(": ");
            var values = series.Values.Span;
            for (var i = 0; i < categories.Length && i < values.Length; i++)
            {
                var last = i + 1 >= categories.Length;
                _ = text.Append(categories[i]).Append(' ').Append(values[i].ToString("G6", CultureInfo.InvariantCulture)).Append(last ? ". " : ", ");
            }
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>Checks that the chart can be drawn.</summary>
    /// <exception cref="ArgumentException">
    /// There are no categories or series, a series has the wrong number of values, a value is not a number, or an area or pie
    /// chart has a negative value.
    /// </exception>
    internal void Validate()
    {
        if (Categories.IsEmpty || Series.IsEmpty)
        {
            throw new ArgumentException("A chart needs at least one category and one series.");
        }

        var needsPositive = Kind is PdfChartKind.Area or PdfChartKind.Pie;
        foreach (var series in Series.Span)
        {
            if (series.Values.Length != Categories.Length)
            {
                throw new ArgumentException($"The series '{series.Name}' needs {Categories.Length} values.");
            }

            ValidateValues(series, needsPositive);
        }
    }

    /// <summary>Checks the values of one series.</summary>
    /// <param name="series">The series.</param>
    /// <param name="needsPositive">Whether a negative value is refused.</param>
    /// <exception cref="ArgumentException">A value is not a number, or is negative when that is refused.</exception>
    private static void ValidateValues(PdfChartSeries series, bool needsPositive)
    {
        foreach (var value in series.Values.Span)
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentException($"The series '{series.Name}' has a value that is not a finite number.");
            }

            if (needsPositive && value < 0D)
            {
                throw new ArgumentException($"The series '{series.Name}' has a negative value, which this kind of chart cannot show.");
            }
        }
    }
}
