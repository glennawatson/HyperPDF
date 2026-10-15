// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Writing;

/// <summary>A growable list of integers held in an array rented from the shared pool. Dispose returns the array.</summary>
internal ref struct PooledInts : IDisposable
{
    /// <summary>The smallest array rented.</summary>
    private const int MinimumCapacity = 16;

    /// <summary>The factor the capacity grows by.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The rented array, or <see langword="null"/> before the first item.</summary>
    private int[]? _items;

    /// <summary>Gets the number of items.</summary>
    internal int Count { get; private set; }

    /// <summary>Gets the items.</summary>
    internal readonly ReadOnlySpan<int> Span => _items is null ? [] : _items.AsSpan(0, Count);

    /// <summary>Gets an item.</summary>
    /// <param name="index">The zero based index.</param>
    internal readonly int this[int index] => Span[index];

    /// <summary>Returns the array to the pool.</summary>
    public void Dispose()
    {
        if (_items is { } items)
        {
            ArrayPool<int>.Shared.Return(items);
        }

        _items = null;
        Count = 0;
    }

    /// <summary>Appends an item.</summary>
    /// <param name="value">The item.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Add(int value)
    {
        if (_items is null || Count == _items.Length)
        {
            Grow();
        }

        _items![Count] = value;
        Count++;
    }

    /// <summary>Appends items that are zero.</summary>
    /// <param name="count">The number of items.</param>
    /// <returns>The new items, to fill in.</returns>
    internal Span<int> AddZeros(int count)
    {
        while (_items is null || _items.Length - Count < count)
        {
            Grow();
        }

        var added = _items.AsSpan(Count, count);
        added.Clear();
        Count += count;
        return added;
    }

    /// <summary>Removes every item, keeping the array.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Clear() => Count = 0;

    /// <summary>Replaces the array with a larger one holding the same items.</summary>
    private void Grow()
    {
        var larger = ArrayPool<int>.Shared.Rent(_items is null ? MinimumCapacity : _items.Length * GrowthFactor);
        if (_items is not null)
        {
            _items.AsSpan(0, Count).CopyTo(larger);
            ArrayPool<int>.Shared.Return(_items);
        }

        _items = larger;
    }
}
