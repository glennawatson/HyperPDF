// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Builds and edits name trees and number trees (ISO 32000-2 7.9.6 and 7.9.7). Every node is an indirect object: the
/// root has /Kids or /Names (/Nums), and every other node has /Limits. A node holds at most
/// <see cref="MaxNodeEntries"/> entries. A single add or remove touches one path from the root, so it costs the depth
/// of the tree plus one node; a build writes each node once.
/// </summary>
internal static class PdfTreeEditing
{
    /// <summary>The most entries (pairs, or kids) in one node.</summary>
    internal const int MaxNodeEntries = 32;

    /// <summary>The values per entry of a /Names or /Nums array: the key and the value.</summary>
    private const int PairLength = 2;

    /// <summary>The position of the lowest key in /Limits.</summary>
    private const int LowerLimit = 0;

    /// <summary>The position of the highest key in /Limits.</summary>
    private const int UpperLimit = 1;

    /// <summary>The most entries in a tree this code builds, guarding against hostile input.</summary>
    private const int MaxEntries = 4_000_000;

    /// <summary>The outcome of removing an entry.</summary>
    internal enum TreeEditResult
    {
        /// <summary>The entry was removed.</summary>
        Done = 0,

        /// <summary>The key is not in the tree.</summary>
        NotFound = 1,

        /// <summary>The tree is not in a shape that can be edited in place; rebuild it.</summary>
        Unsupported = 2,
    }

    /// <summary>Where a walk down a tree ended.</summary>
    private enum DescendResult
    {
        /// <summary>The walk reached a leaf.</summary>
        Leaf = 0,

        /// <summary>No kid's limits cover the key.</summary>
        NotCovered = 1,

        /// <summary>The tree is not in a shape that can be edited in place.</summary>
        Unsupported = 2,
    }

    /// <summary>Builds a tree from entries sorted by key, writing the nodes as indirect objects.</summary>
    /// <param name="edits">The edits the nodes belong to.</param>
    /// <param name="entries">The entries, sorted by key with no repeated key.</param>
    /// <param name="numeric">Whether the keys are integers (a number tree) rather than strings.</param>
    /// <param name="rootId">The object that becomes the root, or an invalid id to make a new one.</param>
    /// <param name="cancellationToken">Cancels the build.</param>
    /// <returns>The root's object id.</returns>
    internal static PdfObjectId Build(PdfObjectEdits edits, List<NameTreeEntry> entries, bool numeric, PdfObjectId rootId, CancellationToken cancellationToken)
    {
        var leafKey = numeric ? KnownName.Nums : KnownName.Names;
        if (entries.Count <= MaxNodeEntries)
        {
            return PlaceRoot(edits, rootId, WriteLeaf(edits.Store, entries, 0, entries.Count, leafKey, false));
        }

        var level = WriteLeaves(edits, entries, leafKey, cancellationToken);
        while (level.Count > MaxNodeEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            level = WriteParents(edits, level);
        }

        var root = new PdfDictionary(edits.Store);
        root.Set(KnownName.Kids, PdfValue.FromArray(KidArray(edits.Store, level, 0, level.Count)));
        return PlaceRoot(edits, rootId, root);
    }

    /// <summary>Lists the object ids of every node below a root, so they can be deleted when the tree is rebuilt.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="root">The root node.</param>
    /// <returns>The kids' ids, not the root's.</returns>
    internal static List<PdfObjectId> CollectKidIds(PdfObjectStore store, PdfDictionary? root)
    {
        var ids = new List<PdfObjectId>();
        var pending = new Stack<PdfDictionary>();
        var seen = new HashSet<int>();
        if (root is not null)
        {
            pending.Push(root);
        }

        while (pending.Count > 0 && ids.Count < MaxEntries)
        {
            var kids = pending.Pop().GetArray(KnownName.Kids);
            for (var i = 0; kids is not null && i < kids.Count; i++)
            {
                var id = kids.GetRaw(i).AsReference();
                if (!id.IsValid || !seen.Add(id.Number) || StoreReading.GetDictionary(store, id) is not { } kid)
                {
                    continue;
                }

                ids.Add(id);
                pending.Push(kid);
            }
        }

        return ids;
    }

