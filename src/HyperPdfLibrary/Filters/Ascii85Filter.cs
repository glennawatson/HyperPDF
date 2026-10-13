// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Filters;

/// <summary>Decodes ASCII85Decode data.</summary>
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

    /// <summary>
    /// Decodes base-85 groups up to the first character that is not a digit, 'z' or white space (normally the '~&gt;'
    /// end marker, ISO 32000-1 section 7.4.3). A leading '&lt;~' is skipped.
    /// </summary>
    /// <param name="input">The encoded data.</param>
    /// <param name="output">The buffer receiving the decoded bytes.</param>
    internal static void Decode(ReadOnlySpan<byte> input, ref PooledBuffer output)
    {
        input = SkipPrefix(input);
        var value = 0U;
        var count = 0;
        foreach (var c in input)
        {
            if (c == ZeroGroup)
            {
                // A 'z' is a complete group, so it drops any partial group before it.
                output.GetSpan(GroupBytes)[..GroupBytes].Clear();
                output.Advance(GroupBytes);
                value = 0;
                count = 0;
                continue;
            }

            if (c is >= FirstDigit and <= LastDigit)
            {
                value = unchecked((value * Base) + (uint)(c - FirstDigit));
                count++;
                if (count == GroupLength)
                {
                    WriteGroup(value, GroupBytes, ref output);
                    value = 0;
                    count = 0;
                }

                continue;
            }

            if (!PdfCharacters.IsWhitespace(c))
            {
                break;
            }
        }

        WritePartial(value, count, ref output);
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
