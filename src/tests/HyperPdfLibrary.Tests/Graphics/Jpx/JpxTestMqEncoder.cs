// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>
/// The MQ arithmetic encoder of ISO 15444-1 annex C (INITENC, ENCODE, FLUSH) and the raw bypass bit writer, written from
/// the standard so the decoder is checked against an independent coder. Context states persist across segments.
/// </summary>
internal sealed class JpxTestMqEncoder
{
    /// <summary>The contexts of the block coder.</summary>
    internal const int ContextCount = 19;

    /// <summary>The run-length context.</summary>
    internal const int RunLength = 17;

    /// <summary>The uniform context.</summary>
    internal const int Uniform = 18;

    /// <summary>The initial probability state of the uniform context.</summary>
    private const int UniformState = 46;

    /// <summary>The initial probability state of the run-length context.</summary>
    private const int RunLengthState = 3;

    /// <summary>The initial probability state of the first zero-coding context.</summary>
    private const int ZeroState = 4;

    /// <summary>The interval's renormalisation threshold.</summary>
    private const uint HalfInterval = 0x8000;

    /// <summary>The bits CT starts with.</summary>
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

    /// <summary>The bits of a byte after 0xFF.</summary>
    private const int StuffedBits = 7;

    /// <summary>The low bits set by SETBITS.</summary>
    private const uint SetBitsMask = 0xFFFF;

    /// <summary>The output bytes, with a virtual byte at index zero that is never written out.</summary>
    private readonly List<byte> _bytes = [];

    /// <summary>Each context's probability state.</summary>
    private readonly int[] _states = new int[ContextCount];

    /// <summary>Each context's more probable symbol.</summary>
    private readonly int[] _mps = new int[ContextCount];

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

    /// <summary>Initializes a new instance of the <see cref="JpxTestMqEncoder"/> class with reset contexts.</summary>
    internal JpxTestMqEncoder() => ResetContexts();

    /// <summary>Gets the probability estimates Qe of table C.2.</summary>
    private static ReadOnlySpan<ushort> Probabilities =>
    [
        0x5601, 0x3401, 0x1801, 0x0AC1, 0x0521, 0x0221, 0x5601, 0x5401, 0x4801, 0x3801, 0x3001, 0x2401, 0x1C01, 0x1601,
        0x5601, 0x5401, 0x5101, 0x4801, 0x3801, 0x3401, 0x3001, 0x2801, 0x2401, 0x2201, 0x1C01, 0x1801, 0x1601, 0x1401,
        0x1201, 0x1101, 0x0AC1, 0x09C1, 0x08A1, 0x0521, 0x0441, 0x02A1, 0x0221, 0x0141, 0x0111, 0x0085, 0x0049, 0x0025,
        0x0015, 0x0009, 0x0005, 0x0001, 0x5601,
    ];

    /// <summary>Gets NMPS of table C.2.</summary>
    private static ReadOnlySpan<byte> NextMps =>
    [
        0x01, 0x02, 0x03, 0x04, 0x05, 0x26, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x1D, 0x0F, 0x10, 0x11, 0x12, 0x13,
        0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26,
        0x27, 0x28, 0x29, 0x2A, 0x2B, 0x2C, 0x2D, 0x2D, 0x2E,
    ];

    /// <summary>Gets NLPS of table C.2.</summary>
    private static ReadOnlySpan<byte> NextLps =>
    [
        0x01, 0x06, 0x09, 0x0C, 0x1D, 0x21, 0x06, 0x0E, 0x0E, 0x0E, 0x11, 0x12, 0x14, 0x15, 0x0E, 0x0E, 0x0F, 0x10, 0x11,
        0x12, 0x13, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20, 0x21, 0x22, 0x23,
        0x24, 0x25, 0x26, 0x27, 0x28, 0x29, 0x2A, 0x2B, 0x2E,
    ];

    /// <summary>Gets the states whose LPS switches the MPS sense (SWITCH in table C.2).</summary>
    private static ReadOnlySpan<byte> SwitchStates => [0x00, 0x06, 0x0E];

