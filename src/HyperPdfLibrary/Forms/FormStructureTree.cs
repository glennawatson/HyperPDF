// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>Reads and rewrites the parts of a structure tree that form widgets touch: the kids of elements and the parent tree number tree (ISO 32000-2 14.7.2 and 14.7.4.4).</summary>
internal static class FormStructureTree
{
    /// <summary>The items of a <c>/Nums</c> key and value pair.</summary>
    private const int PairLength = 2;

    /// <summary>Gets the structure type of the single element most tagged files wrap their content in.</summary>
    private static ReadOnlySpan<byte> DocumentType => "Document"u8;

    /// <summary>Gets the first parent tree key no entry uses.</summary>
    /// <param name="root">The structure tree root.</param>
    /// <returns>The key.</returns>
    internal static int NextKey(PdfDictionary root)
    {
        var next = root.GetInt32(KnownName.ParentTreeNextKey, 0);
        var entries = new List<NameTreeEntry>();
        NameTree.EnumerateNumbers(root.GetDictionary(KnownName.ParentTree), entries);
        foreach (var entry in entries)
        {
            next = (int)Math.Max(next, entry.Key.AsInteger() + 1);
        }

        return next;
    }

    /// <summary>Finds the element new <c>Form</c> elements go under: the document element when the tree has exactly one, otherwise the root.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="root">The structure tree root.</param>
    /// <param name="rootId">The root's object.</param>
    /// <returns>The container.</returns>
    internal static StructContainer FindContainer(PdfObjectStore store, PdfDictionary root, PdfObjectId rootId)
    {
        var raw = root.GetRaw(KnownName.K);
        var resolved = StoreReading.Resolve(store, raw);
        var only = resolved.AsArray() is { Count: 1 } single ? single.GetRaw(0) : raw;
        var id = only.AsReference();
        var element = id.IsValid ? StoreReading.GetDictionary(store, id) : null;
        return element is not null && IsDocumentElement(store, element) ? new(id, element) : new(rootId, root);
    }

    /// <summary>Adds kids to the end of an element's or root's <c>/K</c>.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="target">The dictionary being rewritten; changed unless its <c>/K</c> is an array object of its own.</param>
    /// <param name="kids">The kids to add.</param>
    internal static void AppendKids(PdfEditTransaction transaction, PdfObjectStore store, PdfDictionary target, List<PdfValue> kids)
    {
        var raw = target.GetRaw(KnownName.K);
        var existing = StoreReading.Resolve(store, raw).AsArray();
        var array = existing?.Clone() ?? new PdfArray(store, kids.Count + 1);
        if (existing is null && !raw.IsNull)
        {
            array.Add(raw);
        }

        foreach (var kid in kids)
        {
            array.Add(kid);
        }

        if (existing is not null && raw.IsReference)
        {
            transaction.Replace(raw.AsReference(), PdfValue.FromArray(array));
            return;
        }

        target.Set(KnownName.K, PdfValue.FromArray(array));
    }

    /// <summary>Adds entries to the end of the parent tree. The new keys are above every key in it.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="tree">The tree as the root holds it: a reference, a direct node or null.</param>
    /// <param name="entries">The entries, in key order.</param>
    /// <returns>The tree value for the root.</returns>
    internal static PdfValue AppendEntries(PdfEditTransaction transaction, PdfObjectStore store, PdfValue tree, List<ParentEntry> entries)
    {
        var node = StoreReading.Resolve(store, tree).AsDictionary()?.Clone() ?? new PdfDictionary(store);
        if (node.GetArray(KnownName.Kids) is { } kids)
        {
            var limits = new PdfArray(store, [PdfValue.FromInteger(entries[0].Key), PdfValue.FromInteger(entries[^1].Key)]);
            var leaf = new PdfDictionary(store);
            leaf.Set(KnownName.Limits, PdfValue.FromArray(limits));
            leaf.Set(KnownName.Nums, PdfValue.FromArray(AppendPairs(store, null, entries)));
            var extended = kids.Clone();
            extended.Add(PdfValue.FromReference(transaction.Add(PdfValue.FromDictionary(leaf))));
            node.Set(KnownName.Kids, PdfValue.FromArray(extended));
        }
        else
        {
            node.Set(KnownName.Nums, PdfValue.FromArray(AppendPairs(store, node.GetArray(KnownName.Nums), entries)));
        }

        if (!tree.IsReference)
        {
            return PdfValue.FromDictionary(node);
        }

        transaction.Replace(tree.AsReference(), PdfValue.FromDictionary(node));
        return tree;
    }