    /// <summary>Adds an entry to a tree, or replaces the value of an existing key.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="rootId">The root node.</param>
    /// <param name="key">The key.</param>
    /// <param name="value">The value, as it is stored.</param>
    /// <param name="numeric">Whether the keys are integers.</param>
    /// <returns><see langword="false"/> when the tree is not in a shape this code can edit in place; nothing was changed.</returns>
    internal static bool TryInsert(PdfObjectEdits edits, PdfObjectId rootId, PdfValue key, PdfValue value, bool numeric)
    {
        var path = new List<PathStep>();
        var leafKey = numeric ? KnownName.Nums : KnownName.Names;
        if (Descend(edits, rootId, key, numeric, false, path, out var leafId) != DescendResult.Leaf
            || edits.Peek(leafId) is not { } probe || probe.GetRaw(leafKey).Kind is not (PdfKind.Array or PdfKind.Null))
        {
            return false;
        }

        var leaf = edits.Edit(leafId)!;
        var pairs = leaf.GetRaw(leafKey).AsArray() ?? new PdfArray(edits.Store);
        var exact = FindPair(pairs, key, numeric, out var position);
        var changed = pairs.Clone();
        if (exact)
        {
            changed.SetAt((position * PairLength) + 1, value);
        }
        else
        {
            changed.Insert(position * PairLength, key);
            changed.Insert((position * PairLength) + 1, value);
        }

        leaf.Set(leafKey, PdfValue.FromArray(changed));
        SplitWhileFull(edits, path, leafId, leafKey);
        RefreshLimits(edits, path, leafId, leafKey);
        return true;
    }

    /// <summary>Removes an entry from a tree.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="rootId">The root node.</param>
    /// <param name="key">The key.</param>
    /// <param name="numeric">Whether the keys are integers.</param>
    /// <returns>
    /// <see cref="TreeEditResult.Done"/> when removed, <see cref="TreeEditResult.NotFound"/> when the key is not in the
    /// tree, or <see cref="TreeEditResult.Unsupported"/> when the tree is not in a shape this code can edit in place.
    /// </returns>
    internal static TreeEditResult TryRemove(PdfObjectEdits edits, PdfObjectId rootId, PdfValue key, bool numeric)
    {
        var path = new List<PathStep>();
        var leafKey = numeric ? KnownName.Nums : KnownName.Names;
        var found = Descend(edits, rootId, key, numeric, true, path, out var leafId);
        if (found != DescendResult.Leaf)
        {
            return found == DescendResult.NotCovered ? TreeEditResult.NotFound : TreeEditResult.Unsupported;
        }

        if (edits.Peek(leafId) is not { } probe || probe.GetRaw(leafKey).AsArray() is not { } stored)
        {
            return TreeEditResult.Unsupported;
        }

        if (!FindPair(stored, key, numeric, out var position))
        {
            return TreeEditResult.NotFound;
        }

        var leaf = edits.Edit(leafId)!;
        var changed = stored.Clone();
        changed.RemoveAt(position * PairLength);
        changed.RemoveAt(position * PairLength);
        leaf.Set(leafKey, PdfValue.FromArray(changed));
        AfterRemove(edits, path, leafId, changed.Count == 0, leafKey);
        return TreeEditResult.Done;
    }

    /// <summary>Finds the position of a key in a sorted /Names or /Nums array.</summary>
    /// <param name="pairs">The array.</param>
    /// <param name="key">The key.</param>
    /// <param name="numeric">Whether the keys are integers.</param>
    /// <param name="position">The pair index of the key, or where it would be inserted.</param>
    /// <returns><see langword="true"/> when the key is present.</returns>
    internal static bool FindPair(PdfArray pairs, PdfValue key, bool numeric, out int position)
    {
        var low = 0;
        var high = (pairs.Count / PairLength) - 1;
        while (low <= high)
        {
            var middle = (low + high) >>> 1;
            var order = Compare(pairs.Get(middle * PairLength), key, numeric);
            if (order == 0)
            {
                position = middle;
                return true;
            }

            low = order < 0 ? middle + 1 : low;
            high = order < 0 ? high : middle - 1;
        }

        position = low;
        return false;
    }

