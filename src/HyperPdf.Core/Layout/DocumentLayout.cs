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

        var rowFirstPage = LayoutRows.GetStarts(sizes, options);
        var rowCount = rowFirstPage.Length;
        var pages = new LayoutRect[sizes.Length];
        var rowTops = new double[rowCount];
        var rowBottoms = new double[rowCount];

        // The row widths wait in the bottoms array until the second pass has read each one and writes the real bottom.
        var rowWidths = rowBottoms;
        var contentWidth = 0.0;
        for (var row = 0; row < rowCount; row++)
        {
            var first = rowFirstPage[row];
            var count = GetRowLength(rowFirstPage, row, sizes.Length);
            var width = GetSlotWidth(sizes, options, row, first, count);
            rowWidths[row] = width;
            contentWidth = Math.Max(contentWidth, width);
        }

        var extentWidth = Math.Max(contentWidth + (Halves * options.Margin), options.ViewportWidth);
        var y = options.Margin;
        for (var row = 0; row < rowCount; row++)
        {
            var first = rowFirstPage[row];
            var count = GetRowLength(rowFirstPage, row, sizes.Length);
            var rowHeight = 0.0;
            for (var i = first; i < first + count; i++)
            {
                rowHeight = Math.Max(rowHeight, sizes[i].Rotate(options.Rotation).Height * options.Scale);
            }

            PlaceRow(sizes, options, pages, new(row, first, count), (extentWidth - rowWidths[row]) / Halves, rowWidths[row], new(y, rowHeight));

            if (options.PageByPage && options.Scroll == ScrollMode.Vertical)
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

        var extentHeight = options.PageByPage && options.Scroll == ScrollMode.Vertical ? y - options.Margin : y - options.Spacing + options.Margin;
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

    /// <summary>Gets the pages intersecting a rectangle. A horizontal layout looks at the sides of the rectangle; the others at its top and bottom.</summary>
    /// <param name="left">The left of the rectangle.</param>
    /// <param name="right">The right of the rectangle.</param>
    /// <param name="top">The top of the rectangle.</param>
    /// <param name="bottom">The bottom of the rectangle.</param>
    /// <param name="first">The first visible page, or -1 when none.</param>
    /// <param name="last">The last visible page, or -2 when none.</param>
    public void GetVisiblePages(double left, double right, double top, double bottom, out int first, out int last)
    {
        if (Options.Scroll != ScrollMode.Horizontal)
        {
            GetVisiblePages(top, bottom, out first, out last);
            return;
        }

        first = -1;
        last = NoLastPage;
        if (_pages.Length == 0 || right < left)
        {
            return;
        }

        var start = FirstPageWhere(!Options.RightToLeft, Options.RightToLeft ? right : left, true);
        var mirrored = Options.RightToLeft;
        var end = FirstPageWhere(mirrored, mirrored ? left : right, false) - 1;
        if (start >= _pages.Length || end < start)
        {
            return;
        }

        first = start;
        last = end;
    }

    /// <summary>Gets the page at a point.</summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <returns>The page index, or -1.</returns>
    public int HitTest(double x, double y)
    {
        GetVisiblePages(x, x, y, y, out var first, out var last);
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

    /// <summary>Gets the page nearest a point, which for a horizontal layout is the page nearest its horizontal position.</summary>
    /// <param name="x">The horizontal position.</param>
    /// <param name="y">The vertical position.</param>
    /// <returns>The page index, or -1 when the layout is empty.</returns>
    public int GetPageNearest(double x, double y)
    {
        if (Options.Scroll != ScrollMode.Horizontal)
        {
            return GetPageNearest(y);
        }

        return _pages.Length == 0 ? -1 : Math.Min(FirstPageWhere(!Options.RightToLeft, x, true), _pages.Length - 1);
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

    /// <summary>Gets the current page for the page indicator at a point: for a horizontal layout the page nearest the horizontal position, otherwise as for a vertical offset.</summary>
    /// <param name="x">The horizontal position, usually the middle of the viewport.</param>
    /// <param name="y">The vertical position, usually the middle of the viewport.</param>
    /// <param name="preferredPage">The page last shown or asked for.</param>
    /// <returns>The page index, or -1 when the layout is empty.</returns>
    public int GetCurrentPage(double x, double y, int preferredPage) =>
        Options.Scroll == ScrollMode.Horizontal ? GetPageNearest(x, y) : GetCurrentPage(y, preferredPage);

    /// <summary>Gets the number of pages in a row.</summary>
    /// <param name="rowFirstPage">The first page of each row.</param>
    /// <param name="row">The row.</param>
    /// <param name="pageCount">The pages in the document.</param>
    /// <returns>The pages in the row.</returns>
    private static int GetRowLength(int[] rowFirstPage, int row, int pageCount) =>
        (row + 1 < rowFirstPage.Length ? rowFirstPage[row + 1] : pageCount) - rowFirstPage[row];

    /// <summary>Places the pages of one row across it.</summary>
    /// <param name="sizes">The unrotated page sizes.</param>
    /// <param name="options">The layout options.</param>
    /// <param name="pages">Receives the page bounds.</param>
    /// <param name="row">Which row, and the pages in it.</param>
    /// <param name="left">Where the row's slot starts.</param>
    /// <param name="slotWidth">The width of the row's slot.</param>
    /// <param name="band">Where the row sits down the page.</param>
    private static void PlaceRow(ReadOnlySpan<PageSize> sizes, in LayoutOptions options, LayoutRect[] pages, RowSpan row, double left, double slotWidth, RowBand band)
    {
        var x = left;
        var cover = LayoutRows.IsCover(row.Index, options);
        if (cover && !options.RightToLeft)
        {
            // The cover sits in the right hand slot of a two page spread, or the left hand slot when the spread reads right to left.
            x += (slotWidth + options.Spacing) / Halves;
        }

        var mirrored = options.RightToLeft && !cover;
        if (mirrored)
        {
            x += slotWidth;
        }

        for (var i = row.First; i < row.First + row.Count; i++)
        {
            var rotated = sizes[i].Rotate(options.Rotation);
            var w = rotated.Width * options.Scale;
            var h = rotated.Height * options.Scale;
            x -= mirrored ? w : 0;
            pages[i] = new(x, band.Top + ((band.Height - h) / Halves), w, h);
            x += mirrored ? -options.Spacing : w + options.Spacing;
        }
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
        if (LayoutRows.IsCover(row, options))
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

    /// <summary>
    /// Finds the first page of a horizontal layout for which an edge test gives a wanted answer. The pages run one way across
    /// the row, so every page after that one gives the same answer.
    /// </summary>
    /// <param name="useRightEdge">Whether to test that a page's right edge is past the value; otherwise that its left edge is before it.</param>
    /// <param name="value">The value to test against.</param>
    /// <param name="wanted">The answer to look for.</param>
    /// <returns>The first page that gives the answer, or the page count when none does.</returns>
    private int FirstPageWhere(bool useRightEdge, double value, bool wanted)
    {
        var lo = 0;
        var hi = _pages.Length;
        while (lo < hi)
        {
            var mid = lo + ((hi - lo) >> 1);
            var page = _pages[mid];
            var answer = useRightEdge ? page.X + page.Width > value : page.X < value;
            if (answer == wanted)
            {
                hi = mid;
            }
            else
            {
                lo = mid + 1;
            }
        }

        return lo;
    }

    /// <summary>The pages of one row.</summary>
    /// <param name="Index">The row's index.</param>
    /// <param name="First">The row's first page.</param>
    /// <param name="Count">The pages in the row.</param>
    private readonly record struct RowSpan(int Index, int First, int Count);

    /// <summary>Where a row sits down the document.</summary>
    /// <param name="Top">The top of the row.</param>
    /// <param name="Height">The height of the tallest page in the row.</param>
    private readonly record struct RowBand(double Top, double Height);
}
