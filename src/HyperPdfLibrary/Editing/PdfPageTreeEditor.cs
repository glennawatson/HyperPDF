// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Puts pages into a page tree, and takes them out, by changing only the nodes on the way down to them: the node that
/// holds the position and its ancestors, whose <c>/Count</c> changes. A node that grows past
/// <see cref="PdfPageTreeWriter.FanOut"/> kids is split into nodes of near equal size, and a root that overflows hands
/// its kids to new nodes below it, so the tree stays shallow. The tree must first pass <see cref="PageTreeVerifier"/>.
/// </summary>
internal static class PdfPageTreeEditor
{
    /// <summary>The most kids the root may hold after an edit, so one level of new nodes below it is enough.</summary>
    private const int MaxRootKids = PdfPageTreeWriter.FanOut * PdfPageTreeWriter.FanOut;

    /// <summary>The kids of slack kept in the root for the siblings a split below can hand up.</summary>
    private const int SiblingRoom = 2;

    /// <summary>Determines whether pages can be moved in place: the tree is sound and no node passes attributes down to its kids.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="set">The document's pages.</param>
    /// <returns><see langword="true"/> when they can.</returns>
    internal static bool CanMove(PdfObjectStore store, PdfPageSet set) =>
        IsSound(store, set) && (set.TreeState & PdfPageSet.NodesCarryAttributes) == 0;

    /// <summary>Finds where pages go.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="set">The document's pages.</param>
    /// <param name="index">The index the first inserted page takes.</param>
    /// <param name="count">The pages inserted.</param>
    /// <returns>The path to the node that takes them, or <see langword="null"/> when the tree has to be written again whole.</returns>
    internal static PageTreePath? Locate(PdfObjectStore store, PdfPageSet set, int index, int count)
    {
        if (!IsSound(store, set) || Descend(store, index, true) is not { } path)
        {
            return null;
        }

        // Room is kept for the siblings a split in the tree below can hand up to the root.
        var rootKids = path[0].Node.GetArray(KnownName.Kids)!.Count;
        if (rootKids + count + (count / PdfPageTreeWriter.FanOut) + SiblingRoom <= MaxRootKids)
        {
            return path;
        }

        path.Dispose();
        return null;
    }

