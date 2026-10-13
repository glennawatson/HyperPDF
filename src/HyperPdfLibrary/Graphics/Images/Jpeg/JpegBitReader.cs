// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>
/// Reads the entropy-coded bits of a JPEG scan from a span. It removes the zero byte stuffed after 0xFF, stops at
/// the next marker without consuming it, and supplies zero bits past the end so a damaged scan ends instead of throwing.
/// </summary>
internal ref struct JpegBitReader
{
    /// <summary>The bit count at or below which the buffer is refilled; a refill leaves more than this many bits.</summary>
    private const int RefillBelow = 32;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bits added by a bulk refill.</summary>
    private const int BulkBits = 32;

    /// <summary>The bytes added by a bulk refill.</summary>
    private const int BulkBytes = 4;

    /// <summary>The value that is 0x01 in every byte of a word.</summary>
    private const uint LowBits = 0x01010101;

    /// <summary>The value that is 0x80 in every byte of a word.</summary>
    private const uint HighBits = 0x80808080;

    /// <summary>The longest read in one call.</summary>
    private const int LongestRead = 16;

    /// <summary>The scan data.</summary>
    private readonly ReadOnlySpan<byte> _data;

    /// <summary>The position of the next unread byte.</summary>
    private int _position;

    /// <summary>The bit buffer; the unread bits are the low <see cref="_bitCount"/> bits.</summary>
    private ulong _buffer;

    /// <summary>The unread bits in <see cref="_buffer"/>.</summary>
    private int _bitCount;

    /// <summary>The zero bits supplied past the data, which are the lowest bits of the buffer.</summary>
    private int _padBits;

    /// <summary>Initializes a new instance of the <see cref="JpegBitReader"/> struct.</summary>
    /// <param name="data">The JPEG data.</param>
    /// <param name="position">The position of the first entropy-coded byte.</param>
    internal JpegBitReader(ReadOnlySpan<byte> data, int position)
    {
        _data = data;
        _position = position;
    }

    /// <summary>Gets the position of the next byte that is not yet in the bit buffer.</summary>
    internal readonly int Position => _position;

    /// <summary>Gets or sets a value indicating whether a code could not be decoded.</summary>
    internal bool Failed { get; set; }

    /// <summary>Gets a value indicating whether more bits were consumed than the data held.</summary>
    internal readonly bool Overrun => _bitCount < _padBits;

    /// <summary>Drops the unread bits and moves past the next restart marker.</summary>
    /// <returns><see langword="true"/> when a restart marker was found; <see langword="false"/> at any other marker or the end.</returns>
    internal bool TryRestart()
    {
        _buffer = 0;
        _bitCount = 0;
        _padBits = 0;
        while (_position + 1 < _data.Length)
        {
            if (_data[_position] != JpegMarkers.MarkerPrefix)
            {
                _position++;
                continue;
            }

            var next = _data[_position + 1];
            if (next is >= JpegMarkers.FirstRestart and <= JpegMarkers.LastRestart)
            {
                _position += JpegMarkers.MarkerLength;
                return true;
            }

            if (next is not (0 or JpegMarkers.MarkerPrefix))
            {
                return false;
            }

            _position += next == 0 ? JpegMarkers.MarkerLength : 1;
        }

        return false;
    }

    /// <summary>Decodes one Huffman symbol.</summary>
    /// <param name="table">The table.</param>
    /// <returns>The symbol; zero, with <see cref="Failed"/> set, for an invalid code.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int DecodeSymbol(JpegHuffmanTable table)
    {
        if (_bitCount < LongestRead)
        {
            Refill();
        }

        var entry = table.Lookup[(int)((_buffer >> (_bitCount - JpegHuffmanTable.LookupBits)) & ((1UL << JpegHuffmanTable.LookupBits) - 1))];
        if (entry == 0)
        {
            return DecodeLongSymbol(table);
        }

        _bitCount -= entry >> ByteBits;
        return entry & byte.MaxValue;
    }

    /// <summary>Reads bits and sign-extends them as a JPEG magnitude.</summary>
    /// <param name="count">The number of bits, from 0 to 16.</param>
    /// <returns>The signed value, or zero for no bits.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int ReceiveExtend(int count)
    {
        if (count == 0)
        {
            return 0;
        }

        var value = ReadBits(count);
        return value < (1 << (count - 1)) ? value - (1 << count) + 1 : value;
    }

    /// <summary>Reads bits as an unsigned number.</summary>
    /// <param name="count">The number of bits, from 1 to 16.</param>
    /// <returns>The value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int ReadBits(int count)
    {
        if (_bitCount < count)
        {
            Refill();
        }

        _bitCount -= count;
        return (int)((_buffer >> _bitCount) & ((1UL << count) - 1));
    }

    /// <summary>Reads one bit.</summary>
    /// <returns>Zero or one.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int ReadBit() => ReadBits(1);

    /// <summary>Decodes a symbol whose code is longer than the lookup covers.</summary>
    /// <param name="table">The table.</param>
    /// <returns>The symbol; zero, with <see cref="Failed"/> set, for an invalid code.</returns>
    private int DecodeLongSymbol(JpegHuffmanTable table)
    {
        var bits = (int)((_buffer >> (_bitCount - JpegHuffmanTable.MaxCodeLength)) & ((1UL << JpegHuffmanTable.MaxCodeLength) - 1));
        for (var length = JpegHuffmanTable.LookupBits + 1; length <= JpegHuffmanTable.MaxCodeLength; length++)
        {
            var code = bits >> (JpegHuffmanTable.MaxCodeLength - length);
            if (code > table.MaxCode[length])
            {
                continue;
            }

            _bitCount -= length;
            var index = table.ValueOffset[length] + code;
            if ((uint)index < (uint)table.Count)
            {
                return table.Values[index];
            }

            break;
        }

        Failed = true;
        return 0;
    }

    /// <summary>Tops the buffer up to more than <see cref="RefillBelow"/> bits.</summary>
    private void Refill()
    {
        while (_bitCount <= RefillBelow)
        {
            if (!TryRefillBulk())
            {
                RefillByte();
            }
        }
    }

    /// <summary>Adds four bytes at once when none is 0xFF.</summary>
    /// <returns><see langword="true"/> when the bytes were added.</returns>
    private bool TryRefillBulk()
    {
        if (_position + BulkBytes > _data.Length)
        {
            return false;
        }

        var word = BinaryPrimitives.ReadUInt32BigEndian(_data[_position..]);
        var inverted = ~word;
        if (((inverted - LowBits) & ~inverted & HighBits) != 0)
        {
            return false;
        }

        _buffer = (_buffer << BulkBits) | word;
        _bitCount += BulkBits;
        _position += BulkBytes;
        return true;
    }

    /// <summary>Adds one byte, handling a stuffed zero, a marker or the end of the data.</summary>
    private void RefillByte()
    {
        if (_position >= _data.Length)
        {
            Pad();
            return;
        }

        var value = _data[_position];
        if (value != JpegMarkers.MarkerPrefix)
        {
            _position++;
            Push(value);
            return;
        }

        if (_position + 1 < _data.Length && _data[_position + 1] == 0)
        {
            _position += JpegMarkers.MarkerLength;
            Push(value);
            return;
        }

        // A marker: supply zero bits and leave the marker for the caller.
        Pad();
    }

    /// <summary>Adds a byte of zero bits past the end of the entropy-coded data.</summary>
    private void Pad()
    {
        _padBits += ByteBits;
        Push(0);
    }

    /// <summary>Adds a byte to the buffer.</summary>
    /// <param name="value">The byte.</param>
    private void Push(byte value)
    {
        _buffer = (_buffer << ByteBits) | value;
        _bitCount += ByteBits;
    }
}
