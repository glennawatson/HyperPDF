// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Charts;

/// <summary>One set of values in a chart, with a value for each category.</summary>
/// <param name="Name">The name shown in the legend.</param>
/// <param name="Values">The values, one for each category of the chart.</param>
[DebuggerDisplay("PdfChartSeries: {Name}, {Values.Length} values")]
public sealed record PdfChartSeries(string Name, ReadOnlyMemory<double> Values);
