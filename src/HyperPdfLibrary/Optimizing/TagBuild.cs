// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Optimizing;

/// <summary>What the conversion of one page's layout nodes into elements reads and updates. Its lists are rented; dispose it when the page is done.</summary>
[DebuggerDisplay("TagBuild: {_nodes.Count} elements")]
internal ref struct TagBuild
{
    /// <summary>The elements, each followed by its descendants.</summary>
    private RentedList<TagNode> _nodes;

    /// <summary>The units the elements own, each element's in a run.</summary>
    private RentedList<int> _owned;

    /// <summary>Initializes a new instance of the <see cref="TagBuild"/> struct.</summary>
    /// <param name="content">The page's marked content from the probe pass.</param>
    /// <param name="units">The page's units.</param>
    /// <param name="owners">The block owning each unit, or -1; updated as nodes claim units.</param>
    /// <param name="scratch">Room to list a node's glyph indices.</param>
    internal TagBuild(PdfMarkedContentPage? content, ReadOnlySpan<TagUnit> units, Span<int> owners, List<int> scratch)
    {
        Content = content;
        Units = units;
        Owners = owners;
        Scratch = scratch;
    }

    /// <summary>Gets the page's marked content from the probe pass.</summary>
    internal readonly PdfMarkedContentPage? Content { get; }

    /// <summary>Gets the page's units.</summary>
    internal readonly ReadOnlySpan<TagUnit> Units { get; }

    /// <summary>Gets the block owning each unit, or -1; updated as nodes claim units.</summary>
    internal readonly Span<int> Owners { get; }

    /// <summary>Gets room to list a node's glyph indices.</summary>
    internal readonly List<int> Scratch { get; }

    /// <summary>Gets the number of elements.</summary>
    internal readonly int NodeCount => _nodes.Count;

    /// <summary>Gets the elements, each followed by its descendants.</summary>
    internal readonly ReadOnlySpan<TagNode> Nodes => _nodes.Span;

    /// <summary>Gets the units the elements own, each element's in a run.</summary>
    internal readonly ReadOnlySpan<int> OwnedUnits => _owned.Span;

    /// <summary>Gets the number of owned units listed.</summary>
    internal readonly int OwnedCount => _owned.Count;

    /// <summary>Appends an element.</summary>
    /// <param name="node">The element.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AddNode(in TagNode node) => _nodes.Add(node);

    /// <summary>Inserts an element, moving the later ones up.</summary>
    /// <param name="index">Where the element goes.</param>
    /// <param name="node">The element.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void InsertNode(int index, in TagNode node) => _nodes.Insert(index, node);

    /// <summary>Replaces an element.</summary>
    /// <param name="index">The element's index.</param>
    /// <param name="node">The new element.</param>
    internal readonly void SetNode(int index, in TagNode node) => _nodes[index] = node;

    /// <summary>Drops the elements from an index on.</summary>
    /// <param name="count">The number of elements to keep.</param>
    internal void TruncateNodes(int count) => _nodes.Count = count;

    /// <summary>Lists a unit an element owns.</summary>
    /// <param name="unit">The unit.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AddOwned(int unit) => _owned.Add(unit);

    /// <summary>Sorts the owned units from an index on.</summary>
    /// <param name="start">The index of the first unit to sort.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly void SortOwned(int start) => _owned.Span[start..].Sort();

    /// <summary>Moves the elements from an index on into a new order of whole subtrees.</summary>
    /// <param name="first">The index of the first element to move.</param>
    /// <param name="scratch">Room for the moved elements, at least as many as there are from <paramref name="first"/> on.</param>
    /// <param name="starts">The index each subtree starts at, in the order they were built.</param>
    internal readonly void ReverseSubtrees(int first, Span<TagNode> scratch, ReadOnlySpan<int> starts)
    {
        var all = _nodes.Span;
        var written = 0;
        for (var k = starts.Length - 1; k >= 0; k--)
        {
            var run = all[starts[k]..(k + 1 < starts.Length ? starts[k + 1] : all.Length)];
            run.CopyTo(scratch[written..]);
            written += run.Length;
        }

        scratch[..written].CopyTo(all[first..]);
    }

    /// <summary>Gives the lists back.</summary>
    internal void Dispose()
    {
        _nodes.Dispose();
        _owned.Dispose();
    }
}
