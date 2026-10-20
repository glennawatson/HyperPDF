// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Layout;

/// <summary>
/// Which pages share a row in each <see cref="PageLayoutMode"/>. Rows are a single page, a two page spread, or the
/// cover alone followed by spreads. Navigation steps by row so a spread is never split.
/// </summary>
public static class PageRows
{
    /// <summary>The number of pages in a two page spread.</summary>
    private const int SpreadPages = 2;

    /// <summary>Gets the number of rows a document fills.</summary>
    /// <param name="pageCount">The page count.</param>
    /// <param name="mode">The arrangement.</param>
    /// <returns>The row count.</returns>
    public static int GetRowCount(int pageCount, PageLayoutMode mode) => mode switch
    {
        _ when pageCount <= 0 => 0,
        PageLayoutMode.Dual => (pageCount + 1) / SpreadPages,
        PageLayoutMode.DualCover => 1 + (pageCount / SpreadPages),
        _ => pageCount,
    };

    /// <summary>Gets the row holding a page.</summary>
    /// <param name="pageIndex">The zero based page.</param>
    /// <param name="mode">The arrangement.</param>
    /// <returns>The zero based row.</returns>
    public static int GetRow(int pageIndex, PageLayoutMode mode) => mode switch
    {
        PageLayoutMode.Dual => pageIndex / SpreadPages,
        PageLayoutMode.DualCover => (pageIndex + 1) / SpreadPages,
        _ => pageIndex,
    };

    /// <summary>Gets the pages in a row.</summary>
    /// <param name="row">The zero based row.</param>
    /// <param name="pageCount">The page count.</param>
    /// <param name="mode">The arrangement.</param>
    /// <param name="first">The first page in the row.</param>
    /// <param name="count">The number of pages in the row.</param>
    public static void GetRowPages(int row, int pageCount, PageLayoutMode mode, out int first, out int count)
    {
        if (mode == PageLayoutMode.Single)
        {
            first = row;
            count = 1;
            return;
        }

        first = mode == PageLayoutMode.Dual ? row * SpreadPages : Math.Max(0, (row * SpreadPages) - 1);
        count = IsCoverRow(row, mode) ? 1 : Math.Min(SpreadPages, pageCount - first);
    }

    /// <summary>Gets the first page of the row after the one holding a page, for the Next page command.</summary>
    /// <param name="pageIndex">The current page.</param>
    /// <param name="pageCount">The page count.</param>
    /// <param name="mode">The arrangement.</param>
    /// <returns>The page, or <paramref name="pageIndex"/> when it is already in the last row.</returns>
    public static int GetNextRowPage(int pageIndex, int pageCount, PageLayoutMode mode)
    {
        var next = GetFirstPage(GetRow(pageIndex, mode) + 1, mode);
        return next < pageCount ? next : pageIndex;
    }

    /// <summary>Gets the page the Next page command goes to. A vertical layout steps by row; a horizontal or wrapped one by page.</summary>
    /// <param name="pageIndex">The current page.</param>
    /// <param name="pageCount">The page count.</param>
    /// <param name="mode">The arrangement.</param>
    /// <param name="scroll">The scroll mode.</param>
    /// <returns>The page, or <paramref name="pageIndex"/> when it is the last.</returns>
    public static int GetNextPage(int pageIndex, int pageCount, PageLayoutMode mode, ScrollMode scroll) =>
        scroll == ScrollMode.Vertical ? GetNextRowPage(pageIndex, pageCount, mode) : Math.Min(pageIndex + 1, Math.Max(0, pageCount - 1));

    /// <summary>Gets the page the Previous page command goes to. A vertical layout steps by row; a horizontal or wrapped one by page.</summary>
    /// <param name="pageIndex">The current page.</param>
    /// <param name="mode">The arrangement.</param>
    /// <param name="scroll">The scroll mode.</param>
    /// <returns>The page, or the first page when already there.</returns>
    public static int GetPreviousPage(int pageIndex, PageLayoutMode mode, ScrollMode scroll) =>
        scroll == ScrollMode.Vertical ? GetPreviousRowPage(pageIndex, mode) : Math.Max(0, pageIndex - 1);

    /// <summary>Gets the first page of the row before the one holding a page, for the Previous page command.</summary>
    /// <param name="pageIndex">The current page.</param>
    /// <param name="mode">The arrangement.</param>
    /// <returns>The page, or the first page of the first row when already there.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetPreviousRowPage(int pageIndex, PageLayoutMode mode) => GetFirstPage(Math.Max(0, GetRow(pageIndex, mode) - 1), mode);

    /// <summary>Determines whether a row holds only the cover page.</summary>
    /// <param name="row">The zero based row.</param>
    /// <param name="mode">The arrangement.</param>
    /// <returns><see langword="true"/> for the cover row.</returns>
    public static bool IsCoverRow(int row, PageLayoutMode mode) => mode == PageLayoutMode.DualCover && row == 0;

    /// <summary>Gets the first page of a row.</summary>
    /// <param name="row">The zero based row.</param>
    /// <param name="mode">The arrangement.</param>
    /// <returns>The zero based page.</returns>
    private static int GetFirstPage(int row, PageLayoutMode mode) => mode switch
    {
        PageLayoutMode.Dual => row * SpreadPages,
        PageLayoutMode.DualCover => Math.Max(0, (row * SpreadPages) - 1),
        _ => row,
    };
}
