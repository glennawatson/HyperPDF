// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// The MQ arithmetic decoder (ISO 15444-1 annex C) and the raw bit reader used by the bypass mode. Reading past the end
/// of a segment sees 0xFF bytes, as if a marker followed: BYTEIN (annex C.3.4) feeds ones at a marker, so truncated code-block data decodes as the standard defines.
/// </summary>
internal record struct JpxMqDecoder
{
    /// <summary>The number of context states, two per probability state for the two MPS values.</summary>
    internal const int StateCount = 94;

    /// <summary>The value of a byte that is all ones.</summary>
    private const int AllOnes = 0xFF;

    /// <summary>The largest byte after 0xFF that is still data rather than a marker.</summary>
    private const int MarkerLimit = 0x8F;

    /// <summary>The interval register's renormalisation threshold.</summary>
    private const uint HalfInterval = 0x8000;

    /// <summary>The shift of the comparable part of the code register.</summary>
    private const int CodeShift = 16;

    /// <summary>The bits of a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bits of a byte after a 0xFF byte, whose top bit is stuffed.</summary>
    private const int StuffedBits = 7;

    /// <summary>The shift that places a byte after 0xFF in the code register.</summary>
    private const int StuffedShift = 9;

    /// <summary>The value fed when a marker or the end of data is reached.</summary>
    private const uint MarkerFill = 0xFF00;

    /// <summary>The mask of the probability estimate in a transition entry.</summary>
    private const uint ProbabilityMask = 0xFFFF;

    /// <summary>The shift of the next state after an MPS in a transition entry.</summary>
    private const int MpsShift = 16;

    /// <summary>The shift of the next state after an LPS in a transition entry.</summary>
    private const int LpsShift = 24;

    /// <summary>The probability states of table C.2: <c>Qe</c>, NMPS, NLPS and the switch flag packed per entry.</summary>
    private static readonly uint[] Transitions = BuildTransitions();

    /// <summary>The coded data.</summary>
    private byte[] _data;

    /// <summary>The position of the next byte.</summary>
    private int _position;

    /// <summary>The end of the segment.</summary>
    private int _end;

    /// <summary>The code register C, or the current byte in raw mode.</summary>
    private uint _code;

    /// <summary>The interval register A.</summary>
    private uint _interval;

    /// <summary>The bits left before the next byte is read, CT.</summary>
    private int _count;

    /// <summary>Gets the probability estimates Qe of table C.2.</summary>
    private static ReadOnlySpan<ushort> Probabilities =>
    [
        0x5601, 0x3401, 0x1801, 0x0AC1, 0x0521, 0x0221, 0x5601, 0x5401, 0x4801, 0x3801, 0x3001, 0x2401, 0x1C01, 0x1601,
        0x5601, 0x5401, 0x5101, 0x4801, 0x3801, 0x3401, 0x3001, 0x2801, 0x2401, 0x2201, 0x1C01, 0x1801, 0x1601, 0x1401,
        0x1201, 0x1101, 0x0AC1, 0x09C1, 0x08A1, 0x0521, 0x0441, 0x02A1, 0x0221, 0x0141, 0x0111, 0x0085, 0x0049, 0x0025,
        0x0015, 0x0009, 0x0005, 0x0001, 0x5601,
    ];

    /// <summary>Gets the next state after an MPS, NMPS of table C.2.</summary>
    private static ReadOnlySpan<byte> NextMps =>
    [
        0x01, 0x02, 0x03, 0x04, 0x05, 0x26, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x1D, 0x0F, 0x10, 0x11, 0x12, 0x13,
        0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26,
        0x27, 0x28, 0x29, 0x2A, 0x2B, 0x2C, 0x2D, 0x2D, 0x2E,
    ];

    /// <summary>Gets the next state after an LPS, NLPS of table C.2.</summary>
    private static ReadOnlySpan<byte> NextLps =>
    [
        0x01, 0x06, 0x09, 0x0C, 0x1D, 0x21, 0x06, 0x0E, 0x0E, 0x0E, 0x11, 0x12, 0x14, 0x15, 0x0E, 0x0E, 0x0F, 0x10, 0x11,
        0x12, 0x13, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20, 0x21, 0x22, 0x23,
        0x24, 0x25, 0x26, 0x27, 0x28, 0x29, 0x2A, 0x2B, 0x2E,
    ];

    /// <summary>Gets the states whose LPS switches the MPS sense, SWITCH of table C.2.</summary>
    private static ReadOnlySpan<byte> Switches =>
    [
        0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    ];

    /// <summary>Starts arithmetic decoding of a segment (INITDEC).</summary>
    /// <param name="data">The coded data.</param>
    /// <param name="offset">The first byte of the segment.</param>
    /// <param name="length">The bytes of the segment.</param>
    internal void Start(byte[] data, int offset, int length)
    {
        _data = data;
        _position = offset;
        _end = offset + length;
        _code = (uint)ReadAt(offset) << CodeShift;
        ByteIn();
        _code <<= StuffedBits;
        _count -= StuffedBits;
        _interval = HalfInterval;
    }

