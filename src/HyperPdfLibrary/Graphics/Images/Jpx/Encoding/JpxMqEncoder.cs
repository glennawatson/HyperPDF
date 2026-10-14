// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace HyperPdfLibrary.Graphics.Images.Jpx.Encoding;

/// <summary>
/// The MQ arithmetic encoder of ISO 15444-1 annex C (INITENC, ENCODE, FLUSH) and the raw bypass bit writer of annex D.6.
/// The context states persist across segments, as the block coder needs. A segment's bytes are read with
/// <see cref="Segment"/> after <see cref="Finish"/> or <see cref="FinishRaw"/>.
/// </summary>
internal sealed class JpxMqEncoder : IDisposable
{
    /// <summary>The interval's renormalisation threshold.</summary>
    private const uint HalfInterval = 0x8000;

    /// <summary>The mask of the probability estimate in a transition entry.</summary>
    private const uint ProbabilityMask = 0xFFFF;

    /// <summary>The shift of the next state after an MPS in a transition entry.</summary>
    private const int MpsShift = 16;

    /// <summary>The shift of the next state after an LPS in a transition entry.</summary>
    private const int LpsShift = 24;

    /// <summary>The bits CT starts with (INITENC).</summary>
    private const int InitialCount = 12;

    /// <summary>The value of a byte that is all ones.</summary>
    private const int AllOnes = 0xFF;

    /// <summary>The bit of the code register that carries into the last byte.</summary>
    private const uint CarryBit = 0x8000000;

    /// <summary>The shift that takes a byte from the code register after 0xFF.</summary>
    private const int StuffedShift = 20;

    /// <summary>The shift that takes a byte from the code register.</summary>
    private const int ByteShift = 19;

    /// <summary>The bits of the code register kept after a stuffed byte.</summary>
    private const uint StuffedMask = 0xFFFFF;

    /// <summary>The bits of the code register kept after a byte.</summary>
    private const uint ByteMask = 0x7FFFF;

    /// <summary>The bits of the code register kept when a carry turns the last byte into 0xFF.</summary>
    private const uint CarryMask = 0x7FFFFFF;

    /// <summary>The bits of a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The byte that ends a raw segment after a 0xFF byte: the stuffed zero bit then alternating padding.</summary>
    private const byte StuffedPadding = 0x2A;

    /// <summary>The bits of a byte after 0xFF.</summary>
    private const int StuffedBits = 7;

    /// <summary>The low bits SETBITS sets.</summary>
    private const uint SetBitsMask = 0xFFFF;

    /// <summary>The smallest buffer rented.</summary>
    private const int MinimumCapacity = 1024;

    /// <summary>The factor the buffer grows by.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The bytes ensured free before a byte is emitted: the byte and room for a flush.</summary>
    private const int Headroom = 4;

    /// <summary>The context states, each the probability state times two plus the MPS.</summary>
    private readonly byte[] _states = new byte[JpxContexts.Count];

    /// <summary>The segment's bytes from index one; index zero is the virtual byte before the first.</summary>
    private byte[] _buffer = ArrayPool<byte>.Shared.Rent(MinimumCapacity);

    /// <summary>The index of the last byte written.</summary>
    private int _end;

    /// <summary>The interval register A.</summary>
    private uint _interval;

    /// <summary>The code register C.</summary>
    private uint _code;

    /// <summary>The bit counter CT.</summary>
    private int _count;

    /// <summary>The raw byte being filled.</summary>
    private int _rawByte;

    /// <summary>The free bits of the raw byte.</summary>
    private int _rawFree;

    /// <summary>Initializes a new instance of the <see cref="JpxMqEncoder"/> class with reset contexts.</summary>
    internal JpxMqEncoder() => ResetContexts();

    /// <summary>Gets the bytes written so far in the arithmetic-coded segment (the last may still change by a carry).</summary>
    internal int EmittedBytes => _end;

    /// <summary>Gets the bytes a raw segment has so far, counting a partly filled last byte.</summary>
    internal int RawBytes => _end + (_rawFree == FreeBits(_end) ? 0 : 1);

    /// <summary>Gets the bytes of the segment that <see cref="Finish"/> or <see cref="FinishRaw"/> ended.</summary>
    internal ReadOnlySpan<byte> Segment => _buffer.AsSpan(1, _end);

    /// <inheritdoc/>
    public void Dispose()
    {
        ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = [];
    }

    /// <summary>Resets every context to its initial state (table D.7).</summary>
    internal void ResetContexts()
    {
        Array.Clear(_states);
        _states[0] = JpxContexts.ZeroState;
        _states[JpxContexts.RunLength] = JpxContexts.RunLengthState;
        _states[JpxContexts.Uniform] = JpxContexts.UniformState;
    }

    /// <summary>Starts an arithmetic-coded segment (INITENC).</summary>
    internal void Start()
    {
        _buffer[0] = 0;
        _end = 0;
        _interval = HalfInterval;
        _code = 0;
        _count = InitialCount;
    }

