// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Checks the shape rules of a name tree (ISO 32000-2 7.9.6) and reports each one a tree breaks.</summary>
internal static class NameTreeInvariants
{
    /// <summary>The most entries a node may hold.</summary>
    internal const int MaxNode = 32;

    /// <summary>The values per entry of a /Names array.</summary>
    private const int PairLength = 2;

    /// <summary>Checks a tree.</summary>
    /// <param name="root">The root node, or <see langword="null"/>.</param>
    /// <param name="count">Receives the number of entries found.</param>
    /// <returns>The problems; empty when the tree is well formed.</returns>
    internal static List<string> Check(PdfDictionary? root, out int count)
    {
        var problems = new List<string>();
        var state = new Walk();
        if (root is not null)
        {
            Visit(root, true, state, problems, 0);
        }

        count = state.Count;
        return problems;
    }

    /// <summary>Visits a node and everything below it.</summary>
    /// <param name="node">The node.</param>
    /// <param name="isRoot">Whether it is the root.</param>
    /// <param name="state">What the walk has seen.</param>
    /// <param name="problems">The problems found.</param>
    /// <param name="depth">The node's depth.</param>
    private static void Visit(PdfDictionary node, bool isRoot, Walk state, List<string> problems, int depth)
    {
        if (depth > MaxNode)
        {
            problems.Add("The tree is too deep.");
            return;
        }

        CheckShape(node, isRoot, problems);
        var first = state.Count;
        var span = new Span();
        if (node.GetArray(KnownName.Names) is { } names)
        {
            VisitLeaf(names, state, problems, span);
        }

        var kids = node.GetArray(KnownName.Kids);
        for (var i = 0; kids is not null && i < kids.Count; i++)
        {
            VisitKid(kids, i, state, problems, depth, span);
        }

        if (!isRoot && state.Count > first)
        {
            CheckLimits(node, span, problems);
        }
    }

    /// <summary>Checks that a node has the keys its place in the tree needs.</summary>
    /// <param name="node">The node.</param>
    /// <param name="isRoot">Whether it is the root.</param>
    /// <param name="problems">The problems found.</param>
    private static void CheckShape(PdfDictionary node, bool isRoot, List<string> problems)
    {
        if (node.ContainsKey(KnownName.Names) == node.ContainsKey(KnownName.Kids))
        {
            problems.Add("A node must have exactly one of /Names and /Kids.");
        }

        if (isRoot == node.ContainsKey(KnownName.Limits))
        {
            problems.Add(isRoot ? "The root must not have /Limits." : "A node below the root must have /Limits.");
        }
    }

    /// <summary>Checks that a node's limits are its first and last names.</summary>
    /// <param name="node">The node.</param>
    /// <param name="span">The first and last names below the node.</param>
    /// <param name="problems">The problems found.</param>
    private static void CheckLimits(PdfDictionary node, Span span, List<string> problems)
    {
        var limits = node.GetArray(KnownName.Limits);
        if (limits is null || PdfText.Decode(limits.Get(0).AsStringBytes()) != span.Lowest || PdfText.Decode(limits.Get(1).AsStringBytes()) != span.Highest)
        {
            problems.Add($"The limits of a node do not match its first ({span.Lowest}) and last ({span.Highest}) names.");
        }
    }

    /// <summary>Checks a leaf's pairs.</summary>
    /// <param name="names">The /Names array.</param>
    /// <param name="state">What the walk has seen.</param>
    /// <param name="problems">The problems found.</param>
    /// <param name="span">The first and last names below the node.</param>
    private static void VisitLeaf(PdfArray names, Walk state, List<string> problems, Span span)
    {
        if (names.Count % PairLength != 0 || names.Count > MaxNode * PairLength)
        {
            problems.Add($"A leaf has {names.Count} values; it must hold at most {MaxNode} pairs.");
        }

        for (var i = 0; i + 1 < names.Count; i += PairLength)
        {
            var name = PdfText.Decode(names.Get(i).AsStringBytes());
            if (state.Last is { } previous && string.CompareOrdinal(previous, name) >= 0)
            {
                problems.Add($"The names are not in order at {name}.");
            }

            state.Last = name;
            state.Count++;
            span.Lowest ??= name;
            span.Highest = name;
        }
    }

    /// <summary>Checks one kid of an intermediate node.</summary>
    /// <param name="kids">The /Kids array.</param>
    /// <param name="index">The kid.</param>
    /// <param name="state">What the walk has seen.</param>
    /// <param name="problems">The problems found.</param>
    /// <param name="depth">The parent's depth.</param>
    /// <param name="span">The first and last names below the parent.</param>
    private static void VisitKid(PdfArray kids, int index, Walk state, List<string> problems, int depth, Span span)
    {
        if (kids.Count > MaxNode)
        {
            problems.Add($"An intermediate node has {kids.Count} kids; it must hold at most {MaxNode}.");
        }

        if (!kids.GetRaw(index).IsReference || kids.GetDictionary(index) is not { } kid)
        {
            problems.Add("A kid must be a reference to a node.");
            return;
        }

        var before = state.Count;
        Visit(kid, false, state, problems, depth + 1);
        var limits = kid.GetArray(KnownName.Limits);
        if (state.Count == before || limits is null)
        {
            return;
        }

        span.Lowest ??= PdfText.Decode(limits.Get(0).AsStringBytes());
        span.Highest = PdfText.Decode(limits.Get(1).AsStringBytes());
    }

    /// <summary>What a walk has seen so far.</summary>
    private sealed class Walk
    {
        /// <summary>Gets or sets the number of entries seen.</summary>
        internal int Count { get; set; }

        /// <summary>Gets or sets the last name seen.</summary>
        internal string? Last { get; set; }
    }

    /// <summary>The lowest and highest names below a node.</summary>
    private sealed class Span
    {
        /// <summary>Gets or sets the lowest name.</summary>
        internal string? Lowest { get; set; }

        /// <summary>Gets or sets the highest name.</summary>
        internal string? Highest { get; set; }
    }
}
