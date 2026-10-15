// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// The parent tree of a document's structure tree (ISO 32000-2 14.7.4.4) held in memory while it is edited: entries are
/// removed, changed or added by key, and <see cref="Write"/> stores the whole tree again as one node, or as leaves of a
/// few dozen entries under one root when it is large.
/// </summary>
[DebuggerDisplay("PdfParentTree: {_entries.Count} entries")]
internal sealed class PdfParentTree
{
    /// <summary>The most key and value pairs written to one leaf.</summary>
    private const int LeafPairs = 64;

    /// <summary>The numbers in a pair: a key and a value.</summary>
    private const int PairLength = 2;

    /// <summary>The entries by key, values as stored.</summary>
    private readonly SortedDictionary<int, PdfValue> _entries = [];

    /// <summary>The objects of the old tree's nodes below the root, deleted when the tree is written again.</summary>
    private readonly List<PdfObjectId> _oldNodes = [];

    /// <summary>The transaction the edit belongs to.</summary>
    private readonly PdfEditTransaction _transaction;

    /// <summary>The document's objects.</summary>
    private readonly PdfObjectStore _store;

    /// <summary>The structure tree root as stored, to be changed when the tree is written.</summary>
    private readonly PdfDictionary _root;

    /// <summary>The structure tree root's object, or an invalid id when the catalog holds it directly.</summary>
    private readonly PdfObjectId _rootId;

    /// <summary>The value of the root's /ParentTree as stored.</summary>
    private readonly PdfValue _treeValue;

    /// <summary>The next unused key.</summary>
    private int _nextKey;

    /// <summary>Initializes a new instance of the <see cref="PdfParentTree"/> class.</summary>
    /// <param name="transaction">The transaction the edit belongs to.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="root">The structure tree root.</param>
    /// <param name="rootId">The root's object, or an invalid id.</param>
    private PdfParentTree(PdfEditTransaction transaction, PdfObjectStore store, PdfDictionary root, PdfObjectId rootId)
    {
        _transaction = transaction;
        _store = store;
        _root = root;
        _rootId = rootId;
        _treeValue = root.GetRaw(KnownName.ParentTree);
        _nextKey = root.GetInt32(KnownName.ParentTreeNextKey, 0);
    }

    /// <summary>Reads the parent tree of a tagged document.</summary>
    /// <param name="transaction">The transaction the edit belongs to.</param>
    /// <param name="store">The document's objects.</param>
    /// <returns>The tree, or <see langword="null"/> when the document has no structure tree root.</returns>
    internal static PdfParentTree? Load(PdfEditTransaction transaction, PdfObjectStore store)
    {
        if (store.Catalog.GetDictionary(KnownName.StructTreeRoot) is not { } root)
        {
            return null;
        }

        var tree = new PdfParentTree(transaction, store, root, store.Catalog.GetRaw(KnownName.StructTreeRoot).AsReference());
        var entries = new List<NameTreeEntry>();
        NameTree.EnumerateNumbersRaw(root.GetDictionary(KnownName.ParentTree), entries);
        foreach (var (key, value) in entries)
        {
            var number = (int)key.AsInteger();
            tree._entries[number] = value;
            tree._nextKey = Math.Max(tree._nextKey, number + 1);
        }

        tree.CollectNodes(tree._treeValue, 0);
        return tree;
    }

    /// <summary>Gets an entry as stored.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The value, or null when there is none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal PdfValue Get(int key) => _entries.GetValueOrDefault(key);

    /// <summary>Removes an entry.</summary>
    /// <param name="key">The key.</param>
    internal void Remove(int key) => _ = _entries.Remove(key);

    /// <summary>Sets an entry.</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    internal void Set(int key, PdfValue value) => _entries[key] = value;

    /// <summary>Forgets structure elements that were deleted: an entry for an object that named one goes, and a slot of a page's array that named one is cleared.</summary>
    /// <param name="elements">The object numbers of the deleted elements.</param>
    internal void RemoveElements(HashSet<int> elements)
    {
        // The entries are changed while they are walked, so the keys are copied first.
        var keys = new List<int>(_entries.Keys);
        foreach (var key in keys)
        {
            var value = _entries[key];
            if (value.IsReference && elements.Contains(value.AsReference().Number))
            {
                _ = _entries.Remove(key);
            }
            else
            {
                ClearSlots(key, value, static (_, slot, deleted) => slot.IsReference && deleted.Contains(slot.AsReference().Number), elements);
            }
        }
    }

    /// <summary>Clears the slots of one page's array, so marked content ids that no longer exist name no element.</summary>
    /// <param name="key">The page's <c>/StructParents</c> key.</param>
    /// <param name="mcids">The marked content ids that are gone.</param>
    internal void ClearMarkedContent(int key, HashSet<int> mcids)
    {
        if (!_entries.TryGetValue(key, out var value))
        {
            return;
        }

        ClearSlots(key, value, static (index, _, gone) => gone.Contains(index), mcids);
    }

