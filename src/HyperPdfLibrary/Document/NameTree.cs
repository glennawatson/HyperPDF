// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Reads name trees (string keys) and number trees (integer keys). Lookups descend through /Limits; enumeration visits
/// every leaf in order. Both stop at loops and at <see cref="PdfLimits.MaxPageTreeDepth"/>.
/// </summary>
internal static class NameTree
{
    /// <summary>The entries per key-value pair in /Names and /Nums arrays.</summary>
    private const int PairLength = 2;

    /// <summary>The most entries enumerated from one tree, guarding against huge or hostile trees.</summary>
    private const int MaxEntries = 1_000_000;

    /// <summary>The largest traversal retained between operations.</summary>
    private const int MaxKeptNodes = 4096;

    /// <summary>Finds a value by string key.</summary>
    /// <param name="root">The tree's root node.</param>
    /// <param name="key">The key's bytes.</param>
    /// <returns>The value, or null when missing.</returns>
    internal static PdfValue Find(PdfDictionary? root, ReadOnlySpan<byte> key)
    {
        if (root is null)
        {
            return default;
        }

        HashSet<PdfDictionary>? visited = null;
        var found = FindIn(root, key, 0, ref visited);
        return found.IsNull ? FindInTable(root, key) : found;
    }

    /// <summary>Visits every entry of a name tree, in order.</summary>
    /// <param name="root">The tree's root node.</param>
    /// <param name="output">The list receiving each key and value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Enumerate(PdfDictionary? root, List<NameTreeEntry> output) => EnumerateCore(root, KnownName.Names, false, output);

    /// <summary>Visits every entry of a name tree, in order, leaving each value as stored so a reference stays a reference.</summary>
    /// <param name="root">The tree's root node.</param>
    /// <param name="output">The list receiving each key and value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void EnumerateRaw(PdfDictionary? root, List<NameTreeEntry> output) => EnumerateCore(root, KnownName.Names, true, output);

    /// <summary>Visits every entry of a number tree, in order.</summary>
    /// <param name="root">The tree's root node.</param>
    /// <param name="output">The list receiving each key and value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void EnumerateNumbers(PdfDictionary? root, List<NameTreeEntry> output) => EnumerateCore(root, KnownName.Nums, false, output);

    /// <summary>Visits every entry of a number tree, in order, leaving each value as stored so a reference stays a reference.</summary>
    /// <param name="root">The tree's root node.</param>
    /// <param name="output">The list receiving each key and value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void EnumerateNumbersRaw(PdfDictionary? root, List<NameTreeEntry> output) => EnumerateCore(root, KnownName.Nums, true, output);

    /// <summary>Finds a value by integer key in a number tree.</summary>
    /// <param name="root">The tree's root node.</param>
    /// <param name="key">The key.</param>
    /// <returns>The value, or null when missing.</returns>
    internal static PdfValue FindNumber(PdfDictionary? root, long key)
    {
        var raw = FindNumberRaw(root, key);
        return raw.IsReference && root?.Owner is { } owner ? StoreReading.Resolve(owner, raw) : raw;
    }

    /// <summary>Finds a value by integer key in a number tree without following a reference.</summary>
    /// <param name="root">The tree's root node.</param>
    /// <param name="key">The key.</param>
    /// <returns>The value as the leaf holds it, or null when missing.</returns>
    internal static PdfValue FindNumberRaw(PdfDictionary? root, long key)
    {
        var node = root;
        for (var depth = 0; node is not null && depth < PdfLimits.MaxPageTreeDepth; depth++)
        {
            if (node.GetArray(KnownName.Nums) is { } numbers)
            {
                return FindNumberInLeaf(numbers, key);
            }

            node = FindNumberKid(node.GetArray(KnownName.Kids), key);
        }

        return default;
    }

