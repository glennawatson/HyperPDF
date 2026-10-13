// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Rewrites a document's page tree with the pages in a new order: one root node whose /Kids list the pages, or, above
/// <see cref="FanOut"/> pages, a balanced tree of nodes with at most that many kids each. Old intermediate nodes are
/// deleted, so attributes a page inherited from them (/Resources, /MediaBox, /CropBox, /Rotate) are pushed down onto the
/// page first; when the root itself carries such attributes they are pushed down to every page and removed from it.
/// Pages that already sit under their new parent with nothing to push down are left untouched, so reordering a flat tree
/// only rewrites the root.
/// </summary>
internal static class PdfPageTreeWriter
{
    /// <summary>The most kids a node holds, which keeps the tree shallow while each node stays small.</summary>
    internal const int FanOut = 64;

    /// <summary>Gets the attributes a page inherits from the page tree.</summary>
    internal static ReadOnlySpan<KnownName> InheritableKeys => [KnownName.Resources, KnownName.MediaBox, KnownName.CropBox, KnownName.Rotate];

    /// <summary>Writes the page tree.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="slots">The pages in their new order.</param>
    internal static void Write(PdfEditTransaction transaction, PdfObjectStore store, ReadOnlySpan<PdfPageSlot> slots)
    {
        var rootValue = store.Catalog.GetRaw(KnownName.Pages);
        var rootId = rootValue.AsReference();
        var oldRoot = StoreReading.Resolve(store, rootValue).AsDictionary();
        var oldNodes = new List<PdfObjectId>();
        CollectNodes(store, oldRoot, rootId, oldNodes);
        if (!rootId.IsValid)
        {
            // A direct root cannot be named by /Parent; it becomes an object of its own.
            rootId = transaction.Add(default);
            var catalog = store.Catalog.Clone();
            catalog.Set(KnownName.Pages, PdfValue.FromReference(rootId));
            ReplaceCatalog(transaction, store, catalog);
        }

        var root = oldRoot?.Clone() ?? new PdfDictionary(store);
        var pushFromRoot = HasInheritable(root);
        var kids = WriteKids(transaction, store, slots, rootId, pushFromRoot);
        root.Set(KnownName.Type, PdfValue.FromName(KnownName.Pages));
        root.Set(KnownName.Kids, PdfValue.FromArray(kids));
        root.Set(KnownName.Count, PdfValue.FromInteger(slots.Length));
        _ = root.Remove(KnownName.Parent);
        if (pushFromRoot)
        {
            foreach (var key in InheritableKeys)
            {
                _ = root.Remove(key);
            }
        }

        transaction.Replace(rootId, PdfValue.FromDictionary(root));
        foreach (var node in oldNodes)
        {
            transaction.Delete(node);
        }
    }

    /// <summary>
    /// Writes the /Kids of a node: the pages themselves when they fit in <see cref="FanOut"/>, otherwise intermediate nodes
    /// that each hold a run of pages, nested until every node has at most <see cref="FanOut"/> kids.
    /// </summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="slots">The pages below the node, in order.</param>
    /// <param name="parentId">The node whose kids these are.</param>
    /// <param name="pushFromRoot">Whether the root's own attributes are being pushed down.</param>
    /// <returns>The kids.</returns>
    internal static PdfArray WriteKids(PdfEditTransaction transaction, PdfObjectStore store, ReadOnlySpan<PdfPageSlot> slots, PdfObjectId parentId, bool pushFromRoot)
    {
        var kids = new PdfArray(store, Math.Min(slots.Length, FanOut));
        if (slots.Length <= FanOut)
        {
            foreach (var slot in slots)
            {
                kids.Add(WritePage(transaction, store, slot.Kid, parentId, pushFromRoot));
            }

            return kids;
        }

        var capacity = ChildCapacity(slots.Length);
        for (var start = 0; start < slots.Length; start += capacity)
        {
            var part = slots.Slice(start, Math.Min(capacity, slots.Length - start));
            var id = transaction.Add(default);
            var node = new PdfDictionary(store);
            node.Set(KnownName.Type, PdfValue.FromName(KnownName.Pages));
            node.Set(KnownName.Parent, PdfValue.FromReference(parentId));
            node.Set(KnownName.Kids, PdfValue.FromArray(WriteKids(transaction, store, part, id, pushFromRoot)));
            node.Set(KnownName.Count, PdfValue.FromInteger(part.Length));
            transaction.Replace(id, PdfValue.FromDictionary(node));
            kids.Add(PdfValue.FromReference(id));
        }

        return kids;
    }

    /// <summary>Gets how many pages each child of a node holds: the smallest power of the fan-out whose full node covers the pages.</summary>
    /// <param name="pageCount">The pages below the node.</param>
    /// <returns>The pages per child.</returns>
    internal static int ChildCapacity(int pageCount)
    {
        long capacity = FanOut;
        while (capacity * FanOut < pageCount)
        {
            capacity *= FanOut;
        }

        return (int)capacity;
    }

