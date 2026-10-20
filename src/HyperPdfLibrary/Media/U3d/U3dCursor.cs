// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>Reads the little-endian values of a U3D block. A read past the end sets <see cref="Failed"/> and gives zero, so a cut-short block never throws.</summary>
/// <param name="data">The block's bytes.</param>
internal ref struct U3dCursor(ReadOnlySpan<byte> data)
{
    /// <summary>The bytes of a U32 value.</summary>
    private const int U32Length = 4;

    /// <summary>The bytes of a U16 value.</summary>
    private const int U16Length = 2;

    /// <summary>The bytes of a U64 or F64 value.</summary>
    private const int U64Length = 8;

    /// <summary>The block's bytes.</summary>
    private readonly ReadOnlySpan<byte> _data = data;

    /// <summary>The position of the next byte.</summary>
    private int _at;

    /// <summary>Gets a value indicating whether a read went past the end of the block.</summary>
    internal bool Failed { get; private set; }

    /// <summary>Gets the position of the next byte.</summary>
    internal readonly int Position => _at;

    /// <summary>Gets the number of bytes left.</summary>
    internal readonly int Remaining => _data.Length - _at;

    /// <summary>Reads an unsigned 32-bit value.</summary>
    /// <returns>The value, or zero when the block is cut short.</returns>
    internal uint ReadU32() => Take(U32Length) ? BinaryPrimitives.ReadUInt32LittleEndian(_data[(_at - U32Length)..]) : 0;

    /// <summary>Reads a signed 16-bit value.</summary>
    /// <returns>The value, or zero when the block is cut short.</returns>
    internal short ReadI16() => Take(U16Length) ? BinaryPrimitives.ReadInt16LittleEndian(_data[(_at - U16Length)..]) : (short)0;

    /// <summary>Reads an unsigned 64-bit value.</summary>
    /// <returns>The value, or zero when the block is cut short.</returns>
    internal ulong ReadU64() => Take(U64Length) ? BinaryPrimitives.ReadUInt64LittleEndian(_data[(_at - U64Length)..]) : 0;

    /// <summary>Reads a 64-bit floating-point value.</summary>
    /// <returns>The value, or zero when the block is cut short.</returns>
    internal double ReadF64() => Take(U64Length) ? BinaryPrimitives.ReadDoubleLittleEndian(_data[(_at - U64Length)..]) : 0;

    /// <summary>Reads a string: a 16-bit byte count, then that many bytes of UTF-8.</summary>
    /// <returns>The string, or an empty string when the block is cut short.</returns>
    internal string ReadString()
    {
        if (!Take(U16Length))
        {
            return string.Empty;
        }

        var length = BinaryPrimitives.ReadUInt16LittleEndian(_data[(_at - U16Length)..]);
        return Take(length) ? Encoding.UTF8.GetString(_data.Slice(_at - length, length)) : string.Empty;
    }

    /// <summary>Moves past bytes.</summary>
    /// <param name="count">The number of bytes.</param>
    /// <returns><see langword="true"/> when they were all there.</returns>
    internal bool Skip(long count) => count <= int.MaxValue && Take((int)count);

    /// <summary>Moves on to the next multiple of four bytes from the start of the block.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Align() => Skip((U32Length - (_at % U32Length)) % U32Length);

    /// <summary>Takes bytes if they are all there.</summary>
    /// <param name="count">The number of bytes.</param>
    /// <returns><see langword="true"/> when the bytes were there.</returns>
    private bool Take(int count)
    {
        if (Failed || count < 0 || count > Remaining)
        {
            Failed = true;
            return false;
        }

        _at += count;
        return true;
    }
}
