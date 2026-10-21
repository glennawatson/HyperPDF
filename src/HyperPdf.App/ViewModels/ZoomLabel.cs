// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;

namespace HyperPdf.App.ViewModels;

/// <summary>Builds the zoom label from the rounded zoom percent.</summary>
internal static class ZoomLabel
{
    /// <summary>Percent multiplier.</summary>
    private const double PercentScale = 100;

    /// <summary>Rounds a zoom factor to a whole percent.</summary>
    /// <param name="zoom">The zoom factor, 1 being 100%.</param>
    /// <returns>The percent.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int RoundPercent(double zoom) => (int)Math.Round(zoom * PercentScale);

    /// <summary>Formats a zoom percent for the zoom label.</summary>
    /// <param name="percent">The rounded percent.</param>
    /// <returns>The text, such as <c>150%</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string Format(int percent) => string.Create(CultureInfo.CurrentCulture, $"{percent}%");
}
