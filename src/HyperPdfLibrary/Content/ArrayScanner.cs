// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace HyperPdfLibrary.Content;

/// <summary>
/// Finds the end of an array operand by jumping between the few bytes that can change its nesting, instead of reading
/// every number, name and string in it as a token; the array is read again, token by token, only if an operator needs its
/// items. Anything unusual (a missing end, or nesting that closes with <c>&gt;&gt;</c>) is left to the token reader.
/// </summary>
internal static class ArrayScanner
{
    /// <summary>The bytes taken by a backslash and the byte it escapes, or by a double bracket.</summary>
    private const int Pair = 2;

    /// <summary>The bytes that open or close a nesting level, start a string, or start a comment.</summary>
    private static readonly SearchValues<byte> Structural = SearchValues.Create("()<>[]%"u8);

    /// <summary>The bytes that matter inside a literal string.</summary>
    private static readonly SearchValues<byte> StringStructural = SearchValues.Create("()\\"u8);

    /// <summary>The bytes that end a comment.</summary>
    private static readonly SearchValues<byte> LineEnds = SearchValues.Create("\r\n"u8);

    /// <summary>Finds the closing bracket of an array.</summary>
    /// <param name="content">The content.</param>
    /// <param name="start">The offset just after the opening bracket.</param>
    /// <param name="close">Receives the offset of the closing bracket.</param>
    /// <returns><see langword="true"/> when the array ends in a plain closing bracket.</returns>
    internal static bool TryFindEnd(ReadOnlySpan<byte> content, int start, out int close)
    {
        close = 0;
        var depth = 1;
        var i = start;
        while (i < content.Length)
        {
            var step = content[i..].IndexOfAny(Structural);
            if (step < 0)
            {
                return false;
            }

            i += step;
            switch (content[i])
            {
                case (byte)'(':
                {
                    i = SkipString(content, i + 1);
                    break;
                }

                case (byte)'<':
                {
                    i = SkipAngle(content, i, ref depth);
                    break;
                }

                case (byte)'>':
                {
                    // A closing double bracket ends a dictionary, which the token reader handles.
                    return false;
                }

                case (byte)'%':
                {
                    var end = content[i..].IndexOfAny(LineEnds);
                    i = end < 0 ? content.Length : i + end;
                    break;
                }

                case (byte)'[':
                {
                    depth++;
                    i++;
                    break;
                }

                default:
                {
                    depth--;
                    if (depth == 0)
                    {
                        close = i;
                        return true;
                    }

                    i++;
                    break;
                }
            }

            if (i < 0)
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>Skips a literal string, whose parentheses nest and whose backslash escapes the byte after it.</summary>
    /// <param name="content">The content.</param>
    /// <param name="start">The offset just after the opening parenthesis.</param>
    /// <returns>The offset after the closing parenthesis, or -1 when the string does not end.</returns>
    private static int SkipString(ReadOnlySpan<byte> content, int start)
    {
        var level = 1;
        var i = start;
        while (i < content.Length)
        {
            var step = content[i..].IndexOfAny(StringStructural);
            if (step < 0)
            {
                return -1;
            }

            i += step;
            if (content[i] == (byte)'\\')
            {
                i += Pair;
                continue;
            }

            level += content[i] == (byte)'(' ? 1 : -1;
            i++;
            if (level == 0)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Skips a hexadecimal string, or counts the opening of a dictionary.</summary>
    /// <param name="content">The content.</param>
    /// <param name="at">The offset of the <c>&lt;</c>.</param>
    /// <param name="depth">The nesting level, raised by a dictionary.</param>
    /// <returns>The offset after what was skipped, or -1 when a hexadecimal string does not end.</returns>
    private static int SkipAngle(ReadOnlySpan<byte> content, int at, ref int depth)
    {
        if (at + 1 < content.Length && content[at + 1] == (byte)'<')
        {
            depth++;
            return at + Pair;
        }

        var end = content[(at + 1)..].IndexOf((byte)'>');
        return end < 0 ? -1 : at + 1 + end + 1;
    }
}