    /// <summary>Takes entries out of a number tree.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="node">The node as its parent holds it: a reference or a direct dictionary.</param>
    /// <param name="keys">The keys to remove.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The value the parent should hold; the same value when it did not change.</returns>
    internal static PdfValue RemoveEntries(PdfEditTransaction transaction, PdfObjectStore store, PdfValue node, HashSet<int> keys, int depth)
    {
        if (depth >= PdfLimits.MaxNesting || StoreReading.Resolve(store, node).AsDictionary() is not { } dictionary)
        {
            return node;
        }

        PdfDictionary? changed = null;
        if (dictionary.GetArray(KnownName.Nums) is { } numbers && FilterPairs(store, numbers, keys) is { } filtered)
        {
            changed = dictionary.Clone();
            changed.Set(KnownName.Nums, PdfValue.FromArray(filtered));
            SetLimits(store, changed, filtered);
        }
        else if (dictionary.GetArray(KnownName.Kids) is { } kids && RewriteKids(transaction, store, kids, keys, depth) is { } rewritten)
        {
            changed = dictionary.Clone();
            changed.Set(KnownName.Kids, PdfValue.FromArray(rewritten));
        }

        if (changed is null)
        {
            return node;
        }

        if (!node.IsReference)
        {
            return PdfValue.FromDictionary(changed);
        }

        transaction.Replace(node.AsReference(), PdfValue.FromDictionary(changed));
        return node;
    }

    /// <summary>Removes the kids of an element that refer to some objects, or the element itself from its parent.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="kidsValue">The element's <c>/K</c> as stored.</param>
    /// <param name="gone">The object numbers whose references go: widgets (through their <c>/OBJR</c>) or child elements.</param>
    /// <returns>The new <c>/K</c> array; <see langword="null"/> when nothing in it was removed.</returns>
    internal static PdfArray? RemoveKids(PdfObjectStore store, PdfValue kidsValue, HashSet<int> gone)
    {
        if (StoreReading.Resolve(store, kidsValue).AsArray() is not { } array)
        {
            return IsGone(store, kidsValue, gone) ? new PdfArray(store) : null;
        }

        PdfArray? kept = null;
        for (var i = 0; i < array.Count; i++)
        {
            var item = array.GetRaw(i);
            if (IsGone(store, item, gone))
            {
                kept ??= CopyPrefix(store, array, i);
                continue;
            }

            kept?.Add(item);
        }

        return kept;
    }

    /// <summary>Copies the first items of an array into a new array with room for all of them.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="source">The array.</param>
    /// <param name="count">The number of items to copy.</param>
    /// <returns>The new array.</returns>
    private static PdfArray CopyPrefix(PdfObjectStore store, PdfArray source, int count)
    {
        var copy = new PdfArray(store, source.Count);
        for (var i = 0; i < count; i++)
        {
            copy.Add(source.GetRaw(i));
        }

        return copy;
    }

    /// <summary>Determines whether a kid refers to one of the removed objects.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="kid">The kid as stored: an element reference, or an object reference dictionary.</param>
    /// <param name="gone">The object numbers.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool IsGone(PdfObjectStore store, PdfValue kid, HashSet<int> gone)
    {
        if (kid.IsReference && gone.Contains(kid.AsReference().Number))
        {
            return true;
        }

        var dictionary = StoreReading.Resolve(store, kid).AsDictionary();
        return dictionary is not null
            && dictionary.IsName(KnownName.Type, KnownName.OBJR)
            && dictionary.GetRaw(KnownName.Obj).AsReference() is { IsValid: true } target
            && gone.Contains(target.Number);
    }