    /// <summary>Compares two keys.</summary>
    /// <param name="left">The first key.</param>
    /// <param name="right">The second key.</param>
    /// <param name="numeric">Whether the keys are integers rather than strings.</param>
    /// <returns>Negative, zero or positive.</returns>
    internal static int Compare(PdfValue left, PdfValue right, bool numeric) =>
        numeric ? left.AsInteger().CompareTo(right.AsInteger()) : left.AsStringBytes().SequenceCompareTo(right.AsStringBytes());

    /// <summary>Sorts entries by key and keeps the last of any repeated key, so a later value replaces an earlier one.</summary>
    /// <param name="entries">The entries, changed in place.</param>
    /// <param name="numeric">Whether the keys are integers.</param>
    /// <param name="cancellationToken">Cancels the sort.</param>
    internal static void SortAndDeduplicate(List<NameTreeEntry> entries, bool numeric, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = entries.RemoveAll(entry => numeric ? !entry.Key.IsNumber : entry.Key.Kind != PdfKind.String);
        var items = new SortItem[entries.Count];
        for (var i = 0; i < items.Length; i++)
        {
            items[i] = new(entries[i], i);
        }

        Array.Sort(items, (a, b) => Compare(a.Entry.Key, b.Entry.Key, numeric) is var order and not 0 ? order : a.Order.CompareTo(b.Order));
        entries.Clear();
        for (var i = 0; i < items.Length; i++)
        {
            if (i + 1 < items.Length && Compare(items[i].Entry.Key, items[i + 1].Entry.Key, numeric) == 0)
            {
                continue;
            }

            entries.Add(items[i].Entry);
        }
    }

    /// <summary>Writes the root over an existing object, or as a new object.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="rootId">The existing object, or an invalid id.</param>
    /// <param name="root">The root dictionary.</param>
    /// <returns>The root's id.</returns>
    private static PdfObjectId PlaceRoot(PdfObjectEdits edits, PdfObjectId rootId, PdfDictionary root)
    {
        if (!rootId.IsValid)
        {
            return edits.Add(root);
        }

        edits.Put(rootId, root);
        return rootId;
    }

    /// <summary>Writes a leaf node.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="entries">The sorted entries.</param>
    /// <param name="start">The first entry of the leaf.</param>
    /// <param name="count">The entries in the leaf.</param>
    /// <param name="leafKey">/Names or /Nums.</param>
    /// <param name="withLimits">Whether to add /Limits; every node except the root has them.</param>
    /// <returns>The node.</returns>
    private static PdfDictionary WriteLeaf(PdfObjectStore store, List<NameTreeEntry> entries, int start, int count, KnownName leafKey, bool withLimits)
    {
        var pairs = new PdfArray(store, count * PairLength);
        for (var i = 0; i < count; i++)
        {
            pairs.Add(entries[start + i].Key);
            pairs.Add(entries[start + i].Value);
        }

        var node = new PdfDictionary(store);
        if (withLimits && count > 0)
        {
            node.Set(KnownName.Limits, PdfValue.FromArray(new(store, [entries[start].Key, entries[start + count - 1].Key])));
        }

        node.Set(leafKey, PdfValue.FromArray(pairs));
        return node;
    }

    /// <summary>Writes the leaves of a large tree, sharing the entries out evenly.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="entries">The sorted entries.</param>
    /// <param name="leafKey">/Names or /Nums.</param>
    /// <param name="cancellationToken">Cancels the build.</param>
    /// <returns>The leaves, in order.</returns>
    private static List<TreeNode> WriteLeaves(PdfObjectEdits edits, List<NameTreeEntry> entries, KnownName leafKey, CancellationToken cancellationToken)
    {
        var leaves = (entries.Count + MaxNodeEntries - 1) / MaxNodeEntries;
        var level = new List<TreeNode>(leaves);
        var baseSize = entries.Count / leaves;
        var extra = entries.Count % leaves;
        var start = 0;
        for (var i = 0; i < leaves; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var size = baseSize + (i < extra ? 1 : 0);
            var id = edits.Add(WriteLeaf(edits.Store, entries, start, size, leafKey, true));
            level.Add(new(id, entries[start].Key, entries[start + size - 1].Key));
            start += size;
        }

        return level;
    }

