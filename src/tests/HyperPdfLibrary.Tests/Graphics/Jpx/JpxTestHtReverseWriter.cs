// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>
/// Writes a backward-growing HT bit-stream (VLC or MagRef): least significant bit first, bytes emitted from the end of
/// the segment back. After a byte above 0x8F, a byte whose low seven bits would all be ones is emitted with only those
/// seven bits and a zero top bit.
/// </summary>
internal sealed class JpxTestHtReverseWriter
{
    /// <summary>The bits of a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bits of a nibble.</summary>
    private const int NibbleBits = 4;

    /// <summary>The low nibble of a byte.</summary>
    private const int LowNibble = 0x0F;

    /// <summary>The bytes that hold Scup.</summary>
    private const int SuffixBytes = 2;

    /// <summary>The seven low bits of a byte.</summary>
    private const int SevenBits = 0x7F;

    /// <summary>The largest byte after which no bit is stuffed.</summary>
    private const int StuffLimit = 0x8F;

    /// <summary>The bytes in emission order: the last byte of the segment first.</summary>
    private readonly List<byte> _bytes = [];

    /// <summary>The bits not yet emitted, the next lowest.</summary>
    private ulong _bits;

    /// <summary>The number of bits in <see cref="_bits"/>.</summary>
    private int _used;

    /// <summary>Whether the last byte emitted was above 0x8F.</summary>
    private bool _afterLarge;

    /// <summary>Initializes a new instance of the <see cref="JpxTestHtReverseWriter"/> class.</summary>
    /// <param name="afterLarge">Whether the stream starts as if after a byte above 0x8F.</param>
    private JpxTestHtReverseWriter(bool afterLarge) => _afterLarge = afterLarge;

    /// <summary>Gets the bytes emitted so far.</summary>
    internal int Count => _bytes.Count;

    /// <summary>
    /// Starts a VLC stream: its last byte and the low nibble of the byte before hold Scup, set by
    /// <see cref="FinishVlc"/>; until then they read as ones.
    /// </summary>
    /// <returns>The writer.</returns>
    internal static JpxTestHtReverseWriter ForVlc()
    {
        var writer = new JpxTestHtReverseWriter(false);
        writer._bytes.Add(byte.MaxValue);
        writer._afterLarge = true;
        writer._bits = LowNibble;
        writer._used = NibbleBits;
        return writer;
    }

    /// <summary>Starts a MagRef stream.</summary>
    /// <returns>The writer.</returns>
    internal static JpxTestHtReverseWriter ForMagRef() => new(true);

    /// <summary>Writes bits, least significant first.</summary>
    /// <param name="value">The bits.</param>
    /// <param name="count">The number of bits, at most 32.</param>
    internal void Write(uint value, int count)
    {
        _bits |= (ulong)(value & (uint)((1UL << count) - 1)) << _used;
        _used += count;
        while (_used >= ByteBits)
        {
            EmitByte();
        }
    }

    /// <summary>Flushes the last bits and returns the bytes in segment order.</summary>
    /// <returns>The stream.</returns>
    internal byte[] Finish()
    {
        Flush();
        var bytes = _bytes.ToArray();
        Array.Reverse(bytes);
        return bytes;
    }

    /// <summary>Flushes the last bits, writes Scup into the last byte and a half, and returns the bytes in segment order.</summary>
    /// <param name="suffixLength">Scup: the bytes of MEL and VLC data.</param>
    /// <returns>The stream.</returns>
    internal byte[] FinishVlc(int suffixLength)
    {
        var bytes = Finish();
        bytes[^1] = (byte)(suffixLength >> NibbleBits);
        bytes[^SuffixBytes] = (byte)((bytes[^SuffixBytes] & ~LowNibble) | (suffixLength & LowNibble));
        return bytes;
    }

    /// <summary>Emits any bits left, padded with zeros.</summary>
    internal void Flush()
    {
        if (_used > 0)
        {
            _used = ByteBits;
            EmitByte();
        }

        _used = 0;
        _bits = 0;
    }

    /// <summary>Emits one byte, stuffing it when needed.</summary>
    private void EmitByte()
    {
        var value = (int)(_bits & byte.MaxValue);
        if (_afterLarge && (value & SevenBits) == SevenBits)
        {
            value = SevenBits;
            _bits >>= ByteBits - 1;
            _used -= ByteBits - 1;
        }
        else
        {
            _bits >>= ByteBits;
            _used -= ByteBits;
        }

        _used = Math.Max(_used, 0);
        _bytes.Add((byte)value);
        _afterLarge = value > StuffLimit;
    }
}
