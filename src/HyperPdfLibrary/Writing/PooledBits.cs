// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Writing;

/// <summary>A set of small non-negative integers held as bits in an array rented from the shared pool. Dispose returns the array.</summary>
internal ref struct PooledBits : IDisposable
{
    /// <summary>The bits in one word.</summary>
    private const int WordBits = 64;

    /// <summary>The shift that turns a bit number into a word number.</summary>
    private const int WordShift = 6;

    /// <summary>The mask that keeps a bit number's place in its word.</summary>
    private const int BitMask = WordBits - 1;

    /// <summary>The rented words.</summary>
    private ulong[]? _words;

    /// <summary>The words in use.</summary>
    private int _wordCount;

    /// <summary>Returns the words to the pool.</summary>
    public void Dispose()
    {
        if (_words is { } words)
        {
            ArrayPool<ulong>.Shared.Return(words);
        }

        _words = null;
        _wordCount = 0;
    }

    /// <summary>Rents the words of an empty set that can hold numbers below a limit.</summary>
    /// <param name="limit">One more than the largest number the set holds.</param>
    internal void Rent(int limit)
    {
        var count = (limit + BitMask) >> WordShift;
        var words = ArrayPool<ulong>.Shared.Rent(Math.Max(count, 1));
        words.AsSpan(0, count).Clear();
        _words = words;
        _wordCount = count;
    }

    /// <summary>Adds a number.</summary>
    /// <param name="bit">The number.</param>
    /// <returns><see langword="true"/> when it was not in the set.</returns>
    internal readonly bool Add(int bit)
    {
        ref var word = ref _words![bit >> WordShift];
        var mask = 1UL << (bit & BitMask);
        if ((word & mask) != 0)
        {
            return false;
        }

        word |= mask;
        return true;
    }

    /// <summary>Determines whether a number is in the set.</summary>
    /// <param name="bit">The number.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly bool Contains(int bit) => (_words![bit >> WordShift] & (1UL << (bit & BitMask))) != 0;

    /// <summary>Empties the set.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly void Clear() => _words.AsSpan(0, _wordCount).Clear();

    /// <summary>Appends the numbers of the set to a list, smallest first.</summary>
    /// <param name="output">Receives the numbers.</param>
    internal readonly void CopyTo(ref PooledInts output)
    {
        var words = _words.AsSpan(0, _wordCount);
        for (var i = 0; i < words.Length; i++)
        {
            var word = words[i];
            while (word != 0)
            {
                output.Add((i << WordShift) + BitOperations.TrailingZeroCount(word));
                word &= word - 1;
            }
        }
    }
}