    /// <summary>Writes the intermediate nodes above a level, sharing the kids out evenly.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="level">The nodes of the level below.</param>
    /// <returns>The nodes written, in order.</returns>
    private static List<TreeNode> WriteParents(PdfObjectEdits edits, List<TreeNode> level)
    {
        var parents = (level.Count + MaxNodeEntries - 1) / MaxNodeEntries;
        var above = new List<TreeNode>(parents);
        var baseSize = level.Count / parents;
        var extra = level.Count % parents;
        var start = 0;
        for (var i = 0; i < parents; i++)
        {
            var size = baseSize + (i < extra ? 1 : 0);
            var node = new PdfDictionary(edits.Store);
            var first = level[start].First;
            var last = level[start + size - 1].Last;
            node.Set(KnownName.Limits, PdfValue.FromArray(new(edits.Store, [first, last])));
            node.Set(KnownName.Kids, PdfValue.FromArray(KidArray(edits.Store, level, start, size)));
            above.Add(new(edits.Add(node), first, last));
            start += size;
        }

        return above;
    }

    /// <summary>Makes a /Kids array of references.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="level">The nodes.</param>
    /// <param name="start">The first node.</param>
    /// <param name="count">The nodes to list.</param>
    /// <returns>The array.</returns>
    private static PdfArray KidArray(PdfObjectStore store, List<TreeNode> level, int start, int count)
    {
        var kids = new PdfArray(store, count);
        for (var i = 0; i < count; i++)
        {
            kids.Add(PdfValue.FromReference(level[start + i].Id));
        }

        return kids;
    }

    /// <summary>Walks from the root to the leaf that holds, or should hold, a key.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="rootId">The root.</param>
    /// <param name="key">The key.</param>
    /// <param name="numeric">Whether keys are integers.</param>
    /// <param name="exact">Whether the key must lie within the kids' limits (a lookup) rather than be placed in the nearest kid (an insert).</param>
    /// <param name="path">Receives the nodes above the leaf and the kid taken at each.</param>
    /// <param name="leafId">Receives the leaf.</param>
    /// <returns>Whether a leaf was reached, no kid covers the key, or the tree cannot be edited in place.</returns>
    private static DescendResult Descend(PdfObjectEdits edits, PdfObjectId rootId, PdfValue key, bool numeric, bool exact, List<PathStep> path, out PdfObjectId leafId)
    {
        leafId = rootId;
        for (var depth = 0; depth < PdfLimits.MaxPageTreeDepth; depth++)
        {
            if (edits.Peek(leafId) is not { } node)
            {
                return DescendResult.Unsupported;
            }

            var kidsRaw = node.GetRaw(KnownName.Kids);
            if (kidsRaw.IsNull)
            {
                return DescendResult.Leaf;
            }

            if (kidsRaw.AsArray() is not { Count: > 0 } kids || node.ContainsKey(KnownName.Names) || node.ContainsKey(KnownName.Nums))
            {
                return DescendResult.Unsupported;
            }

            var chosen = ChooseKid(edits, kids, key, numeric, exact, out var index);
            if (chosen != DescendResult.Leaf)
            {
                return chosen;
            }

            path.Add(new(leafId, index));
            leafId = kids.GetRaw(index).AsReference();
        }

        return DescendResult.Unsupported;
    }

