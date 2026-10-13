// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Layers;

/// <summary>
/// Turns zoom limits into zoom classes. A class is the number of distinct limits at or below a magnification, so a
/// group's state cannot change inside one class and a renderer needs a new picture only when the class changes.
/// </summary>
internal static class OptionalContentZoom
{
    /// <summary>The zoom class that means no magnification is known, so zoom limits do not apply.</summary>
    internal const int NoZoom = -1;

    /// <summary>Collects the distinct finite limits above zero, ascending.</summary>
    /// <param name="limits">The zoom range of each zoom-managed group.</param>
    /// <returns>The limits.</returns>
    internal static double[] CollectLimits(Dictionary<int, ZoomLimit> limits)
    {
        var values = new SortedSet<double>();
        foreach (var limit in limits.Values)
        {
            AddLimit(values, limit.Min);
            AddLimit(values, limit.Max);
        }

        var result = new double[values.Count];
        values.CopyTo(result);
        return result;
    }

    /// <summary>Gets the classes in which a range shows its group.</summary>
    /// <param name="limit">The group's range.</param>
    /// <param name="limits">The document's limits, ascending.</param>
    /// <returns>The classes.</returns>
    internal static ZoomClassRange ToClasses(ZoomLimit limit, double[] limits)
    {
        if (limit.Max <= 0 || limit.Min >= limit.Max)
        {
            return new(0, 0);
        }

        var first = limit.Min > 0 ? Array.BinarySearch(limits, limit.Min) + 1 : 0;
        var end = double.IsPositiveInfinity(limit.Max) ? int.MaxValue : Array.BinarySearch(limits, limit.Max) + 1;
        return new(first, end);
    }

    /// <summary>Gets the zoom class of a magnification.</summary>
    /// <param name="limits">The document's limits, ascending.</param>
    /// <param name="magnification">The magnification, where 1 is 100 percent.</param>
    /// <returns>The class; 0 when there are no limits.</returns>
    internal static int ClassOf(double[] limits, double magnification)
    {
        if (limits.Length == 0 || double.IsNaN(magnification))
        {
            return 0;
        }

        var found = Array.BinarySearch(limits, magnification);
        return found >= 0 ? found + 1 : ~found;
    }

    /// <summary>Adds a limit that can split the magnifications.</summary>
    /// <param name="values">The limits so far.</param>
    /// <param name="value">The candidate.</param>
    private static void AddLimit(SortedSet<double> values, double value)
    {
        if (value > 0 && double.IsFinite(value))
        {
            _ = values.Add(value);
        }
    }
}
