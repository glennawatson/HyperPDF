// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace HyperPdf.Core.Signatures.Signing;

/// <summary>Decodes PDF text strings: literal <c>(...)</c> with escapes, or hex <c>&lt;...&gt;</c>, in UTF-16BE or PDFDocEncoding.</summary>
internal static class PdfText
{
    /// <summary>The hex digits in a byte.</summary>
    private const int HexPair = 2;

    /// <summary>The bits in an octal digit.</summary>
    private const int OctalBits = 3;

    /// <summary>The most digits in an octal escape.</summary>
    private const int OctalDigits = 3;

    /// <summary>Gets the UTF-16 big endian byte order mark.</summary>
    private static ReadOnlySpan<byte> Utf16Mark => [0xFE, 0xFF];

    /// <summary>Decodes a string value.</summary>
    /// <param name="value">The value, starting with <c>(</c> or <c>&lt;</c>.</param>
    /// <returns>The text, or an empty string for anything else.</returns>
    internal static string Decode(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
        {
            return string.Empty;
        }

        var bytes = value[0] switch
        {
            (byte)'(' => Unescape(value[1..^1]),
            (byte)'<' => FromHex(value[1..^1]),
            _ => [],
        };
        return bytes.AsSpan().StartsWith(Utf16Mark) ? Encoding.BigEndianUnicode.GetString(bytes, Utf16Mark.Length, bytes.Length - Utf16Mark.Length) : Encoding.Latin1.GetString(bytes);
    }

    /// <summary>Decodes hex digits, ignoring white space; an odd final digit is followed by 0.</summary>
    /// <param name="hex">The digits.</param>
    /// <returns>The bytes.</returns>
    private static byte[] FromHex(ReadOnlySpan<byte> hex)
    {
        var digits = new StringBuilder(hex.Length);
        foreach (var b in hex)
        {
            if (!PdfSyntax.IsSpace(b))
            {
                _ = digits.Append((char)b);
            }
        }

        if (digits.Length % HexPair != 0)
        {
            _ = digits.Append('0');
        }

        return Convert.FromHexString(digits.ToString());
    }

    /// <summary>Undoes literal string escapes.</summary>
    /// <param name="literal">The bytes between the parentheses.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Unescape(ReadOnlySpan<byte> literal)
    {
        var output = new List<byte>(literal.Length);
        for (var i = 0; i < literal.Length; i++)
        {
            if (literal[i] != (byte)'\\' || i + 1 >= literal.Length)
            {
                output.Add(literal[i]);
                continue;
            }

            i++;
            if (IsOctal(literal[i]))
            {
                output.Add(ReadOctal(literal, ref i));
                continue;
            }

            output.Add(Escaped(literal[i]));
        }

        return [.. output];
    }

    /// <summary>Reads up to three octal digits, leaving the index on the last one.</summary>
    /// <param name="literal">The bytes.</param>
    /// <param name="index">The first digit; moved to the last.</param>
    /// <returns>The byte.</returns>
    private static byte ReadOctal(ReadOnlySpan<byte> literal, ref int index)
    {
        var code = 0;
        var digits = 0;
        for (; digits < OctalDigits && index < literal.Length && IsOctal(literal[index]); digits++, index++)
        {
            code = (code << OctalBits) | (literal[index] - '0');
        }

        index--;
        return (byte)code;
    }

    /// <summary>Determines whether a byte is an octal digit.</summary>
    /// <param name="value">The byte.</param>
    /// <returns><see langword="true"/> for 0 to 7.</returns>
    private static bool IsOctal(byte value) => value is >= (byte)'0' and <= (byte)'7';

    /// <summary>Gets the byte a one letter escape stands for.</summary>
    /// <param name="escaped">The letter after the backslash.</param>
    /// <returns>The byte.</returns>
    private static byte Escaped(byte escaped) => escaped switch
    {
        (byte)'n' => (byte)'\n',
        (byte)'r' => (byte)'\r',
        (byte)'t' => (byte)'\t',
        (byte)'b' => (byte)'\b',
        (byte)'f' => (byte)'\f',
        _ => escaped,
    };
}