    /// <summary>Takes the next unused key.</summary>
    /// <returns>The key.</returns>
    internal int TakeKey()
    {
        var key = _nextKey;
        _nextKey++;
        return key;
    }

    /// <summary>Stores the tree again and updates the root's /ParentTreeNextKey.</summary>
    internal void Write()
    {
        var tree = BuildTree();
        var root = _root.Clone();
        if (_treeValue.IsReference)
        {
            _transaction.Replace(_treeValue.AsReference(), PdfValue.FromDictionary(tree));
        }
        else
        {
            root.Set(KnownName.ParentTree, PdfValue.FromDictionary(tree));
        }

        root.Set(KnownName.ParentTreeNextKey, PdfValue.FromInteger(_nextKey));
        if (_rootId.IsValid)
        {
            _transaction.Replace(_rootId, PdfValue.FromDictionary(root));
        }
        else
        {
            var catalog = _store.Catalog.Clone();
            catalog.Set(KnownName.StructTreeRoot, PdfValue.FromDictionary(root));
            PdfPageTreeWriter.ReplaceCatalog(_transaction, _store, catalog);
        }

        foreach (var node in _oldNodes)
        {
            _transaction.Delete(node);
        }
    }

    /// <summary>Clears the slots of an entry's array that a test selects, writing the array back where it was stored.</summary>
    /// <typeparam name="TState">The type of the state the test reads.</typeparam>
    /// <param name="key">The entry's key.</param>
    /// <param name="value">The entry as stored.</param>
    /// <param name="selects">Decides whether a slot is cleared, from its index, its raw value and the state.</param>
    /// <param name="state">The state the test reads.</param>
    private void ClearSlots<TState>(int key, PdfValue value, Func<int, PdfValue, TState, bool> selects, TState state)
    {
        if (StoreReading.Resolve(_store, value).AsArray() is not { } array)
        {
            return;
        }

        PdfArray? copy = null;
        for (var i = 0; i < array.Count; i++)
        {
            if (array.GetRaw(i).IsNull || !selects(i, array.GetRaw(i), state))
            {
                continue;
            }

            copy ??= array.Clone();
            copy.SetAt(i, default);
        }

        if (copy is null)
        {
            return;
        }

        if (value.IsReference)
        {
            _transaction.Replace(value.AsReference(), PdfValue.FromArray(copy));
        }
        else
        {
            _entries[key] = PdfValue.FromArray(copy);
        }
    }

    /// <summary>Remembers the objects of the old tree's nodes below its root.</summary>
    /// <param name="node">The node as stored.</param>
    /// <param name="depth">The nesting depth.</param>
    private void CollectNodes(PdfValue node, int depth)
    {
        if (depth > PdfLimits.MaxPageTreeDepth || StoreReading.Resolve(_store, node).AsDictionary()?.GetArray(KnownName.Kids) is not { } kids)
        {
            return;
        }

        for (var i = 0; i < kids.Count; i++)
        {
            var raw = kids.GetRaw(i);
            if (raw.IsReference)
            {
                _oldNodes.Add(raw.AsReference());
            }

            CollectNodes(raw, depth + 1);
        }
    }

    /// <summary>Builds the tree's root node.</summary>
    /// <returns>A node with the entries, or with leaves that hold them.</returns>
    private PdfDictionary BuildTree()
    {
        var node = new PdfDictionary(_store);
        if (_entries.Count <= LeafPairs)
        {
            node.Set(KnownName.Nums, PdfValue.FromArray(BuildPairs(_entries.AsEnumerable(), _entries.Count)));
            return node;
        }

        var kids = new PdfArray(_store);
        foreach (var chunk in _entries.Chunk(LeafPairs))
        {
            var leaf = new PdfDictionary(_store);
            PdfArray limits = new(_store, [PdfValue.FromInteger(chunk[0].Key), PdfValue.FromInteger(chunk[^1].Key)]);
            leaf.Set(KnownName.Limits, PdfValue.FromArray(limits));
            leaf.Set(KnownName.Nums, PdfValue.FromArray(BuildPairs(chunk, chunk.Length)));
            kids.Add(PdfValue.FromReference(_transaction.Add(PdfValue.FromDictionary(leaf))));
        }

        node.Set(KnownName.Kids, PdfValue.FromArray(kids));
        return node;
    }

    /// <summary>Builds a /Nums array.</summary>
    /// <param name="entries">The entries, in key order.</param>
    /// <param name="count">The number of entries.</param>
    /// <returns>The array.</returns>
    private PdfArray BuildPairs(IEnumerable<KeyValuePair<int, PdfValue>> entries, int count)
    {
        var numbers = new PdfArray(_store, count * PairLength);
        foreach (var (key, value) in entries)
        {
            numbers.Add(PdfValue.FromInteger(key));
            numbers.Add(value);
        }

        return numbers;
    }
}
