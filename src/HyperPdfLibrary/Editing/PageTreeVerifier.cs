// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Checks that a page tree can be edited in place: every node is an indirect dictionary reached once, every
/// <c>/Count</c> equals the pages below the node, and the pages add up to the page count the document reports. A tree
/// that fails is written again whole, which repairs it.
/// </summary>
internal static class PageTreeVerifier
{
    /// <summary>Checks the tree.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="pageCount">The pages the document reports.</param>
    /// <param name="carriesAttributes">Receives whether some node carries attributes its kids inherit.</param>
    /// <returns><see langword="true"/> when the tree can be edited in place.</returns>
    internal static bool Verify(PdfObjectStore store, int pageCount, out bool carriesAttributes)
    {
        carriesAttributes = false;
        var rootRaw = store.Catalog.GetRaw(KnownName.Pages);
        var rootId = rootRaw.AsReference();
        if (!rootId.IsValid || StoreReading.Resolve(store, rootRaw).AsDictionary() is not { } root)
        {
            return false;
        }

        var visited = new HashSet<int> { rootId.Number, };
        var total = CountPages(store, root, 0, visited, ref carriesAttributes);
        return total == pageCount && root.GetInt32(KnownName.Count, -1) == total;
    }

    /// <summary>Determines whether a kid is a node with kids of its own rather than a page.</summary>
    /// <param name="kid">The kid's dictionary.</param>
    /// <returns><see langword="true"/> for a node.</returns>
    internal static bool IsNode(PdfDictionary kid) => kid.GetArray(KnownName.Kids) is not null && !kid.IsName(KnownName.Type, KnownName.Page);

    /// <summary>Counts the pages below a node, checking each node below it.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="node">The node.</param>
    /// <param name="depth">The node's depth.</param>
    /// <param name="visited">The nodes already reached.</param>
    /// <param name="carriesAttributes">Set when a node carries attributes its kids inherit.</param>
    /// <returns>The pages, or -1 when the tree cannot be edited in place.</returns>
    private static int CountPages(PdfObjectStore store, PdfDictionary node, int depth, HashSet<int> visited, ref bool carriesAttributes)
    {
        if (depth >= PdfLimits.MaxPageTreeDepth || node.GetArray(KnownName.Kids) is not { } kids)
        {
            return -1;
        }

        carriesAttributes |= PdfPageTreeWriter.HasInheritable(node);
        var total = 0;
        for (var i = 0; i < kids.Count; i++)
        {
            var raw = kids.GetRaw(i);
            if (StoreReading.Resolve(store, raw).AsDictionary() is not { } kid)
            {
                return -1;
            }

            var count = IsNode(kid) ? CountNode(store, raw, kid, depth + 1, visited, ref carriesAttributes) : 1;
            if (count < 0)
            {
                return -1;
            }

            total += count;
        }

        return total;
    }

    /// <summary>Counts the pages below a node that is a kid, checking its <c>/Count</c>.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="raw">The kid's entry.</param>
    /// <param name="node">The kid's dictionary.</param>
    /// <param name="depth">The kid's depth.</param>
    /// <param name="visited">The nodes already reached.</param>
    /// <param name="carriesAttributes">Set when a node carries attributes its kids inherit.</param>
    /// <returns>The pages, or -1 when the tree cannot be edited in place.</returns>
    private static int CountNode(PdfObjectStore store, PdfValue raw, PdfDictionary node, int depth, HashSet<int> visited, ref bool carriesAttributes)
    {
        // A node that is not indirect cannot be named by a /Parent, and one reached twice is a loop or a shared node.
        var id = raw.AsReference();
        if (!id.IsValid || !visited.Add(id.Number))
        {
            return -1;
        }

        var count = CountPages(store, node, depth, visited, ref carriesAttributes);
        return count >= 0 && node.GetInt32(KnownName.Count, -1) == count ? count : -1;
    }
}
