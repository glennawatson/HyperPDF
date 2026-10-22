// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Flattens the page tree into an array of pages, depth first in document order, carrying inherited attributes down.
/// Loops and over-deep trees are cut off; when the tree yields no pages, the file is scanned for page objects instead.
/// </summary>
internal static class PageTreeReader
{
    /// <summary>Reads every page.</summary>
    /// <param name="objects">The document's objects.</param>
    /// <returns>The pages.</returns>
    internal static PdfPageSet Read(PdfObjectStore objects)
    {
        var root = objects.Catalog.GetRaw(KnownName.Pages);

        // The root's /Count is the page total in a well-formed tree; it only sizes the result array, so a wrong one costs one resize.
        var expected = StoreReading.Resolve(objects, root).AsDictionary()?.GetInt32(KnownName.Count, 0) ?? 0;
        var pages = new PdfPageBuffer(Math.Clamp(expected, 0, objects.Size));
        var walk = default(PageTreeWalk);
        try
        {
            walk = new(objects.Size);
            var node = new PendingNode(root, default, 0, 0);
            var walking = true;
            while (walking)
            {
                PdfOpenContext.ThrowIfCancelled(objects.Context);
                if (Visit(objects, node, ref walk, ref pages, out var firstChild))
                {
                    node = firstChild;
                }
                else
                {
                    walking = TryNextChild(ref walk, out node);
                }
            }

            if (pages.Count > 0)
            {
                return pages.ToSet(objects);
            }
        }
        finally
        {
            walk.Release();
            pages.Dispose();
        }

        return ScanForPages(objects);
    }

    /// <summary>Visits one node: adds a page, or enters its first kid.</summary>
    /// <param name="objects">The document's objects.</param>
    /// <param name="node">The node.</param>
    /// <param name="walk">The active ancestors and the object numbers already visited.</param>
    /// <param name="pages">The pages found.</param>
    /// <param name="firstChild">The first kid to visit, when the node has kids.</param>
    /// <returns>Whether the node has a first kid.</returns>
    private static bool Visit(PdfObjectStore objects, in PendingNode node, ref PageTreeWalk walk, ref PdfPageBuffer pages, out PendingNode firstChild)
    {
        firstChild = default;
        var id = node.Reference.AsReference();
        if (id.IsValid && PageLeafProbe.TryRead(objects, id))
        {
            // A plain page is recognised from its bytes and parsed when it is first used.
            pages.Add(new(id, null, node.InheritIndex));
            return false;
        }

        var dictionary = StoreReading.Resolve(objects, node.Reference).AsDictionary();
        if (dictionary is null)
        {
            return false;
        }

        var kids = dictionary.GetArray(KnownName.Kids);
        if (kids is null || dictionary.IsName(KnownName.Type, KnownName.Page))
        {
            // The page itself is made when it is first used; what reading it would report is reported now.
            PdfPage.ReportFaults(dictionary, id);
            pages.Add(new(id, id.IsValid ? null : dictionary, node.InheritIndex));
            return false;
        }

        return Enter(objects, node, dictionary, kids, ref walk, ref pages, out firstChild);
    }

    /// <summary>Enters an intermediate node: checks it is not too deep or a repeat, and moves to its first kid.</summary>
    /// <param name="objects">The document's objects.</param>
    /// <param name="node">The node.</param>
    /// <param name="dictionary">The node's dictionary.</param>
    /// <param name="kids">The node's kids.</param>
    /// <param name="walk">The active ancestors and the object numbers already visited.</param>
    /// <param name="pages">The pages found.</param>
    /// <param name="firstChild">The first kid to visit, when the node has kids.</param>
    /// <returns>Whether the node has a first kid.</returns>
    private static bool Enter(PdfObjectStore objects, in PendingNode node, PdfDictionary dictionary, PdfArray kids, ref PageTreeWalk walk, ref PdfPageBuffer pages, out PendingNode firstChild)
    {
        firstChild = default;
        var id = node.Reference.AsReference();

        // A page listed twice is listed twice, because each leaf is a page of the document; only intermediate nodes are guarded against loops.
        if (node.Depth >= PdfLimits.MaxPageTreeDepth)
        {
            PdfOpenContext.Report(objects.Context, PdfDiagnosticCode.RecursionLimit, "The page tree was too deep and was cut off.", id.Number, -1);
            return false;
        }

        if (id.IsValid && !walk.TryMarkVisited(id.Number))
        {
            PdfOpenContext.Report(objects.Context, PdfDiagnosticCode.RecursionLimit, "The page tree loops back on itself; the repeat was ignored.", id.Number, -1);
            return false;
        }

        var inherited = node.Inherited.With(dictionary);
        if (kids.Count == 0)
        {
            return false;
        }

        var depth = node.Depth + 1;
        var inheritIndex = pages.Intern(inherited, node.InheritIndex);
        if (kids.Count > 1)
        {
            walk.Push(new(kids, 1, kids.Count, inherited, inheritIndex, depth));
        }

        firstChild = new(kids.GetRaw(0), inherited, inheritIndex, depth);
        return true;
    }

