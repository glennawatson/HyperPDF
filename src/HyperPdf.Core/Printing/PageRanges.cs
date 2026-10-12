// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.InteropServices;

namespace HyperPdf.Core.Printing;

/// <summary>
/// Reads page ranges as people type them, such as <c>1-3, 7, 10-</c>: page numbers start at 1, a range may leave out its
/// end to mean "to the last page", and commas, semicolons or spaces separate the parts. Allocates nothing beyond the
/// output list's growth.
/// </summary>
public static class PageRanges
{
    /// <summary>Reads page ranges into zero based page indices, sorted, each page once.</summary>
    /// <param name="text">The ranges.</param>
    /// <param name="pageCount">The pages in the document.</param>
    /// <param name="pages">The list receiving the page indices; cleared first.</param>
    /// <returns><see langword="true"/> when every part named pages in the document and at least one page was chosen.</returns>
    public static bool TryParse(ReadOnlySpan<char> text, int pageCount, List<int> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        pages.Clear();
        foreach (var range in text.SplitAny(", ;"))
        {
            var part = text[range].Trim();
            if (part.IsEmpty)
            {
                continue;
            }

            if (!TryParsePart(part, pageCount, out var first, out var last))
            {
                pages.Clear();
                return false;
            }

            for (var page = first; page <= last; page++)
            {
                pages.Add(page - 1);
            }
        }

        if (pages.Count == 0)
        {
            return false;
        }

        var span = CollectionsMarshal.AsSpan(pages);
        span.Sort();
        var unique = 1;
        for (var i = 1; i < span.Length; i++)
        {
            if (span[i] == span[unique - 1])
            {
                continue;
            }

            span[unique] = span[i];
            unique++;
        }

        pages.RemoveRange(unique, pages.Count - unique);
        return true;
    }

    /// <summary>Reads one part: a page, or a range with an optional end.</summary>
    /// <param name="part">The part.</param>
    /// <param name="pageCount">The pages in the document.</param>
    /// <param name="first">The first page number.</param>
    /// <param name="last">The last page number.</param>
    /// <returns><see langword="true"/> when the part is valid.</returns>
    private static bool TryParsePart(ReadOnlySpan<char> part, int pageCount, out int first, out int last)
    {
        last = 0;
        var dash = part.IndexOf('-');
        if (dash < 0)
        {
            var valid = TryParsePage(part, pageCount, out first);
            last = first;
            return valid;
        }

        var end = part[(dash + 1)..].Trim();
        if (!TryParsePage(part[..dash].Trim(), pageCount, out first))
        {
            return false;
        }

        if (end.IsEmpty)
        {
            last = pageCount;
            return true;
        }

        return TryParsePage(end, pageCount, out last) && last >= first;
    }

    /// <summary>Reads a page number in the document.</summary>
    /// <param name="text">The number.</param>
    /// <param name="pageCount">The pages in the document.</param>
    /// <param name="page">The page number.</param>
    /// <returns><see langword="true"/> when it is a page of the document.</returns>
    private static bool TryParsePage(ReadOnlySpan<char> text, int pageCount, out int page) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out page) && page >= 1 && page <= pageCount;
}
