// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Syntax;

/// <summary>
/// An operation-local stack the parser collects array items and dictionary entries on, so each array or dictionary is
/// allocated once at its final size instead of growing.
/// </summary>
internal ref struct ParseScratch
{
    /// <summary>The values kept on the stack before an overflow array is needed.</summary>
    private const int InlineCapacity = 64;

    /// <summary>The factor the capacity grows by.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The values before the first overflow.</summary>
    private ValueBuffer _inlineValues;

    /// <summary>The keys before the first overflow.</summary>
    private KeyBuffer _inlineKeys;

    /// <summary>The values after an overflow, or <see langword="null"/> while they fit inline.</summary>
    private PdfValue[]? _values;

    /// <summary>The keys after an overflow, parallel to <see cref="_values"/>.</summary>
    private int[]? _keys;

    /// <summary>Gets the number of values on the stack.</summary>
    internal int Count { get; private set; }

    /// <summary>Pushes a value.</summary>
    /// <param name="key">The dictionary key, or zero for an array item.</param>
    /// <param name="value">The value.</param>
    internal void Push(int key, PdfValue value)
    {
        if (_values is null && Count == InlineCapacity)
        {
            _values = ArrayPool<PdfValue>.Shared.Rent(InlineCapacity * GrowthFactor);
            _keys = ArrayPool<int>.Shared.Rent(InlineCapacity * GrowthFactor);
            Span<PdfValue> values = _inlineValues;
            Span<int> keys = _inlineKeys;
            values.CopyTo(_values);
            keys.CopyTo(_keys);
        }

        if (_values is not null)
        {
            if (Count == _values.Length)
            {
                GrowOverflow();
            }

            _keys![Count] = key;
            _values[Count] = value;
        }
        else if (Count == 0)
        {
            _inlineKeys.SetFirst(key);
            _inlineValues.SetFirst(value);
        }
        else
        {
            _inlineKeys[Count] = key;
            _inlineValues[Count] = value;
        }

        Count++;
    }

    /// <summary>Pops the values pushed since a mark into a new array.</summary>
    /// <param name="mark">The count before the first item was pushed.</param>
    /// <param name="owner">The objects references resolve against.</param>
    /// <returns>The array.</returns>
    internal PdfArray PopArray(int mark, PdfObjectStore? owner)
    {
        if (_values is null && mark == 0 && Count == 1)
        {
            var first = _inlineValues.GetFirst();
            var single = new PdfArray(owner, new ReadOnlySpan<PdfValue>(in first));
            Release(0);
            return single;
        }

        Span<PdfValue> values = _values is null ? _inlineValues : _values;
        var array = new PdfArray(owner, values.Slice(mark, Count - mark));
        Release(mark);
        return array;
    }

    /// <summary>Pops the entries pushed since a mark into a new dictionary.</summary>
    /// <param name="mark">The count before the first entry was pushed.</param>
    /// <param name="owner">The objects references resolve against.</param>
    /// <returns>The dictionary.</returns>
    internal PdfDictionary PopDictionary(int mark, PdfObjectStore? owner)
    {
        var dictionary = new PdfDictionary(owner, Count - mark);
        if (_values is null && mark == 0 && Count == 1)
        {
            dictionary.Add(new(_inlineKeys.GetFirst()), _inlineValues.GetFirst());
            Release(0);
            return dictionary;
        }

        Span<PdfValue> values = _values is null ? _inlineValues : _values;
        Span<int> keys = _keys is null ? _inlineKeys : _keys;
        for (var i = mark; i < Count; i++)
        {
            dictionary.Add(new(keys[i]), values[i]);
        }

        Release(mark);
        return dictionary;
    }

    /// <summary>Gives the overflow arrays, if any were needed, back to the pool.</summary>
    internal void Dispose()
    {
        if (_values is null)
        {
            return;
        }

        ArrayPool<PdfValue>.Shared.Return(_values, true);
        ArrayPool<int>.Shared.Return(_keys!);
        _values = null;
        _keys = null;
    }

    /// <summary>Drops the values above a mark, clearing references so they can be collected.</summary>
    /// <param name="mark">The count to return to.</param>
    internal void Release(int mark)
    {
        Span<PdfValue> values = _values is null ? _inlineValues : _values;
        values.Slice(mark, Count - mark).Clear();
        Count = mark;
    }

    /// <summary>Moves the overflow arrays into larger ones from the pool.</summary>
    private void GrowOverflow()
    {
        var values = ArrayPool<PdfValue>.Shared.Rent(Count * GrowthFactor);
        var keys = ArrayPool<int>.Shared.Rent(Count * GrowthFactor);
        _values.AsSpan(0, Count).CopyTo(values);
        _keys.AsSpan(0, Count).CopyTo(keys);
        ArrayPool<PdfValue>.Shared.Return(_values!, true);
        ArrayPool<int>.Shared.Return(_keys!);
        _values = values;
        _keys = keys;
    }

    /// <summary>The inline values.</summary>
    [InlineArray(InlineCapacity)]
    private struct ValueBuffer
    {
        /// <summary>The first value.</summary>
        private PdfValue _element0;

        /// <summary>Sets the first value.</summary>
        /// <param name="value">The value.</param>
        internal void SetFirst(PdfValue value) => _element0 = value;

        /// <summary>Gets the first value.</summary>
        /// <returns>The value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly PdfValue GetFirst() => _element0;
    }

    /// <summary>The inline keys.</summary>
    [InlineArray(InlineCapacity)]
    private struct KeyBuffer
    {
        /// <summary>The first key.</summary>
        private int _element0;

        /// <summary>Sets the first key.</summary>
        /// <param name="key">The key.</param>
        internal void SetFirst(int key) => _element0 = key;

        /// <summary>Gets the first key.</summary>
        /// <returns>The key.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly int GetFirst() => _element0;
    }
}
