// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Layout;

/// <summary>
/// The positions of every page in a continuous layout. Lookups are binary searches over row offsets, so locating the
/// visible pages costs O(log n) regardless of document length.
/// </summary>
[DebuggerDisplay("DocumentLayout: {PageCount} pages, {ExtentWidth} x {ExtentHeight}")]
public sealed record DocumentLayout
{
    /// <summary>The number of pages in a two page spread.</summary>
    private const int SpreadPages = 2;

    /// <summary>Divisor used to centre content.</summary>
    private const double Halves = 2.0;

    /// <summary>The sentinel returned as the last page when nothing is visible, so loops from first to last never run.</summary>
    private const int NoLastPage = -2;

    /// <summary>Page bounds by page index.</summary>
    private readonly LayoutRect[] _pages;

    /// <summary>The top of each row.</summary>
    private readonly double[] _rowTops;

    /// <summary>The bottom of each row.</summary>
    private readonly double[] _rowBottoms;

    /// <summary>The first page of each row.</summary>
    private readonly int[] _rowFirstPage;

    /// <summary>Initializes a new instance of the <see cref="DocumentLayout"/> record.</summary>
    /// <param name="pages">The page bounds.</param>
    /// <param name="rowTops">The row tops.</param>
    /// <param name="rowBottoms">The row bottoms.</param>
    /// <param name="rowFirstPage">The first page of each row.</param>
    /// <param name="extentWidth">The total width.</param>
    /// <param name="extentHeight">The total height.</param>
    /// <param name="options">The options used.</param>
    private DocumentLayout(LayoutRect[] pages, double[] rowTops, double[] rowBottoms, int[] rowFirstPage, double extentWidth, double extentHeight, in LayoutOptions options)
    {
        _pages = pages;
        _rowTops = rowTops;
        _rowBottoms = rowBottoms;
        _rowFirstPage = rowFirstPage;
        ExtentWidth = extentWidth;
        ExtentHeight = extentHeight;
        Options = options;
    }

    /// <summary>Gets an empty layout.</summary>
    public static DocumentLayout Empty { get; } = new([], [], [], [], 0, 0, default);

    /// <summary>Gets the options the layout was computed with.</summary>
    public LayoutOptions Options { get; }

    /// <summary>Gets the total content width.</summary>
    public double ExtentWidth { get; }

    /// <summary>Gets the total content height.</summary>
    public double ExtentHeight { get; }

    /// <summary>Gets the number of pages.</summary>
    public int PageCount => _pages.Length;

    /// <summary>Gets the number of rows.</summary>
    public int RowCount => _rowTops.Length;

    /// <summary>Computes a layout.</summary>
    /// <param name="sizes">The unrotated page sizes.</param>
    /// <param name="options">The layout options.</param>
    /// <returns>The layout.</returns>
    public static DocumentLayout Create(ReadOnlySpan<PageSize> sizes, in LayoutOptions options)
    {
        if (sizes.IsEmpty)
        {
            return Empty;
        }

        var rowCount = PageRows.GetRowCount(sizes.Length, options.Mode);
        var pages = new LayoutRect[sizes.Length];
        var rowTops = new double[rowCount];
        var rowBottoms = new double[rowCount];
        var rowFirstPage = new int[rowCount];
        var rowWidths = new double[rowCount];

        var contentWidth = 0.0;
        for (var row = 0; row < rowCount; row++)
        {
            PageRows.GetRowPages(row, sizes.Length, options.Mode, out var first, out var count);
            rowFirstPage[row] = first;
            var width = GetSlotWidth(sizes, options, row, first, count);
            rowWidths[row] = width;
            contentWidth = Math.Max(contentWidth, width);
        }

        var extentWidth = Math.Max(contentWidth + (Halves * options.Margin), options.ViewportWidth);
        var y = options.Margin;
        for (var row = 0; row < rowCount; row++)
        {
            PageRows.GetRowPages(row, sizes.Length, options.Mode, out var first, out var count);
            var rowHeight = 0.0;
            for (var i = first; i < first + count; i++)
            {
                rowHeight = Math.Max(rowHeight, sizes[i].Rotate(options.Rotation).Height * options.Scale);
            }

            var x = (extentWidth - rowWidths[row]) / Halves;
            if (PageRows.IsCoverRow(row, options.Mode))
            {
                // The cover sits in the right hand slot of a two page spread.
                x += (rowWidths[row] + options.Spacing) / Halves;
            }

            for (var i = first; i < first + count; i++)
            {
                var rotated = sizes[i].Rotate(options.Rotation);
                var w = rotated.Width * options.Scale;
                var h = rotated.Height * options.Scale;
                pages[i] = new(x, y + ((rowHeight - h) / Halves), w, h);
                x += w + options.Spacing;
            }

            if (options.PageByPage)
            {
                // Slots are flush and at least a viewport tall, so nothing of a neighbouring row shows.
                var slot = Math.Max(rowHeight + (Halves * options.Margin), options.ViewportHeight);
                rowTops[row] = y - options.Margin;
                rowBottoms[row] = rowTops[row] + slot;
                ShiftRow(pages, first, count, rowTops[row] + ((slot - rowHeight) / Halves) - y);
                y = rowBottoms[row] + options.Margin;
                continue;
            }

            rowTops[row] = y;
            rowBottoms[row] = y + rowHeight;
            y += rowHeight + options.Spacing;
        }

        var extentHeight = options.PageByPage ? y - options.Margin : y - options.Spacing + options.Margin;
        return new(pages, rowTops, rowBottoms, rowFirstPage, extentWidth, extentHeight, options);
    }