    /// <summary>Visits every leaf entry under a node.</summary>
    /// <param name="root">The root node.</param>
    /// <param name="leafKey">/Names or /Nums.</param>
    /// <param name="raw">Whether values are left as stored instead of resolved.</param>
    /// <param name="output">The output list.</param>
    private static void EnumerateCore(PdfDictionary? root, KnownName leafKey, bool raw, List<NameTreeEntry> output)
    {
        if (root is null)
        {
            return;
        }

        var slots = root.Owner?.ScratchPools.Slots<Walk>();
        var walk = slots?.Take() ?? new Walk();
        try
        {
            EnumerateLeaves(root, leafKey, raw, output, walk);
        }
        finally
        {
            var keep = walk.Visited.EnsureCapacity(0) <= MaxKeptNodes && walk.Pending.EnsureCapacity(0) <= MaxKeptNodes;
            walk.Visited.Clear();
            walk.Pending.Clear();
            if (keep)
            {
                _ = slots?.Offer(walk);
            }
        }
    }

    /// <summary>Visits leaves using an exclusively owned traversal stack and loop guard.</summary>
    /// <param name="root">The root node.</param>
    /// <param name="leafKey">The leaf array key.</param>
    /// <param name="raw">Whether references are preserved.</param>
    /// <param name="output">Receives entries.</param>
    /// <param name="walk">The reusable traversal storage.</param>
    private static void EnumerateLeaves(PdfDictionary root, KnownName leafKey, bool raw, List<NameTreeEntry> output, Walk walk)
    {
        var visited = walk.Visited;
        var pending = walk.Pending;
        pending.Push(new(root, 0));
        while (pending.Count > 0 && output.Count < MaxEntries)
        {
            var next = pending.Pop();
            var node = next.Node;
            var depth = next.Depth;
            if (!visited.Add(node))
            {
                continue;
            }

            AddLeaf(node.GetArray(leafKey), raw, output);
            var kids = node.GetArray(KnownName.Kids);
            for (var i = (kids?.Count ?? 0) - 1; i >= 0 && depth < PdfLimits.MaxPageTreeDepth; i--)
            {
                if (kids!.GetDictionary(i) is { } kid)
                {
                    pending.Push(new(kid, depth + 1));
                }
            }
        }
    }

    /// <summary>Adds a leaf's pairs.</summary>
    /// <param name="pairs">The /Names or /Nums array.</param>
    /// <param name="raw">Whether values are left as stored instead of resolved.</param>
    /// <param name="output">The output list.</param>
    private static void AddLeaf(PdfArray? pairs, bool raw, List<NameTreeEntry> output)
    {
        for (var i = 0; pairs is not null && i + 1 < pairs.Count && output.Count < MaxEntries; i += PairLength)
        {
            output.Add(new(pairs.Get(i), raw ? pairs.GetRaw(i + 1) : pairs.Get(i + 1)));
        }
    }

    /// <summary>Searches a leaf's sorted pairs, falling back to a scan for trees that are not sorted.</summary>
    /// <param name="names">The /Names array.</param>
    /// <param name="key">The key.</param>
    /// <returns>The value, or null.</returns>
    private static PdfValue FindInLeaf(PdfArray names, ReadOnlySpan<byte> key)
    {
        var low = 0;
        var high = (names.Count / PairLength) - 1;
        while (low <= high)
        {
            var middle = (low + high) >>> 1;
            var order = names.Get(middle * PairLength).AsStringBytes().SequenceCompareTo(key);
            if (order == 0)
            {
                return names.Get((middle * PairLength) + 1);
            }

            low = order < 0 ? middle + 1 : low;
            high = order < 0 ? high : middle - 1;
        }

        for (var i = 0; i + 1 < names.Count; i += PairLength)
        {
            if (names.Get(i).AsStringBytes().SequenceEqual(key))
            {
                return names.Get(i + 1);
            }
        }

        return default;
    }

