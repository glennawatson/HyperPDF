// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

/// <summary>
/// The MQ arithmetic encoder of T.88 annex E (the encoder of E.2). Contexts use the decoder's layout: one byte per
/// context, the probability state index in the low seven bits and the more probable symbol in the top bit, so a zeroed
/// buffer is a set of fresh contexts. <see cref="Finish"/> ends the data with the 0xFF 0xAC marker JBIG2 requires.
/// </summary>
internal ref struct Jbig2MqEncoder : IDisposable
{
    /// <summary>The top bit of the interval register, which renormalisation keeps set.</summary>
    private const uint HalfInterval = 0x8000;

    /// <summary>The initial count of shifts before the first byte leaves the code register.</summary>
    private const int InitialCount = 12;

    /// <summary>The marker byte that follows 0xFF at the end of the data.</summary>
    private const byte EndMarker = 0xAC;

    /// <summary>A byte with every bit set, which the carry rules treat specially.</summary>
    private const byte Fill = 0xFF;

    /// <summary>The mask of the state index in a context byte.</summary>
    private const int IndexMask = 0x7F;

    /// <summary>The shift of the more probable symbol in a context byte.</summary>
    private const int MpsShift = 7;

    /// <summary>The code register bits above which a carry has reached the byte being built.</summary>
    private const uint CarryLimit = 0x8000000;

    /// <summary>The code register shift that yields a byte after 0xFF (seven data bits).</summary>
    private const int StuffedShift = 20;

    /// <summary>The code register shift that yields an ordinary byte.</summary>
    private const int ByteShift = 19;

    /// <summary>The code register mask kept after an ordinary byte leaves.</summary>
    private const uint ByteMask = 0x7FFFF;

    /// <summary>The code register mask kept after a byte following 0xFF leaves.</summary>
    private const uint StuffedMask = 0xFFFFF;

    /// <summary>The code register mask kept when a carry made the byte 0xFF.</summary>
    private const uint CarryMask = 0x7FFFFFF;

    /// <summary>The bits left to shift after a byte following 0xFF.</summary>
    private const int StuffedCount = 7;

    /// <summary>The bits left to shift after an ordinary byte.</summary>
    private const int ByteCount = 8;

    /// <summary>The bits set by the termination step of the flush.</summary>
    private const uint FlushBits = 0xFFFF;

    /// <summary>The amount the flush takes back when its bits overshoot the interval.</summary>
    private const uint FlushBack = 0x8000;

    /// <summary>The coded bytes so far.</summary>
    private PooledBuffer _output;

    /// <summary>The code register.</summary>
    private uint _c;

    /// <summary>The interval register.</summary>
    private uint _a;

    /// <summary>The bits left before the next byte leaves the code register.</summary>
    private int _ct;

    /// <summary>The byte being built, which a carry can still change.</summary>
    private byte _b;

    /// <summary>Whether <see cref="_b"/> is the placeholder that comes before the first real byte.</summary>
    private bool _placeholder;

    /// <summary>Initializes a new instance of the <see cref="Jbig2MqEncoder"/> struct.</summary>
    /// <param name="capacity">The expected size of the coded data in bytes.</param>
    internal Jbig2MqEncoder(int capacity)
    {
        // The buffer starts empty and rents on first use; this asks for the expected size up front.
        _ = _output.GetSpan(capacity);
        _a = HalfInterval;
        _ct = InitialCount;
        _placeholder = true;
    }

    /// <summary>Releases the coder's buffer without writing it.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _output.Dispose();

    /// <summary>Encodes one bit with a context.</summary>
    /// <param name="context">The context byte, updated in place.</param>
    /// <param name="bit">The bit, 0 or 1.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Encode(ref byte context, int bit)
    {
        int state = context;
        var index = state & IndexMask;
        var mps = state >> MpsShift;
        var qe = Jbig2MqStates.Qe(index);
        _a -= qe;
        if (bit == mps)
        {
            if ((_a & HalfInterval) != 0)
            {
                _c += qe;
                return;
            }

            if (_a < qe)
            {
                _a = qe;
            }
            else
            {
                _c += qe;
            }

            context = (byte)((mps << MpsShift) | Jbig2MqStates.NextMps(index));
        }
        else
        {
            if (_a < qe)
            {
                _c += qe;
            }
            else
            {
                _a = qe;
            }

            var nextMps = Jbig2MqStates.Switches(index) ? 1 - mps : mps;
            context = (byte)((nextMps << MpsShift) | Jbig2MqStates.NextLps(index));
        }

        Renormalize();
    }

    /// <summary>Flushes the coder, appends the 0xFF 0xAC marker, writes the coded bytes to a buffer and releases the coder's own.</summary>
    /// <param name="destination">Receives the coded bytes appended to its current contents.</param>
    internal void Finish(ref PooledBuffer destination)
    {
        var temp = _c + _a;
        _c |= FlushBits;
        if (_c >= temp)
        {
            _c -= FlushBack;
        }

        _c <<= _ct;
        ByteOut();
        _c <<= _ct;
        ByteOut();
        Emit();
        if (_b != Fill)
        {
            _output.WriteByte(Fill);
        }

        _output.WriteByte(EndMarker);
        destination.Write(_output.WrittenSpan);
        _output.Dispose();
    }

    /// <summary>Doubles the interval until its top bit is set, moving bytes out as the code register fills.</summary>
    private void Renormalize()
    {
        do
        {
            _a <<= 1;
            _c <<= 1;
            _ct--;
            if (_ct == 0)
            {
                ByteOut();
            }
        }
        while ((_a & HalfInterval) == 0);
    }

    /// <summary>Moves a byte out of the code register, propagating a carry into the byte before it.</summary>
    private void ByteOut()
    {
        if (_b == Fill)
        {
            Stuffed();
            return;
        }

        if (_c < CarryLimit)
        {
            Ordinary();
            return;
        }

        _b++;
        if (_b == Fill)
        {
            _c &= CarryMask;
            Stuffed();
            return;
        }

        Ordinary();
    }

    /// <summary>Starts a byte of seven data bits after a 0xFF byte.</summary>
    private void Stuffed()
    {
        Emit();
        _b = (byte)(_c >> StuffedShift);
        _c &= StuffedMask;
        _ct = StuffedCount;
    }

    /// <summary>Starts an ordinary byte of eight data bits.</summary>
    private void Ordinary()
    {
        Emit();
        _b = (byte)(_c >> ByteShift);
        _c &= ByteMask;
        _ct = ByteCount;
    }

    /// <summary>Writes the byte being built, which can no longer change; the placeholder before the first byte is dropped.</summary>
    private void Emit()
    {
        if (_placeholder)
        {
            _placeholder = false;
            return;
        }

        _output.WriteByte(_b);
    }
}
