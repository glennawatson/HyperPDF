// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Rendering;

/// <summary>Chooses which pages around the current one are worth preparing, and in what order.</summary>
public static class NeighbourPages
{
    /// <summary>Gets how many pages before the current one are prepared.</summary>
    public static int Behind { get; } = 1;

    /// <summary>Gets how many pages after the current one are prepared.</summary>
    public static int Ahead { get; } = 2;

    /// <summary>Gets whether a page is inside the window around the current page, which runs from one page behind to two ahead.</summary>
    /// <param name="page">The page.</param>
    /// <param name="current">The current page.</param>
    /// <returns><see langword="true"/> when the page is worth preparing or keeping prepared.</returns>
    public static bool IsInWindow(int page, int current) => page >= current - Behind && page <= current + Ahead && page != current;

    /// <summary>Lists the pages to prepare, nearest in the direction of travel first, and never the current page.</summary>
    /// <param name="current">The current page.</param>
    /// <param name="pageCount">The number of pages in the document.</param>
    /// <param name="movedBackward">Whether the reader moved to the current page from a later one.</param>
    /// <returns>The pages inside the document, in the order to prepare them.</returns>
    public static int[] Order(int current, int pageCount, bool movedBackward)
    {
        var behind = current - Behind;
        var next = current + 1;
        var furthest = current + Ahead;
        int[] candidates = movedBackward ? [behind, next, furthest] : [next, furthest, behind];
        var kept = new List<int>(candidates.Length);
        foreach (var page in candidates)
        {
            if ((uint)page < (uint)pageCount)
            {
                kept.Add(page);
            }
        }

        return [.. kept];
    }
}