    /// <summary>Gets whether every page the path takes would inherit nothing a page left out of its own dictionary.</summary>
    /// <param name="path">The path.</param>
    /// <param name="pages">The dictionaries of the pages that will be inserted.</param>
    /// <returns><see langword="true"/> when each page holds every inheritable key that a node on the path carries.</returns>
    internal static bool KeepsAttributes(PageTreePath path, ReadOnlySpan<PdfDictionary> pages)
    {
        if (path.AttributeMask == 0)
        {
            return true;
        }

        foreach (var page in pages)
        {
            if (!HoldsAll(page, path.AttributeMask))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Puts kids into the node a path ends at.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="path">Where they go, from <see cref="Locate"/>.</param>
    /// <param name="inserted">The kids' entries: references, or direct pages.</param>
    internal static void Insert(PdfEditTransaction transaction, PdfObjectStore store, PageTreePath path, ReadOnlySpan<PdfValue> inserted)
    {
        var deepest = path.Count - 1;
        var leaf = path.Deepest;
        var kids = WithKids(store, leaf.Node.GetArray(KnownName.Kids)!, path.InsertAt, inserted);
        Adopt(transaction, store, kids, path.InsertAt, inserted.Length, leaf.Id);
        PdfArray? siblings = null;
        for (var level = deepest; level >= 0; level--)
        {
            var node = path[level];
            if (level < deepest)
            {
                kids = WithKids(store, node.Node.GetArray(KnownName.Kids)!, node.ChildIndex + 1, siblings);
            }

            siblings = Place(transaction, store, path, level, kids, inserted.Length);
        }
    }

    /// <summary>Takes a page out of the tree, leaving the page object as it is.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="index">The page's index in the tree as it is now.</param>
    /// <returns><see langword="true"/> when the page was taken out.</returns>
    internal static bool Remove(PdfEditTransaction transaction, PdfObjectStore store, int index)
    {
        using var path = Descend(store, index, false);
        if (path is null)
        {
            return false;
        }

        var removeKid = true;
        for (var level = path.Count - 1; level >= 0; level--)
        {
            var node = path[level];
            var kids = node.Node.GetArray(KnownName.Kids)!;
            if (removeKid)
            {
                kids = kids.Clone();
                kids.RemoveAt(node.ChildIndex);
            }

            if (kids.Count == 0 && level > 0)
            {
                // A node left without kids goes from its parent too.
                transaction.Delete(node.Id);
                removeKid = true;
                continue;
            }

            transaction.Replace(node.Id, WithKidsAndCount(node.Node, kids, node.Node.GetInt32(KnownName.Count) - 1));
            removeKid = false;
        }

        return true;
    }

    /// <summary>Determines whether the tree behind a page set can be edited in place, checking it once.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="set">The document's pages.</param>
    /// <returns><see langword="true"/> when it can.</returns>
    private static bool IsSound(PdfObjectStore store, PdfPageSet set)
    {
        var state = set.TreeState;
        if ((state & PdfPageSet.TreeVerified) != 0)
        {
            return true;
        }

        if ((state & PdfPageSet.TreeUnusable) != 0)
        {
            return false;
        }

        var sound = PageTreeVerifier.Verify(store, set.Count, out var carries);
        set.TreeState = sound ? PdfPageSet.TreeVerified | (carries ? PdfPageSet.NodesCarryAttributes : 0) : PdfPageSet.TreeUnusable;
        return sound;
    }

    /// <summary>Walks down from the root, by the page counts of the nodes, to a position.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="position">The page's index, or the index an inserted page takes.</param>
    /// <param name="insertion">Whether a position at the end of a node is allowed.</param>
    /// <returns>The path, or <see langword="null"/> when the position is not in the tree.</returns>
    private static PageTreePath? Descend(PdfObjectStore store, int position, bool insertion)
    {
        var rootRaw = store.Catalog.GetRaw(KnownName.Pages);
        var id = rootRaw.AsReference();
        var node = StoreReading.Resolve(store, rootRaw).AsDictionary();
        var path = new PageTreePath();
        var remaining = position;
        while (node is not null && id.IsValid && path.Count < PdfLimits.MaxPageTreeDepth)
        {
            var step = FindKid(store, node, remaining, insertion);
            path.Add(new(id, node, step.Index));
            path.AttributeMask |= AttributesOf(node);
            if (step.Node is null)
            {
                return Finish(path, step, insertion);
            }

            id = step.Raw.AsReference();
            node = step.Node;
            remaining = step.Remaining;
        }

        path.Dispose();
        return null;
    }

    /// <summary>Completes a path that reached the node holding the position.</summary>
    /// <param name="path">The path.</param>
    /// <param name="step">The last step.</param>
    /// <param name="insertion">Whether the position is an insertion.</param>
    /// <returns>The path, or <see langword="null"/> when the position is not in the tree.</returns>
    private static PageTreePath? Finish(PageTreePath path, in Step step, bool insertion)
    {
        // A kid found before its position leaves 0 (before it) or, for the last kid, 1 (after it). Past the last kid, only an insertion at the very end is a position.
        var found = !step.Raw.IsNull;
        var valid = insertion ? (found && step.Remaining <= 1) || (!found && step.Remaining == 0) : found && step.Remaining == 0;
        if (!valid)
        {
            path.Dispose();
            return null;
        }

        path.InsertAt = step.Index + step.Remaining;
        return path;
    }

    /// <summary>Finds the kid of a node that holds a position.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="node">The node.</param>
    /// <param name="remaining">The position among the pages below the node.</param>
    /// <param name="insertion">Whether a position at the end of the node is allowed.</param>
    /// <returns>The kid, with the position among the pages below it; a kid that is a node has <see cref="Step.Node"/> set.</returns>
    private static Step FindKid(PdfObjectStore store, PdfDictionary node, int remaining, bool insertion)
    {
        var kids = node.GetArray(KnownName.Kids)!;
        for (var i = 0; i < kids.Count; i++)
        {
            var raw = kids.GetRaw(i);
            var kid = StoreReading.Resolve(store, raw).AsDictionary();
            var inner = kid is not null && PageTreeVerifier.IsNode(kid) ? kid : null;
            var span = inner is null ? 1 : inner.GetInt32(KnownName.Count);
            if (remaining < span || (insertion && i == kids.Count - 1 && remaining == span))
            {
                return new(i, remaining, raw, inner);
            }

            remaining -= span;
        }

        return new(kids.Count, remaining, default, null);
    }

    /// <summary>Gets the inheritable keys a node carries.</summary>
    /// <param name="node">The node.</param>
    /// <returns>One bit for each key, in the order of <see cref="PdfPageTreeWriter.InheritableKeys"/>.</returns>
    private static int AttributesOf(PdfDictionary node)
    {
        var mask = 0;
        var keys = PdfPageTreeWriter.InheritableKeys;
        for (var i = 0; i < keys.Length; i++)
        {
            if (node.ContainsKey(keys[i]))
            {
                mask |= 1 << i;
            }
        }

        return mask;
    }

    /// <summary>Determines whether a page holds every inheritable key in a mask.</summary>
    /// <param name="page">The page.</param>
    /// <param name="mask">One bit for each key, in the order of <see cref="PdfPageTreeWriter.InheritableKeys"/>.</param>
    /// <returns><see langword="true"/> when it holds them all.</returns>
    private static bool HoldsAll(PdfDictionary page, int mask)
    {
        var keys = PdfPageTreeWriter.InheritableKeys;
        for (var i = 0; i < keys.Length; i++)
        {
            if ((mask & (1 << i)) != 0 && !page.ContainsKey(keys[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Copies a <c>/Kids</c> array with entries put in at a position.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="kids">The kids.</param>
    /// <param name="at">The position.</param>
    /// <param name="added">The entries.</param>
    /// <returns>The new array.</returns>
    private static PdfArray WithKids(PdfObjectStore store, PdfArray kids, int at, ReadOnlySpan<PdfValue> added)
    {
        var copy = new PdfArray(store, kids.Count + added.Length);
        foreach (var kid in kids.Items[..at])
        {
            copy.Add(kid);
        }

        foreach (var kid in added)
        {
            copy.Add(kid);
        }

        foreach (var kid in kids.Items[at..])
        {
            copy.Add(kid);
        }

        return copy;
    }

    /// <summary>Copies a <c>/Kids</c> array with sibling nodes put in at a position.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="kids">The kids.</param>
    /// <param name="at">The position.</param>
    /// <param name="siblings">The references to the new nodes, or <see langword="null"/> for none.</param>
    /// <returns>The new array; <paramref name="kids"/> itself when there are no siblings.</returns>
    private static PdfArray WithKids(PdfObjectStore store, PdfArray kids, int at, PdfArray? siblings) =>
        siblings is null ? kids : WithKids(store, kids, at, siblings.Items);

    /// <summary>Writes a node with new kids, splitting it or pushing its kids down when it holds too many.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="path">The path being edited.</param>
    /// <param name="level">The node's depth on the path.</param>
    /// <param name="kids">The node's new kids.</param>
    /// <param name="added">The pages added below the node.</param>
    /// <returns>References to the new nodes that now sit after the node in its parent, or <see langword="null"/> for none.</returns>
    private static PdfArray? Place(PdfEditTransaction transaction, PdfObjectStore store, PageTreePath path, int level, PdfArray kids, int added)
    {
        var node = path[level];
        var count = node.Node.GetInt32(KnownName.Count) + added;
        if (kids.Count <= PdfPageTreeWriter.FanOut)
        {
            transaction.Replace(node.Id, WithKidsAndCount(node.Node, kids, count));
            return null;
        }

        if (level == 0)
        {
            PushDown(transaction, store, node, kids, count);
            return null;
        }

        return Split(transaction, store, node, path[level - 1].Id, kids);
    }

    /// <summary>Splits a node that holds too many kids into nodes of near equal size; the first stays the node.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="node">The node.</param>
    /// <param name="parentId">The node's parent.</param>
    /// <param name="kids">The node's new kids.</param>
    /// <returns>References to the new nodes, which follow the node in its parent.</returns>
    private static PdfArray Split(PdfEditTransaction transaction, PdfObjectStore store, PathNode node, PdfObjectId parentId, PdfArray kids)
    {
        var parts = PartCount(kids.Count);
        var siblings = new PdfArray(store, parts - 1);
        var start = 0;
        for (var part = 0; part < parts; part++)
        {
            var slice = Slice(store, kids, start, PartSize(kids.Count, parts, part));
            start += slice.Count;
            if (part == 0)
            {
                transaction.Replace(node.Id, WithKidsAndCount(node.Node, slice, PagesBelow(store, slice)));
                continue;
            }

            siblings.Add(PdfValue.FromReference(AddNode(transaction, store, node.Node, parentId, slice)));
        }

        return siblings;
    }

    /// <summary>Moves the root's kids into new nodes below it.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="root">The root.</param>
    /// <param name="kids">The root's new kids.</param>
    /// <param name="count">The root's new page count.</param>
    private static void PushDown(PdfEditTransaction transaction, PdfObjectStore store, PathNode root, PdfArray kids, int count)
    {
        var parts = PartCount(kids.Count);
        var children = new PdfArray(store, parts);
        var start = 0;
        for (var part = 0; part < parts; part++)
        {
            var slice = Slice(store, kids, start, PartSize(kids.Count, parts, part));
            start += slice.Count;
            children.Add(PdfValue.FromReference(AddNode(transaction, store, null, root.Id, slice)));
        }

        transaction.Replace(root.Id, WithKidsAndCount(root.Node, children, count));
    }

    /// <summary>Adds a node holding kids, and points the kids at it.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="attributes">The node whose inheritable attributes the new node repeats, or <see langword="null"/> for none.</param>
    /// <param name="parentId">The new node's parent.</param>
    /// <param name="kids">The new node's kids.</param>
    /// <returns>The new node's id.</returns>
    private static PdfObjectId AddNode(PdfEditTransaction transaction, PdfObjectStore store, PdfDictionary? attributes, PdfObjectId parentId, PdfArray kids)
    {
        var id = transaction.Add(default);
        var node = new PdfDictionary(store);
        node.Set(KnownName.Type, PdfValue.FromName(KnownName.Pages));
        node.Set(KnownName.Parent, PdfValue.FromReference(parentId));
        node.Set(KnownName.Kids, PdfValue.FromArray(kids));
        node.Set(KnownName.Count, PdfValue.FromInteger(PagesBelow(store, kids)));
        CopyAttributes(attributes, node);
        Adopt(transaction, store, kids, 0, kids.Count, id);
        transaction.Replace(id, PdfValue.FromDictionary(node));
        return id;
    }

    /// <summary>Repeats the inheritable attributes of one node on another.</summary>
    /// <param name="source">The node to copy from, or <see langword="null"/>.</param>
    /// <param name="target">The node to copy to.</param>
    private static void CopyAttributes(PdfDictionary? source, PdfDictionary target)
    {
        if (source is null)
        {
            return;
        }

        foreach (var key in PdfPageTreeWriter.InheritableKeys)
        {
            if (source.GetRaw(key) is { IsNull: false } value)
            {
                target.Set(key, value);
            }
        }
    }

    /// <summary>Points kids at a parent, writing again each kid that names another one.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="kids">The array holding the kids; a direct kid is changed in it.</param>
    /// <param name="first">The first kid.</param>
    /// <param name="count">The kids.</param>
    /// <param name="parentId">The parent.</param>
    private static void Adopt(PdfEditTransaction transaction, PdfObjectStore store, PdfArray kids, int first, int count, PdfObjectId parentId)
    {
        for (var i = first; i < first + count; i++)
        {
            var raw = kids.GetRaw(i);
            if (StoreReading.Resolve(store, raw).AsDictionary() is not { } kid)
            {
                continue;
            }

            var current = kid.GetRaw(KnownName.Parent).AsReference();
            if (current.IsValid && current.Number == parentId.Number)
            {
                continue;
            }

            var copy = kid.Clone();
            copy.Set(KnownName.Parent, PdfValue.FromReference(parentId));
            if (raw.AsReference() is { IsValid: true } id)
            {
                transaction.Replace(id, PdfValue.FromDictionary(copy));
            }
            else
            {
                kids.SetAt(i, PdfValue.FromDictionary(copy));
            }
        }
    }

    /// <summary>Counts the pages below some kids.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="kids">The kids.</param>
    /// <returns>The pages.</returns>
    private static int PagesBelow(PdfObjectStore store, PdfArray kids)
    {
        var total = 0;
        for (var i = 0; i < kids.Count; i++)
        {
            var kid = StoreReading.Resolve(store, kids.GetRaw(i)).AsDictionary();
            total += kid is not null && PageTreeVerifier.IsNode(kid) ? kid.GetInt32(KnownName.Count) : 1;
        }

        return total;
    }

    /// <summary>Copies a run of kids into an array of its own.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="kids">The array to copy from.</param>
    /// <param name="start">The position of the first kid copied.</param>
    /// <param name="size">The number of kids copied.</param>
    /// <returns>The new array.</returns>
    private static PdfArray Slice(PdfObjectStore store, PdfArray kids, int start, int size) => new(store, kids.Items.Slice(start, size));

    /// <summary>Copies a node with new kids and a new page count.</summary>
    /// <param name="node">The node.</param>
    /// <param name="kids">The kids.</param>
    /// <param name="count">The pages below the node.</param>
    /// <returns>The node as a value.</returns>
    private static PdfValue WithKidsAndCount(PdfDictionary node, PdfArray kids, int count)
    {
        var copy = node.Clone();
        copy.Set(KnownName.Kids, PdfValue.FromArray(kids));
        copy.Set(KnownName.Count, PdfValue.FromInteger(count));
        return PdfValue.FromDictionary(copy);
    }

    /// <summary>Gets how many nodes of at most the fan-out hold some kids.</summary>
    /// <param name="kids">The kids.</param>
    /// <returns>The nodes.</returns>
    private static int PartCount(int kids) => (kids + PdfPageTreeWriter.FanOut - 1) / PdfPageTreeWriter.FanOut;

    /// <summary>Gets how many kids one of several near equal nodes holds.</summary>
    /// <param name="kids">The kids to share out.</param>
    /// <param name="parts">The nodes.</param>
    /// <param name="part">The node.</param>
    /// <returns>The kids it holds.</returns>
    private static int PartSize(int kids, int parts, int part) => (kids / parts) + (part < kids % parts ? 1 : 0);

    /// <summary>The kid of a node that holds a position.</summary>
    /// <param name="Index">The kid's index, or the kid count when the position is past the last kid.</param>
    /// <param name="Remaining">The position among the pages below the kid.</param>
    /// <param name="Raw">The kid's entry; null past the last kid.</param>
    /// <param name="Node">The kid's dictionary when it is a node; otherwise <see langword="null"/>.</param>
    private readonly record struct Step(int Index, int Remaining, PdfValue Raw, PdfDictionary? Node);
}
