// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Filters;

/// <summary>Decodes and encodes ASCII85Decode data.</summary>
internal static class Ascii85Filter
{
    /// <summary>The characters in a group.</summary>
    private const int GroupLength = 5;

    /// <summary>The bytes a group decodes to.</summary>
    private const int GroupBytes = 4;

    /// <summary>The base of the encoding.</summary>
    private const uint Base = 85;

    /// <summary>The first digit character.</summary>
    private const byte FirstDigit = (byte)'!';

    /// <summary>The last digit character.</summary>
    private const byte LastDigit = (byte)'u';

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The fewest characters in a partial final group that give a byte.</summary>
    private const int MinPartialLength = 2;

    /// <summary>The character that stands for four zero bytes.</summary>
    private const byte ZeroGroup = (byte)'z';

    /// <summary>The characters written on a line before a line feed.</summary>
    private const int LineLength = 75;

    /// <summary>
    /// Encodes bytes as base-85 (ISO 32000-2 7.4.3): five characters for every four bytes, 'z' for a group of four zero
    /// bytes, and n + 1 characters for a final partial group of n bytes. Lines are 75 characters, and the data ends with '~&gt;'.
    /// </summary>
    /// <param name="input">The bytes.</param>
    /// <param name="output">The buffer receiving the text.</param>
    internal static void Encode(ReadOnlySpan<byte> input, ref PooledBuffer output)
    {
        var column = 0;
        var index = 0;
        for (; index + GroupBytes <= input.Length; index += GroupBytes)
        {
            var value = BinaryPrimitives.ReadUInt32BigEndian(input[index..]);
            var span = output.GetSpan(GroupLength + 1);
            var written = value == 0 ? WriteZeroGroup(span) : WriteDigits(value, GroupLength, span);
            column += written;
            if (column >= LineLength)
            {
                span[written] = (byte)'\n';
                written++;
                column = 0;
            }

            output.Advance(written);
        }

        WritePartialGroup(input[index..], ref output);
        output.Write("~>"u8);
    }

    /// <summary>
    /// Decodes base-85 groups up to the first character that is not a digit, 'z' or white space (normally the '~&gt;'
    /// end marker, ISO 32000-1 section 7.4.3). A leading '&lt;~' is skipped.
    /// </summary>
    /// <param name="input">The encoded data.</param>
    /// <param name="output">The buffer receiving the decoded bytes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Decode(ReadOnlySpan<byte> input, ref PooledBuffer output) => _ = TryDecode(input, ref output);

    /// <summary>
    /// Decodes like <see cref="Decode"/> and says whether the data followed ISO 32000-2 7.4.3: only digits, 'z' and white
    /// space, a '~' only as the start of the '~&gt;' end marker, no 'z' inside a group, no group above 2^32 - 1 and no final
    /// group of one character. Damaged data keeps whatever decoded, so a reader still shows it.
    /// </summary>
    /// <param name="input">The encoded data.</param>
    /// <param name="output">The buffer receiving the decoded bytes.</param>
    /// <returns><see langword="false"/> when the data broke one of those rules.</returns>
    internal static bool TryDecode(ReadOnlySpan<byte> input, ref PooledBuffer output)
    {
        input = SkipPrefix(input);
        var value = 0UL;
        var count = 0;
        var clean = true;
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (c == ZeroGroup)
            {
                // A 'z' is a complete group, so it drops any partial group before it.
                clean &= count == 0;
                output.GetSpan(GroupBytes)[..GroupBytes].Clear();
                output.Advance(GroupBytes);
                value = 0;
                count = 0;
            }
            else if (c is >= FirstDigit and <= LastDigit)
            {
                value = (value * Base) + (uint)(c - FirstDigit);
                count++;
                if (count == GroupLength)
                {
                    clean &= value <= uint.MaxValue;
                    WriteGroup(unchecked((uint)value), GroupBytes, ref output);
                    value = 0;
                    count = 0;
                }
            }
            else if (!PdfCharacters.IsWhitespace(c))
            {
                clean &= IsEndMarker(input, i);
                break;
            }
        }

