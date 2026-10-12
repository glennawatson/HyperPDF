// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Forms.Detection;

/// <summary>A place to write found on a printed or scanned form.</summary>
/// <param name="Bounds">Where text goes, in page space (points, top-left origin): inside a box, or above a line.</param>
/// <param name="Kind">The kind.</param>
/// <param name="Cells">The number of character boxes of a comb, otherwise 0.</param>
[DebuggerDisplay("FormRegion: {Kind} at {Bounds}")]
public readonly record struct FormRegion(PageRect Bounds, FormRegionKind Kind, int Cells)
{
    /// <summary>How far below a line a point still counts as on it, in points.</summary>
    private const float BelowLine = 4;

    /// <summary>Finds the place to write under a point.</summary>
    /// <param name="regions">The places on the page.</param>
    /// <param name="point">The point, in page space.</param>
    /// <returns>The place, or <see langword="null"/>.</returns>
    public static FormRegion? At(ReadOnlySpan<FormRegion> regions, PagePoint point)
    {
        foreach (ref readonly var region in regions)
        {
            if (region.Contains(point))
            {
                return region;
            }
        }

        return null;
    }

    /// <summary>Gets whether a point is on this place: inside it, or just below a line or a row of boxes.</summary>
    /// <param name="point">The point, in page space.</param>
    /// <returns><see langword="true"/> when the point is on it.</returns>
    public bool Contains(PagePoint point)
    {
        var below = Kind == FormRegionKind.Box ? 0 : BelowLine;
        return point.X >= Bounds.Left && point.X <= Bounds.Right && point.Y >= Bounds.Top && point.Y <= Bounds.Bottom + below;
    }
}
