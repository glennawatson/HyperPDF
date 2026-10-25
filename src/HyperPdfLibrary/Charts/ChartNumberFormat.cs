// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Charts;

/// <summary>Writes the numbers on a chart's axes and bars.</summary>
internal static class ChartNumberFormat
{
    /// <summary>The factor between decades.</summary>
    private const double Decade = 10D;

    /// <summary>The most decimal places shown.</summary>
    private const int MaximumDecimals = 6;

    /// <summary>Writes a value with just enough decimal places for ticks a step apart.</summary>
    /// <param name="value">The value.</param>
    /// <param name="step">The step between ticks, which decides how many decimals show.</param>
    /// <returns>The text.</returns>
    internal static string Format(double value, double step)
    {
        var decimals = (int)Math.Clamp(Math.Ceiling(-Math.Log10(step)), 0D, MaximumDecimals);
        return Math.Abs(value) < Math.Pow(Decade, -MaximumDecimals) ? "0" : value.ToString($"F{decimals}", CultureInfo.InvariantCulture);
    }

    /// <summary>Writes a value as it is, trimmed of needless zeros.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The text.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string Format(double value) => value.ToString("G6", CultureInfo.InvariantCulture);
}