    /// <summary>Starts raw decoding of a bypass segment.</summary>
    /// <param name="data">The coded data.</param>
    /// <param name="offset">The first byte of the segment.</param>
    /// <param name="length">The bytes of the segment.</param>
    internal void StartRaw(byte[] data, int offset, int length)
    {
        _data = data;
        _position = offset;
        _end = offset + length;
        _code = 0;
        _count = 0;
    }

    /// <summary>Decodes one decision (DECODE).</summary>
    /// <param name="context">The context state: the probability state times two plus the MPS.</param>
    /// <returns>The decoded bit.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int Decode(ref byte context)
    {
        var state = context;
        var entry = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(Transitions), state);
        var probability = entry & ProbabilityMask;
        var mps = state & 1;
        _interval -= probability;
        if ((_code >> CodeShift) < probability)
        {
            return DecodeLps(ref context, entry, probability, mps);
        }

        _code -= probability << CodeShift;
        return (_interval & HalfInterval) != 0 ? mps : DecodeMpsExchange(ref context, entry, probability, mps);
    }

    /// <summary>Reads one raw bit of a bypass segment.</summary>
    /// <returns>The bit.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int DecodeRaw()
    {
        if (_count == 0)
        {
            RawByteIn();
        }

        _count--;
        return (int)(_code >> _count) & 1;
    }

    /// <summary>Builds the packed transition table from table C.2.</summary>
    /// <returns>One entry per context state: <c>Qe | NMPS state &lt;&lt; 16 | NLPS state &lt;&lt; 24</c>.</returns>
    private static uint[] BuildTransitions()
    {
        var table = new uint[StateCount];
        for (var state = 0; state < StateCount; state++)
        {
            var index = state >> 1;
            var mps = state & 1;
            var lpsMps = Switches[index] != 0 ? 1 - mps : mps;
            var nextMps = (uint)((NextMps[index] << 1) | mps);
            var nextLps = (uint)((NextLps[index] << 1) | lpsMps);
            table[state] = Probabilities[index] | (nextMps << MpsShift) | (nextLps << LpsShift);
        }

        return table;
    }

    /// <summary>Finishes a decision on the LPS sub-interval (LPS_EXCHANGE then RENORMD).</summary>
    /// <param name="context">The context state.</param>
    /// <param name="entry">The context's transition entry.</param>
    /// <param name="probability">The probability estimate.</param>
    /// <param name="mps">The context's MPS.</param>
    /// <returns>The decoded bit.</returns>
    private int DecodeLps(ref byte context, uint entry, uint probability, int mps)
    {
        int bit;
        if (_interval < probability)
        {
            bit = mps;
            context = (byte)(entry >> MpsShift);
        }
        else
        {
            bit = 1 - mps;
            context = (byte)(entry >> LpsShift);
        }

        _interval = probability;
        Renormalize();
        return bit;
    }

    /// <summary>Finishes a decision on the MPS sub-interval that needs renormalising (MPS_EXCHANGE then RENORMD).</summary>
    /// <param name="context">The context state.</param>
    /// <param name="entry">The context's transition entry.</param>
    /// <param name="probability">The probability estimate.</param>
    /// <param name="mps">The context's MPS.</param>
    /// <returns>The decoded bit.</returns>
    private int DecodeMpsExchange(ref byte context, uint entry, uint probability, int mps)
    {
        int bit;
        if (_interval < probability)
        {
            bit = 1 - mps;
            context = (byte)(entry >> LpsShift);
        }
        else
        {
            bit = mps;
            context = (byte)(entry >> MpsShift);
        }

        Renormalize();
        return bit;
    }

    /// <summary>Doubles the interval until it is at least half full (RENORMD).</summary>
    private void Renormalize()
    {
        do
        {
            if (_count == 0)
            {
                ByteIn();
            }

            _interval <<= 1;
            _code <<= 1;
            _count--;
        }
        while (_interval < HalfInterval);
    }

    /// <summary>Reads the next byte into the code register (BYTEIN).</summary>
    private void ByteIn()
    {
        var next = (uint)ReadAt(_position + 1);
        if (ReadAt(_position) != AllOnes)
        {
            _position++;
            _code += next << ByteBits;
            _count = ByteBits;
        }
        else if (next > MarkerLimit)
        {
            _code += MarkerFill;
            _count = ByteBits;
        }
        else
        {
            _position++;
            _code += next << StuffedShift;
            _count = StuffedBits;
        }
    }

    /// <summary>Reads the next byte of a raw segment, skipping the stuffed bit after 0xFF.</summary>
    private void RawByteIn()
    {
        if (_code != AllOnes)
        {
            _code = (uint)ReadAt(_position);
            _position++;
            _count = ByteBits;
            return;
        }

        var next = ReadAt(_position);
        if (next > MarkerLimit)
        {
            _count = ByteBits;
            return;
        }

        _code = (uint)next;
        _position++;
        _count = StuffedBits;
    }

    /// <summary>Reads a byte, or 0xFF past the end of the segment.</summary>
    /// <param name="position">The position.</param>
    /// <returns>The byte.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly int ReadAt(int position) => position < _end ? _data[position] : AllOnes;
}
