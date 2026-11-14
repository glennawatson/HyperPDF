// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace HyperPdf.Head.Linux.Wayland;

/// <summary>Builds Wayland requests in a reused buffer, so sending a request allocates nothing once the buffer has grown.</summary>
internal sealed class WaylandMessageWriter
{
    /// <summary>The Wayland word size in bytes.</summary>
    private const int Word = 4;

    /// <summary>The first buffer size in bytes.</summary>
    private const int InitialCapacity = 256;

    /// <summary>How much larger the buffer grows each time it is full.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The shift of the size in the second head word.</summary>
    private const int SizeShift = 16;

    /// <summary>The buffer.</summary>
    private byte[] _buffer = new byte[InitialCapacity];

    /// <summary>The offset where the current message starts.</summary>
    private int _start;

    /// <summary>The opcode of the current message.</summary>
    private int _opcode;

    /// <summary>Gets the bytes of every finished message since the last <see cref="Clear"/>.</summary>
    internal ReadOnlySpan<byte> Written => _buffer.AsSpan(0, Length);

    /// <summary>Gets the number of bytes written.</summary>
    internal int Length { get; private set; }

    /// <summary>Starts a message.</summary>
    /// <param name="objectId">The object the request is for.</param>
    /// <param name="opcode">The request number.</param>
    internal void Begin(uint objectId, int opcode)
    {
        _start = Length;
        _opcode = opcode;
        WriteWord(objectId);
        WriteWord(0);
    }

    /// <summary>Writes an unsigned argument, which also covers object ids and enums.</summary>
    /// <param name="value">The value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WriteUInt(uint value) => WriteWord(value);

    /// <summary>Writes a signed argument.</summary>
    /// <param name="value">The value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void WriteInt(int value) => WriteWord((uint)value);

    /// <summary>Writes a string argument: its length with the terminator, the UTF-8 bytes, a zero and padding to a whole word.</summary>
    /// <param name="value">The string.</param>
    internal void WriteString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var byteCount = Encoding.UTF8.GetByteCount(value);
        var padded = (byteCount + 1 + (Word - 1)) & ~(Word - 1);
        WriteWord((uint)(byteCount + 1));
        Reserve(padded);
        var target = _buffer.AsSpan(Length, padded);
        target.Clear();
        _ = Encoding.UTF8.GetBytes(value, target);
        Length += padded;
    }

    /// <summary>Finishes the message by writing its size and opcode into the head.</summary>
    internal void End()
    {
        var second = ((uint)(Length - _start) << SizeShift) | (uint)_opcode;
        BinaryPrimitives.WriteUInt32LittleEndian(_buffer.AsSpan(_start + Word, Word), second);
    }

    /// <summary>Forgets everything written, keeping the buffer.</summary>
    internal void Clear()
    {
        Length = 0;
        _start = 0;
    }

    /// <summary>Appends one word in little endian order, the host order on every platform the head runs on.</summary>
    /// <param name="value">The word.</param>
    private void WriteWord(uint value)
    {
        Reserve(Word);
        BinaryPrimitives.WriteUInt32LittleEndian(_buffer.AsSpan(Length, Word), value);
        Length += Word;
    }

    /// <summary>Makes room for more bytes.</summary>
    /// <param name="count">The bytes about to be written.</param>
    private void Reserve(int count)
    {
        if (Length + count > _buffer.Length)
        {
            Array.Resize(ref _buffer, Math.Max(_buffer.Length * GrowthFactor, Length + count));
        }
    }
}
