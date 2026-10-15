// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Layers;

/// <summary>
/// Holds the entries of the /Order levels being read. Each level adds its entries above those of the levels that hold it
/// and, when it ends, copies them out and removes them, so reading a tree makes only the arrays it returns.
/// </summary>
[DebuggerDisplay("LayerNodeStack: {Count} entries")]
internal sealed class LayerNodeStack
{
    /// <summary>The entries; the first <see cref="Count"/> are in use.</summary>
    private PdfLayerNode[] _items = [];

    /// <summary>Gets the number of entries in use.</summary>
    internal int Count { get; private set; }

    /// <summary>Adds an entry.</summary>
    /// <param name="node">The entry.</param>
    internal void Push(PdfLayerNode node)
    {
        Reserve(1);
        _items[Count] = node;
        Count++;
    }

    /// <summary>Adds each entry of an array.</summary>
    /// <param name="nodes">The entries.</param>
    internal void PushRange(PdfLayerNode[] nodes)
    {
        Reserve(nodes.Length);
        nodes.CopyTo(_items, Count);
        Count += nodes.Length;
    }

    /// <summary>Copies the entries from a position to the top into an array and removes them.</summary>
    /// <param name="start">The position of the first entry to take.</param>
    /// <returns>The entries.</returns>
    internal PdfLayerNode[] Take(int start)
    {
        var taken = Count == start ? [] : _items.AsSpan(start, Count - start).ToArray();
        _items.AsSpan(start, Count - start).Clear();
        Count = start;
        return taken;
    }

    /// <summary>Drops every entry, so an idle stack holds no layer.</summary>
    internal void Clear()
    {
        Array.Clear(_items, 0, Count);
        Count = 0;
    }

    /// <summary>Makes room for more entries.</summary>
    /// <param name="extra">The number of entries about to be added.</param>
    private void Reserve(int extra)
    {
        var needed = Count + extra;
        if (needed > _items.Length)
        {
            Array.Resize(ref _items, Math.Max(needed, Math.Max(1, _items.Length) << 1));
        }
    }
}
