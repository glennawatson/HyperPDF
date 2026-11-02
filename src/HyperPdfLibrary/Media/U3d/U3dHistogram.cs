// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Media.U3d;

/// <summary>
/// The adaptive frequency counts of one dynamic compression context (ECMA-363 clause 10). Symbol 0 is the escape symbol, which
/// starts with a count of one; a value v is symbol v + 1. The counts halve when their total reaches a limit, so the model follows
/// recent values.
/// </summary>
internal sealed class U3dHistogram
{
    /// <summary>The total count at which every count is halved.</summary>
    private const uint HalvingTotal = 0x1FFF;

    /// <summary>The first symbol the histogram does not count.</summary>
    private const uint SymbolLimit = 0xFFFF;

    /// <summary>The slots added when the histogram grows.</summary>
    private const int Growth = 32;

    /// <summary>How many times each symbol has been seen.</summary>
    private ushort[] _counts = NewCounts(Growth);

    /// <summary>For each symbol, how many times it and every larger symbol have been seen.</summary>
    private ushort[] _upper = NewCounts(Growth);

    /// <summary>Gets the total count of every symbol, escape included.</summary>
    internal uint Total => _upper[0];

    /// <summary>Gets the count of one symbol.</summary>
    /// <param name="symbol">The symbol.</param>
    /// <returns>How many times it was seen; the escape symbol starts at one.</returns>
    internal uint CountOf(uint symbol) => symbol < _counts.Length ? _counts[symbol] : 0U;

    /// <summary>Gets the sum of the counts of every symbol smaller than a symbol.</summary>
    /// <param name="symbol">The symbol.</param>
    /// <returns>The sum.</returns>
    internal uint CountBelow(uint symbol) => symbol < _upper.Length ? (uint)(_upper[0] - _upper[symbol]) : _upper[0];

    /// <summary>Finds the symbol whose share of the total holds a point.</summary>
    /// <param name="point">A point from zero up to the total.</param>
    /// <returns>The last symbol whose lower bound is at or below the point; the escape symbol when the point is outside the total.</returns>
    internal uint SymbolAt(uint point)
    {
        if (point == 0 || point > Total)
        {
            return 0;
        }

        uint found = 0;
        for (uint symbol = 0; symbol < _upper.Length && CountBelow(symbol) <= point; symbol++)
        {
            found = symbol;
        }

        return found;
    }

    /// <summary>Counts one more of a symbol, halving every count first when the total has reached its limit.</summary>
    /// <param name="symbol">The symbol, with the escape symbol as zero.</param>
    internal void Add(uint symbol)
    {
        if (symbol >= SymbolLimit)
        {
            return;
        }

        if (symbol >= _counts.Length)
        {
            Grow((int)symbol + Growth);
        }

        if (_upper[0] >= HalvingTotal)
        {
            Halve();
        }

        _counts[symbol]++;
        for (var i = 0; i <= symbol; i++)
        {
            _upper[i]++;
        }
    }

    /// <summary>Makes a count array whose escape symbol has a count of one.</summary>
    /// <param name="size">The length.</param>
    /// <returns>The array.</returns>
    private static ushort[] NewCounts(int size)
    {
        var counts = new ushort[size];
        counts[0] = 1;
        return counts;
    }

    /// <summary>Makes the arrays at least a size, keeping the counts.</summary>
    /// <param name="size">The new length.</param>
    private void Grow(int size)
    {
        Array.Resize(ref _counts, size);
        Array.Resize(ref _upper, size);
    }

    /// <summary>Halves every count, then adds one to the escape symbol so it can always be written.</summary>
    private void Halve()
    {
        ushort sum = 0;
        for (var i = _counts.Length - 1; i >= 0; i--)
        {
            _counts[i] >>= 1;
            sum += _counts[i];
            _upper[i] = sum;
        }

        _counts[0]++;
        _upper[0]++;
    }
}