    /// <summary>Replaces the catalog object and makes the store read it again.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="catalog">The new catalog.</param>
    internal static void ReplaceCatalog(PdfEditTransaction transaction, PdfObjectStore store, PdfDictionary catalog)
    {
        var id = store.Trailer.GetRaw(KnownName.Root).AsReference();
        if (id.IsValid)
        {
            transaction.Replace(id, PdfValue.FromDictionary(catalog));
        }
        else
        {
            transaction.SetTrailerEntry(KnownName.Root, PdfValue.FromReference(transaction.Add(PdfValue.FromDictionary(catalog))));
        }

        lock (store.Gate)
        {
            StoreTransactions.RefreshCatalogLocked(store);
        }
    }

    /// <summary>Determines whether a node carries any inheritable attribute.</summary>
    /// <param name="node">The node.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    internal static bool HasInheritable(PdfDictionary node)
    {
        foreach (var key in InheritableKeys)
        {
            if (node.ContainsKey(key))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Points a page at the root, pushing down what it inherited, when needed.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="kid">The page's /Kids entry.</param>
    /// <param name="rootId">The root node.</param>
    /// <param name="pushFromRoot">Whether the root's own attributes are being pushed down.</param>
    /// <returns>The page's new /Kids entry.</returns>
    private static PdfValue WritePage(PdfEditTransaction transaction, PdfObjectStore store, PdfValue kid, PdfObjectId rootId, bool pushFromRoot)
    {
        var page = StoreReading.Resolve(store, kid).AsDictionary();
        if (page is null)
        {
            return kid;
        }

        var parent = page.GetRaw(KnownName.Parent).AsReference();
        if (parent.IsValid && parent.Number == rootId.Number && !pushFromRoot)
        {
            return kid;
        }

        var copy = page.Clone();
        foreach (var key in InheritableKeys)
        {
            if (!copy.ContainsKey(key) && FindInherited(page, key) is { IsNull: false } inherited)
            {
                copy.Set(key, inherited);
            }
        }

        copy.Set(KnownName.Parent, PdfValue.FromReference(rootId));
        var id = kid.AsReference();
        if (!id.IsValid)
        {
            return PdfValue.FromDictionary(copy);
        }

        transaction.Replace(id, PdfValue.FromDictionary(copy));
        return kid;
    }

    /// <summary>Finds an attribute on the nearest ancestor that has it.</summary>
    /// <param name="page">The page.</param>
    /// <param name="key">The key.</param>
    /// <returns>The raw value, or null when no ancestor has it.</returns>
    private static PdfValue FindInherited(PdfDictionary page, KnownName key)
    {
        var visited = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance) { page, };
        var node = page.GetDictionary(KnownName.Parent);
        for (var depth = 0; node is not null && depth < PdfLimits.MaxPageTreeDepth && visited.Add(node); depth++)
        {
            var value = node.GetRaw(key);
            if (!value.IsNull)
            {
                return value;
            }

            node = node.GetDictionary(KnownName.Parent);
        }

        return default;
    }

    /// <summary>Collects the intermediate page tree nodes below the root, which the flat tree no longer needs.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="root">The root node.</param>
    /// <param name="rootId">The root's id.</param>
    /// <param name="output">Receives the ids of indirect intermediate nodes.</param>
    private static void CollectNodes(PdfObjectStore store, PdfDictionary? root, PdfObjectId rootId, List<PdfObjectId> output)
    {
        if (root is null)
        {
            return;
        }

        var visited = new HashSet<int> { rootId.Number, };
        var pending = new Stack<NodeVisit>();
        pending.Push(new(root, 0));
        while (pending.TryPop(out var visit))
        {
            var kids = visit.Node.GetArray(KnownName.Kids);
            for (var i = 0; kids is not null && i < kids.Count && visit.Depth < PdfLimits.MaxPageTreeDepth; i++)
            {
                var raw = kids.GetRaw(i);
                if (ReadNode(store, raw, visited) is not { } node)
                {
                    continue;
                }

                AddNode(output, raw.AsReference());
                pending.Push(new(node, visit.Depth + 1));
            }
        }
    }

    /// <summary>Reads an intermediate node reached for the first time.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="raw">The /Kids entry.</param>
    /// <param name="visited">The indirect nodes already reached.</param>
    /// <returns>The node, or <see langword="null"/> for a page, a non-node or a node already reached.</returns>
    private static PdfDictionary? ReadNode(PdfObjectStore store, PdfValue raw, HashSet<int> visited)
    {
        if (StoreReading.Resolve(store, raw).AsDictionary() is not { } node || node.IsName(KnownName.Type, KnownName.Page) || node.GetArray(KnownName.Kids) is null)
        {
            return null;
        }

        var id = raw.AsReference();
        return !id.IsValid || visited.Add(id.Number) ? node : null;
    }

    /// <summary>Adds an indirect node to the list of nodes to delete.</summary>
    /// <param name="output">The list.</param>
    /// <param name="id">The node's id; invalid for a direct node, which needs no deleting.</param>
    private static void AddNode(List<PdfObjectId> output, PdfObjectId id)
    {
        if (id.IsValid)
        {
            output.Add(id);
        }
    }

    /// <summary>A page tree node waiting to be visited.</summary>
    /// <param name="Node">The node.</param>
    /// <param name="Depth">Its depth.</param>
    private readonly record struct NodeVisit(PdfDictionary Node, int Depth);
}