    /// <summary>
    /// Searches a node and then every kid whose /Limits cover the key (kids without /Limits too), backtracking when a kid
    /// does not hold it.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="key">The key.</param>
    /// <param name="depth">The node's depth.</param>
    /// <param name="visited">The nodes already searched, created when the first kid is searched.</param>
    /// <returns>The value, or null.</returns>
    private static PdfValue FindIn(PdfDictionary node, ReadOnlySpan<byte> key, int depth, ref HashSet<PdfDictionary>? visited)
    {
        if (depth >= PdfLimits.MaxPageTreeDepth)
        {
            return default;
        }

        if (node.GetArray(KnownName.Names) is { } names)
        {
            var hit = FindInLeaf(names, key);
            if (!hit.IsNull)
            {
                return hit;
            }
        }

        return FindInKids(node.GetArray(KnownName.Kids), key, depth + 1, ref visited);
    }

    /// <summary>Searches each kid whose /Limits cover the key, in order, until one holds it.</summary>
    /// <param name="kids">The /Kids array.</param>
    /// <param name="key">The key.</param>
    /// <param name="depth">The kids' depth.</param>
    /// <param name="visited">The nodes already searched.</param>
    /// <returns>The value, or null.</returns>
    private static PdfValue FindInKids(PdfArray? kids, ReadOnlySpan<byte> key, int depth, ref HashSet<PdfDictionary>? visited)
    {
        // A tree with sorted /Limits is searched by halving; a tree without them, or one that does not hold the key where its
        // limits say, is searched kid by kid, which skips the kid already searched.
        if (kids is not null && FindKidByLimits(kids, key) is { } guess)
        {
            var hit = FindInKid(guess, key, depth, ref visited);
            if (!hit.IsNull)
            {
                return hit;
            }
        }

        return FindInKidsInOrder(kids, key, depth, ref visited);
    }

    /// <summary>Searches a kid unless it was searched already.</summary>
    /// <param name="kid">The kid.</param>
    /// <param name="key">The key.</param>
    /// <param name="depth">The kid's depth.</param>
    /// <param name="visited">The nodes already searched.</param>
    /// <returns>The value, or null.</returns>
    private static PdfValue FindInKid(PdfDictionary kid, ReadOnlySpan<byte> key, int depth, ref HashSet<PdfDictionary>? visited)
    {
        visited ??= [with(ReferenceEqualityComparer.Instance)];
        return visited.Add(kid) ? FindIn(kid, key, depth, ref visited) : default;
    }

    /// <summary>Searches each kid whose /Limits cover the key, in order, until one holds it.</summary>
    /// <param name="kids">The /Kids array.</param>
    /// <param name="key">The key.</param>
    /// <param name="depth">The kids' depth.</param>
    /// <param name="visited">The nodes already searched.</param>
    /// <returns>The value, or null.</returns>
    private static PdfValue FindInKidsInOrder(PdfArray? kids, ReadOnlySpan<byte> key, int depth, ref HashSet<PdfDictionary>? visited)
    {
        for (var i = 0; kids is not null && i < kids.Count; i++)
        {
            var kid = kids.GetDictionary(i);
            if (kid is null || !CoversKey(kid, key))
            {
                continue;
            }

            var found = FindInKid(kid, key, depth, ref visited);
            if (!found.IsNull)
            {
                return found;
            }
        }

        return default;
    }

    /// <summary>Finds the kid whose /Limits cover a key by halving the kids, which the standard keeps in order.</summary>
    /// <param name="kids">The /Kids array.</param>
    /// <param name="key">The key.</param>
    /// <returns>The kid, or <see langword="null"/> when a kid has no usable /Limits or none covers the key.</returns>
    private static PdfDictionary? FindKidByLimits(PdfArray kids, ReadOnlySpan<byte> key)
    {
        var low = 0;
        var high = kids.Count - 1;
        while (low <= high)
        {
            var middle = (low + high) >>> 1;
            var kid = kids.GetDictionary(middle);
            if (kid?.GetArray(KnownName.Limits) is not { Count: >= PairLength } limits)
            {
                return null;
            }

            if (limits.Get(0).AsStringBytes().SequenceCompareTo(key) > 0)
            {
                high = middle - 1;
            }
            else if (limits.Get(1).AsStringBytes().SequenceCompareTo(key) < 0)
            {
                low = middle + 1;
            }
            else
            {
                return kid;
            }
        }

        return null;
    }

