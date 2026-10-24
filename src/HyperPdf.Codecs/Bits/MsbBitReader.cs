// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs.Bits;

/// <summary>
/// Reads bits from a span, most significant bit of each byte first, as FLAC, MP3, AAC and H.264 store them. The next bits are kept
/// at the top of a 64 bit window that is topped up by one unaligned 8 byte read, so most calls touch only the window. Reading past
/// the end gives zero bits and sets <see cref="IsOverrun"/> instead of throwing, which keeps the hot loops free of error handling;
/// the caller checks the flag once a frame or a block is done.
/// </summary>
[DebuggerDisplay("MsbBitReader: bit {BitPosition} of {_data.Length * 8}")]
public ref struct MsbBitReader
{
    /// <summary>The bits in the window.</summary>
    private const int WindowBits = 64;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The shift that divides a bit count by the bits in a byte.</summary>
    private const int ByteShift = 3;

    /// <summary>The most bits <see cref="Read"/> returns at once.</summary>
    private const int MostBits = 32;

    /// <summary>The fewest bits the window holds after it is topped up from the middle of the data.</summary>
    private const int TopUpBits = WindowBits - ByteBits + 1;

    /// <summary>The bytes loaded at once.</summary>
    private const int LoadBytes = 8;

    /// <summary>The data.</summary>
    private readonly ReadOnlySpan<byte> _data;

    /// <summary>The index of the next byte to load into the window.</summary>
    private int _next;

    /// <summary>The next bits, at the top; the bits below the counted ones are the data that follows, or zero.</summary>
    private ulong _window;

    /// <summary>The bits in the window that count.</summary>
    private int _count;

    /// <summary>The zero bits supplied after the end of the data and counted in the window.</summary>
    private int _virtual;

    /// <summary>Initializes a new instance of the <see cref="MsbBitReader"/> struct.</summary>
    /// <param name="data">The bytes to read.</param>
    public MsbBitReader(ReadOnlySpan<byte> data)
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
    /// <returns>The bits, in the low end of the result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint Peek(int count)
    {
        if (_count < count)
        {
            Refill();
        }

        return count == 0 ? 0 : (uint)(_window >> (WindowBits - count));
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

        _window <<= count;
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

    /// <summary>Reads bits as a two's complement number.</summary>
    /// <param name="count">The number of bits, from 1 to 32.</param>
    /// <returns>The number, with its sign.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadSigned(int count)
    {
        var shift = MostBits - count;
        return (int)(Read(count) << shift) >> shift;
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

        var high = Read(count - MostBits);
        return ((ulong)high << MostBits) | Read(MostBits);
    }

    /// <summary>Reads a run of 0 bits that ends at a 1 bit, and consumes the 1.</summary>
    /// <returns>The number of 0 bits.</returns>
    public int ReadUnary()
    {
        var zeros = 0;
        while (true)
        {
            if (_count == 0)
            {
                Refill();
            }

            var leading = BitOperations.LeadingZeroCount(_window);
            if (leading < _count)
            {
                // Two shifts, because one shift by the whole window is no shift at all.
                _window <<= leading;
                _window <<= 1;
                _count -= leading + 1;
                return zeros + leading;
            }

            // The whole window is zeros; take it and look further.
            zeros += _count;
            _window = 0;
            _count = 0;
            if (IsOverrun)
            {
                return zeros;
            }
        }
    }

    /// <summary>Skips to the next byte boundary.</summary>
    public void Align()
    {
        var partial = (int)(BitPosition & (ByteBits - 1));
        if (partial != 0)
        {
            Skip(ByteBits - partial);
        }
    }

    /// <summary>Moves to a bit position.</summary>
    /// <param name="bitPosition">The number of bits from the start of the data.</param>
    public void Seek(long bitPosition)
    {
        var clamped = Math.Clamp(bitPosition, 0, (long)_data.Length * ByteBits);
        _next = (int)(clamped >> ByteShift);
        _window = 0;
        _count = 0;
        _virtual = 0;
        Refill();
        Skip((int)(clamped & (ByteBits - 1)));
    }

    /// <summary>Tops the window up with whole bytes.</summary>
    private void Refill()
    {
        if (_next + LoadBytes <= _data.Length)
        {
            // One unaligned read supplies the next 64 bits; they are placed under the bits already held, and the bits beyond the
            // counted ones repeat what the next refill loads, so overlapping them with OR changes nothing.
            var value = BinaryPrimitives.ReadUInt64BigEndian(_data[_next..]);
            _window |= value >> _count;
            var taken = (WindowBits - _count) >> ByteShift;
            _next += taken;
            _count += taken << ByteShift;
            return;
        }

        while (_count < TopUpBits)
        {
            if (_next < _data.Length)
            {
                _window |= (ulong)_data[_next] << (WindowBits - ByteBits - _count);
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
