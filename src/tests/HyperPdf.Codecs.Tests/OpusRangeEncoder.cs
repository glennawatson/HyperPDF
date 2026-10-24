// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;

namespace HyperPdf.Codecs.Tests;

/// <summary>The range encoder of RFC 6716 section 5.1, written here so the decoder can be tested against the coding it undoes.</summary>
internal sealed class OpusRangeEncoder
{
    /// <summary>The bits in a symbol.</summary>
    private const int SymbolBits = 8;

    /// <summary>The bits of the code value.</summary>
    private const int CodeBits = 32;

    /// <summary>The largest symbol.</summary>
    private const int SymbolMax = 255;

    /// <summary>The top of the code range.</summary>
    private const uint CodeTop = 1U << (CodeBits - 1);

    /// <summary>The value below which the range is normalised.</summary>
    private const uint CodeBottom = CodeTop >> SymbolBits;

    /// <summary>The shift that brings the top symbol of the value down.</summary>
    private const int CodeShift = CodeBits - SymbolBits - 1;

    /// <summary>The packet.</summary>
    private readonly byte[] _buffer;

    /// <summary>The bytes written from the front.</summary>
    private int _offset;

    /// <summary>The bytes written from the back.</summary>
    private int _endOffset;

    /// <summary>The raw bits waiting to be written at the back.</summary>
    private uint _endWindow;

    /// <summary>The count of bits in the window.</summary>
    private int _endBits;

    /// <summary>The size of the range.</summary>
    private uint _range = CodeTop;

    /// <summary>The low end of the range.</summary>
    private uint _value;

    /// <summary>The byte waiting for a carry, or -1.</summary>
    private int _remainder = -1;

    /// <summary>The count of 255 bytes waiting for a carry.</summary>
    private int _extension;

    /// <summary>Initializes a new instance of the <see cref="OpusRangeEncoder"/> class.</summary>
    /// <param name="size">The size of the packet in bytes.</param>
    internal OpusRangeEncoder(int size) => _buffer = new byte[size];

    /// <summary>Encodes a symbol by its cumulative frequencies.</summary>
    /// <param name="low">The frequency below the symbol.</param>
    /// <param name="high">The frequency up to and including the symbol.</param>
    /// <param name="total">The total frequency.</param>
    internal void Encode(uint low, uint high, uint total)
    {
        var unit = _range / total;
        if (low != 0)
        {
            _value += _range - (unit * (total - low));
            _range = unit * (high - low);
        }
        else
        {
            _range -= unit * (total - high);
        }

        Normalize();
    }

    /// <summary>Encodes a bit with a probability of one over a power of two.</summary>
    /// <param name="bit">The bit.</param>
    /// <param name="logProbability">The logarithm of the reciprocal of the probability that the bit is set.</param>
    internal void EncodeBit(bool bit, int logProbability)
    {
        var range = _range;
        var low = _value;
        var split = range >> logProbability;
        range -= split;
        if (bit)
        {
            _value = low + range;
        }

        _range = bit ? split : range;
        Normalize();
    }

    /// <summary>Encodes a symbol of a table of inverse cumulative frequencies.</summary>
    /// <param name="symbol">The symbol.</param>
    /// <param name="inverseCdf">The table.</param>
    /// <param name="totalBits">The bits of the total.</param>
    internal void EncodeSymbol(int symbol, ReadOnlySpan<byte> inverseCdf, int totalBits)
    {
        var unit = _range >> totalBits;
        if (symbol > 0)
        {
            _value += _range - (unit * inverseCdf[symbol - 1]);
            _range = unit * (uint)(inverseCdf[symbol - 1] - inverseCdf[symbol]);
        }
        else
        {
            _range -= unit * inverseCdf[symbol];
        }

        Normalize();
    }

    /// <summary>Writes raw bits at the back of the packet.</summary>
    /// <param name="value">The bits.</param>
    /// <param name="bits">The count.</param>
    internal void EncodeRaw(uint value, int bits)
    {
        var window = _endWindow;
        var used = _endBits;
        if (used + bits > CodeBits)
        {
            do
            {
                WriteBack((byte)(window & SymbolMax));
                window >>= SymbolBits;
                used -= SymbolBits;
            }
            while (used >= SymbolBits);
        }

        window |= value << used;
        _endWindow = window;
        _endBits = used + bits;
    }

    /// <summary>Encodes a number below a limit uniformly.</summary>
    /// <param name="value">The number.</param>
    /// <param name="total">One more than the largest number.</param>
    internal void EncodeUnsigned(uint value, uint total)
    {
        total--;
        var bits = total == 0 ? 0 : BitOperations.Log2(total) + 1;
        if (bits > SymbolBits)
        {
            bits -= SymbolBits;
            var top = (total >> bits) + 1;
            var upper = value >> bits;
            Encode(upper, upper + 1, top);
            EncodeRaw(value & ((1U << bits) - 1), bits);
        }
        else
        {
            Encode(value, value + 1, total + 1);
        }
    }

    /// <summary>Ends the coding and returns the packet.</summary>
    /// <returns>The packet, as large as it was made.</returns>
    internal byte[] Finish()
    {
        var shift = CodeBits - (BitOperations.Log2(_range) + 1);
        var mask = (CodeTop - 1) >> shift;
        var end = (_value + mask) & ~mask;
        if ((end | mask) >= _value + _range)
        {
            shift++;
            mask >>= 1;
            end = (_value + mask) & ~mask;
        }

        while (shift > 0)
        {
            CarryOut((int)(end >> CodeShift));
            end = (end << SymbolBits) & (CodeTop - 1);
            shift -= SymbolBits;
        }

        if (_remainder >= 0 || _extension > 0)
        {
            CarryOut(0);
        }

        var window = _endWindow;
        var used = _endBits;
        while (used >= SymbolBits)
        {
            WriteBack((byte)(window & SymbolMax));
            window >>= SymbolBits;
            used -= SymbolBits;
        }

        if (used > 0)
        {
            _buffer[_buffer.Length - _endOffset - 1] |= (byte)window;
        }

        return _buffer;
    }

    /// <summary>Writes a byte at the front of the packet.</summary>
    /// <param name="value">The byte.</param>
    private void WriteFront(byte value)
    {
        _buffer[_offset] = value;
        _offset++;
    }

    /// <summary>Writes a byte at the back of the packet.</summary>
    /// <param name="value">The byte.</param>
    private void WriteBack(byte value)
    {
        _endOffset++;
        _buffer[_buffer.Length - _endOffset] = value;
    }

    /// <summary>Passes the top symbol of the value on, handling a carry into the bytes already held.</summary>
    /// <param name="symbol">The symbol, with its carry in the ninth bit.</param>
    private void CarryOut(int symbol)
    {
        if (symbol != SymbolMax)
        {
            var carry = symbol >> SymbolBits;
            if (_remainder >= 0)
            {
                WriteFront((byte)(_remainder + carry));
            }

            var filler = (byte)((SymbolMax + carry) & SymbolMax);
            for (; _extension > 0; _extension--)
            {
                WriteFront(filler);
            }

            _remainder = symbol & SymbolMax;
        }
        else
        {
            _extension++;
        }
    }

    /// <summary>Shifts out bytes while the range is small.</summary>
    private void Normalize()
    {
        while (_range <= CodeBottom)
        {
            CarryOut((int)(_value >> CodeShift));
            _value = (_value << SymbolBits) & (CodeTop - 1);
            _range <<= SymbolBits;
        }
    }
}
