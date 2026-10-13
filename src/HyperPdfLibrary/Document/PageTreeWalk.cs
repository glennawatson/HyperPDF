// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using HyperPdfLibrary.IO;

namespace HyperPdfLibrary.Document;

/// <summary>
/// The working memory of one read of the page tree: the nodes already visited, one bit per object number, and the
/// ancestors whose kids remain, both in pooled arrays that <see cref="Release"/> gives back.
/// </summary>
internal ref struct PageTreeWalk
{
    /// <summary>The bits in a word of the visited set.</summary>
    private const int WordBits = 64;

    /// <summary>The number of object numbers the visited set holds.</summary>
    private readonly int _visitedLimit;

    /// <summary>The visited set; a pooled array.</summary>
    private ulong[]? _visited;

    /// <summary>The ancestors, innermost last; a pooled array.</summary>
    private AncestorCursor[]? _ancestors;

    /// <summary>The number of ancestors.</summary>
    private int _ancestorCount;

    /// <summary>Initializes a new instance of the <see cref="PageTreeWalk"/> struct.</summary>
    /// <param name="objectCount">One more than the highest object number.</param>
    internal PageTreeWalk(int objectCount)
    {
        var words = (objectCount / WordBits) + 1;
        _visited = ArrayPool<ulong>.Shared.Rent(words);
        _visited.AsSpan(0, words).Clear();
        _visitedLimit = words * WordBits;
    }

    /// <summary>Marks a node as visited.</summary>
    /// <param name="number">The node's object number.</param>
    /// <returns><see langword="true"/> when the node was not visited before.</returns>
    internal readonly bool TryMarkVisited(int number)
    {
        if ((uint)number >= (uint)_visitedLimit)
        {
            // A number beyond the table cannot name a stored node; the depth limit still bounds a loop.
            return true;
        }

        ref var word = ref _visited![number / WordBits];
        var bit = 1UL << (number % WordBits);
        if ((word & bit) != 0)
        {
            return false;
        }

        word |= bit;
        return true;
    }

    /// <summary>Enters an ancestor whose remaining kids are visited later.</summary>
    /// <param name="cursor">The ancestor.</param>
    internal void Push(in AncestorCursor cursor)
    {
        PooledArrays.EnsureRoom(ref _ancestors, _ancestorCount, clear: true)[_ancestorCount] = cursor;
        _ancestorCount++;
    }

    /// <summary>Takes the innermost ancestor.</summary>
    /// <param name="cursor">The ancestor, when there is one.</param>
    /// <returns><see langword="true"/> when an ancestor was taken.</returns>
    internal bool TryPop(out AncestorCursor cursor)
    {
        if (_ancestorCount == 0)
        {
            cursor = default;
            return false;
        }

        _ancestorCount--;
        cursor = _ancestors![_ancestorCount];
        _ancestors[_ancestorCount] = default;
        return true;
    }

    /// <summary>Gives the pooled arrays back.</summary>
    internal void Release()
    {
        if (_visited is not null)
        {
            ArrayPool<ulong>.Shared.Return(_visited);
            _visited = null;
        }

        PooledArrays.Return(ref _ancestors, _ancestorCount, clear: true);
        _ancestorCount = 0;
    }
}