    /// <summary>Advances to the next kid of the nearest ancestor that has one.</summary>
    /// <param name="walk">The active ancestors.</param>
    /// <param name="child">The next kid, when present.</param>
    /// <returns>Whether another kid remains.</returns>
    private static bool TryNextChild(ref PageTreeWalk walk, out PendingNode child)
    {
        while (walk.TryPop(out var ancestor))
        {
            if (ancestor.NextChildIndex >= ancestor.ChildCount)
            {
                continue;
            }

            child = new(ancestor.Kids.GetRaw(ancestor.NextChildIndex), ancestor.Inherited, ancestor.InheritIndex, ancestor.Depth);
            if (ancestor.NextChildIndex + 1 < ancestor.ChildCount)
            {
                walk.Push(ancestor with { NextChildIndex = ancestor.NextChildIndex + 1 });
            }

            return true;
        }

        child = default;
        return false;
    }

    /// <summary>Finds page objects by scanning every object, for files whose page tree is broken.</summary>
    /// <param name="objects">The document's objects.</param>
    /// <returns>The pages, in object order.</returns>
    private static PdfPageSet ScanForPages(PdfObjectStore objects)
    {
        var pages = new PdfPageBuffer(0);
        try
        {
            var previous = 0;
            for (var number = 1; number < objects.Size; number++)
            {
                PdfOpenContext.ThrowIfCancelled(objects.Context);
                var id = new PdfObjectId(number, 0);
                if (StoreReading.GetObject(objects, id).AsDictionary() is not { } dictionary || !dictionary.IsName(KnownName.Type, KnownName.Page))
                {
                    continue;
                }

                previous = pages.Intern(InheritFromParents(dictionary), previous);
                PdfPage.ReportFaults(dictionary, id);
                pages.Add(new(id, null, previous));
            }

            if (pages.Count > 0)
            {
                PdfOpenContext.Report(objects.Context, PdfDiagnosticCode.PageTreeRebuilt, "The page tree was missing or broken; the pages were found by scanning.", 0, -1);
            }

            return pages.ToSet(objects);
        }
        finally
        {
            pages.Dispose();
        }
    }

    /// <summary>Collects the attributes a page inherits by walking its /Parent chain; loops and over-deep chains are cut off.</summary>
    /// <param name="page">The page dictionary.</param>
    /// <returns>The inherited attributes.</returns>
    private static InheritedAttributes InheritFromParents(PdfDictionary page)
    {
        var parent = page.GetDictionary(KnownName.Parent);
        if (parent is null)
        {
            return default;
        }

        var chain = new List<PdfDictionary>();
        var visited = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance) { page, };
        while (parent is not null && chain.Count < PdfLimits.MaxPageTreeDepth && visited.Add(parent))
        {
            chain.Add(parent);
            parent = parent.GetDictionary(KnownName.Parent);
        }

        // The nearest ancestor is applied last, so it wins.
        var inherited = default(InheritedAttributes);
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            inherited = inherited.With(chain[i]);
        }

        return inherited;
    }

    /// <summary>A page tree node waiting to be visited.</summary>
    /// <param name="Reference">The node, usually a reference.</param>
    /// <param name="Inherited">The attributes it inherits.</param>
    /// <param name="InheritIndex">The position of those attributes in the page set's table.</param>
    /// <param name="Depth">Its depth in the tree.</param>
    private readonly record struct PendingNode(PdfValue Reference, InheritedAttributes Inherited, int InheritIndex, int Depth);
}
