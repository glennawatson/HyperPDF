// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Viewer.Chrome;

/// <summary>Decides how far the list of pages in the side panel scrolls to keep the page being read in view.</summary>
internal static class SidebarFollow
{
    /// <summary>
    /// Finds the first row to show. The list stays still while the page is in view, and moves only when the reader reaches an edge:
    /// one row when the page is just past the bottom, and to put the page at the top when it is further away or above.
    /// </summary>
    /// <param name="first">The first row shown now.</param>
    /// <param name="visible">The rows that fit the panel.</param>
    /// <param name="count">The rows in the list.</param>
    /// <param name="page">The row of the page being read.</param>
    /// <returns>The first row to show.</returns>
    internal static int First(int first, int visible, int count, int page)
    {
        if ((uint)page >= (uint)count)
        {
            return first;
        }

        var rows = Math.Max(1, visible);
        if (page < first)
        {
            return page;
        }

        if (page < first + rows)
        {
            return first;
        }

        return page == first + rows ? first + 1 : page;
    }
}
