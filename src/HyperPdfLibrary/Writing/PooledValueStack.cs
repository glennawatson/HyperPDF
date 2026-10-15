// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Writing;

/// <summary>A last-in first-out stack of values held in an array rented from the shared pool. Dispose returns the array.</summary>
internal ref struct PooledValueStack : IDisposable
{
    /// <summary>The smallest array rented.</summary>
    private const int MinimumCapacity = 16;

    /// <summary>The factor the capacity grows by.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The rented array, or <see langword="null"/> before the first push.</summary>
    private PdfValue[]? _items;

    /// <summary>Gets the number of values on the stack.</summary>
    internal int Count { get; private set; }

    /// <summary>Gets the values in insertion order.</summary>
    internal readonly ReadOnlySpan<PdfValue> Span => _items is { } items ? items.AsSpan(0, Count) : [];

    /// <summary>Returns the array to the pool, clearing the values so they can be collected.</summary>
    public void Dispose()
    {
        if (_items is { } items)
        {
            ArrayPool<PdfValue>.Shared.Return(items, true);
        }

        _items = null;
        Count = 0;
    }

    /// <summary>Pushes a value.</summary>
    /// <param name="value">The value.</param>
    internal void Push(PdfValue value)
    {
        if (_items is null || Count == _items.Length)
        {
            Grow();
        }

        _items![Count] = value;
        Count++;
    }

    /// <summary>Pops the newest value.</summary>
    /// <param name="value">Receives the value.</param>
    /// <returns><see langword="true"/> when the stack was not empty.</returns>
    internal bool TryPop(out PdfValue value)
    {
        if (Count == 0)
        {
            value = default;
            return false;
        }

        Count--;
        value = _items![Count];
        return true;
    }

    /// <summary>Replaces the array with a larger one holding the same values.</summary>
    private void Grow()
    {
        var larger = ArrayPool<PdfValue>.Shared.Rent(_items is null ? MinimumCapacity : _items.Length * GrowthFactor);
        if (_items is not null)
        {
            _items.AsSpan(0, Count).CopyTo(larger);
            ArrayPool<PdfValue>.Shared.Return(_items, true);
        }

        _items = larger;
    }
}
