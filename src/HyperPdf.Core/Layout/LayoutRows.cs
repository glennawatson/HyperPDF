// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Layout;

/// <summary>Decides which pages share a row for each scroll mode.</summary>
internal static class LayoutRows
{
    /// <summary>The sides of the content that have a margin.</summary>
    private const int MarginSides = 2;

    /// <summary>Gets the first page of each row.</summary>
    /// <param name="sizes">The unrotated page sizes.</param>
    /// <param name="options">The layout options.</param>
    /// <returns>The first page of every row, in order.</returns>
    internal static int[] GetStarts(ReadOnlySpan<PageSize> sizes, in LayoutOptions options) => options.Scroll switch
    {
        ScrollMode.Horizontal => [0],
        ScrollMode.Wrapped => GetWrappedStarts(sizes, options),
        _ => GetVerticalStarts(sizes.Length, options.Mode),
    };

    /// <summary>Determines whether a row holds only the cover page of a book style layout.</summary>
    /// <param name="row">The row.</param>
    /// <param name="options">The layout options.</param>
    /// <returns><see langword="true"/> for the cover row.</returns>
    internal static bool IsCover(int row, in LayoutOptions options) => options.Scroll == ScrollMode.Vertical && PageRows.IsCoverRow(row, options.Mode);

    /// <summary>Gets the rows of a vertical layout: single pages, spreads, or a cover and then spreads.</summary>
    /// <param name="pageCount">The pages.</param>
    /// <param name="mode">The arrangement.</param>
    /// <returns>The first page of every row.</returns>
    private static int[] GetVerticalStarts(int pageCount, PageLayoutMode mode)
    {
        var starts = new int[PageRows.GetRowCount(pageCount, mode)];
        for (var row = 0; row < starts.Length; row++)
        {
            PageRows.GetRowPages(row, pageCount, mode, out var first, out _);
            starts[row] = first;
        }

        return starts;
    }

    /// <summary>Fills each row with as many pages as fit the viewport width; a page wider than the viewport has a row to itself.</summary>
    /// <param name="sizes">The unrotated page sizes.</param>
    /// <param name="options">The layout options.</param>
    /// <returns>The first page of every row.</returns>
    private static int[] GetWrappedStarts(ReadOnlySpan<PageSize> sizes, in LayoutOptions options)
    {
        var available = options.ViewportWidth - (MarginSides * options.Margin);
        var starts = new List<int>();
        var used = 0.0;
        for (var i = 0; i < sizes.Length; i++)
        {
            var width = sizes[i].Rotate(options.Rotation).Width * options.Scale;
            var needed = used > 0 ? used + options.Spacing + width : width;
            if (used > 0 && needed > available)
            {
                starts.Add(i);
                used = width;
                continue;
            }

            if (i == 0)
            {
                starts.Add(0);
            }

            used = needed;
        }

        return [.. starts];
    }
}
