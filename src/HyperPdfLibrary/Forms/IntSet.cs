// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.IO;

namespace HyperPdfLibrary.Forms;

/// <summary>A set of numbers kept ascending in a pooled array. Dispose gives the array back.</summary>
[DebuggerDisplay("IntSet: {Count} numbers")]
internal ref struct IntSet
{
    /// <summary>The numbers, ascending; a pooled array.</summary>
    private int[]? _items;

    /// <summary>Gets the number of numbers.</summary>
    internal int Count { get; private set; }

    /// <summary>Adds a number.</summary>
    /// <param name="value">The number.</param>
    /// <returns><see langword="true"/> when the number was not in the set.</returns>
    internal bool Add(int value)
    {
        var at = _items is null ? -1 : Array.BinarySearch(_items, 0, Count, value);
        if (at >= 0)
        {
            return false;
        }

        var insert = ~at;
        _ = PooledArrays.EnsureRoom(ref _items, Count, clear: false);
        Array.Copy(_items!, insert, _items!, insert + 1, Count - insert);
        _items![insert] = value;
        Count++;
        return true;
    }

    /// <summary>Gives the array back.</summary>
    internal void Dispose()
    {
        PooledArrays.Return(ref _items, Count, clear: false);
        Count = 0;
    }
}
