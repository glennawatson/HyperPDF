// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>Writes packet header bits (ISO 15444-1 B.10.1): most significant first, seven bits in a byte after 0xFF.</summary>
internal sealed class JpxTestBitWriter
{
    /// <summary>The bits of a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bits of a byte after 0xFF.</summary>
    private const int StuffedBits = 7;

    /// <summary>The value of a byte that is all ones.</summary>
    private const int AllOnes = 0xFF;

    /// <summary>The bytes written.</summary>
    private readonly List<byte> _bytes = [];

    /// <summary>The byte being filled.</summary>
    private int _current;

    /// <summary>The free bits of the byte being filled.</summary>
    private int _free = ByteBits;

    /// <summary>The bits the byte being filled holds.</summary>
    private int _capacity = ByteBits;

    /// <summary>Writes one bit.</summary>
    /// <param name="bit">The bit.</param>
    internal void WriteBit(int bit)
    {
        _free--;
        _current |= (bit & 1) << _free;
        if (_free == 0)
        {
            Emit();
        }
    }

    /// <summary>Writes bits, most significant first.</summary>
    /// <param name="value">The value.</param>
    /// <param name="count">The number of bits.</param>
    internal void WriteBits(int value, int count)
    {
        for (var i = count - 1; i >= 0; i--)
        {
            WriteBit((value >> i) & 1);
        }
    }

    /// <summary>Pads to a byte boundary, adds a byte after a final 0xFF, and returns the header bytes.</summary>
    /// <returns>The bytes.</returns>
    internal byte[] Finish()
    {
        if (_free != _capacity)
        {
            Emit();
        }

        if (_bytes.Count > 0 && _bytes[^1] == AllOnes)
        {
            _bytes.Add(0);
        }

        return [.. _bytes];
    }

    /// <summary>Moves the byte being filled to the output.</summary>
    private void Emit()
    {
        _bytes.Add((byte)_current);
        _capacity = _current == AllOnes ? StuffedBits : ByteBits;
        _free = _capacity;
        _current = 0;
    }
}