    /// <summary>Picks the kid whose limits cover a key, or for an insert the first kid that reaches it or else the last.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="kids">The /Kids array.</param>
    /// <param name="key">The key.</param>
    /// <param name="numeric">Whether keys are integers.</param>
    /// <param name="exact">Whether the key must lie within the chosen kid's limits.</param>
    /// <param name="index">Receives the kid's position.</param>
    /// <returns>Whether a kid was chosen, none covers the key (a lookup), or a kid cannot be used.</returns>
    private static DescendResult ChooseKid(PdfObjectEdits edits, PdfArray kids, PdfValue key, bool numeric, bool exact, out int index)
    {
        var low = 0;
        var high = kids.Count - 1;
        index = kids.Count - 1;
        while (low <= high)
        {
            var middle = (low + high) >>> 1;
            if (!TryGetKidLimits(edits, kids, middle, out _, out var upper))
            {
                return DescendResult.Unsupported;
            }

            if (Compare(upper, key, numeric) >= 0)
            {
                index = middle;
                high = middle - 1;
            }
            else
            {
                low = middle + 1;
            }
        }

        if (!TryGetKidLimits(edits, kids, index, out var lower, out var highest))
        {
            return DescendResult.Unsupported;
        }

        return !exact || (Compare(lower, key, numeric) <= 0 && Compare(highest, key, numeric) >= 0) ? DescendResult.Leaf : DescendResult.NotCovered;
    }

    /// <summary>Reads the limits of a kid.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="kids">The /Kids array.</param>
    /// <param name="index">The kid.</param>
    /// <param name="lower">Receives the lowest key.</param>
    /// <param name="upper">Receives the highest key.</param>
    /// <returns><see langword="false"/> when the kid is not an indirect dictionary with limits.</returns>
    private static bool TryGetKidLimits(PdfObjectEdits edits, PdfArray kids, int index, out PdfValue lower, out PdfValue upper)
    {
        lower = default;
        upper = default;
        var raw = kids.GetRaw(index);
        if (!raw.IsReference || edits.Peek(raw.AsReference()) is not { } kid || kid.GetRaw(KnownName.Limits).AsArray() is not { Count: >= PairLength } limits)
        {
            return false;
        }

        lower = limits.Get(LowerLimit);
        upper = limits.Get(UpperLimit);
        return true;
    }

    /// <summary>Splits full nodes from the leaf up to the root.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="path">The nodes above the leaf.</param>
    /// <param name="leafId">The leaf.</param>
    /// <param name="leafKey">/Names or /Nums.</param>
    private static void SplitWhileFull(PdfObjectEdits edits, List<PathStep> path, PdfObjectId leafId, KnownName leafKey)
    {
        var id = leafId;
        var level = path.Count;
        var isLeaf = true;
        while (true)
        {
            var node = edits.Edit(id)!;
            var key = isLeaf ? leafKey : KnownName.Kids;
            var items = node.GetRaw(key).AsArray()!;
            var capacity = isLeaf ? MaxNodeEntries * PairLength : MaxNodeEntries;
            if (items.Count <= capacity)
            {
                return;
            }

            var right = SplitOff(edits, node, key, isLeaf);
            var rightId = edits.Add(right);
            if (level == 0)
            {
                PushDownRoot(edits, node, rightId, key);
                return;
            }

            level--;
            var parent = edits.Edit(path[level].Id)!;
            var kids = parent.GetRaw(KnownName.Kids).AsArray()!.Clone();
            kids.Insert(path[level].KidIndex + 1, PdfValue.FromReference(rightId));
            parent.Set(KnownName.Kids, PdfValue.FromArray(kids));
            SetLimits(edits, node, key, isLeaf);
            id = path[level].Id;
            isLeaf = false;
        }
    }

    /// <summary>Moves the upper half of a node's items into a new node.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="node">The node, which keeps the lower half.</param>
    /// <param name="key">The array key: /Names, /Nums or /Kids.</param>
    /// <param name="isLeaf">Whether the node is a leaf.</param>
    /// <returns>The new node.</returns>
    private static PdfDictionary SplitOff(PdfObjectEdits edits, PdfDictionary node, KnownName key, bool isLeaf)
    {
        var items = node.GetRaw(key).AsArray()!;
        var unit = isLeaf ? PairLength : 1;
        var keep = (items.Count / unit / PairLength) * unit;
        var left = new PdfArray(edits.Store, keep);
        var right = new PdfArray(edits.Store, items.Count - keep);
        for (var i = 0; i < items.Count; i++)
        {
            (i < keep ? left : right).Add(items.GetRaw(i));
        }

        node.Set(key, PdfValue.FromArray(left));
        var created = new PdfDictionary(edits.Store);
        created.Set(key, PdfValue.FromArray(right));
        SetLimits(edits, created, key, isLeaf);
        return created;
    }

