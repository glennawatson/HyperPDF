// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace HyperPdf.Core.Signatures.Signing;

/// <summary>Decodes PDF text strings: literal <c>(...)</c> with escapes, or hex <c>&lt;...&gt;</c>, in UTF-16BE or PDFDocEncoding.</summary>
internal static class PdfText
{
    /// <summary>The bits in a hex digit.</summary>
    private const int HexBits = 4;

    /// <summary>The value of the hex digit a.</summary>
    private const int HexLetterBase = 10;

    /// <summary>The characters or bytes of a value decoded on the stack.</summary>
    private const int StackChars = 256;

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

        char[]? rented = null;
        var text = value.Length <= StackChars ? stackalloc char[StackChars] : (rented = ArrayPool<char>.Shared.Rent(value.Length));
        try
        {
            return new(text[..Decode(value, text)]);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Decodes a string value into characters, without making a string.</summary>
    /// <param name="value">The value, starting with <c>(</c> or <c>&lt;</c>.</param>
    /// <param name="destination">The characters, at least as many as the value has bytes.</param>
    /// <returns>The characters written; zero for anything but a string value.</returns>
    internal static int Decode(ReadOnlySpan<byte> value, Span<char> destination)
    {
        if (value.IsEmpty)
        {
            return 0;
        }

        byte[]? rented = null;
        var bytes = value.Length <= StackChars ? stackalloc byte[StackChars] : (rented = ArrayPool<byte>.Shared.Rent(value.Length));
        try
        {
            var count = value[0] switch
            {
                (byte)'(' => Unescape(value[1..^1], bytes),
                (byte)'<' => FromHex(value[1..^1], bytes),
                _ => 0,
            };
            var raw = bytes[..count];
            return raw.StartsWith(Utf16Mark) ? Encoding.BigEndianUnicode.GetChars(raw[Utf16Mark.Length..], destination) : Encoding.Latin1.GetChars(raw, destination);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Decodes hex digits, ignoring white space; an odd final digit is followed by 0.</summary>
    /// <param name="hex">The digits.</param>
    /// <param name="destination">The bytes, at least as many as the digits.</param>
    /// <returns>The bytes written.</returns>
    /// <exception cref="FormatException">A digit is not hexadecimal.</exception>
    private static int FromHex(ReadOnlySpan<byte> hex, Span<byte> destination)
    {
        var written = 0;
        var high = -1;
        foreach (var b in hex)
        {
            if (PdfSyntax.IsSpace(b))
            {
                continue;
            }

            var digit = HexDigit(b);
            if (high < 0)
            {
                high = digit;
                continue;
            }

            destination[written] = (byte)((high << HexBits) | digit);
            written++;
            high = -1;
        }

        if (high >= 0)
        {
            destination[written] = (byte)(high << HexBits);
            written++;
        }

        return written;
    }

    /// <summary>Gets the value of a hexadecimal digit.</summary>
    /// <param name="digit">The digit's character code.</param>
    /// <returns>The value, 0 to 15.</returns>
    /// <exception cref="FormatException">The character is not a hexadecimal digit.</exception>
    private static int HexDigit(byte digit) => digit switch
    {
        >= (byte)'0' and <= (byte)'9' => digit - '0',
        >= (byte)'a' and <= (byte)'f' => digit - 'a' + HexLetterBase,
        >= (byte)'A' and <= (byte)'F' => digit - 'A' + HexLetterBase,
        _ => throw new FormatException("The string has a character that is not a hexadecimal digit."),
    };

    /// <summary>Undoes literal string escapes.</summary>
    /// <param name="literal">The bytes between the parentheses.</param>
    /// <param name="destination">The bytes, at least as many as the literal.</param>
    /// <returns>The bytes written.</returns>
    private static int Unescape(ReadOnlySpan<byte> literal, Span<byte> destination)
    {
        var written = 0;
        for (var i = 0; i < literal.Length; i++)
        {
            if (literal[i] != (byte)'\\' || i + 1 >= literal.Length)
            {
                destination[written] = literal[i];
                written++;
                continue;
            }

            i++;
            destination[written] = IsOctal(literal[i]) ? ReadOctal(literal, ref i) : Escaped(literal[i]);
            written++;
        }

        return written;
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
