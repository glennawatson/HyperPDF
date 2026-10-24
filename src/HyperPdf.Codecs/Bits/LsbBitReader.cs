// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs.Bits;

/// <summary>
/// Reads bits from a span, least significant bit of each byte first, as Vorbis and Opus store them. The next bits are kept at the
/// bottom of a 64 bit window that is topped up by one unaligned 8 byte read. Reading past the end gives zero bits and sets
/// <see cref="IsOverrun"/> instead of throwing, so the hot loops need no error handling; the caller checks the flag once a packet is done.
/// </summary>
[DebuggerDisplay("LsbBitReader: bit {BitPosition} of {_data.Length * 8}")]
public ref struct LsbBitReader
{
    /// <summary>The bits in the window.</summary>
    private const int WindowBits = 64;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The shift that divides a bit count by the bits in a byte.</summary>
    private const int ByteShift = 3;

    /// <summary>The most bits <see cref="Read"/> returns at once.</summary>
    private const int MostBits = 32;

    /// <summary>The bytes loaded at once.</summary>
    private const int LoadBytes = 8;

    /// <summary>The fewest bits the window holds after a top up.</summary>
    private const int TopUpBits = WindowBits - ByteBits;

    /// <summary>The data.</summary>
    private readonly ReadOnlySpan<byte> _data;

    /// <summary>The index of the next byte to load into the window.</summary>
    private int _next;

    /// <summary>The next bits, at the bottom; the bits above the counted ones are the data that follows, or zero.</summary>
    private ulong _window;

    /// <summary>The bits in the window that count.</summary>
    private int _count;

    /// <summary>The zero bits supplied after the end of the data and counted in the window.</summary>
    private int _virtual;

    /// <summary>Initializes a new instance of the <see cref="LsbBitReader"/> struct.</summary>
    /// <param name="data">The bytes to read.</param>
    public LsbBitReader(ReadOnlySpan<byte> data)
    {
        _data = data;
        _next = 0;
        _window = 0;
        _count = 0;
        _virtual = 0;
        Refill();
    }

    /// <summary>Gets the number of bits read so far.</summary>
    public readonly long BitPosition => ((long)_next * ByteBits) + _virtual - _count;

    /// <summary>Gets the number of bits left in the data.</summary>
    public readonly long BitsLeft => ((long)_data.Length * ByteBits) - BitPosition;

    /// <summary>Gets a value indicating whether more bits were read than the data holds.</summary>
    public readonly bool IsOverrun => BitPosition > (long)_data.Length * ByteBits;

    /// <summary>Looks at the next bits without reading them.</summary>
    /// <param name="count">The number of bits, from 0 to 32.</param>
    /// <returns>The bits, the first one in bit 0.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint Peek(int count)
    {
        if (_count < count)
        {
            Refill();
        }

        return (uint)(_window & ((1UL << count) - 1));
    }

    /// <summary>Skips bits.</summary>
    /// <param name="count">The number of bits, from 0 to 32.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Skip(int count)
    {
        if (_count < count)
        {
            Refill();
        }

        _window >>= count;
        _count -= count;
    }

    /// <summary>Reads bits as an unsigned number.</summary>
    /// <param name="count">The number of bits, from 0 to 32.</param>
    /// <returns>The number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint Read(int count)
    {
        var value = Peek(count);
        Skip(count);
        return value;
    }

    /// <summary>Reads one bit.</summary>
    /// <returns><see langword="true"/> for a 1.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ReadBit() => Read(1) != 0;

    /// <summary>Reads up to 64 bits as an unsigned number.</summary>
    /// <param name="count">The number of bits, from 0 to 64.</param>
    /// <returns>The number.</returns>
    public ulong ReadLong(int count)
    {
        if (count <= MostBits)
        {
            return Read(count);
        }

        var low = Read(MostBits);
        return ((ulong)Read(count - MostBits) << MostBits) | low;
    }

    /// <summary>Tops the window up with whole bytes.</summary>
    private void Refill()
    {
        if (_next + LoadBytes <= _data.Length)
        {
            // One unaligned read supplies the next 64 bits; they are placed above the bits already held, and the bits beyond the
            // counted ones repeat what the next refill loads, so overlapping them with OR changes nothing.
            var value = BinaryPrimitives.ReadUInt64LittleEndian(_data[_next..]);
            _window |= value << _count;
            var taken = (WindowBits - _count) >> ByteShift;
            _next += taken;
            _count += taken << ByteShift;
            return;
        }

        while (_count <= TopUpBits)
        {
            if (_next < _data.Length)
            {
                _window |= (ulong)_data[_next] << _count;
                _next++;
            }
            else
            {
                _virtual += ByteBits;
            }

            _count += ByteBits;
        }
    }
}
