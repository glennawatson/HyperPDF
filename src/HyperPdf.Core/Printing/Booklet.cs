// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Core.Printing;

/// <summary>
/// Orders pages for a saddle-stitched booklet: each sheet holds four pages, two on each side, so that the printed
/// stack, folded in half, reads in order. The page count is rounded up to a multiple of four with blank pages at the
/// end.
/// </summary>
public static class Booklet
{
    /// <summary>Pages on each side of a sheet.</summary>
    private const int PagesPerSide = 2;

    /// <summary>Pages on one sheet: two on each side.</summary>
    private const int PagesPerSheet = 4;

    /// <summary>Gets the number of sheets a booklet of some pages needs.</summary>
    /// <param name="pageCount">The pages.</param>
    /// <returns>The sheets.</returns>
    public static int SheetCount(int pageCount) => (Math.Max(0, pageCount) + PagesPerSheet - 1) / PagesPerSheet;

    /// <summary>Lists the pages in printing order: for each sheet, the front's left and right, then the back's left and right.</summary>
    /// <param name="pageCount">The pages.</param>
    /// <param name="output">Receives the page positions in the original order, or -1 for a blank page; cleared first.</param>
    public static void Arrange(int pageCount, List<int> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        output.Clear();
        CollectionsMarshal.SetCount(output, SheetCount(pageCount) * PagesPerSheet);
        Arrange(pageCount, CollectionsMarshal.AsSpan(output));
    }

    /// <summary>Lists the pages in printing order into a caller-supplied span.</summary>
    /// <param name="pageCount">The pages.</param>
    /// <param name="output">Receives the page positions in the original order, or -1 for a blank page; it must hold <see cref="SheetCount"/> times four positions.</param>
    public static void Arrange(int pageCount, Span<int> output)
    {
        var total = SheetCount(pageCount) * PagesPerSheet;
        ArgumentOutOfRangeException.ThrowIfLessThan(output.Length, total);
        var at = 0;
        for (var sheet = 0; sheet < total / PagesPerSheet; sheet++)
        {
            var outer = PagesPerSide * sheet;
            output[at] = Page(total - 1 - outer, pageCount);
            output[at + 1] = Page(outer, pageCount);
            output[at + PagesPerSide] = Page(outer + 1, pageCount);
            output[at + PagesPerSheet - 1] = Page(total - PagesPerSide - outer, pageCount);
            at += PagesPerSheet;
        }
    }

    /// <summary>Gets a position, or -1 when it falls among the blank pages added at the end.</summary>
    /// <param name="position">The position.</param>
    /// <param name="pageCount">The real pages.</param>
    /// <returns>The position or -1.</returns>
    private static int Page(int position, int pageCount) => position < pageCount ? position : -1;
}