    /// <summary>Gives a split root two new children, so the root keeps its object number.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="root">The root, holding the lower half.</param>
    /// <param name="rightId">The new node with the upper half.</param>
    /// <param name="key">The array key the root split on.</param>
    private static void PushDownRoot(PdfObjectEdits edits, PdfDictionary root, PdfObjectId rightId, KnownName key)
    {
        var left = new PdfDictionary(edits.Store);
        left.Set(key, root.GetRaw(key));
        SetLimits(edits, left, key, key != KnownName.Kids);
        var leftId = edits.Add(left);
        _ = root.Remove(key);
        _ = root.Remove(KnownName.Limits);
        root.Set(KnownName.Kids, PdfValue.FromArray(new(edits.Store, [PdfValue.FromReference(leftId), PdfValue.FromReference(rightId)])));
    }

    /// <summary>Sets a node's /Limits from its first and last items.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="node">The node.</param>
    /// <param name="key">The array key: /Names, /Nums or /Kids.</param>
    /// <param name="isLeaf">Whether the node is a leaf.</param>
    private static void SetLimits(PdfObjectEdits edits, PdfDictionary node, KnownName key, bool isLeaf)
    {
        var items = node.GetRaw(key).AsArray()!;
        if (items.Count == 0)
        {
            _ = node.Remove(KnownName.Limits);
            return;
        }

        if (isLeaf)
        {
            node.Set(KnownName.Limits, PdfValue.FromArray(new(edits.Store, [items.Get(0), items.Get(items.Count - PairLength)])));
            return;
        }

        if (TryGetKidLimits(edits, items, 0, out var lower, out _) && TryGetKidLimits(edits, items, items.Count - 1, out _, out var upper))
        {
            node.Set(KnownName.Limits, PdfValue.FromArray(new(edits.Store, [lower, upper])));
        }
    }

    /// <summary>Brings the limits of the nodes on a path back in line with what they hold, from the leaf up to the root's kid.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="path">The nodes above the leaf; the first is the root, which has no limits.</param>
    /// <param name="leafId">The leaf, or an invalid id when it was deleted.</param>
    /// <param name="leafKey">/Names or /Nums.</param>
    private static void RefreshLimits(PdfObjectEdits edits, List<PathStep> path, PdfObjectId leafId, KnownName leafKey)
    {
        if (path.Count > 0 && leafId.IsValid)
        {
            RefreshNode(edits, leafId, leafKey);
        }

        for (var i = path.Count - 1; i >= 1; i--)
        {
            RefreshNode(edits, path[i].Id, leafKey);
        }
    }

    /// <summary>Sets a node's /Limits from what it holds, writing the node only when they differ.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="id">The node.</param>
    /// <param name="leafKey">/Names or /Nums.</param>
    private static void RefreshNode(PdfObjectEdits edits, PdfObjectId id, KnownName leafKey)
    {
        if (edits.Peek(id) is not { } node)
        {
            return;
        }

        var isLeaf = node.GetRaw(leafKey).Kind == PdfKind.Array;
        var copy = node.Clone();
        SetLimits(edits, copy, isLeaf ? leafKey : KnownName.Kids, isLeaf);
        if (!SameLimits(node.GetRaw(KnownName.Limits).AsArray(), copy.GetRaw(KnownName.Limits).AsArray()))
        {
            edits.Edit(id)!.Set(KnownName.Limits, copy.GetRaw(KnownName.Limits));
        }
    }