    /// <summary>Resets every context to its initial state (table D.7).</summary>
    internal void ResetContexts()
    {
        Array.Clear(_states);
        Array.Clear(_mps);
        _states[0] = ZeroState;
        _states[RunLength] = RunLengthState;
        _states[Uniform] = UniformState;
    }

    /// <summary>Starts an arithmetic-coded segment (INITENC).</summary>
    internal void Start()
    {
        _bytes.Clear();
        _bytes.Add(0);
        _interval = HalfInterval;
        _code = 0;
        _count = InitialCount;
    }

    /// <summary>Starts a raw bypass segment.</summary>
    internal void StartRaw()
    {
        _bytes.Clear();
        _rawByte = 0;
        _rawFree = ByteBits;
    }

    /// <summary>Encodes one decision (ENCODE).</summary>
    /// <param name="bit">The decision.</param>
    /// <param name="context">The context.</param>
    internal void Encode(int bit, int context)
    {
        var state = _states[context];
        var probability = (uint)Probabilities[state];
        _interval -= probability;
        if (bit == _mps[context])
        {
            EncodeMps(context, state, probability);
        }
        else
        {
            EncodeLps(context, state, probability);
        }
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

        _bytes.Add((byte)_rawByte);
        _rawFree = _rawByte == AllOnes ? StuffedBits : ByteBits;
        _rawByte = 0;
    }

    /// <summary>Terminates an arithmetic-coded segment (FLUSH) and returns its bytes.</summary>
    /// <returns>The segment bytes; a final 0xFF is dropped as the standard allows.</returns>
    internal byte[] Finish()
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
        var count = _bytes.Count - 1;
        if (_bytes[^1] == AllOnes)
        {
            count--;
        }

        return [.. _bytes.GetRange(1, count)];
    }

    /// <summary>Terminates a raw segment and returns its bytes.</summary>
    /// <returns>The segment bytes.</returns>
    internal byte[] FinishRaw()
    {
        var full = _bytes.Count > 0 && _bytes[^1] == AllOnes ? StuffedBits : ByteBits;
        if (_rawFree != full)
        {
            _bytes.Add((byte)_rawByte);
        }

        return [.. _bytes];
    }

    /// <summary>Encodes the more probable symbol (CODEMPS).</summary>
    /// <param name="context">The context.</param>
    /// <param name="state">The context's state.</param>
    /// <param name="probability">The state's Qe.</param>
    private void EncodeMps(int context, int state, uint probability)
    {
        if ((_interval & HalfInterval) != 0)
        {
            _code += probability;
            return;
        }

        if (_interval < probability)
        {
            _interval = probability;
        }
        else
        {
            _code += probability;
        }

        _states[context] = NextMps[state];
        Renormalize();
    }

    /// <summary>Encodes the less probable symbol (CODELPS).</summary>
    /// <param name="context">The context.</param>
    /// <param name="state">The context's state.</param>
    /// <param name="probability">The state's Qe.</param>
    private void EncodeLps(int context, int state, uint probability)
    {
        if (_interval < probability)
        {
            _code += probability;
        }
        else
        {
            _interval = probability;
        }

        if (SwitchStates.Contains((byte)state))
        {
            _mps[context] = 1 - _mps[context];
        }

        _states[context] = NextLps[state];
        Renormalize();
    }

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
        if (_bytes[^1] == AllOnes)
        {
            Emit(_code >> StuffedShift, StuffedMask, StuffedBits);
            return;
        }

        if ((_code & CarryBit) == 0)
        {
            Emit(_code >> ByteShift, ByteMask, ByteBits);
            return;
        }

        _bytes[^1]++;
        if (_bytes[^1] == AllOnes)
        {
            _code &= CarryMask;
            Emit(_code >> StuffedShift, StuffedMask, StuffedBits);
            return;
        }

        Emit(_code >> ByteShift, ByteMask, ByteBits);
    }

    /// <summary>Appends a byte and keeps the remaining code bits.</summary>
    /// <param name="value">The byte.</param>
    /// <param name="mask">The code bits kept.</param>
    /// <param name="count">The new bit counter.</param>
    private void Emit(uint value, uint mask, int count)
    {
        _bytes.Add((byte)value);
        _code &= mask;
        _count = count;
    }
}