    /// <summary>Starts a raw bypass segment.</summary>
    internal void StartRaw()
    {
        _end = 0;
        _rawByte = 0;
        _rawFree = ByteBits;
    }

    /// <summary>Encodes one decision (ENCODE).</summary>
    /// <param name="bit">The decision.</param>
    /// <param name="context">The context.</param>
    internal void Encode(int bit, int context)
    {
        var state = _states[context];
        var entry = JpxMqDecoder.TransitionTable[state];
        var probability = entry & ProbabilityMask;
        _interval -= probability;
        if (bit == (state & 1))
        {
            if ((_interval & HalfInterval) != 0)
            {
                _code += probability;
                return;
            }

            // CODEMPS with the conditional exchange.
            if (_interval < probability)
            {
                _interval = probability;
            }
            else
            {
                _code += probability;
            }

            _states[context] = (byte)(entry >> MpsShift);
        }
        else
        {
            // CODELPS with the conditional exchange.
            if (_interval < probability)
            {
                _code += probability;
            }
            else
            {
                _interval = probability;
            }

            _states[context] = (byte)(entry >> LpsShift);
        }

        Renormalize();
    }

    /// <summary>Writes one raw bit; a byte after 0xFF holds seven bits.</summary>
    /// <param name="bit">The bit.</param>
    internal void EncodeRaw(int bit)
    {
        _rawFree--;
        _rawByte |= bit << _rawFree;
        if (_rawFree != 0)
        {
            return;
        }

        Append((byte)_rawByte);
        _rawFree = _rawByte == AllOnes ? StuffedBits : ByteBits;
        _rawByte = 0;
    }

    /// <summary>Terminates an arithmetic-coded segment (FLUSH): the last byte is dropped when it is 0xFF, as the standard allows.</summary>
    internal void Finish()
    {
        var temp = _code + _interval;
        _code |= SetBitsMask;
        if (_code >= temp)
        {
            _code -= HalfInterval;
        }

        _code <<= _count;
        ByteOut();
        _code <<= _count;
        ByteOut();
        if (_buffer[_end] == AllOnes)
        {
            _end--;
        }
    }

    /// <summary>
    /// Terminates a raw segment: a part-filled last byte is padded with alternating zeros and ones, and a last byte of
    /// 0xFF gets a stuffed byte after it (a zero bit then the alternating padding, 0x2A), because a segment must not end in
    /// 0xFF (D.6).
    /// </summary>
    internal void FinishRaw()
    {
        if (_rawFree != FreeBits(_end))
        {
            var pad = 0;
            while (_rawFree > 0)
            {
                _rawFree--;
                _rawByte |= pad << _rawFree;
                pad ^= 1;
            }

            Append((byte)_rawByte);
        }

        if (_end > 0 && _buffer[_end] == AllOnes)
        {
            Append(StuffedPadding);
        }
    }

    /// <summary>Gets the free bits of a fresh raw byte after the byte at an index.</summary>
    /// <param name="last">The index of the last byte written, or zero for none.</param>
    /// <returns>Seven after a 0xFF byte, otherwise eight.</returns>
    private int FreeBits(int last) => last > 0 && _buffer[last] == AllOnes ? StuffedBits : ByteBits;

    /// <summary>Doubles the interval until it is at least half full (RENORME).</summary>
    private void Renormalize()
    {
        do
        {
            _interval <<= 1;
            _code <<= 1;
            _count--;
            if (_count == 0)
            {
                ByteOut();
            }
        }
        while ((_interval & HalfInterval) == 0);
    }

    /// <summary>Moves a byte from the code register to the output (BYTEOUT), propagating a carry.</summary>
    private void ByteOut()
    {
        if (_buffer[_end] == AllOnes)
        {
            Emit(_code >> StuffedShift, StuffedMask, StuffedBits);
            return;
        }

        if ((_code & CarryBit) == 0)
        {
            Emit(_code >> ByteShift, ByteMask, ByteBits);
            return;
        }

        _buffer[_end]++;
        if (_buffer[_end] == AllOnes)
        {
            _code &= CarryMask;
            Emit(_code >> StuffedShift, StuffedMask, StuffedBits);
            return;
        }

        Emit(_code >> ByteShift, ByteMask, ByteBits);
    }

    /// <summary>Appends a byte from the code register and keeps the remaining code bits.</summary>
    /// <param name="value">The byte.</param>
    /// <param name="mask">The code bits kept.</param>
    /// <param name="count">The new bit counter.</param>
    private void Emit(uint value, uint mask, int count)
    {
        Append((byte)value);
        _code &= mask;
        _count = count;
    }

    /// <summary>Appends a byte, growing the buffer when it is full.</summary>
    /// <param name="value">The byte.</param>
    private void Append(byte value)
    {
        if (_end + Headroom >= _buffer.Length)
        {
            var larger = ArrayPool<byte>.Shared.Rent(_buffer.Length * GrowthFactor);
            _buffer.AsSpan(0, _end + 1).CopyTo(larger);
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = larger;
        }

        _end++;
        _buffer[_end] = value;
    }
}
