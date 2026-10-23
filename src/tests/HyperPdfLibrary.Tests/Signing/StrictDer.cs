// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Signing;

/// <summary>
/// A small DER parser that does not trust the framework: it checks tags, minimal lengths, nesting, minimal INTEGERs and
/// the order of SET OF elements (ITU-T X.690, clauses 8.1, 8.3 and 11.6), and that nothing follows the value.
/// </summary>
internal static class StrictDer
{
    /// <summary>The bit that marks a constructed value.</summary>
    private const byte ConstructedBit = 0x20;

    /// <summary>The low tag bits that announce a multi-byte tag number.</summary>
    private const byte HighTagNumber = 0x1F;

    /// <summary>The flag that marks the long length form.</summary>
    private const int LongForm = 0x80;

    /// <summary>The mask for the number of length bytes in the long form.</summary>
    private const int LengthByteCount = 0x7F;

    /// <summary>The largest length the short form holds.</summary>
    private const int MaxShortLength = 127;

    /// <summary>The tag of an INTEGER.</summary>
    private const byte IntegerTag = 0x02;

    /// <summary>The tag of a SET.</summary>
    private const byte SetTag = 0x31;

    /// <summary>The most nesting levels accepted.</summary>
    private const int MaxDepth = 64;

    /// <summary>The most length bytes accepted, as the lengths here fit an int.</summary>
    private const int MaxLengthBytes = 4;

    /// <summary>The bits in a byte.</summary>
    private const int BitsPerByte = 8;

    /// <summary>The smallest INTEGER content length that has a second byte to compare.</summary>
    private const int TwoBytes = 2;

    /// <summary>The padding byte of a negative INTEGER.</summary>
    private const byte AllOnes = 0xFF;

    /// <summary>Checks that the bytes are exactly one DER value.</summary>
    /// <param name="der">The bytes.</param>
    /// <returns><see langword="null"/> when the encoding is valid, or what is wrong with it.</returns>
    internal static string? Check(ReadOnlySpan<byte> der)
    {
        var problem = CheckValue(der, 0, out var consumed);
        if (problem is not null)
        {
            return problem;
        }

        return consumed == der.Length ? null : $"{der.Length - consumed} bytes follow the value";
    }

    /// <summary>Checks one value and the values inside it.</summary>
    /// <param name="bytes">The bytes starting at the value.</param>
    /// <param name="depth">The nesting level.</param>
    /// <param name="consumed">Receives the bytes the value takes.</param>
    /// <returns><see langword="null"/> when the value is valid, or what is wrong with it.</returns>
    private static string? CheckValue(ReadOnlySpan<byte> bytes, int depth, out int consumed)
    {
        consumed = 0;
        if (depth > MaxDepth)
        {
            return "nesting is too deep";
        }

        if (bytes.Length < TwoBytes)
        {
            return "truncated header";
        }

        var tag = bytes[0];
        if ((tag & HighTagNumber) == HighTagNumber)
        {
            return "multi-byte tag";
        }

        var problem = ReadLength(bytes[1..], out var length, out var lengthBytes);
        if (problem is not null)
        {
            return problem;
        }

        var start = 1 + lengthBytes;
        if (length > bytes.Length - start)
        {
            return $"length {length} runs past the end";
        }

        consumed = start + length;
        var contents = bytes.Slice(start, length);
        return (tag & ConstructedBit) != 0 ? CheckChildren(tag, contents, depth) : CheckPrimitive(tag, contents);
    }

    /// <summary>Reads a length and checks it is the shortest form.</summary>
    /// <param name="bytes">The bytes starting at the length.</param>
    /// <param name="length">Receives the length.</param>
    /// <param name="lengthBytes">Receives the bytes the length takes.</param>
    /// <returns><see langword="null"/> when the length is valid, or what is wrong with it.</returns>
    private static string? ReadLength(ReadOnlySpan<byte> bytes, out int length, out int lengthBytes)
    {
        length = 0;
        lengthBytes = 1;
        int first = bytes[0];
        if (first < LongForm)
        {
            length = first;
            return null;
        }

        var count = first & LengthByteCount;
        if (count == 0 || count > MaxLengthBytes)
        {
            return "indefinite or oversize length";
        }

        if (bytes.Length <= count)
        {
            return "truncated length";
        }

        if (bytes[1] == 0)
        {
            return "length has a leading zero byte";
        }

        for (var i = 1; i <= count; i++)
        {
            length = (length << BitsPerByte) | bytes[i];
        }

        lengthBytes = 1 + count;
        return length > MaxShortLength ? null : "long length form for a short length";
    }

    /// <summary>Checks the values inside a constructed value, which must fill it exactly.</summary>
    /// <param name="tag">The tag of the constructed value.</param>
    /// <param name="contents">The contents.</param>
    /// <param name="depth">The nesting level of the constructed value.</param>
    /// <returns><see langword="null"/> when the contents are valid, or what is wrong with them.</returns>
    private static string? CheckChildren(byte tag, ReadOnlySpan<byte> contents, int depth)
    {
        var at = 0;
        var previousStart = -1;
        var previousLength = 0;
        while (at < contents.Length)
        {
            var problem = CheckValue(contents[at..], depth + 1, out var consumed);
            if (problem is not null)
            {
                return problem;
            }

            if (tag == SetTag && previousStart >= 0 && contents.Slice(previousStart, previousLength).SequenceCompareTo(contents.Slice(at, consumed)) > 0)
            {
                return "SET elements are out of order";
            }

            previousStart = at;
            previousLength = consumed;
            at += consumed;
        }

        return null;
    }

    /// <summary>Checks a primitive value; only INTEGER has a rule that can be checked without knowing the schema.</summary>
    /// <param name="tag">The tag.</param>
    /// <param name="contents">The contents.</param>
    /// <returns><see langword="null"/> when the contents are valid, or what is wrong with them.</returns>
    private static string? CheckPrimitive(byte tag, ReadOnlySpan<byte> contents)
    {
        if (tag != IntegerTag)
        {
            return null;
        }

        if (contents.IsEmpty)
        {
            return "empty INTEGER";
        }

        if (contents.Length < TwoBytes)
        {
            return null;
        }

        var redundantZero = contents[0] == 0 && contents[1] < LongForm;
        var redundantOnes = contents[0] == AllOnes && contents[1] >= LongForm;
        return redundantZero || redundantOnes ? "INTEGER has a redundant leading byte" : null;
    }
}
