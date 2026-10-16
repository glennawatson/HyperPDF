// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts;

/// <summary>Reads the bytes, LEB128 numbers and zigzag deltas of the packed CMap resources.</summary>
internal ref struct PackedDataReader
{
    /// <summary>The bits each LEB128 byte carries.</summary>
    private const int GroupBits = 7;

    /// <summary>The flag of a LEB128 byte that another byte follows.</summary>
    private const int MoreFlag = 0x80;

    /// <summary>The value bits of a LEB128 byte.</summary>
    private const int GroupMask = 0x7F;

    /// <summary>The largest shift of a 32-bit LEB128 number.</summary>
    private const int MaxShift = 28;

    /// <summary>The value bits available in the last group of a 32-bit number.</summary>
    private const int LastGroupMask = 0x0F;

    /// <summary>The data.</summary>
    private readonly ReadOnlySpan<byte> _data;

    /// <summary>The position of the next byte.</summary>
    private int _position;

    /// <summary>Initializes a new instance of the <see cref="PackedDataReader"/> struct.</summary>
    /// <param name="data">The decompressed resource.</param>
    internal PackedDataReader(ReadOnlySpan<byte> data) => _data = data;

    /// <summary>Gets the number of unread bytes.</summary>
    internal readonly int Remaining => _data.Length - _position;

    /// <summary>Reads one byte.</summary>
    /// <returns>The byte.</returns>
    /// <exception cref="PdfException">The data ended early.</exception>
    internal byte ReadByte()
    {
        if ((uint)_position >= (uint)_data.Length)
        {
            throw new PdfException("The packed CMap data ended early.");
        }

        var value = _data[_position];
        _position++;
        return value;
    }

    /// <summary>Reads bytes.</summary>
    /// <param name="count">The number of bytes.</param>
    /// <returns>The bytes.</returns>
    /// <exception cref="PdfException">The data ended early.</exception>
    internal ReadOnlySpan<byte> ReadBytes(int count)
    {
        if (count < 0 || count > _data.Length - _position)
        {
            throw new PdfException("The packed CMap data ended early.");
        }

        var bytes = _data.Slice(_position, count);
        _position += count;
        return bytes;
    }

    /// <summary>Reads an unsigned LEB128 number.</summary>
    /// <returns>The number.</returns>
    /// <exception cref="PdfException">The data ended early or the number is too long.</exception>
    internal uint ReadNumber()
    {
        uint value = 0;
        for (var shift = 0; shift <= MaxShift; shift += GroupBits)
        {
            var b = ReadByte();
            if (shift == MaxShift && (b & GroupMask) > LastGroupMask)
            {
                throw new PdfException(PdfError.Format, "A packed CMap number exceeds its unsigned width.");
            }

            value |= (uint)(b & GroupMask) << shift;
            if ((b & MoreFlag) == 0)
            {
                return value;
            }
        }

        throw new PdfException("A packed CMap number is too long.");
    }

    /// <summary>Reads a zigzag-encoded signed LEB128 number.</summary>
    /// <returns>The number.</returns>
    internal int ReadSigned()
    {
        var value = ReadNumber();
        return (int)(value >> 1) ^ -(int)(value & 1);
    }
}
