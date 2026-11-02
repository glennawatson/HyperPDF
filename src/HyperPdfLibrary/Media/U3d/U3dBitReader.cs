// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>
/// Reads the values of a block's data section the way ECMA-363 clause 10 writes them: as symbols of an arithmetic code, read
/// from the first bit of each byte. A file whose profile says it is not compressed holds plain little-endian values instead, which
/// this reader also reads. A read past the end sets <see cref="Failed"/> and gives zero, so a cut-short block never throws.
/// </summary>
/// <param name="data">The block's data section.</param>
/// <param name="contexts">The adaptive counts of the object the block belongs to, or <see langword="null"/> for a plain file.</param>
internal ref struct U3dBitReader(ReadOnlySpan<byte> data, U3dContextSet? contexts)
{
    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bits in the code word the decoder compares with the probability range.</summary>
    private const int CodeBits = 15;

    /// <summary>The highest value of the 16-bit probability range.</summary>
    private const uint FullRange = 0x0000FFFF;

    /// <summary>The most significant bit of the range.</summary>
    private const uint HalfMask = 0x00008000;

    /// <summary>Every bit of the range but the most significant.</summary>
    private const uint NotHalfMask = 0x00007FFF;

    /// <summary>The second most significant bit of the range.</summary>
    private const uint QuarterMask = 0x00004000;

    /// <summary>Every bit of the range but the two most significant.</summary>
    private const uint NotThreeQuarterMask = 0x00003FFF;

    /// <summary>The number of values a plain 8-bit symbol can take.</summary>
    private const uint ByteValues = 256;

    /// <summary>The bits in the top nibble the fast count looks at.</summary>
    private const int NibbleShift = 12;

    /// <summary>The mask of one nibble.</summary>
    private const uint NibbleMask = 0xF;

    /// <summary>The bytes in a 16-bit value.</summary>
    private const int BytesInU16 = 2;

    /// <summary>The bytes in a 32-bit value.</summary>
    private const int BytesInU32 = 4;

    /// <summary>How many bytes a short string may use before it is read into a heap array.</summary>
    private const int StackBytes = 256;

    /// <summary>The number of bits the fast count removes from the range, indexed by the top nibble of low xor high.</summary>
    private static readonly byte[] LeadingBits = [4, 3, 2, 2, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0];

    /// <summary>The block's data section.</summary>
    private readonly ReadOnlySpan<byte> _data = data;

    /// <summary>The adaptive counts, or <see langword="null"/> for a plain file.</summary>
    private readonly U3dContextSet? _contexts = contexts;

    /// <summary>The position of the next bit.</summary>
    private int _bit;

    /// <summary>The lowest value of the probability range.</summary>
    private uint _low;

    /// <summary>The highest value of the probability range.</summary>
    private uint _high = FullRange;

    /// <summary>The bits decided but not yet shifted out because the range straddles the middle.</summary>
    private uint _underflow;

    /// <summary>Gets a value indicating whether a read went past the end of the block.</summary>
    internal bool Failed { get; private set; }

    /// <summary>Gets the number of whole bytes not yet read; zero once a read has failed.</summary>
    internal readonly int RemainingBytes => Failed ? 0 : Math.Max(0, (_data.Length * ByteBits) - _bit) / ByteBits;

    /// <summary>Gets the number of whole bytes read from a plain block.</summary>
    internal readonly int BytePosition => _bit / ByteBits;

    /// <summary>Marks the block as read wrongly, so that every later read gives zero.</summary>
    internal void Fail() => Failed = true;

    /// <summary>Reads an unsigned 8-bit value.</summary>
    /// <returns>The value, or zero when the block is cut short.</returns>
    internal byte ReadU8()
    {
        if (_contexts is null)
        {
            return ReadPlainByte();
        }

        var symbol = ReadSymbol(U3dContexts.Plain);
        return Failed ? (byte)0 : (byte)SwapBits(symbol - 1);
    }

    /// <summary>Reads an unsigned 16-bit value.</summary>
    /// <returns>The value, or zero when the block is cut short.</returns>
    internal ushort ReadU16()
    {
        var low = ReadU8();
        var high = ReadU8();
        return Failed ? (ushort)0 : (ushort)(low | (high << ByteBits));
    }

    /// <summary>Reads an unsigned 32-bit value.</summary>
    /// <returns>The value, or zero when the block is cut short.</returns>
    internal uint ReadU32()
    {
        uint low = ReadU16();
        uint high = ReadU16();
        return Failed ? 0 : low | (high << (ByteBits * BytesInU16));
    }

    /// <summary>Reads a signed 32-bit value.</summary>
    /// <returns>The value, or zero when the block is cut short.</returns>
    internal int ReadI32() => unchecked((int)ReadU32());

    /// <summary>Reads a 32-bit floating-point value.</summary>
    /// <returns>The value, or zero when the block is cut short.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal float ReadF32() => BitConverter.UInt32BitsToSingle(ReadU32());

    /// <summary>Reads three 32-bit floating-point values.</summary>
    /// <returns>The vector, or zeros when the block is cut short.</returns>
    internal Vector3 ReadVector3()
    {
        var x = ReadF32();
        var y = ReadF32();
        var z = ReadF32();
        return new(x, y, z);
    }

    /// <summary>Reads four 32-bit floating-point values.</summary>
    /// <returns>The vector, or zeros when the block is cut short.</returns>
    internal Vector4 ReadVector4()
    {
        var x = ReadF32();
        var y = ReadF32();
        var z = ReadF32();
        var w = ReadF32();
        return new(x, y, z, w);
    }

    /// <summary>
    /// Reads sixteen 32-bit floating-point values written in the order A to P of ECMA-363, which is column by column. In the
    /// row-vector convention of <see cref="Matrix4x4"/> that is row by row, so the values go into the fields in the order read.
    /// </summary>
    /// <returns>The matrix, or zeros when the block is cut short.</returns>
    internal Matrix4x4 ReadMatrix()
    {
        var m11 = ReadF32();
        var m12 = ReadF32();
        var m13 = ReadF32();
        var m14 = ReadF32();
        var m21 = ReadF32();
        var m22 = ReadF32();
        var m23 = ReadF32();
        var m24 = ReadF32();
        var m31 = ReadF32();
        var m32 = ReadF32();
        var m33 = ReadF32();
        var m34 = ReadF32();
        var m41 = ReadF32();
        var m42 = ReadF32();
        var m43 = ReadF32();
        var m44 = ReadF32();
        return new(m11, m12, m13, m14, m21, m22, m23, m24, m31, m32, m33, m34, m41, m42, m43, m44);
    }

    /// <summary>Reads a 64-bit floating-point value.</summary>
    /// <returns>The value, or zero when the block is cut short.</returns>
    internal double ReadF64()
    {
        ulong low = ReadU32();
        ulong high = ReadU32();
        return BitConverter.UInt64BitsToDouble(low | (high << (ByteBits * BytesInU32)));
    }

    /// <summary>Reads a string: a 16-bit byte count, then that many bytes of UTF-8.</summary>
    /// <returns>The string, or an empty string when the block is cut short.</returns>
    internal string ReadString()
    {
        var length = ReadU16();
        if (Failed || length > RemainingBytes)
        {
            Failed = true;
            return string.Empty;
        }

        if (length == 0)
        {
            return string.Empty;
        }

        Span<byte> bytes = length <= StackBytes ? stackalloc byte[StackBytes] : new byte[length];
        for (var i = 0; i < length; i++)
        {
            bytes[i] = ReadU8();
        }

        return Encoding.UTF8.GetString(bytes[..length]);
    }

    /// <summary>Reads an unsigned 32-bit value written in a compression context.</summary>
    /// <param name="context">The context, from <see cref="U3dContexts"/>.</param>
    /// <returns>The value, or zero when the block is cut short.</returns>
    internal uint ReadCompressedU32(uint context)
    {
        if (_contexts is null || context >= U3dContexts.StaticLimit)
        {
            return ReadU32();
        }

        var symbol = ReadSymbol(context);
        if (symbol != 0)
        {
            return symbol - 1;
        }

        var value = ReadU32();
        Count(context, value + 1);
        return value;
    }

    /// <summary>Reverses the order of the bits of a byte.</summary>
    /// <param name="value">The value, below 256.</param>
    /// <returns>The value with its bits reversed.</returns>
    private static uint SwapBits(uint value)
    {
        uint result = 0;
        for (var i = 0; i < ByteBits; i++)
        {
            result = (result << 1) | ((value >> i) & 1);
        }

        return result;
    }

    /// <summary>Determines whether the most significant bits of two range ends are equal.</summary>
    /// <param name="low">The low end.</param>
    /// <param name="high">The high end.</param>
    /// <returns><see langword="true"/> when they are.</returns>
    private static bool SameTopBit(uint low, uint high) => ((low ^ high) & HalfMask) == 0;

    /// <summary>Reads the next byte of a plain file.</summary>
    /// <returns>The byte, or zero when the block is cut short.</returns>
    private byte ReadPlainByte()
    {
        var at = _bit / ByteBits;
        if (Failed || at >= _data.Length)
        {
            Failed = true;
            return 0;
        }

        _bit += ByteBits;
        return _data[at];
    }

    /// <summary>Gets one bit of the block, with bits past the end reading as zero.</summary>
    /// <param name="index">The bit's position.</param>
    /// <returns>The bit.</returns>
    private readonly uint BitAt(int index) =>
        index >= 0 && (index / ByteBits) < _data.Length ? (uint)((_data[index / ByteBits] >> (index % ByteBits)) & 1) : 0;

    /// <summary>Reads the code word: the next bit, then, after any underflow bits, the next fifteen, most significant first.</summary>
    /// <returns>The 16-bit code.</returns>
    private readonly uint ReadCode()
    {
        var code = BitAt(_bit);
        var start = _bit + 1 + (int)_underflow;
        for (var i = 0; i < CodeBits; i++)
        {
            code = (code << 1) | BitAt(start + i);
        }

        return code;
    }

    /// <summary>Gets the total count of a context.</summary>
    /// <param name="context">The context.</param>
    /// <returns>The total.</returns>
    private readonly uint TotalOf(uint context) => context switch
    {
        U3dContexts.Plain => ByteValues,
        >= U3dContexts.StaticBase => context - U3dContexts.StaticBase,
        _ => _contexts!.For(context).Total,
    };

    /// <summary>Counts a symbol in an adaptive context.</summary>
    /// <param name="context">The context; a plain or static context is left alone.</param>
    /// <param name="symbol">The symbol.</param>
    private readonly void Count(uint context, uint symbol)
    {
        if (context is > U3dContexts.Plain and < U3dContexts.StaticBase)
        {
            _contexts!.For(context).Add(symbol);
        }
    }

    /// <summary>Reads one symbol and moves past the bits it used.</summary>
    /// <param name="context">The context.</param>
    /// <returns>The symbol, with the escape symbol as zero; zero when the block is cut short.</returns>
    private uint ReadSymbol(uint context)
    {
        var total = TotalOf(context);
        var range = unchecked(_high + 1 - _low);
        if (Failed || total == 0 || range == 0)
        {
            Failed = true;
            return 0;
        }

        var code = ReadCode();
        var point = unchecked((total * (1 + code - _low)) - 1) / range;
        uint symbol;
        uint below;
        uint count;
        if (context >= U3dContexts.StaticBase || context == U3dContexts.Plain)
        {
            symbol = point + 1;
            below = point;
            count = 1;
        }
        else
        {
            var histogram = _contexts!.For(context);
            symbol = histogram.SymbolAt(point);
            below = histogram.CountBelow(symbol);
            count = histogram.CountOf(symbol);
        }

        _high = unchecked(_low - 1 + (range * (below + count) / total));
        _low = unchecked(_low + (range * below / total));
        Count(context, symbol);
        Renormalise();
        if (_bit > _data.Length * ByteBits)
        {
            Failed = true;
        }

        return symbol;
    }

    /// <summary>Shifts out the leading bits the low and high ends of the range share, and counts the bits left undecided.</summary>
    private void Renormalise()
    {
        var low = _low;
        var high = _high;
        int shifted = LeadingBits[((low >> NibbleShift) ^ (high >> NibbleShift)) & NibbleMask];
        var keep = FullRange >> shifted;
        low = (low & keep) << shifted;
        high = ((high & keep) << shifted) | ((1U << shifted) - 1);
        while (SameTopBit(low, high))
        {
            low = (low & NotHalfMask) << 1;
            high = ((high & NotHalfMask) << 1) | 1;
            shifted++;
        }

        var savedLow = low & HalfMask;
        var savedHigh = high & HalfMask;
        if (shifted > 0)
        {
            shifted += (int)_underflow;
            _underflow = 0;
        }

        while ((low & QuarterMask) != 0 && (high & QuarterMask) == 0)
        {
            low = (low & NotThreeQuarterMask) << 1;
            high = (((high & NotThreeQuarterMask) << 1) | 1);
            _underflow++;
        }

        _low = low | savedLow;
        _high = high | savedHigh;
        _bit += shifted;
    }
}
