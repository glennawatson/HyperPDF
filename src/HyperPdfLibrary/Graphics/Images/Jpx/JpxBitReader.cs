// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// Reads packet header bits (ISO 15444-1 B.10.1): most significant bit first, with a zero bit stuffed after every 0xFF
/// byte. Past the end it reads zeros, our rule for truncated data, so a damaged header ends the packet instead of throwing.
/// </summary>
internal ref struct JpxBitReader
{
    /// <summary>The bits of a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The two-byte window that marks a stuffed bit.</summary>
    private const int StuffedWindow = 0xFF00;

    /// <summary>The mask of the two-byte window.</summary>
    private const int WindowMask = 0xFFFF;

    /// <summary>The value of a byte that is all ones.</summary>
    private const int AllOnes = 0xFF;

    /// <summary>The bytes being read.</summary>
    private readonly ReadOnlySpan<byte> _data;

    /// <summary>The last two bytes read.</summary>
    private int _window;

    /// <summary>The bits left in the current byte.</summary>
    private int _count;

    /// <summary>Initializes a new instance of the <see cref="JpxBitReader"/> struct.</summary>
    /// <param name="data">The bytes, starting at the packet header.</param>
    internal JpxBitReader(ReadOnlySpan<byte> data) => _data = data;

    /// <summary>Gets the bytes consumed so far.</summary>
    internal int Position { get; private set; }

    /// <summary>Gets a value indicating whether a read went past the end of the data.</summary>
    internal bool Overran { get; private set; }

    /// <summary>Reads one bit.</summary>
    /// <returns>The bit.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int ReadBit()
    {
        if (_count == 0)
        {
            ReadByte();
        }

        _count--;
        return (_window >> _count) & 1;
    }

    /// <summary>Reads bits as an unsigned number, most significant first.</summary>
    /// <param name="count">The number of bits, at most 32.</param>
    /// <returns>The value.</returns>
    internal uint ReadBits(int count)
    {
        var value = 0U;
        for (var i = 0; i < count; i++)
        {
            value = (value << 1) | (uint)ReadBit();
        }

        return value;
    }

    /// <summary>Skips to the end of the current byte, and past a zero byte stuffed after 0xFF.</summary>
    internal void Align()
    {
        if ((_window & AllOnes) == AllOnes)
        {
            ReadByte();
        }

        _count = 0;
    }

    /// <summary>Loads the next byte.</summary>
    private void ReadByte()
    {
        _window = (_window << ByteBits) & WindowMask;
        _count = _window == StuffedWindow ? ByteBits - 1 : ByteBits;
        if (Position < _data.Length)
        {
            _window |= _data[Position];
            Position++;
        }
        else
        {
            Overran = true;
        }
    }
}
