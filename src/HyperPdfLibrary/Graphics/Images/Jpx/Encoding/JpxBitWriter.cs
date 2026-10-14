// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace HyperPdfLibrary.Graphics.Images.Jpx.Encoding;

/// <summary>
/// Writes the bits of a packet header (ISO 15444-1 B.10.1): most significant bit first, with a zero stuffed bit at the top of
/// the byte after any 0xFF byte, so a header never holds a marker. The bytes go into a pooled buffer that is reused for
/// every packet.
/// </summary>
internal sealed class JpxBitWriter : IDisposable
{
    /// <summary>The bits of a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bits of a byte after 0xFF.</summary>
    private const int StuffedBits = 7;

    /// <summary>The value of a byte that is all ones.</summary>
    private const int AllOnes = 0xFF;

    /// <summary>The smallest buffer rented.</summary>
    private const int MinimumCapacity = 256;

    /// <summary>The factor the buffer grows by.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The bytes written.</summary>
    private byte[] _bytes = ArrayPool<byte>.Shared.Rent(MinimumCapacity);

    /// <summary>The number of bytes written.</summary>
    private int _length;

    /// <summary>The byte being filled.</summary>
    private int _current;

    /// <summary>The free bits of the byte being filled.</summary>
    private int _free = ByteBits;

    /// <summary>The bits the byte being filled holds.</summary>
    private int _capacity = ByteBits;

    /// <inheritdoc/>
    public void Dispose()
    {
        ArrayPool<byte>.Shared.Return(_bytes);
        _bytes = [];
    }

    /// <summary>Starts a new header.</summary>
    internal void Reset()
    {
        _length = 0;
        _current = 0;
        _free = ByteBits;
        _capacity = ByteBits;
    }

    /// <summary>Writes one bit.</summary>
    /// <param name="bit">The bit, 0 or 1.</param>
    internal void WriteBit(int bit)
    {
        _free--;
        _current |= (bit & 1) << _free;
        if (_free == 0)
        {
            Emit();
        }
    }

    /// <summary>Writes bits, most significant first.</summary>
    /// <param name="value">The value.</param>
    /// <param name="count">The number of bits.</param>
    internal void WriteBits(int value, int count)
    {
        for (var i = count - 1; i >= 0; i--)
        {
            WriteBit((value >> i) & 1);
        }
    }

    /// <summary>Pads to a byte boundary and adds a zero byte after a final 0xFF.</summary>
    /// <returns>The header bytes, valid until the next <see cref="Reset"/>.</returns>
    internal ReadOnlySpan<byte> Finish()
    {
        if (_free != _capacity)
        {
            Emit();
        }

        if (_length > 0 && _bytes[_length - 1] == AllOnes)
        {
            Store(0);
        }

        return _bytes.AsSpan(0, _length);
    }

    /// <summary>Moves the byte being filled to the output.</summary>
    private void Emit()
    {
        Store((byte)_current);
        _capacity = _current == AllOnes ? StuffedBits : ByteBits;
        _free = _capacity;
        _current = 0;
    }

    /// <summary>Appends a byte, growing the buffer when it is full.</summary>
    /// <param name="value">The byte.</param>
    private void Store(byte value)
    {
        if (_length == _bytes.Length)
        {
            var larger = ArrayPool<byte>.Shared.Rent(_bytes.Length * GrowthFactor);
            _bytes.AsSpan(0, _length).CopyTo(larger);
            ArrayPool<byte>.Shared.Return(_bytes);
            _bytes = larger;
        }

        _bytes[_length] = value;
        _length++;
    }
}
