// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Numerics;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// A set of integers kept as bits, for the character codes and glyph numbers of one font. The bits grow on demand, so a
/// simple font's 256 codes cost four words. Values from zero up to <see cref="BitLimit"/> are bits; any other value goes
/// to a small overflow set that is rarely made. The words come from the shared array pool when the set is pooled.
/// </summary>
[DebuggerDisplay("CodeBitSet: {Count} values")]
internal sealed class CodeBitSet : IDisposable
{
    /// <summary>The first value that does not fit in the bits.</summary>
    internal const int BitLimit = 1 << 20;

    /// <summary>The value <see cref="NextBit"/> returns when no bit is left.</summary>
    internal const int NoBit = -1;

    /// <summary>The shift that turns a value into its word index.</summary>
    private const int WordShift = 6;

    /// <summary>The mask that selects a value's bit within its word.</summary>
    private const int BitMask = (1 << WordShift) - 1;

    /// <summary>The words a new set starts with, enough for 256 values.</summary>
    private const int InitialWords = 4;

    /// <summary>The factor the word array grows by.</summary>
    private const int GrowthFactor = 2;

    /// <summary>Whether the words are rented from the shared array pool.</summary>
    private readonly bool _pooled;

    /// <summary>The bits; the array may be longer than <see cref="_words"/> implies.</summary>
    private ulong[] _bits = [];

    /// <summary>The words of <see cref="_bits"/> in use.</summary>
    private int _words;

    /// <summary>The number of values in the bits.</summary>
    private int _bitCount;

    /// <summary>Initializes a new instance of the <see cref="CodeBitSet"/> class that allocates its own words.</summary>
    internal CodeBitSet()
        : this(false)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CodeBitSet"/> class.</summary>
    /// <param name="pooled">Whether to rent the words from the shared array pool; the set must then be disposed.</param>
    internal CodeBitSet(bool pooled) => _pooled = pooled;

    /// <summary>Gets the number of values in the set.</summary>
    internal int Count => _bitCount + (Overflow?.Count ?? 0);

    /// <summary>Gets the values that are not bits (negative or at least <see cref="BitLimit"/>), or <see langword="null"/> when there are none.</summary>
    internal HashSet<int>? Overflow { get; private set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_pooled && _bits.Length > 0)
        {
            ArrayPool<ulong>.Shared.Return(_bits);
        }

        _bits = [];
        _words = 0;
        _bitCount = 0;
        Overflow = null;
    }

    /// <summary>Adds a value.</summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when the value was not in the set.</returns>
    internal bool Add(int value)
    {
        if ((uint)value >= BitLimit)
        {
            Overflow ??= [];
            return Overflow.Add(value);
        }

        var word = value >> WordShift;
        if (word >= _words)
        {
            Grow(word + 1);
        }

        var bit = 1UL << (value & BitMask);
        if ((_bits[word] & bit) != 0)
        {
            return false;
        }

        _bits[word] |= bit;
        _bitCount++;
        return true;
    }

    /// <summary>Determines whether a value is in the set.</summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    internal bool Contains(int value)
    {
        if ((uint)value >= BitLimit)
        {
            return Overflow?.Contains(value) ?? false;
        }

        var word = value >> WordShift;
        return word < _words && (_bits[word] & (1UL << (value & BitMask))) != 0;
    }

    /// <summary>Finds the smallest bit value at or after a value; walk the set with <c>for (v = NextBit(0); v != NoBit; v = NextBit(v + 1))</c>.</summary>
    /// <param name="from">The first value to consider.</param>
    /// <returns>The value, or <see cref="NoBit"/> when none is left. Values in <see cref="Overflow"/> are not found.</returns>
    internal int NextBit(int from)
    {
        var word = from >> WordShift;
        if (from < 0 || word >= _words)
        {
            return NoBit;
        }

        var remaining = _bits[word] & (ulong.MaxValue << (from & BitMask));
        while (remaining == 0)
        {
            word++;
            if (word >= _words)
            {
                return NoBit;
            }

            remaining = _bits[word];
        }

        return (word << WordShift) + BitOperations.TrailingZeroCount(remaining);
    }

    /// <summary>Adds every value of another set.</summary>
    /// <param name="other">The other set.</param>
    internal void UnionWith(CodeBitSet other)
    {
        if (other._words > _words)
        {
            Grow(other._words);
        }

        var count = 0;
        for (var i = 0; i < _words; i++)
        {
            if (i < other._words)
            {
                _bits[i] |= other._bits[i];
            }

            count += BitOperations.PopCount(_bits[i]);
        }

        _bitCount = count;
        if (other.Overflow is not { } overflow)
        {
            return;
        }

        Overflow ??= [];
        Overflow.UnionWith(overflow);
    }

    /// <summary>Makes room for at least a number of words, keeping the bits and clearing the new ones.</summary>
    /// <param name="words">The words needed.</param>
    private void Grow(int words)
    {
        if (words <= _bits.Length)
        {
            // Pooled arrays are not cleared on return, so words past the old end are cleared when first used.
            _bits.AsSpan(_words, words - _words).Clear();
            _words = words;
            return;
        }

        var size = Math.Max(Math.Max(words, _bits.Length * GrowthFactor), InitialWords);
        var bigger = _pooled ? ArrayPool<ulong>.Shared.Rent(size) : new ulong[size];
        _bits.AsSpan(0, _words).CopyTo(bigger);
        bigger.AsSpan(_words).Clear();
        if (_pooled && _bits.Length > 0)
        {
            ArrayPool<ulong>.Shared.Return(_bits);
        }

        _bits = bigger;
        _words = words;
    }
}
