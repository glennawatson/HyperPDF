// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;

namespace HyperPdf.Ocr.Tests;

/// <summary>Builds the little endian bytes a recognition model is stored as.</summary>
internal sealed class ModelBytes
{
    /// <summary>The bytes so far.</summary>
    private readonly List<byte> _bytes = [];

    /// <summary>Adds a byte.</summary>
    /// <param name="value">The byte.</param>
    /// <returns>This builder.</returns>
    internal ModelBytes Byte(byte value)
    {
        _bytes.Add(value);
        return this;
    }

    /// <summary>Adds a signed byte.</summary>
    /// <param name="value">The value.</param>
    /// <returns>This builder.</returns>
    internal ModelBytes SByte(sbyte value) => Byte((byte)value);

    /// <summary>Adds a 32 bit integer.</summary>
    /// <param name="value">The value.</param>
    /// <returns>This builder.</returns>
    internal ModelBytes Int(int value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        _bytes.AddRange(buffer);
        return this;
    }

    /// <summary>Adds a 32 bit unsigned integer.</summary>
    /// <param name="value">The value.</param>
    /// <returns>This builder.</returns>
    internal ModelBytes UInt(uint value) => Int((int)value);

    /// <summary>Adds a 64 bit integer.</summary>
    /// <param name="value">The value.</param>
    /// <returns>This builder.</returns>
    internal ModelBytes Long(long value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
        _bytes.AddRange(buffer);
        return this;
    }

    /// <summary>Adds a 32 bit float.</summary>
    /// <param name="value">The value.</param>
    /// <returns>This builder.</returns>
    internal ModelBytes Float(float value) => Int(BitConverter.SingleToInt32Bits(value));

    /// <summary>Adds a 64 bit float.</summary>
    /// <param name="value">The value.</param>
    /// <returns>This builder.</returns>
    internal ModelBytes Double(double value) => Long(BitConverter.DoubleToInt64Bits(value));

    /// <summary>Adds a string as a 32 bit length and UTF-8 bytes.</summary>
    /// <param name="value">The text.</param>
    /// <returns>This builder.</returns>
    internal ModelBytes Text(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        _ = UInt((uint)bytes.Length);
        _bytes.AddRange(bytes);
        return this;
    }

    /// <summary>Adds raw bytes.</summary>
    /// <param name="value">The bytes.</param>
    /// <returns>This builder.</returns>
    internal ModelBytes Raw(byte[] value)
    {
        _bytes.AddRange(value);
        return this;
    }

    /// <summary>Gets the bytes built.</summary>
    /// <returns>The bytes.</returns>
    internal byte[] ToArray() => [.. _bytes];
}
