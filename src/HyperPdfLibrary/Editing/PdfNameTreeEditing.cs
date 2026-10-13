// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>Adds, removes and rebuilds the entries of a name tree held in the catalog's <c>/Names</c> dictionary.</summary>
internal static class PdfNameTreeEditing
{
    /// <summary>Adds an entry, or replaces the value of an existing name.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="tree">The key of the tree in <c>/Names</c>.</param>
    /// <param name="key">The name, as a string value.</param>
    /// <param name="value">The value, as it is stored.</param>
    /// <param name="cancellationToken">Cancels a rebuild.</param>
    internal static void Set(PdfObjectEdits edits, PdfName tree, PdfValue key, PdfValue value, CancellationToken cancellationToken)
    {
        var raw = edits.NamesForRead()?.GetRaw(tree) ?? default;
        if (raw.IsReference && PdfTreeEditing.TryInsert(edits, raw.AsReference(), key, value, false))
        {
            return;
        }

        _ = Rebuild(edits, tree, [new(key, value)], null, cancellationToken);
    }

    /// <summary>Adds several entries, replacing the values of names the tree already has.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="tree">The key of the tree in <c>/Names</c>.</param>
    /// <param name="added">The entries.</param>
    /// <param name="cancellationToken">Cancels the rebuild.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetMany(PdfObjectEdits edits, PdfName tree, List<NameTreeEntry> added, CancellationToken cancellationToken) =>
        Rebuild(edits, tree, added, null, cancellationToken);

    /// <summary>Removes an entry.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="tree">The key of the tree in <c>/Names</c>.</param>
    /// <param name="key">The name, as a string value.</param>
    /// <param name="cancellationToken">Cancels a rebuild.</param>
    /// <returns><see langword="true"/> when the name was in the tree.</returns>
    internal static bool Remove(PdfObjectEdits edits, PdfName tree, PdfValue key, CancellationToken cancellationToken)
    {
        var raw = edits.NamesForRead()?.GetRaw(tree) ?? default;
        if (raw.IsReference)
        {
            switch (PdfTreeEditing.TryRemove(edits, raw.AsReference(), key, false))
            {
                case PdfTreeEditing.TreeEditResult.Done:
                    {
                        DropIfEmpty(edits, tree, raw.AsReference());
                        return true;
                    }

                case PdfTreeEditing.TreeEditResult.NotFound:
                    {
                        return false;
                    }

                default:
                    {
                        break;
                    }
            }
        }

        return Rebuild(edits, tree, [], key, cancellationToken);
    }

    /// <summary>Rebuilds a tree from its entries, so every node is full to the same degree and every node has correct limits.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="tree">The key of the tree in <c>/Names</c>.</param>
    /// <param name="cancellationToken">Cancels the rebuild.</param>
    internal static void Rebalance(PdfObjectEdits edits, PdfName tree, CancellationToken cancellationToken) =>
        _ = Rebuild(edits, tree, [], null, cancellationToken);

    /// <summary>Reads the entries of a name tree as the edit currently has it.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="tree">The key of the tree in <c>/Names</c>.</param>
    /// <returns>The entries in key order; values are as stored.</returns>
    internal static List<NameTreeEntry> Read(PdfObjectEdits edits, PdfName tree)
    {
        var entries = new List<NameTreeEntry>();
        NameTree.EnumerateRaw(GetRoot(edits, tree, out _), entries);
        return entries;
    }

    /// <summary>Finds the root dictionary of a tree.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="tree">The key of the tree in <c>/Names</c>.</param>
    /// <param name="raw">Receives the value of the key in <c>/Names</c>.</param>
    /// <returns>The root, or <see langword="null"/> when the document has no such tree.</returns>
    private static PdfDictionary? GetRoot(PdfObjectEdits edits, PdfName tree, out PdfValue raw)
    {
        raw = edits.NamesForRead()?.GetRaw(tree) ?? default;
        return raw.IsReference ? edits.Peek(raw.AsReference()) : raw.AsDictionary();
    }

    /// <summary>Rebuilds a tree from every entry, with entries added and one name removed.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="tree">The key of the tree in <c>/Names</c>.</param>
    /// <param name="added">The entries to add.</param>
    /// <param name="removed">The name to remove, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the rebuild.</param>
    /// <returns><see langword="true"/> when the name to remove was in the tree, or none was asked for.</returns>
    private static bool Rebuild(PdfObjectEdits edits, PdfName tree, List<NameTreeEntry> added, PdfValue? removed, CancellationToken cancellationToken)
    {
        // The readers see the store, so write the pending changes before reading the tree.
        edits.Flush();
        var root = GetRoot(edits, tree, out var raw);
        var entries = new List<NameTreeEntry>();
        NameTree.EnumerateRaw(root, entries);
        var oldKids = PdfTreeEditing.CollectKidIds(edits.Store, root);
        var found = true;
        if (removed is { } name)
        {
            found = entries.RemoveAll(entry => entry.Key.AsStringBytes().SequenceEqual(name.AsStringBytes())) > 0;
        }

        entries.AddRange(added);
        PdfTreeEditing.SortAndDeduplicate(entries, false, cancellationToken);
        foreach (var kid in oldKids)
        {
            edits.Delete(kid);
        }

        var rootId = raw.IsReference ? raw.AsReference() : default;
        if (entries.Count == 0)
        {
            RemoveTree(edits, tree, rootId);
            return found;
        }

        var builtId = PdfTreeEditing.Build(edits, entries, false, rootId, cancellationToken);
        if (!raw.IsReference || builtId != rootId)
        {
            edits.Names().Set(tree, PdfValue.FromReference(builtId));
        }

        return found;
    }

    /// <summary>Removes a tree that has no entries left.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="tree">The key of the tree in <c>/Names</c>.</param>
    /// <param name="rootId">The root's object, or an invalid id when the root is direct.</param>
    private static void RemoveTree(PdfObjectEdits edits, PdfName tree, PdfObjectId rootId)
    {
        if (rootId.IsValid)
        {
            edits.Delete(rootId);
        }

        if (edits.NamesForRead() is null)
        {
            return;
        }

        var names = edits.Names();
        _ = names.Remove(tree);
        if (names.Count == 0)
        {
            _ = edits.Catalog().Remove(KnownName.Names);
        }
    }

    /// <summary>Removes the tree when a removal left it empty.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="tree">The key of the tree in <c>/Names</c>.</param>
    /// <param name="rootId">The root.</param>
    private static void DropIfEmpty(PdfObjectEdits edits, PdfName tree, PdfObjectId rootId)
    {
        if (edits.Peek(rootId) is { } root && !root.ContainsKey(KnownName.Kids) && (root.GetRaw(KnownName.Names).AsArray()?.Count ?? 0) == 0)
        {
            RemoveTree(edits, tree, rootId);
        }
    }
}