        WritePartial(unchecked((uint)value), count, ref output);
        return clean && count != 1;
    }

    /// <summary>Determines whether the character at a position starts the '~&gt;' end marker. A '~' at the very end counts: the data was cut after it.</summary>
    /// <param name="input">The encoded data.</param>
    /// <param name="index">The position of a character that is not a digit, 'z' or white space.</param>
    /// <returns><see langword="true"/> for a '~' that is last or is followed by '&gt;'.</returns>
    private static bool IsEndMarker(ReadOnlySpan<byte> input, int index) =>
        input[index] == (byte)'~' && (index + 1 >= input.Length || input[index + 1] == (byte)'>');

    /// <summary>Writes the 'z' that stands for four zero bytes.</summary>
    /// <param name="destination">The space to write to.</param>
    /// <returns>The characters written.</returns>
    private static int WriteZeroGroup(Span<byte> destination)
    {
        destination[0] = ZeroGroup;
        return 1;
    }

    /// <summary>Writes the leading base-85 digits of a group value, most significant first.</summary>
    /// <param name="value">The group value.</param>
    /// <param name="count">The digits to write, from 1 to 5.</param>
    /// <param name="destination">The space to write to.</param>
    /// <returns>The characters written.</returns>
    private static int WriteDigits(uint value, int count, Span<byte> destination)
    {
        Span<byte> digits = stackalloc byte[GroupLength];
        for (var i = GroupLength - 1; i >= 0; i--)
        {
            digits[i] = (byte)((value % Base) + FirstDigit);
            value /= Base;
        }

        digits[..count].CopyTo(destination);
        return count;
    }

    /// <summary>Writes a final group of one to three bytes: padded with zeros, never a 'z', and cut to n + 1 characters.</summary>
    /// <param name="tail">The remaining bytes, fewer than four.</param>
    /// <param name="output">The output.</param>
    private static void WritePartialGroup(ReadOnlySpan<byte> tail, ref PooledBuffer output)
    {
        if (tail.IsEmpty)
        {
            return;
        }

        Span<byte> padded = stackalloc byte[GroupBytes];
        padded.Clear();
        tail.CopyTo(padded);
        var written = WriteDigits(BinaryPrimitives.ReadUInt32BigEndian(padded), tail.Length + 1, output.GetSpan(GroupLength));
        output.Advance(written);
    }

    /// <summary>Skips white space and an optional '&lt;~' prefix.</summary>
    /// <param name="input">The encoded data.</param>
    /// <returns>The data after the prefix.</returns>
    private static ReadOnlySpan<byte> SkipPrefix(ReadOnlySpan<byte> input)
    {
        var start = 0;
        while (start < input.Length && PdfCharacters.IsWhitespace(input[start]))
        {
            start++;
        }

        var rest = input[start..];
        return rest.StartsWith("<~"u8) ? rest["<~"u8.Length..] : rest;
    }

    /// <summary>Writes a final partial group, padded with the highest digit; it gives one byte fewer than its characters.</summary>
    /// <param name="value">The value so far.</param>
    /// <param name="count">The characters in the group.</param>
    /// <param name="output">The output.</param>
    private static void WritePartial(uint value, int count, ref PooledBuffer output)
    {
        if (count < MinPartialLength)
        {
            return;
        }

        for (var i = count; i < GroupLength; i++)
        {
            value = unchecked((value * Base) + (LastDigit - FirstDigit));
        }

        WriteGroup(value, count - 1, ref output);
    }

    /// <summary>Writes the leading bytes of a group's value, most significant first.</summary>
    /// <param name="value">The group value.</param>
    /// <param name="count">The bytes to write.</param>
    /// <param name="output">The output.</param>
    private static void WriteGroup(uint value, int count, ref PooledBuffer output)
    {
        var destination = output.GetSpan(GroupBytes);
        for (var i = 0; i < count; i++)
        {
            destination[i] = (byte)(value >> ((GroupBytes - 1 - i) * ByteBits));
        }

        output.Advance(count);
    }
}
