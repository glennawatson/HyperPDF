// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
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

    /// <summary>Builds new label text only when the rounded percent of the zoom is not the one already shown.</summary>
    /// <param name="zoom">The zoom factor, 1 being 100%.</param>
    /// <param name="shown">The percent the label shows; set to the new percent when it changed.</param>
    /// <param name="text">The new text when the percent changed.</param>
    /// <returns>Whether the percent changed.</returns>
    internal static bool TryChange(double zoom, ref int shown, [NotNullWhen(true)] out string? text)
    {
        var percent = RoundPercent(zoom);
        text = percent == shown ? null : Format(percent);
        shown = percent;
        return text is not null;
    }
}
