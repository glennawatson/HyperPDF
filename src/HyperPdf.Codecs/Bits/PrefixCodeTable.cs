// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs.Bits;

/// <summary>
/// A prefix code read with two table lookups. The first lookup uses the next few bits and either names the symbol or points to a
/// second table for the codes that are longer than that. The table never changes after it is built, so decoders share it.
/// </summary>
[DebuggerDisplay("PrefixCodeTable: {_maxLength} bits at most")]
internal sealed class PrefixCodeTable
{
    /// <summary>The symbol <see cref="Decode"/> returns when the bits are not the start of any code.</summary>
    internal const int Invalid = -1;

    /// <summary>The bits that a first lookup uses at most.</summary>
    private const int RootBitsLimit = 10;

    /// <summary>The bits that hold the code length in an entry.</summary>
    private const int LengthBits = 5;

    /// <summary>The mask of the code length in an entry.</summary>
    private const int LengthMask = (1 << LengthBits) - 1;

    /// <summary>The flag of an entry that points to a second table.</summary>
    private const int LinkFlag = int.MinValue;

    /// <summary>The first table.</summary>
    private readonly int[] _root;

    /// <summary>The second tables, one after another.</summary>
    private readonly int[] _links;

    /// <summary>The bits the first lookup uses.</summary>
    private readonly int _rootBits;

    /// <summary>The longest code.</summary>
    private readonly int _maxLength;

    /// <summary>Initializes a new instance of the <see cref="PrefixCodeTable"/> class.</summary>
    /// <param name="lengths">The length of each symbol's code in bits, or 0 for a symbol that has no code.</param>
    /// <param name="codes">The code of each symbol, right-aligned.</param>
    internal PrefixCodeTable(ReadOnlySpan<byte> lengths, ReadOnlySpan<uint> codes)
    {
        _maxLength = 0;
        foreach (var length in lengths)
        {
            _maxLength = Math.Max(_maxLength, length);
        }

        _rootBits = Math.Min(_maxLength, RootBitsLimit);
        _root = new int[1 << _rootBits];
        var linkBits = CountLinkBits(lengths, codes, out var linkSize);
        _links = new int[linkSize];
        Fill(lengths, codes, linkBits);
    }

    /// <summary>Reads one symbol.</summary>
    /// <param name="reader">The bits.</param>
    /// <returns>The symbol, or <see cref="Invalid"/> when the bits are not the start of any code, in which case nothing is consumed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int Decode(ref MsbBitReader reader)
    {
        var bits = (int)reader.Peek(_maxLength);
        var entry = _root[bits >> (_maxLength - _rootBits)];
        if (entry < 0)
        {
            var linkBits = entry & LengthMask;
            var start = (entry & int.MaxValue) >> LengthBits;
            var index = (bits >> (_maxLength - _rootBits - linkBits)) & ((1 << linkBits) - 1);
            entry = _links[start + index];
        }

        var length = entry & LengthMask;
        if (length == 0)
        {
            return Invalid;
        }

        reader.Skip(length);
        return entry >> LengthBits;
    }

    /// <summary>Finds how many bits the second table of each first-table slot needs.</summary>
    /// <param name="lengths">The code lengths.</param>
    /// <param name="codes">The codes.</param>
    /// <param name="size">Receives the total size of the second tables.</param>
    /// <returns>The bits of the second table for each slot, or 0 for a slot that has none.</returns>
    private int[] CountLinkBits(ReadOnlySpan<byte> lengths, ReadOnlySpan<uint> codes, out int size)
    {
        var bits = new int[_root.Length];
        for (var symbol = 0; symbol < lengths.Length; symbol++)
        {
            var extra = lengths[symbol] - _rootBits;
            if (extra <= 0)
            {
                continue;
            }

            var slot = (int)(codes[symbol] >> extra);
            bits[slot] = Math.Max(bits[slot], extra);
        }

        size = 0;
        foreach (var count in bits)
        {
            size += count == 0 ? 0 : 1 << count;
        }

        return bits;
    }

    /// <summary>Fills both tables.</summary>
    /// <param name="lengths">The code lengths.</param>
    /// <param name="codes">The codes.</param>
    /// <param name="linkBits">The bits of the second table of each slot.</param>
    private void Fill(ReadOnlySpan<byte> lengths, ReadOnlySpan<uint> codes, int[] linkBits)
    {
        var next = 0;
        for (var slot = 0; slot < linkBits.Length; slot++)
        {
            if (linkBits[slot] == 0)
            {
                continue;
            }

            _root[slot] = LinkFlag | (next << LengthBits) | linkBits[slot];
            next += 1 << linkBits[slot];
        }

        for (var symbol = 0; symbol < lengths.Length; symbol++)
        {
            var length = lengths[symbol];
            if (length == 0)
            {
                continue;
            }

            var entry = (symbol << LengthBits) | length;
            if (length <= _rootBits)
            {
                var shift = _rootBits - length;
                var first = (int)(codes[symbol] << shift);
                _root.AsSpan(first, 1 << shift).Fill(entry);
                continue;
            }

            var extra = length - _rootBits;
            var link = _root[(int)(codes[symbol] >> extra)];
            var bits = link & LengthMask;
            var start = (link & int.MaxValue) >> LengthBits;
            var remaining = bits - extra;
            var offset = (int)(codes[symbol] & ((1U << extra) - 1)) << remaining;
            _links.AsSpan(start + offset, 1 << remaining).Fill(entry);
        }
    }
}
