// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs;

/// <summary>The CABAC arithmetic decoding engine of clause 9.3.3.2 of ITU-T H.264 and clause 9.3.4.3 of ITU-T H.265, with the context states it decodes with.</summary>
internal ref struct CabacEngine
{
    /// <summary>The bits of codIOffset at the start.</summary>
    private const int OffsetBits = 9;

    /// <summary>The value codIRange starts at.</summary>
    private const int InitialRange = 510;

    /// <summary>The smallest codIRange that needs no renormalisation.</summary>
    private const int MinRange = 256;

    /// <summary>The leading zeros of a 32 bit value whose highest set bit is bit 8.</summary>
    private const int LeadingZerosAtMin = 23;

    /// <summary>The shift that finds the quantised range index.</summary>
    private const int RangeIndexShift = 6;

    /// <summary>The mask of the quantised range index.</summary>
    private const int RangeIndexMask = 3;

    /// <summary>The amount codIRange shrinks by before the terminating bin is tested.</summary>
    private const int TerminateRange = 2;

    /// <summary>The bits of a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The context state's bits that hold the most probable symbol.</summary>
    private const int MpsMask = 1;

    /// <summary>The shift of the probability state in a context byte.</summary>
    private const int StateShift = 1;

    /// <summary>The offset from a byte to the third byte of a read window.</summary>
    private const int ThirdByte = 2;

    /// <summary>The bits read ahead into a window.</summary>
    private const int WindowBits = 24;

    /// <summary>The slice data.</summary>
    private readonly ReadOnlySpan<byte> _data;

    /// <summary>The context states: the probability state in the high bits and the most probable symbol in the low bit.</summary>
    private readonly Span<byte> _contexts;

    /// <summary>The range of the interval.</summary>
    private int _range;

    /// <summary>The offset into the interval.</summary>
    private int _offset;

    /// <summary>The index of the next bit to read.</summary>
    private long _bitPosition;

    /// <summary>Initializes a new instance of the <see cref="CabacEngine"/> struct and reads the first bits (clause 9.3.1.2).</summary>
    /// <param name="data">The slice data.</param>
    /// <param name="bitPosition">The index of the bit where arithmetic coded data starts, which is byte aligned.</param>
    /// <param name="contexts">The context states, already initialised.</param>
    internal CabacEngine(ReadOnlySpan<byte> data, long bitPosition, Span<byte> contexts)
    {
        _data = data;
        _contexts = contexts;
        _bitPosition = bitPosition;
        _range = InitialRange;
        _offset = ReadBits(OffsetBits);
    }

    /// <summary>Gets the slice data the engine reads.</summary>
    internal readonly ReadOnlySpan<byte> Data => _data;

    /// <summary>Gets the index of the next bit the engine would read.</summary>
    internal readonly long BitPosition => _bitPosition;

    /// <summary>Gets a value indicating whether the engine has read past the end of the data.</summary>
    internal readonly bool IsOverrun => _bitPosition > (long)_data.Length * ByteBits;

    /// <summary>Decodes one bin with a context (clause 9.3.3.2.1).</summary>
    /// <param name="ctxIdx">The context index, not 276.</param>
    /// <returns>The bin, 0 or 1.</returns>
    internal int DecodeDecision(int ctxIdx)
    {
        var context = _contexts[ctxIdx];
        var state = context >> StateShift;
        var mps = context & MpsMask;
        var rangeLps = CabacTables.RangeLps[(state << 2) + ((_range >> RangeIndexShift) & RangeIndexMask)];
        _range -= rangeLps;
        int bin;
        if (_offset >= _range)
        {
            bin = mps ^ 1;
            _offset -= _range;
            _range = rangeLps;

            // At the least skewed state the more probable symbol swaps.
            _contexts[ctxIdx] = (byte)((CabacTables.TransitionLps[state] << StateShift) | (state == 0 ? bin : mps));
        }
        else
        {
            bin = mps;
            _contexts[ctxIdx] = (byte)((CabacTables.TransitionMps[state] << StateShift) | mps);
        }

        Renormalize();
        return bin;
    }

    /// <summary>Decodes one bin that is as likely to be 0 as 1 (clause 9.3.3.2.3).</summary>
    /// <returns>The bin.</returns>
    internal int DecodeBypass()
    {
        _offset = (_offset << 1) | ReadBits(1);
        if (_offset < _range)
        {
            return 0;
        }

        _offset -= _range;
        return 1;
    }

    /// <summary>Decodes the bin that tells the end of a slice or an I_PCM macroblock (clause 9.3.3.2.4).</summary>
    /// <returns>The bin; 1 means arithmetic decoding has ended.</returns>
    internal int DecodeTerminate()
    {
        _range -= TerminateRange;
        if (_offset >= _range)
        {
            return 1;
        }

        Renormalize();
        return 0;
    }

    /// <summary>Starts decoding again at a bit position, as after the samples of an I_PCM macroblock (clause 9.3.1.2).</summary>
    /// <param name="bitPosition">The index of the bit where arithmetic coded data resumes.</param>
    internal void Restart(long bitPosition)
    {
        _bitPosition = bitPosition;
        _range = InitialRange;
        _offset = ReadBits(OffsetBits);
    }

    /// <summary>Doubles the range until it is at least 256, shifting in bits (clause 9.3.3.2.2).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Renormalize()
    {
        if (_range >= MinRange)
        {
            return;
        }

        var shift = BitOperations.LeadingZeroCount((uint)_range) - LeadingZerosAtMin;
        _range <<= shift;
        _offset = (_offset << shift) | ReadBits(shift);
    }

    /// <summary>Reads bits, most significant first; bits past the end read as zero.</summary>
    /// <param name="count">The bits to read, up to 9.</param>
    /// <returns>The bits.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ReadBits(int count)
    {
        var position = _bitPosition;
        _bitPosition += count;
        var index = (int)(position / ByteBits);
        var window = (ByteAt(index) << (WindowBits - ByteBits)) | (ByteAt(index + 1) << ByteBits) | ByteAt(index + ThirdByte);
        return (window >> (WindowBits - (int)(position % ByteBits) - count)) & ((1 << count) - 1);
    }

    /// <summary>Gets a byte of the data, or zero past the end.</summary>
    /// <param name="index">The byte's index.</param>
    /// <returns>The byte.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly int ByteAt(int index) => (uint)index < (uint)_data.Length ? _data[index] : 0;
}
