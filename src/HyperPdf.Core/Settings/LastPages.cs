// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Settings;

/// <summary>Remembers the page each recent document was closed at, newest last, so it reopens where the reader left off.</summary>
public static class LastPages
{
    /// <summary>The most documents remembered; older ones are forgotten first.</summary>
    private const int Limit = 200;

    /// <summary>Records the page a document showed, replacing any earlier record for it.</summary>
    /// <param name="pages">The remembered pages, newest last.</param>
    /// <param name="filePath">The document.</param>
    /// <param name="pageIndex">The zero-based page.</param>
    public static void Remember(List<LastViewedPage> pages, string filePath, int pageIndex)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        var existing = IndexOf(pages, filePath);
        var page = Math.Max(0, pageIndex);
        if (existing == pages.Count - 1 && existing >= 0 && pages[existing].PageIndex == page)
        {
            // Already the newest record at this page: nothing to change.
            return;
        }

        if (existing >= 0)
        {
            pages.RemoveAt(existing);
        }

        pages.Add(new() { FilePath = filePath, PageIndex = page });
        if (pages.Count > Limit)
        {
            pages.RemoveRange(0, pages.Count - Limit);
        }
    }

    /// <summary>Gets the page a document was closed at.</summary>
    /// <param name="pages">The remembered pages.</param>
    /// <param name="filePath">The document.</param>
    /// <returns>The zero-based page, or 0 when the document is not remembered.</returns>
    public static int Find(List<LastViewedPage> pages, string filePath)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var index = IndexOf(pages, filePath);
        return index < 0 ? 0 : pages[index].PageIndex;
    }

    /// <summary>Finds a document's record, searching from the newest.</summary>
    /// <param name="pages">The remembered pages.</param>
    /// <param name="filePath">The document.</param>
    /// <returns>The record's index, or -1.</returns>
    private static int IndexOf(List<LastViewedPage> pages, string filePath)
    {
        for (var i = pages.Count - 1; i >= 0; i--)
        {
            if (string.Equals(pages[i].FilePath, filePath, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