    /// <summary>Compares two /Limits arrays.</summary>
    /// <param name="left">The first array, or <see langword="null"/>.</param>
    /// <param name="right">The second array, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when both hold the same two keys.</returns>
    private static bool SameLimits(PdfArray? left, PdfArray? right) =>
        left is not null && right is not null && left.Count == right.Count && left.Items.SequenceEqual(right.Items);

    /// <summary>Removes nodes a removal left empty, fixes the limits above, and folds a root with a single kid.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="path">The nodes above the leaf.</param>
    /// <param name="leafId">The leaf.</param>
    /// <param name="leafEmpty">Whether the leaf lost its last entry.</param>
    /// <param name="leafKey">/Names or /Nums.</param>
    private static void AfterRemove(PdfObjectEdits edits, List<PathStep> path, PdfObjectId leafId, bool leafEmpty, KnownName leafKey)
    {
        if (!leafEmpty || path.Count == 0)
        {
            RefreshLimits(edits, path, leafId, leafKey);
            return;
        }

        var emptyId = leafId;
        var level = path.Count - 1;
        while (level >= 0)
        {
            var parent = edits.Edit(path[level].Id)!;
            var kids = parent.GetRaw(KnownName.Kids).AsArray()!.Clone();
            kids.RemoveAt(path[level].KidIndex);
            parent.Set(KnownName.Kids, PdfValue.FromArray(kids));
            edits.Delete(emptyId);
            if (kids.Count > 0 || level == 0)
            {
                break;
            }

            emptyId = path[level].Id;
            level--;
        }

        path.RemoveRange(level + 1, path.Count - level - 1);
        if (edits.Peek(path[0].Id)?.GetRaw(KnownName.Kids).AsArray() is { Count: 0 })
        {
            // A root left with no kids is a leaf with no entries.
            var root = edits.Edit(path[0].Id)!;
            _ = root.Remove(KnownName.Kids);
            root.Set(leafKey, PdfValue.FromArray(new(edits.Store)));
        }

        RefreshLimits(edits, path, default, leafKey);
        FoldRoot(edits, path[0].Id, leafKey);
    }

    /// <summary>Folds a root with a single kid into the root, so a tree that shrank loses its extra level.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="rootId">The root.</param>
    /// <param name="leafKey">/Names or /Nums.</param>
    private static void FoldRoot(PdfObjectEdits edits, PdfObjectId rootId, KnownName leafKey)
    {
        if (edits.Peek(rootId)?.GetRaw(KnownName.Kids).AsArray() is not { Count: 1 } kids)
        {
            return;
        }

        var kidId = kids.GetRaw(0).AsReference();
        if (!kidId.IsValid || edits.Peek(kidId) is not { } kid)
        {
            return;
        }

        var isLeaf = kid.GetRaw(leafKey).Kind == PdfKind.Array;
        var items = isLeaf ? kid.GetRaw(leafKey).AsArray() : kid.GetRaw(KnownName.Kids).AsArray();
        if (items is null || (isLeaf && items.Count > MaxNodeEntries * PairLength))
        {
            return;
        }

        var root = edits.Edit(rootId)!;
        _ = root.Remove(KnownName.Kids);
        root.Set(isLeaf ? leafKey : KnownName.Kids, PdfValue.FromArray(items));
        edits.Delete(kidId);
    }

    /// <summary>A node on the way down, and the kid taken.</summary>
    /// <param name="Id">The node.</param>
    /// <param name="KidIndex">The position of the kid taken in the node's /Kids.</param>
    internal readonly record struct PathStep(PdfObjectId Id, int KidIndex);

    /// <summary>A written node and its limits.</summary>
    /// <param name="Id">The node.</param>
    /// <param name="First">The lowest key.</param>
    /// <param name="Last">The highest key.</param>
    private readonly record struct TreeNode(PdfObjectId Id, PdfValue First, PdfValue Last);

    /// <summary>An entry waiting to be sorted, with its place in the input.</summary>
    /// <param name="Entry">The entry.</param>
    /// <param name="Order">The position in the input, so a later duplicate wins.</param>
    private readonly record struct SortItem(NameTreeEntry Entry, int Order);
}
