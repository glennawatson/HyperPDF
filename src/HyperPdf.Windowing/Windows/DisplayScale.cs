// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;

namespace HyperPdf.Windowing.Windows;

/// <summary>Works out how many device pixels one unit of the window's layout takes, from what the system reports and what the user asked for.</summary>
public static class DisplayScale
{
    /// <summary>The dots per inch of a display at a scale of one.</summary>
    private const double BaseDpi = 96;

    /// <summary>The smallest scale. Below it text is too small to read on any display.</summary>
    private const double Smallest = 0.5;

    /// <summary>The largest scale, enough for a magnified display.</summary>
    private const double Largest = 8;

    /// <summary>Gets the name of the environment variable that sets the scale by hand, such as 1.5, for a system that reports none or the wrong one.</summary>
    public static string OverrideVariable => "HYPERPDF_SCALE";

    /// <summary>Turns a display's dots per inch into a scale.</summary>
    /// <param name="dpi">The dots per inch the system reports, or 0 when it reports none.</param>
    /// <returns>The scale, with the user's own setting in place of the system's when there is one.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double FromDpi(double dpi) => Resolve(dpi > 0 ? dpi / BaseDpi : 1);

    /// <summary>Applies the user's own setting to a scale the system reports.</summary>
    /// <param name="detected">The scale the system reports.</param>
    /// <returns>The scale to use, kept within what can be read.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Resolve(double detected) =>
        Clamp(TryParse(Environment.GetEnvironmentVariable(OverrideVariable), out var asked) ? asked : detected);

    /// <summary>Reads a scale written as a number.</summary>
    /// <param name="text">The text, such as "1.5", or <see langword="null"/>.</param>
    /// <param name="scale">Receives the scale.</param>
    /// <returns><see langword="true"/> when the text is a positive number.</returns>
    public static bool TryParse(string? text, out double scale) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out scale) && double.IsFinite(scale) && scale > 0;

    /// <summary>Keeps a scale within what can be read.</summary>
    /// <param name="scale">The scale.</param>
    /// <returns>The scale, no less than a half and no more than eight.</returns>
    private static double Clamp(double scale) => double.IsFinite(scale) ? Math.Clamp(scale, Smallest, Largest) : 1;
}
