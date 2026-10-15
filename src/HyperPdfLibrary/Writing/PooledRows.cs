// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Writing;

/// <summary>Rows of integers of different lengths held in two pooled arrays. Dispose returns them.</summary>
internal ref struct PooledRows : IDisposable
{
    /// <summary>The items of all rows, one row after another.</summary>
    private PooledInts _items;

    /// <summary>The offset after the last item of each finished row.</summary>
    private PooledInts _ends;

    /// <summary>Gets the number of finished rows.</summary>
    internal readonly int RowCount => _ends.Count;

    /// <summary>Gets a finished row.</summary>
    /// <param name="row">The zero based row.</param>
    internal readonly ReadOnlySpan<int> this[int row]
    {
        get
        {
            var start = row == 0 ? 0 : _ends[row - 1];
            return _items.Span[start.._ends[row]];
        }
    }

    /// <summary>Returns the arrays to the pool.</summary>
    public void Dispose()
    {
        _items.Dispose();
        _ends.Dispose();
    }

    /// <summary>Appends an item to the row being built.</summary>
    /// <param name="value">The item.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Add(int value) => _items.Add(value);

    /// <summary>Finishes the row being built.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void EndRow() => _ends.Add(_items.Count);
}
