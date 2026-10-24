// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace HyperPdfLibrary.Syntax;

/// <summary>Unescapes literal strings, hexadecimal strings and names.</summary>
internal static class PdfStringDecoder
{
    /// <summary>The number of digits in an octal escape.</summary>
    private const int OctalDigits = 3;

    /// <summary>The bits in an octal digit.</summary>
    private const int OctalBits = 3;

    /// <summary>The bits in a hexadecimal digit.</summary>
    private const int HexBits = 4;

    /// <summary>The number of hexadecimal digits after a '#' in a name.</summary>
    private const int NameEscapeDigits = 2;

    /// <summary>The length of a carriage return and line feed pair.</summary>
    private const int CarriageReturnLineFeed = 2;

    /// <summary>Determines whether a literal string's raw bytes are already its value.</summary>
    /// <param name="raw">The bytes between the parentheses.</param>
    /// <returns><see langword="true"/> when there are no escapes or carriage returns.</returns>
    internal static bool IsPlainLiteral(ReadOnlySpan<byte> raw) => raw.IndexOfAny((byte)'\\', (byte)'\r') < 0;

    /// <summary>Unescapes a literal string.</summary>
    /// <param name="raw">The bytes between the parentheses.</param>
    /// <param name="destination">The buffer, at least as long as <paramref name="raw"/>.</param>
    /// <returns>The number of bytes written.</returns>
    internal static int DecodeLiteral(ReadOnlySpan<byte> raw, Span<byte> destination)
    {
        var written = 0;
        var index = 0;
        while (index < raw.Length)
        {
            var special = raw[index..].IndexOfAny((byte)'\\', (byte)'\r');
            if (special < 0)
            {
                raw[index..].CopyTo(destination[written..]);
                return written + raw.Length - index;
            }

            raw.Slice(index, special).CopyTo(destination[written..]);
            written += special;
            index += special;
            index = raw[index] == '\r' ? CarriageReturn(raw, index, destination, ref written) : Escape(raw, index + 1, destination, ref written);
        }

        return written;
    }

    /// <summary>Decodes a hexadecimal string, ignoring white space; an odd final digit is followed by zero.</summary>
    /// <param name="raw">The bytes between the angle brackets.</param>
    /// <param name="destination">The buffer, at least half as long as <paramref name="raw"/>, rounded up.</param>
    /// <returns>The number of bytes written.</returns>
    internal static int DecodeHex(ReadOnlySpan<byte> raw, Span<byte> destination) =>
        TryDecodeCleanHex(raw, destination, out var written) ? written : DecodeHexSlowly(raw, destination);

    /// <summary>Decodes a hexadecimal string that is an even run of digits with the vectorised decoder.</summary>
    /// <param name="raw">The bytes between the angle brackets.</param>
    /// <param name="destination">The buffer.</param>
    /// <param name="written">The number of bytes written.</param>
    /// <returns><see langword="false"/> when the string has white space, another byte or an odd final digit.</returns>
    internal static bool TryDecodeCleanHex(ReadOnlySpan<byte> raw, Span<byte> destination, out int written)
    {
        // A clean even run of digits is the usual case for fonts that show text as hex.
        written = 0;
        return (raw.Length & 1) == 0
            && raw.IndexOfAnyExcept(PdfCharacters.HexDigits) < 0
            && Convert.FromHexString(raw, destination, out _, out written) == OperationStatus.Done;
    }

    /// <summary>Decodes a hexadecimal string one digit at a time, ignoring white space.</summary>
    /// <param name="raw">The bytes between the angle brackets.</param>
    /// <param name="destination">The buffer.</param>
    /// <returns>The number of bytes written.</returns>
    internal static int DecodeHexSlowly(ReadOnlySpan<byte> raw, Span<byte> destination)
    {
        var written = 0;
        var high = PdfCharacters.NotHex;
        foreach (var c in raw)
        {
            var digit = PdfCharacters.HexValue(c);
            if (digit < 0)
            {
                continue;
            }

            if (high < 0)
            {
                high = digit;
                continue;
            }

            destination[written] = (byte)((high << HexBits) | digit);
            written++;
            high = PdfCharacters.NotHex;
        }

        if (high >= 0)
        {
            destination[written] = (byte)(high << HexBits);
            written++;
        }

        return written;
    }

