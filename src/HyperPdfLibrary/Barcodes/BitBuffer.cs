// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Barcodes;

/// <summary>A growing list of bits, written most significant bit first, that packs into bytes.</summary>
[DebuggerDisplay("BitBuffer: {Length} bits")]
internal sealed class BitBuffer
{
    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bits in a byte, less one: the highest bit's index, and the amount to round a bit count up by.</summary>
    private const int HighBit = ByteBits - 1;

    /// <summary>The bits so far.</summary>
    private readonly List<bool> _bits = [];

    /// <summary>Gets how many bits there are.</summary>
    internal int Length => _bits.Count;

    /// <summary>Adds the low bits of a value.</summary>
    /// <param name="value">The value.</param>
    /// <param name="count">How many of its low bits to add, from the highest of them down.</param>
    internal void Append(int value, int count)
    {
        for (var bit = count - 1; bit >= 0; bit--)
        {
            _bits.Add(((value >> bit) & 1) != 0);
        }
    }

    /// <summary>Packs the bits into bytes, filling the last byte with zero bits.</summary>
    /// <returns>The bytes.</returns>
    internal byte[] ToBytes()
    {
        var bytes = new byte[(_bits.Count + HighBit) / ByteBits];
        for (var i = 0; i < _bits.Count; i++)
        {
            if (_bits[i])
            {
                bytes[i / ByteBits] |= (byte)(1 << (HighBit - (i % ByteBits)));
            }
        }

        return bytes;
    }
}
