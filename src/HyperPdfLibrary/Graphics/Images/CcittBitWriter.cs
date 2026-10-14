// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images;

/// <summary>Writes CCITT codes, most significant bit first, into a pooled buffer it owns.</summary>
internal ref struct CcittBitWriter
{
    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bytes written.</summary>
    private PooledBuffer _output;

    /// <summary>The bits not yet written, right-aligned.</summary>
    private uint _pending;

    /// <summary>Gets the bits written since the last byte boundary: the position within the current byte.</summary>
    internal int PendingBits { get; private set; }

    /// <summary>Gets the whole bytes written.</summary>
    internal readonly ReadOnlySpan<byte> WrittenSpan => _output.WrittenSpan;

    /// <summary>Returns the buffer to the pool.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Dispose() => _output.Dispose();

    /// <summary>Writes a code.</summary>
    /// <param name="code">The code.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Write(CcittCode code) => Write(code.Value, code.Length);

    /// <summary>Writes the low bits of a value.</summary>
    /// <param name="value">The bits, right-aligned.</param>
    /// <param name="length">The number of bits, at most 16.</param>
    internal void Write(int value, int length)
    {
        _pending = (_pending << length) | (uint)value;
        PendingBits += length;
        while (PendingBits >= ByteBits)
        {
            PendingBits -= ByteBits;
            _output.WriteByte((byte)(_pending >> PendingBits));
        }

        _pending &= (1U << PendingBits) - 1;
    }

    /// <summary>Writes zero bits up to the next byte boundary; nothing when already on one.</summary>
    internal void AlignToByte()
    {
        if (PendingBits != 0)
        {
            Write(0, ByteBits - PendingBits);
        }
    }
}
