// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>
/// Reads big-endian integers and bits, most significant first, from JBIG2 data. Byte reads ignore the bit position, as
/// in PDFium. Reads past the end fail instead of throwing.
/// </summary>
internal ref struct Jbig2Reader
{
    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The index of the last bit in a byte.</summary>
    private const int LastBit = 7;

    /// <summary>The shift that turns a byte count into a bit count.</summary>
    private const int ByteShift = 3;

    /// <summary>The bytes in a 16-bit integer.</summary>
    private const int ShortBytes = 2;

    /// <summary>The bytes in a 32-bit integer.</summary>
    private const int IntBytes = 4;

    /// <summary>The most bits read at once.</summary>
    private const int MaxBits = 32;

    /// <summary>The data.</summary>
    private readonly ReadOnlySpan<byte> _data;

    /// <summary>The token for the containing segment decode.</summary>
    private readonly CancellationToken _cancellationToken;

    /// <summary>The position of the current byte.</summary>
    private int _offset;

    /// <summary>The next bit within the current byte, 0 for the most significant.</summary>
    private int _bit;

    /// <summary>Initializes a new instance of the <see cref="Jbig2Reader"/> struct.</summary>
    /// <param name="data">The data.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    internal Jbig2Reader(ReadOnlySpan<byte> data, CancellationToken cancellationToken = default)
    {
        _data = data;
        _cancellationToken = cancellationToken;
    }

    /// <summary>Gets the token passed to arithmetic region decoders.</summary>
    internal readonly CancellationToken CancellationToken => _cancellationToken;

    /// <summary>Gets the data.</summary>
    internal readonly ReadOnlySpan<byte> Data => _data;

    /// <summary>Gets or sets the position of the current byte; values past the end are clamped to it.</summary>
    internal int Offset
    {
        readonly get => _offset;
        set => _offset = Math.Clamp(value, 0, _data.Length);
    }

    /// <summary>Gets the bytes from the current byte to the end.</summary>
    internal readonly int BytesLeft => _data.Length - _offset;

    /// <summary>Gets a value indicating whether the current byte is inside the data.</summary>
    internal readonly bool IsInBounds => _offset < _data.Length;

    /// <summary>Gets or sets the position in bits.</summary>
    internal long BitPosition
    {
        readonly get => ((long)_offset << ByteShift) + _bit;
        set
        {
            var clamped = Math.Clamp(value, 0, (long)_data.Length << ByteShift);
            _offset = (int)(clamped >> ByteShift);
            _bit = (int)(clamped & LastBit);
        }
    }

    /// <summary>Gets the byte at the current position without consuming it.</summary>
    /// <returns>The byte, or zero past the end.</returns>
    internal readonly byte PeekByte() => IsInBounds ? _data[_offset] : (byte)0;

    /// <summary>Reads one byte.</summary>
    /// <param name="value">The byte.</param>
    /// <returns><see langword="false"/> past the end.</returns>
    internal bool TryReadByte(out byte value)
    {
        if (!IsInBounds)
        {
            value = 0;
            return false;
        }

        value = _data[_offset];
        _offset++;
        return true;
    }

    /// <summary>Reads one signed byte.</summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="false"/> past the end.</returns>
    internal bool TryReadSByte(out sbyte value)
    {
        var read = TryReadByte(out var raw);
        value = unchecked((sbyte)raw);
        return read;
    }

    /// <summary>Reads a big-endian 16-bit integer.</summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="false"/> when fewer than two bytes remain.</returns>
    internal bool TryReadUInt16(out ushort value)
    {
        if (BytesLeft < ShortBytes)
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadUInt16BigEndian(_data[_offset..]);
        _offset += ShortBytes;
        return true;
    }

    /// <summary>Reads a big-endian 32-bit integer.</summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="false"/> when fewer than four bytes remain.</returns>
    internal bool TryReadUInt32(out uint value)
    {
        if (BytesLeft < IntBytes)
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadUInt32BigEndian(_data[_offset..]);
        _offset += IntBytes;
        return true;
    }

    /// <summary>Reads a big-endian 32-bit signed integer.</summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="false"/> when fewer than four bytes remain.</returns>
    internal bool TryReadInt32(out int value)
    {
        var read = TryReadUInt32(out var raw);
        value = unchecked((int)raw);
        return read;
    }

    /// <summary>Reads one bit.</summary>
    /// <param name="value">The bit.</param>
    /// <returns><see langword="false"/> past the end.</returns>
    internal bool TryReadBit(out int value)
    {
        if (!IsInBounds)
        {
            value = 0;
            return false;
        }

        value = (_data[_offset] >> (LastBit - _bit)) & 1;
        AdvanceBit();
        return true;
    }

    /// <summary>Reads up to 32 bits; near the end it reads only the bits left, as PDFium does.</summary>
    /// <param name="count">The bits wanted, from 0 to 32.</param>
    /// <param name="value">The bits, right-aligned.</param>
    /// <returns><see langword="false"/> when the position is past the end.</returns>
    internal bool TryReadBits(int count, out uint value)
    {
        value = 0;
        if (!IsInBounds || (uint)count > MaxBits)
        {
            return false;
        }

        var available = ((long)_data.Length << ByteShift) - BitPosition;
        var take = (int)Math.Min(count, available);
        for (var i = 0; i < take; i++)
        {
            value = (value << 1) | (uint)((_data[_offset] >> (LastBit - _bit)) & 1);
            AdvanceBit();
        }

        return true;
    }

    /// <summary>Moves to the next byte boundary when part of a byte has been read.</summary>
    internal void AlignByte()
    {
        if (_bit == 0)
        {
            return;
        }

        Skip(1);
        _bit = 0;
    }

    /// <summary>Moves forward by whole bytes, stopping at the end.</summary>
    /// <param name="count">The bytes to skip.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Skip(long count) => _offset = (int)Math.Min(_offset + Math.Max(count, 0), _data.Length);

    /// <summary>Moves to the next bit.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AdvanceBit()
    {
        _bit++;
        if (_bit < ByteBits)
        {
            return;
        }

        _bit = 0;
        _offset++;
    }
}
