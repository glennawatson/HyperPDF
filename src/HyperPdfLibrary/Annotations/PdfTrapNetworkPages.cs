// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Annotations;

/// <summary>
/// Walks the page tree as it is now and finds the pages that hold a trap network annotation. Only <c>/Kids</c> and
/// <c>/Annots</c> are read, so a save that finds none does little work; loops and over-deep trees are cut off.
/// </summary>
internal static class PdfTrapNetworkPages
{
    /// <summary>Finds the pages with a trap network annotation.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="cancellationToken">Stops the walk.</param>
    /// <returns>The pages, in page order, or <see langword="null"/> when there are none.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static List<PdfTrapNetworkPage>? Find(PdfObjectStore store, CancellationToken cancellationToken)
    {
        var walk = new Walk(store);
        Visit(walk, store.Catalog.GetRaw(KnownName.Pages), 0);
        while (walk.Pending.TryPop(out var cursor))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (cursor.Next >= cursor.Kids.Count)
            {
                continue;
            }

            walk.Pending.Push(cursor with { Next = cursor.Next + 1 });
            Visit(walk, cursor.Kids.GetRaw(cursor.Next), cursor.Depth);
        }

        return walk.Found;
    }

    /// <summary>Visits a page tree entry: a page is checked, a node's kids are queued.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="entry">The entry as written in its parent's <c>/Kids</c>.</param>
    /// <param name="depth">The entry's depth in the tree.</param>
    private static void Visit(Walk walk, PdfValue entry, int depth)
    {
        if (StoreReading.Resolve(walk.Store, entry).AsDictionary() is not { } node)
        {
            return;
        }

        if (node.GetArray(KnownName.Kids) is not { } kids || node.IsName(KnownName.Type, KnownName.Page))
        {
            VisitPage(walk, entry, node);
            return;
        }

        // As in PageTreeReader, a page listed twice counts twice; only the nodes above pages are guarded against loops.
        if (depth < PdfLimits.MaxPageTreeDepth && (!entry.IsReference || walk.Visited.Add(entry.AsReference().Number)))
        {
            walk.Pending.Push(new(kids, 0, depth + 1));
        }
    }

    /// <summary>Counts a page and notes it when it holds a trap network.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="entry">The page as written in its parent's <c>/Kids</c>.</param>
    /// <param name="page">The page dictionary.</param>
    private static void VisitPage(Walk walk, PdfValue entry, PdfDictionary page)
    {
        var index = walk.PageCount;
        walk.PageCount = index + 1;
        if (page.GetArray(KnownName.Annots) is { } annotations && PdfTrapNetworkReader.Find(walk.Store, annotations, out var last) is var count and > 0)
        {
            (walk.Found ??= []).Add(new(walk.Store, index, entry.AsReference(), page, annotations, last, count));
        }
    }

    /// <summary>A node's kids and the next one to visit.</summary>
    /// <param name="Kids">The <c>/Kids</c> array.</param>
    /// <param name="Next">The index of the next kid.</param>
    /// <param name="Depth">The kids' depth in the tree.</param>
    [DebuggerDisplay("KidsCursor: {Next} of {Kids.Count}")]
    private readonly record struct KidsCursor(PdfArray Kids, int Next, int Depth);

    /// <summary>The state of one walk.</summary>
    /// <param name="store">The document's objects.</param>
    [DebuggerDisplay("Walk: {PageCount} pages")]
    private sealed class Walk(PdfObjectStore store)
    {
        /// <summary>Gets the document's objects.</summary>
        internal PdfObjectStore Store { get; } = store;

        /// <summary>Gets the page tree nodes seen, so a loop ends.</summary>
        internal HashSet<int> Visited { get; } = [];

        /// <summary>Gets the nodes whose kids are still being visited.</summary>
        internal Stack<KidsCursor> Pending { get; } = new();

        /// <summary>Gets or sets the pages visited so far.</summary>
        internal int PageCount { get; set; }

        /// <summary>Gets or sets the pages with a trap network, or <see langword="null"/> before the first.</summary>
        internal List<PdfTrapNetworkPage>? Found { get; set; }
    }
}
