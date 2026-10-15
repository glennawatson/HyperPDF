// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Writing;

/// <summary>Writes the bit-packed integer rows in linearization hint tables into a pooled buffer. Dispose returns the buffer.</summary>
internal ref struct PdfHintBitWriter : IDisposable
{
    /// <summary>The bits in one byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The longest hint field.</summary>
    private const int MaxBits = 32;

    /// <summary>The encoded bytes.</summary>
    private PooledBuffer _bytes;

    /// <summary>The number of bits written.</summary>
    private int _bitCount;

    /// <summary>Gets the number of complete output bytes.</summary>
    internal readonly int ByteCount => _bytes.Length;

    /// <summary>Gets the encoded table.</summary>
    internal readonly ReadOnlySpan<byte> WrittenSpan => _bytes.WrittenSpan;

    /// <summary>Returns the buffer to the pool.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _bytes.Dispose();

    /// <summary>Writes the most significant bits of an unsigned value first.</summary>
    /// <param name="value">The value.</param>
    /// <param name="width">The number of bits to write.</param>
    internal void Write(uint value, int width)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(width, MaxBits);
        for (var bit = width - 1; bit >= 0; bit--)
        {
            var index = _bitCount / ByteBits;
            if (index == _bytes.Length)
            {
                _bytes.WriteByte(0);
            }

            if (((value >> bit) & 1) != 0)
            {
                _bytes.Array![index] |= (byte)(1 << (ByteBits - 1 - (_bitCount % ByteBits)));
            }

            _bitCount++;
        }
    }

    /// <summary>Ends a packed row at the next byte boundary.</summary>
    internal void Align()
    {
        while ((_bitCount % ByteBits) != 0)
        {
            Write(0, 1);
        }
    }
}
