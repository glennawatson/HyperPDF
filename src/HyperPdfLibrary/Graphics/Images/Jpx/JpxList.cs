// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// A growable list over an array rented from the shared pool, with elements reached by reference. The decoder keeps one
/// per element type and clears it between tiles, so the code-block bookkeeping allocates nothing per block.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
[DebuggerDisplay("JpxList: {Count} items")]
internal sealed class JpxList<T> : IDisposable
    where T : struct
{
    /// <summary>The smallest array rented.</summary>
    private const int MinimumCapacity = 64;

    /// <summary>The factor the capacity grows by.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The cache this list returns its array to.</summary>
    private readonly ScratchPools _scratchPools;

    /// <summary>The rented array.</summary>
    private T[] _items = [];

    /// <summary>Initializes a new instance of the <see cref="JpxList{T}"/> class with an explicit cache.</summary>
    /// <param name="scratchPools">The bounded cache.</param>
    internal JpxList(ScratchPools scratchPools) => _scratchPools = scratchPools;

    /// <summary>Gets the number of elements.</summary>
    internal int Count { get; private set; }

    /// <summary>Gets an element by reference.</summary>
    /// <param name="index">The index, below <see cref="Count"/>.</param>
    /// <returns>The element.</returns>
    internal ref T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref _items[index];
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_items.Length > 0)
        {
            _scratchPools.Get<T>().Return(_items);
        }

        _items = [];
        Count = 0;
    }

    /// <summary>Gets the elements as a span.</summary>
    /// <returns>The first <see cref="Count"/> elements.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Span<T> AsSpan() => _items.AsSpan(0, Count);

    /// <summary>Adds an element.</summary>
    /// <param name="item">The element.</param>
    /// <returns>The index of the element.</returns>
    internal int Add(in T item)
    {
        if (Count == _items.Length)
        {
            Grow(Count + 1);
        }

        _items[Count] = item;
        var index = Count;
        Count = index + 1;
        return index;
    }

    /// <summary>Adds default elements.</summary>
    /// <param name="count">The number of elements.</param>
    /// <returns>The index of the first element.</returns>
    internal int AddDefault(int count)
    {
        var start = Count;
        if (start + count > _items.Length)
        {
            Grow(start + count);
        }

        _items.AsSpan(start, count).Clear();
        Count = start + count;
        return start;
    }

    /// <summary>Removes every element, keeping the array.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Clear() => Count = 0;

    /// <summary>Moves the elements to a larger rented array.</summary>
    /// <param name="needed">The capacity needed.</param>
    private void Grow(int needed)
    {
        var pool = _scratchPools.Get<T>();
        var larger = pool.Rent(Math.Max(Math.Max(needed, MinimumCapacity), _items.Length * GrowthFactor));
        _items.AsSpan(0, Count).CopyTo(larger);
        if (_items.Length > 0)
        {
            pool.Return(_items);
        }

        _items = larger;
    }
}
