// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace HyperPdf.Rendering.Vulkan.Interop;

/// <summary>Writes a Vulkan structure field by field into a buffer, aligning each field the way the C compiler does on 64 bit systems.</summary>
internal ref struct VkWriter
{
    /// <summary>The alignment of a 32 bit field.</summary>
    private const int Align4 = 4;

    /// <summary>The alignment of a 64 bit field, and of a whole structure that holds one.</summary>
    private const int Align8 = 8;

    /// <summary>The buffer being written.</summary>
    private readonly Span<byte> _buffer;

    /// <summary>The offset of the next field.</summary>
    private int _position;

    /// <summary>Initializes a new instance of the <see cref="VkWriter"/> struct.</summary>
    /// <param name="buffer">The buffer, which is cleared first so padding is zero.</param>
    internal VkWriter(Span<byte> buffer)
    {
        buffer.Clear();
        _buffer = buffer;
        _position = 0;
    }

    /// <summary>Gets the size of the structure written so far, padded to the 8 byte alignment structures with 64 bit fields have.</summary>
    internal readonly int Size => (_position + (Align8 - 1)) & ~(Align8 - 1);

    /// <summary>Writes an unsigned 32 bit field.</summary>
    /// <param name="value">The value.</param>
    internal void U32(uint value)
    {
        Align(Align4);
        BinaryPrimitives.WriteUInt32LittleEndian(_buffer[_position..], value);
        _position += Align4;
    }

    /// <summary>Writes a signed 32 bit field.</summary>
    /// <param name="value">The value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void I32(int value) => U32((uint)value);

    /// <summary>Writes a 32 bit float field.</summary>
    /// <param name="value">The value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void F32(float value) => U32(BitConverter.SingleToUInt32Bits(value));

    /// <summary>Writes an unsigned 64 bit field, which also holds handles and pointers.</summary>
    /// <param name="value">The value.</param>
    internal void U64(ulong value)
    {
        Align(Align8);
        BinaryPrimitives.WriteUInt64LittleEndian(_buffer[_position..], value);
        _position += Align8;
    }

    /// <summary>Moves to the next multiple of an alignment.</summary>
    /// <param name="alignment">The alignment.</param>
    private void Align(int alignment) => _position = (_position + (alignment - 1)) & ~(alignment - 1);
}
