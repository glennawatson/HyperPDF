// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>
/// Writes the entropy-coded bytes of a scan: Huffman codes and raw bits packed most significant bit first, with a zero
/// byte stuffed after every 0xFF and restart markers between intervals (ISO/IEC 10918-1, B.1.1.5).
/// </summary>
internal ref struct JpegBitWriter : IJpegEntropySink
{
    /// <summary>The bits flushed at a time.</summary>
    private const int FlushBits = 32;

    /// <summary>The bytes flushed at a time.</summary>
    private const int FlushBytes = 4;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The restart markers cycle through eight values.</summary>
    private const int RestartCycle = 8;

    /// <summary>The value that is 0x01 in every byte of a word.</summary>
    private const uint LowBits = 0x01010101;

    /// <summary>The value that is 0x80 in every byte of a word.</summary>
    private const uint HighBits = 0x80808080;

    /// <summary>The space asked of the output for each flush: four bytes, each of which may be stuffed.</summary>
    private const int FlushSpace = FlushBytes * 2;

    /// <summary>The code lookup of every table slot, 256 entries each.</summary>
    private readonly ReadOnlySpan<uint> _codes;

    /// <summary>The output; a copy of the caller's buffer that shares its array until the array grows, so the caller takes it back with <see cref="Output"/>.</summary>
    private PooledBuffer _output;

    /// <summary>The bits not yet written; the pending bits are the low <see cref="_bitCount"/> bits.</summary>
    private ulong _accumulator;

    /// <summary>The pending bits.</summary>
    private int _bitCount;

    /// <summary>Initializes a new instance of the <see cref="JpegBitWriter"/> struct.</summary>
    /// <param name="output">The buffer that receives the bytes; the writer works on a copy.</param>
    /// <param name="codes">The code lookup of every table slot, <see cref="JpegHuffmanBuilder.SymbolSlots"/> entries each.</param>
    internal JpegBitWriter(PooledBuffer output, ReadOnlySpan<uint> codes)
    {
        _output = output;
        _codes = codes;
    }

    /// <summary>Gets the buffer with everything written so far; the caller replaces its own buffer with this one.</summary>
    internal readonly PooledBuffer Output => _output;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Symbol(int table, int symbol, uint bits, int bitCount)
    {
        var entry = _codes[(table * JpegHuffmanBuilder.SymbolSlots) + symbol];
        var size = (int)(entry >> JpegHuffmanBuilder.SizeShift);
        Put(((entry & JpegHuffmanBuilder.CodeMask) << bitCount) | bits, size + bitCount);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Symbol(int table, int symbol)
    {
        var entry = _codes[(table * JpegHuffmanBuilder.SymbolSlots) + symbol];
        Put(entry & JpegHuffmanBuilder.CodeMask, (int)(entry >> JpegHuffmanBuilder.SizeShift));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Bits(uint bits, int bitCount) => Put(bits, bitCount);

    /// <inheritdoc/>
    public void Restart(int index)
    {
        Finish();
        var span = _output.GetSpan(JpegMarkers.MarkerLength);
        span[0] = JpegMarkers.MarkerPrefix;
        span[1] = (byte)(JpegMarkers.FirstRestart + (index % RestartCycle));
        _output.Advance(JpegMarkers.MarkerLength);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EndScan() => Finish();

    /// <summary>Writes the pending bits, padding the last byte with ones.</summary>
    private void Finish()
    {
        if (_bitCount == 0)
        {
            return;
        }

        var padding = (ByteBits - (_bitCount % ByteBits)) % ByteBits;
        Put((1U << padding) - 1, padding);
        while (_bitCount > 0)
        {
            _bitCount -= ByteBits;
            WriteByte((byte)(_accumulator >> _bitCount));
        }

        _accumulator = 0;
    }

    /// <summary>Adds bits to the pending bits and writes whole words when enough have collected.</summary>
    /// <param name="bits">The bits, right aligned.</param>
    /// <param name="count">The number of bits, up to 31.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Put(uint bits, int count)
    {
        _accumulator = (_accumulator << count) | bits;
        _bitCount += count;
        if (_bitCount >= FlushBits)
        {
            FlushWord();
        }
    }

    /// <summary>Writes the oldest four bytes of the pending bits.</summary>
    private void FlushWord()
    {
        _bitCount -= FlushBits;
        var word = (uint)(_accumulator >> _bitCount);
        var span = _output.GetSpan(FlushSpace);
        var inverted = ~word;
        if (((inverted - LowBits) & ~inverted & HighBits) == 0)
        {
            // No byte of the word is 0xFF, so none needs a stuffed zero.
            BinaryPrimitives.WriteUInt32BigEndian(span, word);
            _output.Advance(FlushBytes);
            return;
        }

        for (var shift = FlushBits - ByteBits; shift >= 0; shift -= ByteBits)
        {
            WriteByte((byte)(word >> shift));
        }
    }

    /// <summary>Writes one byte, followed by a zero when it is 0xFF.</summary>
    /// <param name="value">The byte.</param>
    private void WriteByte(byte value)
    {
        _output.WriteByte(value);
        if (value == JpegMarkers.MarkerPrefix)
        {
            _output.WriteByte(0);
        }
    }
}
