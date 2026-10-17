// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// A growable list whose array comes from the shared pool. It lets the optimiser collect entries for one page without
/// allocating a list; <see cref="Dispose"/> gives the array back. Copy it only by reference.
/// </summary>
/// <typeparam name="T">The entry type.</typeparam>
[DebuggerDisplay("RentedList: {Count} entries")]
internal ref struct RentedList<T>
{
    /// <summary>The capacity of the first array.</summary>
    private const int InitialCapacity = 16;

    /// <summary>The factor by which a full array grows.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The rented array, or <see langword="null"/> before the first entry.</summary>
    private T[]? _array;

    /// <summary>Gets or sets the number of entries; the entries up to it are in use.</summary>
    internal int Count { get; set; }

    /// <summary>Gets the entries.</summary>
    internal readonly Span<T> Span => _array is null ? [] : _array.AsSpan(0, Count);

    /// <summary>Gets a reference to an entry.</summary>
    /// <param name="index">The index.</param>
    internal readonly ref T this[int index] => ref Span[index];

    /// <summary>Appends an entry.</summary>
    /// <param name="value">The entry.</param>
    internal void Add(T value)
    {
        Reserve(1);
        _array![Count] = value;
        Count++;
    }

    /// <summary>Appends entries.</summary>
    /// <param name="values">The entries.</param>
    internal void AddRange(scoped ReadOnlySpan<T> values)
    {
        Reserve(values.Length);
        values.CopyTo(_array.AsSpan(Count));
        Count += values.Length;
    }

    /// <summary>Inserts an entry, moving the later ones up.</summary>
    /// <param name="index">Where the entry goes.</param>
    /// <param name="value">The entry.</param>
    internal void Insert(int index, T value)
    {
        Reserve(1);
        _array.AsSpan(index, Count - index).CopyTo(_array.AsSpan(index + 1));
        _array![index] = value;
        Count++;
    }

    /// <summary>Removes every entry and keeps the array.</summary>
    internal void Clear()
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            Span.Clear();
        }

        Count = 0;
    }

    /// <summary>Gives the array back to the pool.</summary>
    internal void Dispose()
    {
        var array = _array;
        _array = null;
        Count = 0;
        if (array is not null)
        {
            ArrayPool<T>.Shared.Return(array, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }
    }

    /// <summary>Makes room for more entries.</summary>
    /// <param name="extra">The entries about to be added.</param>
    private void Reserve(int extra)
    {
        if (_array is not null && _array.Length - Count >= extra)
        {
            return;
        }

        var larger = ArrayPool<T>.Shared.Rent(Math.Max(InitialCapacity, Math.Max(Count + extra, (_array?.Length ?? 0) * GrowthFactor)));
        if (_array is not null)
        {
            _array.AsSpan(0, Count).CopyTo(larger);
            ArrayPool<T>.Shared.Return(_array, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }

        _array = larger;
    }
}