    /// <summary>Decodes a name's #xx escapes.</summary>
    /// <param name="raw">The bytes after the slash.</param>
    /// <param name="destination">The buffer, at least as long as <paramref name="raw"/>.</param>
    /// <returns>The number of bytes written.</returns>
    internal static int DecodeName(ReadOnlySpan<byte> raw, Span<byte> destination)
    {
        var written = 0;
        var advance = 1;
        for (var i = 0; i < raw.Length; i += advance)
        {
            var escaped = TryDecodeNameEscape(raw, i, out var value);
            destination[written] = escaped ? value : raw[i];
            written++;
            advance = escaped ? NameEscapeDigits + 1 : 1;
        }

        return written;
    }

    /// <summary>Decodes a #xx escape at a position in a name.</summary>
    /// <param name="raw">The name's bytes.</param>
    /// <param name="index">The position.</param>
    /// <param name="value">The decoded byte.</param>
    /// <returns><see langword="true"/> when the position holds a valid escape.</returns>
    private static bool TryDecodeNameEscape(ReadOnlySpan<byte> raw, int index, out byte value)
    {
        value = 0;
        if (raw[index] != '#' || index + NameEscapeDigits >= raw.Length)
        {
            return false;
        }

        var high = PdfCharacters.HexValue(raw[index + 1]);
        var low = PdfCharacters.HexValue(raw[index + NameEscapeDigits]);
        if (high < 0 || low < 0)
        {
            return false;
        }

        value = (byte)((high << HexBits) | low);
        return true;
    }

    /// <summary>Writes a line end for a carriage return, which with an optional line feed reads as one line feed.</summary>
    /// <param name="raw">The raw string.</param>
    /// <param name="index">The carriage return's offset.</param>
    /// <param name="destination">The output.</param>
    /// <param name="written">The bytes written so far.</param>
    /// <returns>The next offset to read.</returns>
    private static int CarriageReturn(ReadOnlySpan<byte> raw, int index, Span<byte> destination, ref int written)
    {
        destination[written] = (byte)'\n';
        written++;
        return index + 1 < raw.Length && raw[index + 1] == '\n' ? index + CarriageReturnLineFeed : index + 1;
    }

    /// <summary>Decodes the escape after a backslash.</summary>
    /// <param name="raw">The raw string.</param>
    /// <param name="index">The offset after the backslash.</param>
    /// <param name="destination">The output.</param>
    /// <param name="written">The bytes written so far.</param>
    /// <returns>The next offset to read.</returns>
    private static int Escape(ReadOnlySpan<byte> raw, int index, Span<byte> destination, ref int written)
    {
        if (index >= raw.Length)
        {
            return index;
        }

        var c = raw[index];
        if (c is >= (byte)'0' and <= (byte)'7')
        {
            return Octal(raw, index, destination, ref written);
        }

        if (c is (byte)'\r' or (byte)'\n')
        {
            // A backslash before a line end continues the string on the next line.
            return c == '\r' && index + 1 < raw.Length && raw[index + 1] == '\n' ? index + CarriageReturnLineFeed : index + 1;
        }

        destination[written] = c switch
        {
            (byte)'n' => (byte)'\n',
            (byte)'r' => (byte)'\r',
            (byte)'t' => (byte)'\t',
            (byte)'b' => (byte)'\b',
            (byte)'f' => (byte)'\f',
            _ => c,
        };
        written++;
        return index + 1;
    }

    /// <summary>Decodes an octal escape of one to three digits.</summary>
    /// <param name="raw">The raw string.</param>
    /// <param name="index">The first digit's offset.</param>
    /// <param name="destination">The output.</param>
    /// <param name="written">The bytes written so far.</param>
    /// <returns>The next offset to read.</returns>
    private static int Octal(ReadOnlySpan<byte> raw, int index, Span<byte> destination, ref int written)
    {
        var value = 0;
        var end = Math.Min(raw.Length, index + OctalDigits);
        while (index < end && raw[index] is >= (byte)'0' and <= (byte)'7')
        {
            value = (value << OctalBits) | (raw[index] - '0');
            index++;
        }

        destination[written] = (byte)value;
        written++;
        return index;
    }
}