    /// <summary>Gets the vertical extent of the row holding a page: its slot in page by page layouts.</summary>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="top">The top of the row.</param>
    /// <param name="bottom">The bottom of the row.</param>
    public void GetRowExtent(int pageIndex, out double top, out double bottom)
    {
        var row = UpperBound(_rowFirstPage, pageIndex) - 1;
        top = _rowTops[row];
        bottom = _rowBottoms[row];
    }

    /// <summary>Gets the bounds of a page.</summary>
    /// <param name="pageIndex">The page index.</param>
    /// <returns>The bounds.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LayoutRect GetPageBounds(int pageIndex) => _pages[pageIndex];

    /// <summary>Gets the pages intersecting a vertical band.</summary>
    /// <param name="top">The top of the band.</param>
    /// <param name="bottom">The bottom of the band.</param>
    /// <param name="first">The first visible page, or -1 when none.</param>
    /// <param name="last">The last visible page, or -2 when none.</param>
    public void GetVisiblePages(double top, double bottom, out int first, out int last)
    {
        first = -1;
        last = NoLastPage;
        if (_rowTops.Length == 0 || bottom < top)
        {
            return;
        }

        // First row whose bottom is below the band top; a row that only touches the band shows nothing.
        var firstRow = UpperBound(_rowBottoms, top);
        if (firstRow >= _rowTops.Length || _rowTops[firstRow] > bottom)
        {
            return;
        }

        // Last row whose top is above the band bottom.
        var lastRow = LowerBound(_rowTops, bottom) - 1;
        lastRow = Math.Clamp(lastRow, firstRow, _rowTops.Length - 1);

        first = _rowFirstPage[firstRow];
        last = lastRow + 1 < _rowFirstPage.Length ? _rowFirstPage[lastRow + 1] - 1 : _pages.Length - 1;
    }

    /// <summary>Gets the page at a point.</summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <returns>The page index, or -1.</returns>
    public int HitTest(double x, double y)
    {
        GetVisiblePages(y, y, out var first, out var last);
        for (var i = first; i <= last && i >= 0; i++)
        {
            if (_pages[i].Contains(x, y))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Gets the page nearest a vertical offset, used for the current page indicator.</summary>
    /// <param name="y">The offset.</param>
    /// <returns>The page index, or -1 when the layout is empty.</returns>
    public int GetPageNearest(double y)
    {
        if (_rowTops.Length == 0)
        {
            return -1;
        }

        var row = LowerBound(_rowBottoms, y);
        return _rowFirstPage[Math.Min(row, _rowFirstPage.Length - 1)];
    }

    /// <summary>
    /// Gets the current page for the page indicator: the page in the row nearest a vertical offset. A spread shows two
    /// pages, so <paramref name="preferredPage"/> is kept when it is one of them; otherwise the row's first page is used.
    /// </summary>
    /// <param name="y">The offset, usually the middle of the viewport.</param>
    /// <param name="preferredPage">The page last shown or asked for.</param>
    /// <returns>The page index, or -1 when the layout is empty.</returns>
    public int GetCurrentPage(double y, int preferredPage)
    {
        if (_rowTops.Length == 0)
        {
            return -1;
        }

        var row = Math.Min(LowerBound(_rowBottoms, y), _rowFirstPage.Length - 1);
        var first = _rowFirstPage[row];
        var end = row + 1 < _rowFirstPage.Length ? _rowFirstPage[row + 1] : _pages.Length;
        return preferredPage >= first && preferredPage < end ? preferredPage : first;
    }

    /// <summary>Gets the width a row occupies, including an empty slot beside the cover page.</summary>
    /// <param name="sizes">The page sizes.</param>
    /// <param name="options">The options.</param>
    /// <param name="row">The row.</param>
    /// <param name="first">The first page.</param>
    /// <param name="count">The page count.</param>
    /// <returns>The width.</returns>
    private static double GetSlotWidth(ReadOnlySpan<PageSize> sizes, in LayoutOptions options, int row, int first, int count)
    {
        var width = 0.0;
        for (var i = first; i < first + count; i++)
        {
            width += sizes[i].Rotate(options.Rotation).Width * options.Scale;
        }

        width += options.Spacing * (count - 1);
        if (PageRows.IsCoverRow(row, options.Mode))
        {
            width = (width * SpreadPages) + options.Spacing;
        }

        return width;
    }

    /// <summary>Moves a row of pages down.</summary>
    /// <param name="pages">The page bounds, updated.</param>
    /// <param name="first">The row's first page.</param>
    /// <param name="count">The row's page count.</param>
    /// <param name="shift">How far to move them.</param>
    private static void ShiftRow(LayoutRect[] pages, int first, int count, double shift)
    {
        for (var i = first; i < first + count; i++)
        {
            pages[i] = pages[i] with { Y = pages[i].Y + shift };
        }
    }

    /// <summary>Finds the first index whose value is greater than <paramref name="value"/>.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="values">Sorted values.</param>
    /// <param name="value">The value.</param>
    /// <returns>The index, or the length when no value is greater.</returns>
    private static int UpperBound<T>(T[] values, T value)
        where T : IComparisonOperators<T, T, bool>
    {
        var lo = 0;
        var hi = values.Length;
        while (lo < hi)
        {
            var mid = lo + ((hi - lo) >> 1);
            if (values[mid] <= value)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }

    /// <summary>Finds the first index whose value is not less than <paramref name="value"/>.</summary>
    /// <param name="values">Sorted values.</param>
    /// <param name="value">The value.</param>
    /// <returns>The index, or the length when every value is smaller.</returns>
    private static int LowerBound(double[] values, double value)
    {
        var lo = 0;
        var hi = values.Length;
        while (lo < hi)
        {
            var mid = lo + ((hi - lo) >> 1);
            if (values[mid] < value)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }
}
