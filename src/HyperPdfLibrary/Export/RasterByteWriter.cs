// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;

namespace HyperPdfLibrary.Export;

/// <summary>Writes numbers and bytes one after another into a buffer whose size the caller worked out, in either byte order.</summary>
/// <param name="buffer">The buffer to fill.</param>
[DebuggerDisplay("RasterByteWriter: {Position} of {Buffer.Length}")]
internal sealed class RasterByteWriter(byte[] buffer)
{
    /// <summary>Gets the buffer being filled.</summary>
    internal byte[] Buffer { get; } = buffer;

    /// <summary>Gets how many bytes have been written.</summary>
    internal int Position { get; private set; }

    /// <summary>Writes one byte.</summary>
    /// <param name="value">The byte.</param>
    internal void WriteByte(byte value)
    {
        Buffer[Position] = value;
        Position++;
    }

    /// <summary>Writes a 16-bit number, least significant byte first.</summary>
    /// <param name="value">The number.</param>
    internal void WriteUInt16LittleEndian(ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(Buffer.AsSpan(Position), value);
        Position += sizeof(ushort);
    }

    /// <summary>Writes a 32-bit number, least significant byte first.</summary>
    /// <param name="value">The number.</param>
    internal void WriteUInt32LittleEndian(uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(Buffer.AsSpan(Position), value);
        Position += sizeof(uint);
    }

    /// <summary>Writes a 16-bit number, most significant byte first.</summary>
    /// <param name="value">The number.</param>
    internal void WriteUInt16BigEndian(ushort value)
    {
        BinaryPrimitives.WriteUInt16BigEndian(Buffer.AsSpan(Position), value);
        Position += sizeof(ushort);
    }

    /// <summary>Writes a 32-bit number, most significant byte first.</summary>
    /// <param name="value">The number.</param>
    internal void WriteUInt32BigEndian(uint value)
    {
        BinaryPrimitives.WriteUInt32BigEndian(Buffer.AsSpan(Position), value);
        Position += sizeof(uint);
    }

    /// <summary>Writes a run of bytes.</summary>
    /// <param name="bytes">The bytes.</param>
    internal void WriteBytes(ReadOnlySpan<byte> bytes)
    {
        bytes.CopyTo(Buffer.AsSpan(Position));
        Position += bytes.Length;
    }

    /// <summary>Leaves a run of zero bytes.</summary>
    /// <param name="count">How many.</param>
    internal void Skip(int count) => Position += count;
}
