// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Windowing.Accessibility;

/// <summary>An immutable snapshot of the published accessible tree with parent, child and sibling links, so each platform's accessibility API walks it without searching.</summary>
[DebuggerDisplay("AccessibleTree: {Count} nodes")]
public sealed class AccessibleTree
{
    /// <summary>The marker for "no node" in the link arrays.</summary>
    private const int NoNode = -1;

    /// <summary>The nodes in the order they were published.</summary>
    private readonly AccessibleNode[] _nodes;

    /// <summary>The index of each node's parent.</summary>
    private readonly int[] _parent;

    /// <summary>The index of each node's first child.</summary>
    private readonly int[] _firstChild;

    /// <summary>The index of each node's last child.</summary>
    private readonly int[] _lastChild;

    /// <summary>The index of each node's next sibling.</summary>
    private readonly int[] _next;

    /// <summary>The index of each node's previous sibling.</summary>
    private readonly int[] _previous;

    /// <summary>The index of each node id.</summary>
    private readonly Dictionary<int, int> _indexOfId;

    /// <summary>Initializes a new instance of the <see cref="AccessibleTree"/> class.</summary>
    /// <param name="nodes">The nodes in tree order, parents before children.</param>
    public AccessibleTree(ReadOnlySpan<AccessibleNode> nodes)
    {
        _nodes = nodes.ToArray();
        _parent = NewLinks(_nodes.Length);
        _firstChild = NewLinks(_nodes.Length);
        _lastChild = NewLinks(_nodes.Length);
        _next = NewLinks(_nodes.Length);
        _previous = NewLinks(_nodes.Length);
        _indexOfId = [with(_nodes.Length)];
        for (var i = 0; i < _nodes.Length; i++)
        {
            _indexOfId[_nodes[i].Id] = i;
            Link(i);
        }
    }

    /// <summary>Gets the marker for "no node" that the link methods return.</summary>
    public static int None => NoNode;

    /// <summary>Gets an empty tree.</summary>
    public static AccessibleTree Empty { get; } = new([]);

    /// <summary>Gets the number of nodes.</summary>
    public int Count => _nodes.Length;

    /// <summary>Gets a node.</summary>
    /// <param name="index">The node index.</param>
    /// <returns>The node.</returns>
    public ref readonly AccessibleNode Node(int index) => ref _nodes[index];

    /// <summary>Finds a node by its id.</summary>
    /// <param name="id">The node id.</param>
    /// <returns>The index, or <see cref="None"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOf(int id) => _indexOfId.GetValueOrDefault(id, None);

    /// <summary>Gets the parent of a node.</summary>
    /// <param name="index">The node index.</param>
    /// <returns>The parent index, or <see cref="None"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Parent(int index) => _parent[index];

    /// <summary>Gets the first child of a node.</summary>
    /// <param name="index">The node index.</param>
    /// <returns>The child index, or <see cref="None"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int FirstChild(int index) => _firstChild[index];

    /// <summary>Gets the last child of a node.</summary>
    /// <param name="index">The node index.</param>
    /// <returns>The child index, or <see cref="None"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int LastChild(int index) => _lastChild[index];

    /// <summary>Gets the next sibling of a node.</summary>
    /// <param name="index">The node index.</param>
    /// <returns>The sibling index, or <see cref="None"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Next(int index) => _next[index];

    /// <summary>Gets the previous sibling of a node.</summary>
    /// <param name="index">The node index.</param>
    /// <returns>The sibling index, or <see cref="None"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Previous(int index) => _previous[index];

    /// <summary>Finds the deepest node whose bounds hold a point.</summary>
    /// <param name="x">The x in window pixels.</param>
    /// <param name="y">The y in window pixels.</param>
    /// <returns>The index, or <see cref="None"/>.</returns>
    public int HitTest(double x, double y)
    {
        var found = None;
        for (var i = 0; i < _nodes.Length; i++)
        {
            var bounds = _nodes[i].Bounds;
            if (x >= bounds.X && x < bounds.X + bounds.Width && y >= bounds.Y && y < bounds.Y + bounds.Height)
            {
                // Children follow their parents in the list, so the last match is the deepest.
                found = i;
            }
        }

        return found;
    }

    /// <summary>Finds the node that has keyboard focus.</summary>
    /// <returns>The index, or <see cref="None"/>.</returns>
    public int FocusedIndex()
    {
        for (var i = 0; i < _nodes.Length; i++)
        {
            if ((_nodes[i].States & AccessibleStates.Focused) != 0)
            {
                return i;
            }
        }

        return None;
    }

    /// <summary>Makes an array of links with no node set.</summary>
    /// <param name="count">The node count.</param>
    /// <returns>The array.</returns>
    private static int[] NewLinks(int count)
    {
        var links = new int[count];
        Array.Fill(links, None);
        return links;
    }

    /// <summary>Adds a node to its parent's child list.</summary>
    /// <param name="index">The node index.</param>
    private void Link(int index)
    {
        var parent = IndexOf(_nodes[index].ParentId);
        _parent[index] = parent;
        if (parent == None)
        {
            return;
        }

        var last = _lastChild[parent];
        if (last == None)
        {
            _firstChild[parent] = index;
        }
        else
        {
            _next[last] = index;
            _previous[index] = last;
        }

        _lastChild[parent] = index;
    }
}