    /// <summary>Determines whether an element is the document element: the one that wraps a tagged file's content.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="element">The element.</param>
    /// <returns><see langword="true"/> when its type is <c>Document</c>.</returns>
    private static bool IsDocumentElement(PdfObjectStore store, PdfDictionary element) =>
        element.GetName(KnownName.S) is { IsNone: false } type && store.Names.GetSpelling(type).SequenceEqual(DocumentType);

    /// <summary>Copies a <c>/Nums</c> array and adds key and value pairs to its end.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="existing">The current array, or null.</param>
    /// <param name="entries">The entries to add.</param>
    /// <returns>The new array.</returns>
    private static PdfArray AppendPairs(PdfObjectStore store, PdfArray? existing, List<ParentEntry> entries)
    {
        var numbers = existing?.Clone() ?? new PdfArray(store, entries.Count * PairLength);
        foreach (var (key, value) in entries)
        {
            numbers.Add(PdfValue.FromInteger(key));
            numbers.Add(value);
        }

        return numbers;
    }

    /// <summary>Copies a <c>/Nums</c> array without some keys.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="numbers">The array.</param>
    /// <param name="keys">The keys to leave out.</param>
    /// <returns>The new array; <see langword="null"/> when no key was in the array.</returns>
    private static PdfArray? FilterPairs(PdfObjectStore store, PdfArray numbers, HashSet<int> keys)
    {
        PdfArray? kept = null;
        for (var i = 0; i + 1 < numbers.Count; i += PairLength)
        {
            if (numbers.Get(i).IsNumber && keys.Contains(numbers.GetInt32(i)))
            {
                kept ??= CopyPrefix(store, numbers, i);
                continue;
            }

            kept?.Add(numbers.GetRaw(i));
            kept?.Add(numbers.GetRaw(i + 1));
        }

        return kept;
    }

    /// <summary>Rewrites the limits of a leaf to match its remaining keys.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="node">The leaf, changed in place.</param>
    /// <param name="numbers">The leaf's remaining pairs.</param>
    private static void SetLimits(PdfObjectStore store, PdfDictionary node, PdfArray numbers)
    {
        if (!node.ContainsKey(KnownName.Limits))
        {
            return;
        }

        if (numbers.Count < PairLength)
        {
            _ = node.Remove(KnownName.Limits);
            return;
        }

        node.Set(KnownName.Limits, PdfValue.FromArray(new(store, [numbers.Get(0), numbers.Get(numbers.Count - PairLength)])));
    }

    /// <summary>Takes entries out of the child nodes of a number tree node.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="kids">The node's kids.</param>
    /// <param name="keys">The keys to remove.</param>
    /// <param name="depth">The node's nesting depth.</param>
    /// <returns>The new kids array; <see langword="null"/> when no kid value changed (indirect kids change in place).</returns>
    private static PdfArray? RewriteKids(PdfEditTransaction transaction, PdfObjectStore store, PdfArray kids, HashSet<int> keys, int depth)
    {
        PdfArray? rewritten = null;
        for (var i = 0; i < kids.Count; i++)
        {
            var kid = kids.GetRaw(i);
            var next = RemoveEntries(transaction, store, kid, keys, depth + 1);
            if (next.Equals(kid))
            {
                continue;
            }

            rewritten ??= kids.Clone();
            rewritten.SetAt(i, next);
        }

        return rewritten;
    }

    /// <summary>An element or root that new elements go under.</summary>
    /// <param name="Id">The object.</param>
    /// <param name="Dictionary">Its dictionary.</param>
    [DebuggerDisplay("StructContainer: {Id}")]
    internal readonly record struct StructContainer(PdfObjectId Id, PdfDictionary Dictionary);

    /// <summary>A parent tree entry.</summary>
    /// <param name="Key">The key.</param>
    /// <param name="Value">The value.</param>
    [DebuggerDisplay("ParentEntry: {Key}")]
    internal readonly record struct ParentEntry(int Key, PdfValue Value);
}
