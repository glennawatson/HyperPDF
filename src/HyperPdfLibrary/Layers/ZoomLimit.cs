// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Layers;

/// <summary>A group's /Usage /Zoom range (ISO 32000-2, 8.11.4.4, Table 100): shown from <see cref="Min"/> up to, not including, <see cref="Max"/>.</summary>
/// <param name="Min">The magnification from which the group is shown.</param>
/// <param name="Max">The magnification below which the group is shown.</param>
[DebuggerDisplay("ZoomLimit: {Min}..{Max}")]
internal readonly record struct ZoomLimit(double Min, double Max)
{
    /// <summary>Gets the range that never limits a group.</summary>
    internal static ZoomLimit Unlimited => new(0, double.PositiveInfinity);

    /// <summary>Gets a value indicating whether the range limits the group.</summary>
    internal bool IsLimited => Min > 0 || !double.IsPositiveInfinity(Max);

    /// <summary>Narrows this range to what it shares with another, as a group listed by several usage application dictionaries needs.</summary>
    /// <param name="other">The other range.</param>
    /// <returns>The shared range.</returns>
    internal ZoomLimit Intersect(ZoomLimit other) => new(Math.Max(Min, other.Min), Math.Min(Max, other.Max));
}
