// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Text;
using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Signatures.Signing;

/// <summary>Just enough PDF syntax to find values in dictionaries and arrays, working on the raw bytes.</summary>
internal static class PdfSyntax
{
    /// <summary>The PDF comment marker.</summary>
    private const byte Percent = (byte)'%';

    /// <summary>The literal string opener.</summary>
    private const byte OpenParen = (byte)'(';

    /// <summary>The literal string closer.</summary>
    private const byte CloseParen = (byte)')';

    /// <summary>The escape character in literal strings.</summary>
    private const byte Backslash = (byte)'\\';

    /// <summary>The dictionary and hex string opener.</summary>
    private const byte Less = (byte)'<';

    /// <summary>The dictionary and hex string closer.</summary>
    private const byte Greater = (byte)'>';

    /// <summary>The array opener.</summary>
    private const byte OpenBracket = (byte)'[';

    /// <summary>The array closer.</summary>
    private const byte CloseBracket = (byte)']';

    /// <summary>The name marker.</summary>
    private const byte Slash = (byte)'/';

    /// <summary>The length of the dictionary markers <c>&lt;&lt;</c> and <c>&gt;&gt;</c>, and of an escape sequence.</summary>
    private const int PairLength = 2;

    /// <summary>PDF white space.</summary>
    private static readonly SearchValues<byte> Spaces = SearchValues.Create(" \n\r\t\f\0"u8);

    /// <summary>PDF white space and delimiters, which end names, numbers and keywords.</summary>
    private static readonly SearchValues<byte> Delimiters = SearchValues.Create(" \n\r\t\f\0()<>[]{}/%"u8);

    /// <summary>Determines whether a byte is PDF white space.</summary>
    /// <param name="value">The byte.</param>
    /// <returns><see langword="true"/> for white space.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsSpace(byte value) => Spaces.Contains(value);

    /// <summary>Determines whether a byte ends a name, number or keyword.</summary>
    /// <param name="value">The byte.</param>
    /// <returns><see langword="true"/> for white space and delimiters.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsDelimiter(byte value) => Delimiters.Contains(value);

    /// <summary>Skips white space and comments.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="index">Where to start.</param>
    /// <returns>The next significant byte's index.</returns>
    internal static int SkipSpace(ReadOnlySpan<byte> data, int index)
    {
        while (index < data.Length)
        {
            if (IsSpace(data[index]))
            {
                index++;
            }
            else if (data[index] == Percent)
            {
                while (index < data.Length && data[index] is not ((byte)'\n' or (byte)'\r'))
                {
                    index++;
                }
            }
            else
            {
                break;
            }
        }

        return index;
    }

    /// <summary>Finds the end of the value starting at an index: a dictionary, array, string, name, number, reference or keyword.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="index">The value's first byte.</param>
    /// <returns>The index just after the value.</returns>
    internal static int ValueEnd(ReadOnlySpan<byte> data, int index)
    {
        if (index >= data.Length)
        {
            return index;
        }

        return data[index] switch
        {
            Less when index + 1 < data.Length && data[index + 1] == Less => ContainerEnd(data, index + PairLength, true),
            Less => data.Slice(index).IndexOf(Greater) is var close and >= 0 ? index + close + 1 : data.Length,
            OpenBracket => ContainerEnd(data, index + 1, false),
            OpenParen => LiteralEnd(data, index + 1),
            Slash => TokenEnd(data, index + 1),
            _ => NumberOrReferenceEnd(data, index),
        };
    }

    /// <summary>Finds a key's value in a dictionary.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="dictionary">The index of the dictionary's opening <c>&lt;&lt;</c>.</param>
    /// <param name="key">The key without its slash.</param>
    /// <returns>The value's first byte, or -1 when the key is absent.</returns>
    internal static int FindKey(ReadOnlySpan<byte> data, int dictionary, ReadOnlySpan<byte> key)
    {
        var index = SkipSpace(data, dictionary + PairLength);
        while (index < data.Length && data[index] == Slash)
        {
            var nameEnd = TokenEnd(data, index + 1);
            var value = SkipSpace(data, nameEnd);
            if (data[(index + 1)..nameEnd].SequenceEqual(key))
            {
                return value;
            }

            index = SkipSpace(data, ValueEnd(data, value));
        }

        return -1;
    }