    /// <summary>Determines whether a node's /Limits cover a key; a node without /Limits might hold anything.</summary>
    /// <param name="kid">The node.</param>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when the key may be in the node.</returns>
    private static bool CoversKey(PdfDictionary kid, ReadOnlySpan<byte> key)
    {
        var limits = kid.GetArray(KnownName.Limits);
        return limits is null || (limits.Get(0).AsStringBytes().SequenceCompareTo(key) <= 0 && limits.Get(1).AsStringBytes().SequenceCompareTo(key) >= 0);
    }

    /// <summary>Looks a key up in a whole-tree table, retained for store-owned roots with wrong /Limits.</summary>
    /// <param name="root">The tree's root node.</param>
    /// <param name="key">The key.</param>
    /// <returns>The value, or null.</returns>
    private static PdfValue FindInTable(PdfDictionary root, ReadOnlySpan<byte> key)
    {
        var table = root.Owner is { IsDisposed: false } owner ? owner.NameTreeTables.GetValue(root, BuildTable) : BuildTable(root);
        return table.GetAlternateLookup<ReadOnlySpan<byte>>().TryGetValue(key, out var value) ? value : default;
    }

    /// <summary>Reads every entry of a name tree into a table; the first of duplicate keys wins.</summary>
    /// <param name="root">The tree's root node.</param>
    /// <returns>The table.</returns>
    private static Dictionary<byte[], PdfValue> BuildTable(PdfDictionary root)
    {
        var entries = new List<NameTreeEntry>();
        Enumerate(root, entries);
        var table = new Dictionary<byte[], PdfValue>(entries.Count, ByteArrayComparer.Instance);
        foreach (var entry in entries)
        {
            _ = table.TryAdd(entry.Key.AsStringBytes().ToArray(), entry.Value);
        }

        return table;
    }

    /// <summary>Scans a number tree leaf.</summary>
    /// <param name="numbers">The /Nums array.</param>
    /// <param name="key">The key.</param>
    /// <returns>The value, or null.</returns>
    private static PdfValue FindNumberInLeaf(PdfArray numbers, long key)
    {
        for (var i = 0; i + 1 < numbers.Count; i += PairLength)
        {
            if (numbers.Get(i).AsInteger(long.MinValue) == key)
            {
                return numbers.GetRaw(i + 1);
            }
        }

        return default;
    }

    /// <summary>Picks the kid whose /Limits cover a number.</summary>
    /// <param name="kids">The /Kids array.</param>
    /// <param name="key">The key.</param>
    /// <returns>The kid, or <see langword="null"/>.</returns>
    private static PdfDictionary? FindNumberKid(PdfArray? kids, long key)
    {
        for (var i = 0; kids is not null && i < kids.Count; i++)
        {
            var kid = kids.GetDictionary(i);
            var limits = kid?.GetArray(KnownName.Limits);
            if (limits is null || (limits.Get(0).AsInteger() <= key && limits.Get(1).AsInteger() >= key))
            {
                return kid;
            }
        }

        return null;
    }

    /// <summary>A tree node waiting to be visited.</summary>
    /// <param name="Node">The node.</param>
    /// <param name="Depth">Its depth.</param>
    private readonly record struct PendingNode(PdfDictionary Node, int Depth);

    /// <summary>Mutable storage owned by one enumeration until it is cleared and returned.</summary>
    private sealed class Walk
    {
        /// <summary>Gets the nodes visited by this enumeration.</summary>
        internal HashSet<PdfDictionary> Visited { get; } = [with(ReferenceEqualityComparer.Instance)];

        /// <summary>Gets the nodes awaiting enumeration.</summary>
        internal Stack<PendingNode> Pending { get; } = [];
    }
}
