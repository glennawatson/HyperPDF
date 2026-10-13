// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.IO;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// The page tree nodes from the root down to the node that holds a position, found without reading the whole tree.
/// The nodes sit in a pooled array that <see cref="Dispose"/> gives back.
/// </summary>
internal sealed class PageTreePath : IDisposable
{
    /// <summary>The nodes, root first; a pooled array.</summary>
    private PathNode[]? _nodes;

    /// <summary>Gets the number of nodes.</summary>
    internal int Count { get; private set; }

    /// <summary>Gets or sets the position in the deepest node's <c>/Kids</c> where an insertion goes.</summary>
    internal int InsertAt { get; set; }

    /// <summary>Gets or sets the inheritable keys, one bit each in the order of <see cref="PdfPageTreeWriter.InheritableKeys"/>, that some node on the path carries.</summary>
    internal int AttributeMask { get; set; }

    /// <summary>Gets the deepest node, which holds the position.</summary>
    internal PathNode Deepest => _nodes![Count - 1];

    /// <summary>Gets a node of the path.</summary>
    /// <param name="level">The node's depth, 0 for the root.</param>
    /// <returns>The node.</returns>
    internal PathNode this[int level] => _nodes![level];

    /// <summary>Gives the pooled array back.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => PooledArrays.Return(ref _nodes, Count, clear: true);

    /// <summary>Adds the next node down.</summary>
    /// <param name="node">The node.</param>
    internal void Add(in PathNode node)
    {
        PooledArrays.EnsureRoom(ref _nodes, Count, clear: true)[Count] = node;
        Count++;
    }
}
