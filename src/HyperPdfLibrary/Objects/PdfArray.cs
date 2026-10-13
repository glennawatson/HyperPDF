// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Objects;

/// <summary>A PDF array. Getters resolve references through the owning document.</summary>
[DebuggerDisplay("PdfArray: {Count} items")]
public sealed class PdfArray
{
    /// <summary>The capacity of an array created empty.</summary>
    private const int DefaultCapacity = 4;

    /// <summary>The factor the capacity grows by.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The items.</summary>
    private PdfValue[] _items;

    /// <summary>Initializes a new instance of the <see cref="PdfArray"/> class.</summary>
    /// <param name="owner">The objects references resolve against.</param>
    public PdfArray(PdfObjectStore? owner)

        : this(owner, DefaultCapacity)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PdfArray"/> class.</summary>
    /// <param name="owner">The objects references resolve against.</param>
    /// <param name="capacity">The initial capacity.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    public PdfArray(PdfObjectStore? owner, int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        Owner = owner;
        _items = capacity == 0 ? [] : new PdfValue[capacity];
    }

    /// <summary>Initializes a new instance of the <see cref="PdfArray"/> class holding items.</summary>
    /// <param name="owner">The objects references resolve against.</param>
    /// <param name="items">The items, copied.</param>
    public PdfArray(PdfObjectStore? owner, ReadOnlySpan<PdfValue> items)
    {
        Owner = owner;
        _items = items.ToArray();
        Count = items.Length;
    }

    /// <summary>Gets the objects references resolve against.</summary>
    public PdfObjectStore? Owner { get; }

    /// <summary>Gets the number of items.</summary>
    public int Count { get; private set; }

    /// <summary>Gets the items without resolving references.</summary>
    public ReadOnlySpan<PdfValue> Items => _items.AsSpan(0, Count);

    /// <summary>Creates an array of numbers.</summary>
    /// <param name="owner">The objects references resolve against.</param>
    /// <param name="numbers">The numbers.</param>
    /// <returns>The array.</returns>
    public static PdfArray FromNumbers(PdfObjectStore? owner, ReadOnlySpan<float> numbers)
    {
        var array = new PdfArray(owner, numbers.Length);
        foreach (var number in numbers)
        {
            array.Add(PdfNumber.ToValue(number));
        }

        return array;
    }

    /// <summary>Gets an item without resolving references.</summary>
    /// <param name="index">The item index.</param>
    /// <returns>The item, or null when out of range.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfValue GetRaw(int index) => (uint)index < (uint)Count ? _items[index] : default;

    /// <summary>Gets an item, following references.</summary>
    /// <param name="index">The item index.</param>
    /// <returns>The item, or null when out of range.</returns>
    public PdfValue Get(int index)
    {
        var value = GetRaw(index);
        return value.IsReference && Owner is not null ? StoreReading.Resolve(Owner, value) : value;
    }

    /// <summary>Gets a number, or zero when missing.</summary>
    /// <param name="index">The item index.</param>
    /// <returns>The number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetNumber(int index) => Get(index).AsNumber();

    /// <summary>Gets a number as a float, or zero when missing.</summary>
    /// <param name="index">The item index.</param>
    /// <returns>The number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetSingle(int index) => Get(index).AsSingle();

    /// <summary>Gets a 32-bit integer, or zero when missing.</summary>
    /// <param name="index">The item index.</param>
    /// <returns>The integer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetInt32(int index) => Get(index).AsInt32();

    /// <summary>Gets a dictionary, or a stream's dictionary.</summary>
    /// <param name="index">The item index.</param>
    /// <returns>The dictionary, or <see langword="null"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfDictionary? GetDictionary(int index) => Get(index).AsDictionary();

    /// <summary>Gets an array.</summary>
    /// <param name="index">The item index.</param>
    /// <returns>The array, or <see langword="null"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfArray? GetArray(int index) => Get(index).AsArray();

    /// <summary>Gets a name.</summary>
    /// <param name="index">The item index.</param>
    /// <returns>The name, or no name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfName GetName(int index) => Get(index).AsName();

    /// <summary>Reads the leading numeric items into a buffer.</summary>
    /// <param name="destination">The buffer.</param>
    /// <returns>The number of numbers read; stops at the first item that is not a number.</returns>
    public int ReadNumbers(Span<float> destination)
    {
        var count = Math.Min(destination.Length, Count);
        for (var i = 0; i < count; i++)
        {
            var value = Get(i);
            if (!value.IsNumber)
            {
                return i;
            }

            destination[i] = value.AsSingle();
        }

        return count;
    }

    /// <summary>Appends an item.</summary>
    /// <param name="value">The item.</param>
    public void Add(PdfValue value)
    {
        if (Count == _items.Length)
        {
            Array.Resize(ref _items, Math.Max(DefaultCapacity, Count * GrowthFactor));
        }

        _items[Count] = value;
        Count++;
    }

    /// <summary>Inserts an item, moving the items after it up by one.</summary>
    /// <param name="index">The position of the new item; the count appends.</param>
    /// <param name="value">The item.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative or above the count.</exception>
    public void Insert(int index, PdfValue value)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)index, (uint)Count, nameof(index));
        if (Count == _items.Length)
        {
            Array.Resize(ref _items, Math.Max(DefaultCapacity, Count * GrowthFactor));
        }

        Array.Copy(_items, index, _items, index + 1, Count - index);
        _items[index] = value;
        Count++;
    }

    /// <summary>Replaces an item.</summary>
    /// <param name="index">The item index.</param>
    /// <param name="value">The item.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    public void SetAt(int index, PdfValue value)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)Count, nameof(index));
        _items[index] = value;
    }

    /// <summary>Removes an item.</summary>
    /// <param name="index">The item index.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    public void RemoveAt(int index)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)Count, nameof(index));
        Count--;
        Array.Copy(_items, index + 1, _items, index, Count - index);
        _items[Count] = default;
    }

    /// <summary>Copies the array, sharing nested values, for editing.</summary>
    /// <returns>The copy.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfArray Clone() => new(Owner, Items);

    /// <summary>Copies the array with room for more items, so adding them does not grow the copy again.</summary>
    /// <param name="extra">The items the caller is about to add.</param>
    /// <returns>The copy.</returns>
    internal PdfArray CloneWithRoom(int extra)
    {
        var copy = new PdfArray(Owner, Count + extra);
        Items.CopyTo(copy._items);
        copy.Count = Count;
        return copy;
    }
}
