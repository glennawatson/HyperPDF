// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>
/// The MQ arithmetic decoder of T.88 annex E. Each context is one byte: the probability state index in the low bits and
/// the more probable symbol in the top bit, so a zeroed buffer is a set of fresh contexts. Past the end of the data it
/// reads 0xFF bytes (as at a marker, annex E.3.4) and sets <see cref="IsComplete"/>, so callers can stop instead of decoding noise.
/// </summary>
internal ref struct Jbig2ArithmeticDecoder
{
    /// <summary>The top bit of the interval register, which renormalisation keeps set.</summary>
    private const uint HalfInterval = 0x8000;

    /// <summary>The shift of the code register's comparison bits.</summary>
    private const int CodeShift = 16;

    /// <summary>The bits shifted into the code register before decoding starts.</summary>
    private const int StartShift = 7;

    /// <summary>A byte with every bit set, read past the end of the data.</summary>
    private const byte Fill = 0xFF;

    /// <summary>The largest byte after 0xFF that is still data; larger ones are markers.</summary>
    private const byte LargestStuffedByte = 0x8F;

    /// <summary>The bits read per byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bits read from a byte that follows 0xFF.</summary>
    private const int StuffedBits = 7;

    /// <summary>The amount added with a normal byte, before subtracting the byte.</summary>
    private const uint ByteBase = 0xFF00;

    /// <summary>The amount added with a byte after 0xFF, before subtracting the byte.</summary>
    private const uint StuffedBase = 0xFE00;

    /// <summary>The shift that places a byte after 0xFF.</summary>
    private const int StuffedShift = 9;

    /// <summary>The mask of the state index in a context byte.</summary>
    private const int IndexMask = 0x7F;

    /// <summary>The shift of the more probable symbol in a context byte.</summary>
    private const int MpsShift = 7;

    /// <summary>
    /// The Qe probability of each state (T.88 table E.1). A static array rather than a span property, because spans of
    /// multi-byte constants allocate on every access in unoptimised builds.
    /// </summary>
    private static readonly ushort[] Qe =
    [
        0x5601, 0x3401, 0x1801, 0x0AC1, 0x0521, 0x0221, 0x5601, 0x5401, 0x4801, 0x3801, 0x3001, 0x2401, 0x1C01, 0x1601,
        0x5601, 0x5401, 0x5101, 0x4801, 0x3801, 0x3401, 0x3001, 0x2801, 0x2401, 0x2201, 0x1C01, 0x1801, 0x1601, 0x1401,
        0x1201, 0x1101, 0x0AC1, 0x09C1, 0x08A1, 0x0521, 0x0441, 0x02A1, 0x0221, 0x0141, 0x0111, 0x0085, 0x0049, 0x0025,
        0x0015, 0x0009, 0x0005, 0x0001, 0x5601,
    ];

    /// <summary>The data.</summary>
    private readonly ReadOnlySpan<byte> _data;

    /// <summary>The token for the containing segment decode.</summary>
    private readonly CancellationToken _cancellationToken;

    /// <summary>The position of the byte last read into the code register.</summary>
    private int _position;

    /// <summary>The code register.</summary>
    private uint _c;

    /// <summary>The interval register.</summary>
    private uint _a;

    /// <summary>The bits left before the next byte is read.</summary>
    private int _ct;

    /// <summary>The byte last read.</summary>
    private byte _b;

    /// <summary>Initializes a new instance of the <see cref="Jbig2ArithmeticDecoder"/> struct.</summary>
    /// <param name="data">The data, usually the whole JBIG2 stream.</param>
    /// <param name="position">The position of the first coded byte.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    internal Jbig2ArithmeticDecoder(ReadOnlySpan<byte> data, int position, CancellationToken cancellationToken = default)
    {
        _data = data;
        _cancellationToken = cancellationToken;
        _position = Math.Clamp(position, 0, data.Length);
        _b = _position < data.Length ? data[_position] : Fill;
        _c = (uint)(_b ^ Fill) << CodeShift;
        ByteIn();
        _c <<= StartShift;
        _ct -= StartShift;
        _a = HalfInterval;
    }

    /// <summary>Gets the position of the byte last read, where the next segment field starts after alignment.</summary>
    internal readonly int Position => _position;

    /// <summary>Gets the token for arithmetic region loops.</summary>
    internal readonly CancellationToken CancellationToken => _cancellationToken;

    /// <summary>Gets a value indicating whether the decoder has read past the end of the data.</summary>
    internal bool IsComplete { readonly get; private set; }

    /// <summary>Gets the next state after a more probable symbol (T.88 table E.1).</summary>
    private static ReadOnlySpan<byte> NextMps =>
    [
        0x01, 0x02, 0x03, 0x04, 0x05, 0x26, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x1D, 0x0F, 0x10, 0x11, 0x12, 0x13,
        0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26,
        0x27, 0x28, 0x29, 0x2A, 0x2B, 0x2C, 0x2D, 0x2D, 0x2E,
    ];

    /// <summary>Gets the next state after a less probable symbol (T.88 table E.1).</summary>
    private static ReadOnlySpan<byte> NextLps =>
    [
        0x01, 0x06, 0x09, 0x0C, 0x1D, 0x21, 0x06, 0x0E, 0x0E, 0x0E, 0x11, 0x12, 0x14, 0x15, 0x0E, 0x0E, 0x0F, 0x10, 0x11,
        0x12, 0x13, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x20, 0x21, 0x22, 0x23,
        0x24, 0x25, 0x26, 0x27, 0x28, 0x29, 0x2A, 0x2B, 0x2E,
    ];

    /// <summary>Gets whether a less probable symbol swaps the more probable symbol (T.88 table E.1).</summary>
    private static ReadOnlySpan<byte> Switch =>
    [
        0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    ];

    /// <summary>Decodes one bit with a context.</summary>
    /// <param name="context">The context byte, updated in place.</param>
    /// <returns>The bit.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int Decode(ref byte context)
    {
        int state = context;
        var index = state & IndexMask;
        uint qe = Qe[index];
        _a -= qe;
        if ((_c >> CodeShift) < _a)
        {
            if ((_a & HalfInterval) != 0)
            {
                return state >> MpsShift;
            }

            var bit = _a < qe ? Lps(ref context, state, index) : Mps(ref context, state, index);
            Renormalize();
            return bit;
        }

        return DecodeUpper(ref context, state, index, qe);
    }

    /// <summary>Takes the more probable symbol and moves to its next state.</summary>
    /// <param name="context">The context byte.</param>
    /// <param name="state">The context's state before the update.</param>
    /// <param name="index">The state index.</param>
    /// <returns>The more probable symbol.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Mps(ref byte context, int state, int index)
    {
        var mps = state >> MpsShift;
        context = (byte)((mps << MpsShift) | NextMps[index]);
        return mps;
    }

    /// <summary>Takes the less probable symbol, swapping the symbols when the state says so.</summary>
    /// <param name="context">The context byte.</param>
    /// <param name="state">The context's state before the update.</param>
    /// <param name="index">The state index.</param>
    /// <returns>The less probable symbol.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Lps(ref byte context, int state, int index)
    {
        var mps = state >> MpsShift;
        var bit = 1 - mps;
        var nextMps = Switch[index] != 0 ? bit : mps;
        context = (byte)((nextMps << MpsShift) | NextLps[index]);
        return bit;
    }

    /// <summary>Decodes when the code register falls in the upper sub-interval.</summary>
    /// <param name="context">The context byte.</param>
    /// <param name="state">The context's state.</param>
    /// <param name="index">The state index.</param>
    /// <param name="qe">The state's probability.</param>
    /// <returns>The bit.</returns>
    private int DecodeUpper(ref byte context, int state, int index, uint qe)
    {
        _c -= _a << CodeShift;
        var bit = _a < qe ? Mps(ref context, state, index) : Lps(ref context, state, index);
        _a = qe;
        Renormalize();
        return bit;
    }

    /// <summary>Doubles the interval until its top bit is set, reading bytes as needed.</summary>
    private void Renormalize()
    {
        do
        {
            if (_ct == 0)
            {
                ByteIn();
            }

            _a <<= 1;
            _c <<= 1;
            _ct--;
        }
        while ((_a & HalfInterval) == 0);
    }

    /// <summary>Reads the next byte into the code register, handling 0xFF stuffing and markers.</summary>
    private void ByteIn()
    {
        if (_b == Fill)
        {
            var next = _position + 1 < _data.Length ? _data[_position + 1] : Fill;
            if (next > LargestStuffedByte)
            {
                _ct = ByteBits;
            }
            else
            {
                _position++;
                _b = next;
                _c += StuffedBase - ((uint)_b << StuffedShift);
                _ct = StuffedBits;
            }
        }
        else
        {
            _position = Math.Min(_position + 1, _data.Length);
            _b = _position < _data.Length ? _data[_position] : Fill;
            _c += ByteBase - ((uint)_b << ByteBits);
            _ct = ByteBits;
        }

        IsComplete |= _position >= _data.Length;
    }
}
