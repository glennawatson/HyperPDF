// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Numerics;

namespace HyperPdf.Codecs.Matroska;

/// <summary>Reads the variable length numbers and the typed values of EBML, the binary format Matroska and WebM are written in.</summary>
internal static class Ebml
{
    /// <summary>The most bytes of an element ID.</summary>
    internal const int MaxIdBytes = 4;

    /// <summary>The most bytes of a variable length number.</summary>
    internal const int MaxNumberBytes = 8;

    /// <summary>The size value that says an element's size is not known.</summary>
    internal const long UnknownSize = -1;

    /// <summary>The value <see cref="ReadNumber"/> returns when the bytes do not hold a number.</summary>
    internal const long InvalidNumber = -2;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The shift that puts a byte at the top of a 32 bit number, so its leading zeros can be counted.</summary>
    private const int TopShift = (MaxIdBytes * ByteBits) - ByteBits;

    /// <summary>Reads an element ID, keeping the length marker bits, as the specification writes IDs.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="length">Receives the bytes the ID takes.</param>
    /// <returns>The ID, or 0 when the bytes do not hold one.</returns>
    internal static uint ReadId(ReadOnlySpan<byte> data, out int length)
    {
        length = 0;
        if (data.IsEmpty || data[0] == 0)
        {
            return 0;
        }

        var bytes = BitOperations.LeadingZeroCount((uint)data[0] << TopShift) + 1;
        if (bytes > MaxIdBytes || data.Length < bytes)
        {
            return 0;
        }

        var id = 0U;
        for (var index = 0; index < bytes; index++)
        {
            id = (id << ByteBits) | data[index];
        }

        length = bytes;
        return id;
    }

    /// <summary>Reads a variable length number with its length marker removed, as sizes and track numbers are written.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="length">Receives the bytes the number takes.</param>
    /// <returns>The number, <see cref="UnknownSize"/> when every value bit is 1, or -2 when the bytes do not hold one.</returns>
    internal static long ReadNumber(ReadOnlySpan<byte> data, out int length)
    {
        length = 0;
        if (data.IsEmpty || data[0] == 0)
        {
            return InvalidNumber;
        }

        var bytes = BitOperations.LeadingZeroCount((uint)data[0] << TopShift) + 1;
        if (data.Length < bytes)
        {
            return InvalidNumber;
        }

        var value = (long)(data[0] & ((1 << (ByteBits - bytes)) - 1));
        var allOnes = value == (1 << (ByteBits - bytes)) - 1;
        for (var index = 1; index < bytes; index++)
        {
            value = (value << ByteBits) | data[index];
            allOnes &= data[index] == byte.MaxValue;
        }

        length = bytes;
        return allOnes ? UnknownSize : value;
    }

    /// <summary>Reads the element at a position and moves past it.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="position">The position of the element, which moves to the element after it.</param>
    /// <param name="limit">The end of the bytes that may hold elements.</param>
    /// <param name="element">Receives the element.</param>
    /// <returns><see langword="false"/> when no whole element starts there.</returns>
    internal static bool Next(ReadOnlySpan<byte> data, ref int position, int limit, out EbmlElement element)
    {
        element = default;
        var id = ReadId(data[position..limit], out var idLength);
        if (id == 0)
        {
            return false;
        }

        var size = ReadNumber(data[(position + idLength)..limit], out var sizeLength);
        if (size == InvalidNumber)
        {
            return false;
        }

        var start = position + idLength + sizeLength;
        var end = size == UnknownSize ? limit : start + size;
        if (end > limit)
        {
            return false;
        }

        element = new(id, start, (int)end);
        position = (int)end;
        return true;
    }

    /// <summary>Reads an unsigned integer value.</summary>
    /// <param name="data">The element's content, of 0 to 8 bytes.</param>
    /// <returns>The number.</returns>
    internal static ulong ReadUnsigned(ReadOnlySpan<byte> data)
    {
        var value = 0UL;
        foreach (var next in data[..Math.Min(data.Length, MaxNumberBytes)])
        {
            value = (value << ByteBits) | next;
        }

        return value;
    }

    /// <summary>Reads a floating point value of 4 or 8 bytes.</summary>
    /// <param name="data">The element's content.</param>
    /// <returns>The number, or 0 for any other length.</returns>
    internal static double ReadFloat(ReadOnlySpan<byte> data) => data.Length switch
    {
        sizeof(float) => BinaryPrimitives.ReadSingleBigEndian(data),
        sizeof(double) => BinaryPrimitives.ReadDoubleBigEndian(data),
        _ => 0,
    };

    /// <summary>Reads a string value, which ends at its first zero byte if it has one.</summary>
    /// <param name="data">The element's content.</param>
    /// <returns>The text.</returns>
    internal static string ReadString(ReadOnlySpan<byte> data)
    {
        var end = data.IndexOf((byte)0);
        return System.Text.Encoding.UTF8.GetString(end < 0 ? data : data[..end]);
    }
}
