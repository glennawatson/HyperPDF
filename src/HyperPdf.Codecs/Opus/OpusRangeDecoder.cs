// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;

namespace HyperPdf.Codecs.Opus;

/// <summary>
/// The range decoder of RFC 6716 section 4.1. The bits that code symbols are read from the front of the packet, and the raw
/// bits that need no modelling are read from its back, so the two meet in the middle and nothing is wasted.
/// </summary>
/// <remarks>Both the SILK and the CELT layers read their symbols through this decoder.</remarks>
internal ref struct OpusRangeDecoder
{
    /// <summary>The bits in a byte, the size of a symbol the coder shifts in.</summary>
    private const int SymbolBits = 8;

    /// <summary>The bits of the code value the coder works with.</summary>
    private const int CodeBits = 32;

    /// <summary>The bits of the code value kept above the byte being shifted in.</summary>
    private const int CodeExtra = 7;

    /// <summary>The largest value of a symbol.</summary>
    private const int SymbolMax = 255;

    /// <summary>The top of the range of the code value.</summary>
    private const uint CodeTop = 1U << (CodeBits - 1);

    /// <summary>The value below which the range is normalised by shifting in another byte.</summary>
    private const uint CodeBottom = CodeTop >> SymbolBits;

    /// <summary>The bits of a range cut down to find its logarithm.</summary>
    private const int SixteenBits = 16;

    /// <summary>The shift that keeps a squared sixteen bit range at sixteen bits.</summary>
    private const int FifteenBits = 15;

    /// <summary>The bits of fraction <see cref="TellFractional"/> counts to: eighths of a bit.</summary>
    private const int FractionBits = 3;

    /// <summary>The most bits a call may read raw at once.</summary>
    private const int MaxRawBits = 25;

    /// <summary>The most bits a quantity read by <see cref="ReadUnsigned"/> may spread over before the top is coded with a model.</summary>
    private const int ModelledBits = 8;

    /// <summary>The packet.</summary>
    private ReadOnlySpan<byte> _buffer;

    /// <summary>The bytes of the packet taken from its back for raw bits.</summary>
    private int _endOffset;

    /// <summary>The raw bits taken from the back and not yet used.</summary>
    private uint _endWindow;

    /// <summary>The count of bits in <see cref="_endWindow"/>.</summary>
    private int _endBits;

    /// <summary>The bits the decoder has consumed, as counted by <see cref="Tell"/>.</summary>
    private int _bitsTotal;

    /// <summary>The next byte of the packet to read from the front.</summary>
    private int _offset;

    /// <summary>The size of the coding range.</summary>
    private uint _range;

    /// <summary>The difference between the top of the range and the coded value.</summary>
    private uint _value;

    /// <summary>The last byte read from the front, not yet absorbed into the value.</summary>
    private int _remainder;

    /// <summary>The range divided by the total of the last frequency read.</summary>
    private uint _scale;

    /// <summary>Initializes a new instance of the <see cref="OpusRangeDecoder"/> struct.</summary>
    /// <param name="packet">The bytes of the frame; an empty frame decodes as zeros.</param>
    internal OpusRangeDecoder(ReadOnlySpan<byte> packet)
    {
        _buffer = packet;
        _endOffset = 0;
        _endWindow = 0;
        _endBits = 0;
        _bitsTotal = CodeBits + 1 - (((CodeBits - CodeExtra) / SymbolBits) * SymbolBits);
        _offset = 0;
        _range = 1U << CodeExtra;
        _scale = 0;
        _remainder = ReadFront();
        _value = _range - 1 - (uint)(_remainder >> (SymbolBits - CodeExtra));
        Normalize();
    }

    /// <summary>Gets the bits the frame holds.</summary>
    internal readonly int TotalBits => _buffer.Length * SymbolBits;

    /// <summary>Gets the size of the coding range, which the encoder also reports at the end of a frame so the two can be checked against each other; CELT seeds its noise with it.</summary>
    internal readonly uint Range => _range;

    /// <summary>Counts every bit of the frame as used, so what follows reads as the end of the data.</summary>
    internal void SkipToEnd() => _bitsTotal += TotalBits - Tell();

    /// <summary>Takes bytes off the end of the frame, which hold other data, so the raw bits stop short of them.</summary>
    /// <param name="bytes">The bytes to take off.</param>
    internal void ShrinkStorage(int bytes) => _buffer = _buffer[..^bytes];

    /// <summary>Gets the whole bits the decoder has used so far.</summary>
    /// <returns>The bit count, rounded up.</returns>
    internal readonly int Tell() => _bitsTotal - BitOperations.Log2(_range) - 1;

    /// <summary>Gets the bits the decoder has used so far, to an eighth of a bit.</summary>
    /// <returns>The count in eighths of a bit.</returns>
    internal readonly int TellFractional()
    {
        // The range is cut to sixteen bits, and squaring it finds one more bit of its logarithm each time.
        var length = BitOperations.Log2(_range) + 1;
        var scaled = _range >> (length - SixteenBits);
        for (var i = 0; i < FractionBits; i++)
        {
            scaled = (scaled * scaled) >> FifteenBits;
            var carry = scaled >> SixteenBits;
            length = (length << 1) | (int)carry;
            scaled >>= (int)carry;
        }

        return (_bitsTotal << FractionBits) - length;
    }

    /// <summary>Reads the cumulative frequency of the next symbol out of a total.</summary>
    /// <param name="total">The total of all the frequencies.</param>
    /// <returns>A value from zero to one less than the total; pass the symbol it falls in to <see cref="Update"/>.</returns>
    internal uint Decode(uint total)
    {
        _scale = _range / total;
        return total - Math.Min((_value / _scale) + 1, total);
    }

    /// <summary>Reads the cumulative frequency of the next symbol out of a total that is a power of two.</summary>
    /// <param name="bits">The bits of the total.</param>
    /// <returns>A value from zero to one less than the total.</returns>
    internal uint DecodeBinary(int bits)
    {
        _scale = _range >> bits;
        return (1U << bits) - Math.Min((_value / _scale) + 1, 1U << bits);
    }

    /// <summary>Takes the symbol found by <see cref="Decode"/> out of the range.</summary>
    /// <param name="low">The cumulative frequency of the symbol.</param>
    /// <param name="high">The cumulative frequency after the symbol.</param>
    /// <param name="total">The total of all the frequencies.</param>
    internal void Update(uint low, uint high, uint total)
    {
        var step = _scale * (total - high);
        _value -= step;
        _range = low != 0 ? _scale * (high - low) : _range - step;
        Normalize();
    }

    /// <summary>Reads a bit that is set with a probability of one over a power of two.</summary>
    /// <param name="logProbability">The base two logarithm of the reciprocal of the probability that the bit is set.</param>
    /// <returns><see langword="true"/> when the bit is set.</returns>
    internal bool ReadBit(int logProbability)
    {
        var range = _range;
        var value = _value;
        var split = range >> logProbability;
        var set = value < split;
        if (!set)
        {
            _value = value - split;
        }

        _range = set ? split : range - split;
        Normalize();
        return set;
    }

    /// <summary>Reads a symbol from a table of inverse cumulative frequencies.</summary>
    /// <param name="inverseCdf">For each symbol, the total less the cumulative frequency after it; the last entry is zero.</param>
    /// <param name="totalBits">The bits of the total.</param>
    /// <returns>The symbol.</returns>
    internal int ReadSymbol(scoped ReadOnlySpan<byte> inverseCdf, int totalBits)
    {
        var value = _value;
        var span = _range;
        var unit = span >> totalBits;
        var symbol = -1;
        uint previous;
        do
        {
            previous = span;
            symbol++;
            span = unit * inverseCdf[symbol];
        }
        while (value < span);

        _value = value - span;
        _range = previous - span;
        Normalize();
        return symbol;
    }

    /// <summary>Reads raw bits from the back of the packet.</summary>
    /// <param name="bits">The number of bits, from zero to 25.</param>
    /// <returns>The bits as a number.</returns>
    internal uint ReadRaw(int bits)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bits, MaxRawBits);
        var window = _endWindow;
        var available = _endBits;
        if (available < bits)
        {
            do
            {
                window |= (uint)ReadBack() << available;
                available += SymbolBits;
            }
            while (available <= CodeBits - SymbolBits);
        }

        var result = window & ((1U << bits) - 1);
        _endWindow = window >> bits;
        _endBits = available - bits;
        _bitsTotal += bits;
        return result;
    }

    /// <summary>Reads a number that is uniform below a limit.</summary>
    /// <param name="total">One more than the largest value.</param>
    /// <returns>The value; zero when the packet is damaged and the value would be out of range.</returns>
    internal uint ReadUnsigned(uint total)
    {
        total--;
        var bits = total == 0 ? 0 : BitOperations.Log2(total) + 1;
        if (bits > ModelledBits)
        {
            bits -= ModelledBits;
            var top = (total >> bits) + 1;
            var symbol = Decode(top);
            Update(symbol, symbol + 1, top);
            var value = (symbol << bits) | ReadRaw(bits);
            return value <= total ? value : total;
        }

        total++;
        var plain = Decode(total);
        Update(plain, plain + 1, total);
        return plain;
    }

    /// <summary>Reads the next byte from the front of the packet.</summary>
    /// <returns>The byte, or zero past the end.</returns>
    private int ReadFront()
    {
        if (_offset >= _buffer.Length)
        {
            return 0;
        }

        var next = _buffer[_offset];
        _offset++;
        return next;
    }

    /// <summary>Reads the next byte from the back of the packet.</summary>
    /// <returns>The byte, or zero when the front has been reached.</returns>
    private int ReadBack()
    {
        if (_endOffset >= _buffer.Length)
        {
            return 0;
        }

        _endOffset++;
        return _buffer[_buffer.Length - _endOffset];
    }

    /// <summary>Shifts in bytes from the front until the range is large again.</summary>
    private void Normalize()
    {
        while (_range <= CodeBottom)
        {
            _bitsTotal += SymbolBits;
            _range <<= SymbolBits;
            var symbol = _remainder;
            _remainder = ReadFront();
            symbol = ((symbol << SymbolBits) | _remainder) >> (SymbolBits - CodeExtra);
            _value = ((_value << SymbolBits) + (uint)(SymbolMax & ~symbol)) & (CodeTop - 1);
        }
    }
}