    /// <summary>Reads a non-negative integer.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="index">The first digit.</param>
    /// <param name="value">The number.</param>
    /// <returns>The index after the number, or -1 when there is none.</returns>
    internal static int ReadLong(ReadOnlySpan<byte> data, int index, out long value)
    {
        value = 0;
        var consumed = 0;
        var parsed = index < data.Length && Utf8Parser.TryParse(data[index..], out value, out consumed) && consumed > 0;
        return parsed ? index + consumed : -1;
    }

    /// <summary>Reads an indirect reference <c>N G R</c>.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="index">The first digit.</param>
    /// <param name="number">The object number.</param>
    /// <returns><see langword="true"/> when the value is a reference.</returns>
    internal static bool TryReadReference(ReadOnlySpan<byte> data, int index, out int number)
    {
        number = 0;
        var end = ReadLong(data, index, out var objectNumber);
        if (end < 0)
        {
            return false;
        }

        end = ReadLong(data, SkipSpace(data, end), out _);
        if (end < 0)
        {
            return false;
        }

        end = SkipSpace(data, end);
        if (end >= data.Length || data[end] != (byte)'R')
        {
            return false;
        }

        number = (int)objectNumber;
        return true;
    }

    /// <summary>Finds the end of a name or keyword.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="index">The first byte after any slash.</param>
    /// <returns>The index after the token.</returns>
    internal static int TokenEnd(ReadOnlySpan<byte> data, int index)
    {
        while (index < data.Length && !IsDelimiter(data[index]))
        {
            index++;
        }

        return index;
    }

    /// <summary>Finds the end of a dictionary or array, skipping nested values.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="index">The first byte inside.</param>
    /// <param name="dictionary">Whether it is a dictionary (closed by <c>&gt;&gt;</c>) rather than an array.</param>
    /// <returns>The index after the closer.</returns>
    private static int ContainerEnd(ReadOnlySpan<byte> data, int index, bool dictionary)
    {
        while (true)
        {
            index = SkipSpace(data, index);
            if (index >= data.Length)
            {
                return data.Length;
            }

            if (dictionary && data[index] == Greater && index + 1 < data.Length && data[index + 1] == Greater)
            {
                return index + PairLength;
            }

            if (!dictionary && data[index] == CloseBracket)
            {
                return index + 1;
            }

            var end = ValueEnd(data, index);
            index = end > index ? end : index + 1;
        }
    }

    /// <summary>Finds the end of a literal string, honouring escapes and nested parentheses.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="index">The first byte inside.</param>
    /// <returns>The index after the closing parenthesis.</returns>
    private static int LiteralEnd(ReadOnlySpan<byte> data, int index)
    {
        var depth = 1;
        while (index < data.Length)
        {
            var value = data[index];
            if (value == Backslash)
            {
                index += PairLength;
                continue;
            }

            depth += value switch
            {
                OpenParen => 1,
                CloseParen => -1,
                _ => 0,
            };
            index++;
            if (depth == 0)
            {
                return index;
            }
        }

        return index;
    }

    /// <summary>Finds the end of a number, a reference <c>N G R</c> or a keyword.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="index">The first byte.</param>
    /// <returns>The index after the value.</returns>
    private static int NumberOrReferenceEnd(ReadOnlySpan<byte> data, int index)
    {
        var end = TokenEnd(data, index);
        if (end == index)
        {
            return index + 1;
        }

        var second = SkipSpace(data, end);
        var secondEnd = TokenEnd(data, second);
        if (secondEnd == second || !IsDigits(data[second..secondEnd]) || !IsDigits(data[index..end]))
        {
            return end;
        }

        var third = SkipSpace(data, secondEnd);
        return third < data.Length && data[third] == (byte)'R' && (third + 1 >= data.Length || IsDelimiter(data[third + 1])) ? third + 1 : end;
    }

    /// <summary>Determines whether bytes are all digits.</summary>
    /// <param name="data">The bytes.</param>
    /// <returns><see langword="true"/> when all are digits.</returns>
    private static bool IsDigits(ReadOnlySpan<byte> data) => data.IndexOfAnyExceptInRange((byte)'0', (byte)'9') < 0;
}
