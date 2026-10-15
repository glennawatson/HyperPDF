// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// Finds sidebars among the regions a page was cut into: a narrow region beside a region at least twice as wide, over
/// the same part of the page. A sidebar is read after the text beside it, so a sidebar on the left no longer comes first.
/// </summary>
internal static class LayoutSidebars
{
    /// <summary>The most a sidebar may take of the width of the page's text.</summary>
    private const float MaxShare = 0.3F;

    /// <summary>How many times wider than the sidebar the text beside it is.</summary>
    private const float WiderBy = 2F;

    /// <summary>The share of the shorter region's height over which two regions must share the page for one to be beside the other.</summary>
    private const float SameBand = 0.5F;

    /// <summary>Marks the sidebars and moves any that come first to after the text beside them.</summary>
    /// <param name="regions">The regions in reading order; reordered and marked in place.</param>
    /// <param name="count">The number of regions.</param>
    /// <param name="lines">The lines the regions are runs of.</param>
    internal static void Order(LayoutRegion[] regions, int count, LayoutLine[] lines)
    {
        if (count < LayoutGeometry.Halves)
        {
            return;
        }

        var content = default(PdfViewerRect);
        for (var i = 0; i < count; i++)
        {
            content = content.Union(Bounds(regions[i], lines));
        }

        var limit = MaxShare * content.Width;
        for (var i = 0; i < count; i++)
        {
            var side = Bounds(regions[i], lines);
            if (side.Width > limit)
            {
                continue;
            }

            var last = LastBeside(regions, count, lines, i, side);
            if (last > i)
            {
                var moved = regions[i] with { IsSidebar = true };
                Array.Copy(regions, i + 1, regions, i, last - i);
                regions[last] = moved;
                i--;
            }
            else if (i > 0 && Beside(side, Bounds(regions[i - 1], lines)))
            {
                regions[i] = regions[i] with { IsSidebar = true };
            }
        }
    }

    /// <summary>Gets the box around the lines of a region.</summary>
    /// <param name="region">The region.</param>
    /// <param name="lines">The lines.</param>
    /// <returns>The box.</returns>
    private static PdfViewerRect Bounds(LayoutRegion region, LayoutLine[] lines)
    {
        var bounds = default(PdfViewerRect);
        for (var i = region.Start; i < region.Start + region.Count; i++)
        {
            bounds = bounds.Union(lines[i].Bounds);
        }

        return bounds;
    }

    /// <summary>Finds the last of the regions after a sidebar that sit beside it.</summary>
    /// <param name="regions">The regions.</param>
    /// <param name="count">The number of regions.</param>
    /// <param name="lines">The lines.</param>
    /// <param name="at">The sidebar's position.</param>
    /// <param name="side">The sidebar's box.</param>
    /// <returns>The position of the last such region; the sidebar's own position when there is none.</returns>
    private static int LastBeside(LayoutRegion[] regions, int count, LayoutLine[] lines, int at, PdfViewerRect side)
    {
        var last = at;
        for (var j = at + 1; j < count && Beside(side, Bounds(regions[j], lines)); j++)
        {
            last = j;
        }

        return last;
    }

    /// <summary>Determines whether a narrow box is beside a box at least twice as wide, over the same part of the page.</summary>
    /// <param name="side">The narrow box.</param>
    /// <param name="other">The other box.</param>
    /// <returns><see langword="true"/> when the first is a sidebar of the second.</returns>
    private static bool Beside(PdfViewerRect side, PdfViewerRect other)
    {
        var shared = LayoutGeometry.Overlap(side.Top, side.Bottom, other.Top, other.Bottom);
        return other.Width >= WiderBy * side.Width && shared >= SameBand * Math.Min(side.Height, other.Height);
    }
}
