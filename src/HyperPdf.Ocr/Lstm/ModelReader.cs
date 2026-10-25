// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace HyperPdf.Ocr.Lstm;

/// <summary>Reads the little endian values a text recognition model is stored as.</summary>
/// <param name="data">The bytes of one model component.</param>
[DebuggerDisplay("ModelReader: {Remaining} bytes left")]
public ref struct ModelReader(ReadOnlySpan<byte> data)
{
    /// <summary>The bytes left to read.</summary>
    private ReadOnlySpan<byte> _rest = data;

    /// <summary>Gets the number of bytes left to read.</summary>
    public readonly int Remaining => _rest.Length;

    /// <summary>Reads one signed byte.</summary>
    /// <returns>The value.</returns>
    /// <exception cref="InvalidDataException">The data ends first.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte ReadSByte() => (sbyte)Take(sizeof(sbyte))[0];

    /// <summary>Reads one unsigned byte.</summary>
    /// <returns>The value.</returns>
    /// <exception cref="InvalidDataException">The data ends first.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte ReadByte() => Take(sizeof(byte))[0];

    /// <summary>Reads a 32 bit signed integer.</summary>
    /// <returns>The value.</returns>
    /// <exception cref="InvalidDataException">The data ends first.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadInt32() => BinaryPrimitives.ReadInt32LittleEndian(Take(sizeof(int)));

    /// <summary>Reads a 64 bit signed integer.</summary>
    /// <returns>The value.</returns>
    /// <exception cref="InvalidDataException">The data ends first.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadInt64() => BinaryPrimitives.ReadInt64LittleEndian(Take(sizeof(long)));

    /// <summary>Reads a 32 bit unsigned integer.</summary>
    /// <returns>The value.</returns>
    /// <exception cref="InvalidDataException">The data ends first.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadUInt32() => BinaryPrimitives.ReadUInt32LittleEndian(Take(sizeof(uint)));

    /// <summary>Reads a 32 bit float.</summary>
    /// <returns>The value.</returns>
    /// <exception cref="InvalidDataException">The data ends first.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ReadSingle() => BinaryPrimitives.ReadSingleLittleEndian(Take(sizeof(float)));

    /// <summary>Reads a 64 bit float.</summary>
    /// <returns>The value.</returns>
    /// <exception cref="InvalidDataException">The data ends first.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double ReadDouble() => BinaryPrimitives.ReadDoubleLittleEndian(Take(sizeof(double)));

    /// <summary>Reads a string stored as a 32 bit length and that many UTF-8 bytes.</summary>
    /// <returns>The text.</returns>
    /// <exception cref="InvalidDataException">The data ends first.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ReadString() => Encoding.UTF8.GetString(Take(CheckedLength(ReadUInt32(), sizeof(byte))));

    /// <summary>Reads a run of bytes without copying them.</summary>
    /// <param name="count">The number of bytes.</param>
    /// <returns>The bytes.</returns>
    /// <exception cref="InvalidDataException">The data ends first.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> ReadBytes(int count) => Take(count);

    /// <summary>Reads 32 bit floats into a destination.</summary>
    /// <param name="destination">Receives the values.</param>
    /// <exception cref="InvalidDataException">The data ends first.</exception>
    public void ReadSingles(Span<float> destination)
    {
        var bytes = Take(destination.Length * sizeof(float));
        if (BitConverter.IsLittleEndian)
        {
            bytes.CopyTo(MemoryMarshal.AsBytes(destination));
            return;
        }

        for (var i = 0; i < destination.Length; i++)
        {
            destination[i] = BinaryPrimitives.ReadSingleLittleEndian(bytes[(i * sizeof(float))..]);
        }
    }

    /// <summary>Reads 64 bit floats into a destination of 32 bit floats.</summary>
    /// <param name="destination">Receives the values, narrowed.</param>
    /// <exception cref="InvalidDataException">The data ends first.</exception>
    public void ReadDoublesAsSingles(Span<float> destination)
    {
        var bytes = Take(destination.Length * sizeof(double));
        for (var i = 0; i < destination.Length; i++)
        {
            destination[i] = (float)BinaryPrimitives.ReadDoubleLittleEndian(bytes[(i * sizeof(double))..]);
        }
    }

    /// <summary>Checks that a stored count of elements fits in what is left.</summary>
    /// <param name="count">The stored count.</param>
    /// <param name="elementSize">The size of one element in bytes.</param>
    /// <returns>The number of bytes the elements take.</returns>
    /// <exception cref="InvalidDataException">The count runs past the end of the data.</exception>
    public readonly int CheckedLength(uint count, int elementSize)
    {
        var bytes = (long)count * elementSize;
        if (bytes > _rest.Length)
        {
            throw new InvalidDataException("The recognition model is truncated.");
        }

        return (int)bytes;
    }

    /// <summary>Takes the next bytes.</summary>
    /// <param name="count">The number of bytes.</param>
    /// <returns>The bytes.</returns>
    /// <exception cref="InvalidDataException">The data ends first.</exception>
    private ReadOnlySpan<byte> Take(int count)
    {
        if ((uint)count > (uint)_rest.Length)
        {
            throw new InvalidDataException("The recognition model is truncated.");
        }

        var taken = _rest[..count];
        _rest = _rest[count..];
        return taken;
    }
}
