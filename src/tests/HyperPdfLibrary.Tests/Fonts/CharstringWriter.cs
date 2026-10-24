// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Assembles Type 1 and Type 2 charstrings from numbers and operators.</summary>
internal sealed class CharstringWriter
{
    /// <summary>The escape byte of two-byte operators.</summary>
    private const byte Escape = 12;

    /// <summary>The largest number in one byte.</summary>
    private const int SmallLimit = 107;

    /// <summary>The largest number in two bytes.</summary>
    private const int MediumLimit = 1131;

    /// <summary>The bias of one-byte numbers.</summary>
    private const int SmallBias = 139;

    /// <summary>The bias of two-byte numbers.</summary>
    private const int MediumBias = 108;

    /// <summary>The first positive two-byte prefix.</summary>
    private const int PositivePrefix = 247;

    /// <summary>The first negative two-byte prefix.</summary>
    private const int NegativePrefix = 251;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The mask of a byte.</summary>
    private const int ByteMask = 0xFF;

    /// <summary>The Type 2 prefix of a 16-bit number.</summary>
    private const byte ShortIntPrefix = 28;

    /// <summary>The Type 1 prefix of a 32-bit number.</summary>
    private const byte LongIntPrefix = 255;

    /// <summary>The size of a 16-bit number.</summary>
    private const int ShortSize = 2;

    /// <summary>The size of a 32-bit number.</summary>
    private const int LongSize = 4;

    /// <summary>Whether large numbers use the Type 1 form.</summary>
    private readonly bool _type1;

    /// <summary>The bytes written.</summary>
    private readonly List<byte> _bytes = [];

    /// <summary>Initializes a new instance of the <see cref="CharstringWriter"/> class.</summary>
    /// <param name="type1">Whether to write Type 1 charstrings rather than Type 2.</param>
    internal CharstringWriter(bool type1) => _type1 = type1;

    /// <summary>Appends numbers.</summary>
    /// <param name="values">The numbers.</param>
    /// <returns>This writer.</returns>
    internal CharstringWriter Numbers(params ReadOnlySpan<int> values)
    {
        foreach (var value in values)
        {
            Number(value);
        }

        return this;
    }

    /// <summary>Appends a one-byte operator.</summary>
    /// <param name="op">The operator.</param>
    /// <returns>This writer.</returns>
    internal CharstringWriter Op(int op)
    {
        _bytes.Add((byte)op);
        return this;
    }

    /// <summary>Appends a two-byte operator.</summary>
    /// <param name="op">The second byte.</param>
    /// <returns>This writer.</returns>
    internal CharstringWriter EscapeOp(int op)
    {
        _bytes.Add(Escape);
        _bytes.Add((byte)op);
        return this;
    }

    /// <summary>Appends raw bytes, such as a hint mask.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>This writer.</returns>
    internal CharstringWriter Raw(params ReadOnlySpan<byte> bytes)
    {
        _bytes.AddRange(bytes);
        return this;
    }

    /// <summary>Gets the charstring.</summary>
    /// <returns>The bytes.</returns>
    internal byte[] ToArray() => [.. _bytes];

    /// <summary>Appends one number in the shortest form.</summary>
    /// <param name="value">The number.</param>
    private void Number(int value)
    {
        if (Math.Abs(value) <= SmallLimit)
        {
            _bytes.Add((byte)(value + SmallBias));
            return;
        }

        if (Math.Abs(value) <= MediumLimit)
        {
            var magnitude = Math.Abs(value) - MediumBias;
            _bytes.Add((byte)((value > 0 ? PositivePrefix : NegativePrefix) + (magnitude >> ByteBits)));
            _bytes.Add((byte)(magnitude & ByteMask));
            return;
        }

        Span<byte> buffer = stackalloc byte[LongSize];
        if (_type1)
        {
            _bytes.Add(LongIntPrefix);
            BinaryPrimitives.WriteInt32BigEndian(buffer, value);
            _bytes.AddRange(buffer);
            return;
        }

        _bytes.Add(ShortIntPrefix);
        BinaryPrimitives.WriteInt16BigEndian(buffer, (short)value);
        _bytes.AddRange(buffer[..ShortSize]);
    }
}
