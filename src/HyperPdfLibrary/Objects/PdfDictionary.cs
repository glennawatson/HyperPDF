// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Objects;

/// <summary>
/// A PDF dictionary. Keys are interned name ids held in a flat array, so a lookup is one vectorised search over a few
/// integers. Getters resolve references through the owning document. Parsed dictionaries are not changed; editing works
/// on copies.
/// </summary>
[DebuggerDisplay("PdfDictionary: {Count} entries")]
public sealed class PdfDictionary
{
    /// <summary>The capacity of a dictionary created empty.</summary>
    private const int DefaultCapacity = 4;

    /// <summary>The factor the capacity grows by.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The key ids.</summary>
    private int[] _keys;

    /// <summary>The values, parallel to <see cref="_keys"/>.</summary>
    private PdfValue[] _values;

    /// <summary>Initializes a new instance of the <see cref="PdfDictionary"/> class.</summary>
    /// <param name="owner">The objects references resolve against.</param>
    public PdfDictionary(PdfObjectStore? owner)

        : this(owner, DefaultCapacity)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PdfDictionary"/> class.</summary>
    /// <param name="owner">The objects references resolve against.</param>
    /// <param name="capacity">The initial capacity.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    public PdfDictionary(PdfObjectStore? owner, int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        Owner = owner;
        _keys = capacity == 0 ? [] : new int[capacity];
        _values = capacity == 0 ? [] : new PdfValue[capacity];
    }

    /// <summary>Gets the objects references resolve against.</summary>
    public PdfObjectStore? Owner { get; }

    /// <summary>Gets the number of entries.</summary>
    public int Count { get; private set; }

    /// <summary>Gets the key of an entry.</summary>
    /// <param name="index">The entry index.</param>
    /// <returns>The key.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    public PdfName GetKeyAt(int index)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)Count, nameof(index));
        return new(_keys[index]);
    }

    /// <summary>Gets the value of an entry, without resolving references.</summary>
    /// <param name="index">The entry index.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    public PdfValue GetValueAt(int index)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)Count, nameof(index));
        return _values[index];
    }

    /// <summary>Determines whether the dictionary has a key.</summary>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when present.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsKey(PdfName key) => IndexOf(key) >= 0;

    /// <summary>Gets a value without resolving references.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The value, or null when missing.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfValue GetRaw(PdfName key)
    {
        var index = IndexOf(key);
        return index >= 0 ? _values[index] : default;
    }

    /// <summary>Gets a value, following references.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The value, or null when missing.</returns>
    public PdfValue Get(PdfName key)
    {
        var value = GetRaw(key);
        return value.IsReference && Owner is not null ? StoreReading.Resolve(Owner, value) : value;
    }

    /// <summary>Gets a dictionary, or a stream's dictionary.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The dictionary, or <see langword="null"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfDictionary? GetDictionary(PdfName key) => Get(key).AsDictionary();

    /// <summary>Gets an array.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The array, or <see langword="null"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfArray? GetArray(PdfName key) => Get(key).AsArray();

    /// <summary>Gets a stream.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The stream, or <see langword="null"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfStream? GetStream(PdfName key) => Get(key).AsStream();

    /// <summary>Gets a name.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The name, or no name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfName GetName(PdfName key) => Get(key).AsName();

    /// <summary>Determines whether a key holds a certain name.</summary>
    /// <param name="key">The key.</param>
    /// <param name="name">The expected name.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsName(PdfName key, KnownName name) => Get(key).IsName(name);

    /// <summary>Gets a number, or zero when missing.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetNumber(PdfName key) => Get(key).AsNumber();

    /// <summary>Gets a number.</summary>
    /// <param name="key">The key.</param>
    /// <param name="fallback">The value used when missing.</param>
    /// <returns>The number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetNumber(PdfName key, double fallback) => Get(key).AsNumber(fallback);

    /// <summary>Gets a number as a float, or zero when missing.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetSingle(PdfName key) => Get(key).AsSingle();

    /// <summary>Gets a number as a float.</summary>
    /// <param name="key">The key.</param>
    /// <param name="fallback">The value used when missing.</param>
    /// <returns>The number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetSingle(PdfName key, float fallback) => Get(key).AsSingle(fallback);

    /// <summary>Gets an integer, or zero when missing.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The integer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long GetInteger(PdfName key) => Get(key).AsInteger();

    /// <summary>Gets an integer.</summary>
    /// <param name="key">The key.</param>
    /// <param name="fallback">The value used when missing.</param>
    /// <returns>The integer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long GetInteger(PdfName key, long fallback) => Get(key).AsInteger(fallback);

    /// <summary>Gets a 32-bit integer, or zero when missing.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The integer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetInt32(PdfName key) => Get(key).AsInt32();

    /// <summary>Gets a 32-bit integer.</summary>
    /// <param name="key">The key.</param>
    /// <param name="fallback">The value used when missing.</param>
    /// <returns>The integer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetInt32(PdfName key, int fallback) => Get(key).AsInt32(fallback);

    /// <summary>Gets a boolean, or <see langword="false"/> when missing.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The boolean.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool GetBoolean(PdfName key) => Get(key).AsBoolean();

    /// <summary>Gets a boolean.</summary>
    /// <param name="key">The key.</param>
    /// <param name="fallback">The value used when missing.</param>
    /// <returns>The boolean.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool GetBoolean(PdfName key, bool fallback) => Get(key).AsBoolean(fallback);

    /// <summary>Gets a string's bytes.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The bytes, or empty.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> GetStringBytes(PdfName key) => Get(key).AsStringBytes();

    /// <summary>Gets a text string, decoding PDFDocEncoding, UTF-16 or UTF-8.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The text, or <see langword="null"/> when missing.</returns>
    public string? GetText(PdfName key)
    {
        var value = Get(key);
        return value.Kind == PdfKind.String ? PdfText.Decode(value.AsStringBytes()) : null;
    }

    /// <summary>Gets a rectangle stored as an array of four numbers, normalised so the first corner is lower-left.</summary>
    /// <param name="key">The key.</param>
    /// <param name="rectangle">The rectangle.</param>
    /// <returns><see langword="true"/> when present and well formed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetRectangle(PdfName key, out PdfRectangle rectangle) => PdfRectangle.TryFromArray(GetArray(key), out rectangle);

    /// <summary>Sets a value.</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value; null removes the entry.</param>
    public void Set(PdfName key, PdfValue value)
    {
        if (value.IsNull)
        {
            _ = Remove(key);
            return;
        }

        var index = IndexOf(key);
        if (index >= 0)
        {
            _values[index] = value;
            return;
        }

        Add(key, value);
    }

    /// <summary>Adds an entry without checking for a duplicate key; the parser uses this, and the last duplicate wins.</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    public void Add(PdfName key, PdfValue value)
    {
        if (Count == _keys.Length)
        {
            var capacity = Math.Max(DefaultCapacity, Count * GrowthFactor);
            Array.Resize(ref _keys, capacity);
            Array.Resize(ref _values, capacity);
        }

        _keys[Count] = key.Id;
        _values[Count] = value;
        Count++;
    }

    /// <summary>Removes an entry.</summary>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when removed.</returns>
    public bool Remove(PdfName key)
    {
        var index = IndexOf(key);
        if (index < 0)
        {
            return false;
        }

        Count--;
        Array.Copy(_keys, index + 1, _keys, index, Count - index);
        Array.Copy(_values, index + 1, _values, index, Count - index);
        _values[Count] = default;
        return true;
    }

    /// <summary>Copies the dictionary, sharing nested values, for editing.</summary>
    /// <returns>The copy.</returns>
    public PdfDictionary Clone()
    {
        var copy = new PdfDictionary(Owner, Count);
        _keys.AsSpan(0, Count).CopyTo(copy._keys);
        _values.AsSpan(0, Count).CopyTo(copy._values);
        copy.Count = Count;
        return copy;
    }

    /// <summary>Copies the dictionary with room for more entries, so adding them does not grow the copy again.</summary>
    /// <param name="extra">The entries the caller is about to add.</param>
    /// <returns>The copy.</returns>
    internal PdfDictionary CloneWithRoom(int extra)
    {
        var copy = new PdfDictionary(Owner, Count + extra);
        _keys.AsSpan(0, Count).CopyTo(copy._keys);
        _values.AsSpan(0, Count).CopyTo(copy._values);
        copy.Count = Count;
        return copy;
    }

    /// <summary>Determines whether a later entry has the same key, so this one is hidden: the last of duplicate keys wins.</summary>
    /// <param name="index">The entry index.</param>
    /// <returns><see langword="true"/> when a later entry repeats the key.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsShadowed(int index) => _keys.AsSpan(index + 1, Count - index - 1).Contains(_keys[index]);

    /// <summary>Finds a key. The last of duplicate keys wins, matching other readers.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The entry index, or -1.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int IndexOf(PdfName key) => _keys.AsSpan(0, Count).LastIndexOf(key.Id);
}
