// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Repairs a packet that is not well-formed the way the XMP toolkit veraPDF uses does before its second read: bytes that
/// are not UTF-8 are read as Windows-1252, and ASCII control characters, literal or as character references, become
/// spaces. A packet that reads after this repair is well-formed XMP to veraPDF.
/// </summary>
internal static class PdfAXmpPacketRepair
{
    /// <summary>The name veraPDF reports for UTF-8.</summary>
    internal const string Utf8 = "UTF-8";

    /// <summary>The name of UTF-16 big-endian.</summary>
    private const string Utf16BigEndian = "UTF-16BE";

    /// <summary>The name of UTF-16 little-endian.</summary>
    private const string Utf16LittleEndian = "UTF-16LE";

    /// <summary>The name of UTF-16 with a byte order mark.</summary>
    private const string Utf16 = "UTF-16";

    /// <summary>The name of UTF-32 big-endian.</summary>
    private const string Utf32BigEndian = "UTF-32BE";

    /// <summary>The name of UTF-32 little-endian.</summary>
    private const string Utf32LittleEndian = "UTF-32LE";

    /// <summary>The name of UTF-32 with a byte order mark.</summary>
    private const string Utf32 = "UTF-32";

    /// <summary>The fewest bytes from which a four-byte pattern is read.</summary>
    private const int PatternLength = 4;

    /// <summary>The length of a UTF-16 byte order mark, and the fewest bytes the guess reads.</summary>
    private const int MarkLength = 2;

    /// <summary>The first byte above ASCII.</summary>
    private const byte FirstHigh = 0x80;

    /// <summary>The first lead byte of a multi-byte UTF-8 sequence, as the toolkit tests it.</summary>
    private const byte FirstLead = 0xC0;

    /// <summary>The mask of a UTF-8 continuation byte's top bits.</summary>
    private const byte ContinuationMask = 0xC0;

    /// <summary>The top bits of a UTF-8 continuation byte.</summary>
    private const byte ContinuationBits = 0x80;

    /// <summary>The first byte of the UTF-8 byte order mark.</summary>
    private const byte Utf8MarkStart = 0xEF;

    /// <summary>The first byte of the big-endian UTF-16 byte order mark.</summary>
    private const byte BigEndianMarkStart = 0xFE;

    /// <summary>The second byte of the big-endian UTF-16 byte order mark.</summary>
    private const byte BigEndianMarkEnd = 0xFF;

    /// <summary>The ASCII delete control character.</summary>
    private const char Delete = '\u007F';

    /// <summary>The last C0 control character.</summary>
    private const char LastControl = '\u001F';

    /// <summary>The most decimal digits a character reference may have before the repair stops reading it.</summary>
    private const int MaxDecimalDigits = 5;

    /// <summary>The most hexadecimal digits a character reference may have before the repair stops reading it.</summary>
    private const int MaxHexDigits = 4;

    /// <summary>The radix of hexadecimal digits.</summary>
    private const int HexRadix = 16;

    /// <summary>The radix of decimal digits.</summary>
    private const int DecimalRadix = 10;

    /// <summary>The Windows-1252 characters for bytes 0x80 to 0x9F; the undefined bytes are spaces, as the toolkit writes them.</summary>
    private const string Windows1252High = "€ ‚ƒ„…†‡ˆ‰Š‹Œ Ž  ‘’“”•–—˜™š›œ žŸ";

    /// <summary>The hexadecimal digits in order of value.</summary>
    private const string HexDigits = "0123456789abcdef";

    /// <summary>The first byte the Windows-1252 table covers.</summary>
    private const int Windows1252Start = 0x80;

    /// <summary>The first byte after the Windows-1252 table.</summary>
    private const int Windows1252End = 0xA0;

    /// <summary>Gets the encoding the toolkit's byte buffer guesses from a packet's first bytes.</summary>
    /// <param name="bytes">The packet.</param>
    /// <returns>The encoding name.</returns>
    internal static string GuessEncoding(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < MarkLength)
        {
            return Utf8;
        }

        return bytes[0] switch
        {
            0 => GuessFromNul(bytes),
            < FirstHigh => GuessFromAscii(bytes),
            Utf8MarkStart => Utf8,
            BigEndianMarkStart => Utf16,
            _ => bytes.Length < PatternLength || bytes[2] != 0 ? Utf16 : Utf32,
        };
    }

    /// <summary>Repairs a packet into text for a second read.</summary>
    /// <param name="bytes">The packet.</param>
    /// <param name="encoding">The encoding the toolkit guesses.</param>
    /// <returns>The repaired text.</returns>
    internal static string Repair(byte[] bytes, string encoding)
    {
        var text = string.Equals(encoding, Utf8, StringComparison.Ordinal)
            ? Encoding.UTF8.GetString(ReadMixedUtf8(bytes))
            : Decode(bytes, encoding);
        return ReplaceControls(text);
    }

    /// <summary>Re-encodes bytes that are not valid UTF-8 sequences as UTF-8 of their Windows-1252 characters.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>UTF-8 bytes.</returns>
    internal static byte[] ReadMixedUtf8(ReadOnlySpan<byte> bytes)
    {
        var output = new List<byte>(bytes.Length);
        var index = 0;
        while (index < bytes.Length)
        {
            var current = bytes[index];
            if (current < FirstHigh)
            {
                output.Add(current);
                index++;
            }
            else if (current >= FirstLead && SequenceLength(bytes, index) is var length and > 0)
            {
                output.AddRange(bytes.Slice(index, length));
                index += length;
            }
            else
            {
                AppendWindows1252(output, current);
                index++;
            }
        }

        return [.. output];
    }

    /// <summary>Replaces ASCII control characters, and character references to them, with spaces.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text with the controls replaced.</returns>
    internal static string ReplaceControls(string text)
    {
        var builder = new StringBuilder(text.Length);
        var index = 0;
        while (index < text.Length)
        {
            var character = text[index];
            if (character == '&' && ControlReferenceLength(text, index) is var length and > 0)
            {
                _ = builder.Append(' ');
                index += length;
                continue;
            }

            _ = builder.Append(IsControl(character) ? ' ' : character);
            index++;
        }

        return builder.ToString();
    }

    /// <summary>Guesses the encoding of a packet starting with a NUL byte.</summary>
    /// <param name="bytes">The packet, at least two bytes long.</param>
    /// <returns>The encoding name.</returns>
    private static string GuessFromNul(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < PatternLength || bytes[1] != 0)
        {
            return Utf16BigEndian;
        }

        return bytes[2] == BigEndianMarkStart && bytes[3] == BigEndianMarkEnd ? Utf32BigEndian : Utf32;
    }

    /// <summary>Guesses the encoding of a packet starting with an ASCII byte.</summary>
    /// <param name="bytes">The packet, at least two bytes long.</param>
    /// <returns>The encoding name.</returns>
    private static string GuessFromAscii(ReadOnlySpan<byte> bytes)
    {
        if (bytes[1] != 0)
        {
            return Utf8;
        }

        return bytes.Length < PatternLength || bytes[2] != 0 ? Utf16LittleEndian : Utf32LittleEndian;
    }

    /// <summary>Gets the length of the well-formed UTF-8 sequence a lead byte starts, as the toolkit checks one.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="start">The lead byte's index.</param>
    /// <returns>The sequence length, or 0 when the continuation bytes are missing.</returns>
    private static int SequenceLength(ReadOnlySpan<byte> bytes, int start)
    {
        var expected = LeadingOnes(bytes[start]) - 1;
        if (start + expected >= bytes.Length)
        {
            return 0;
        }

        for (var i = 1; i <= expected; i++)
        {
            if ((bytes[start + i] & ContinuationMask) != ContinuationBits)
            {
                return 0;
            }
        }

        return expected + 1;
    }

    /// <summary>Counts the leading one bits of a byte.</summary>
    /// <param name="value">The byte.</param>
    /// <returns>The count.</returns>
    private static int LeadingOnes(byte value)
    {
        var count = 0;
        for (var mask = (int)FirstHigh; mask != 0 && (value & mask) != 0; mask >>= 1)
        {
            count++;
        }

        return count;
    }

    /// <summary>Appends a byte's Windows-1252 character as UTF-8.</summary>
    /// <param name="output">The output.</param>
    /// <param name="value">The byte, 0x80 or above.</param>
    private static void AppendWindows1252(List<byte> output, byte value)
    {
        var character = value is >= Windows1252Start and < Windows1252End ? Windows1252High[value - Windows1252Start] : (char)value;
        Span<byte> encoded = stackalloc byte[PatternLength];
        var written = Encoding.UTF8.GetBytes([character], encoded);
        output.AddRange(encoded[..written]);
    }

    /// <summary>Decodes a packet that is not UTF-8 in the encoding the toolkit guessed.</summary>
    /// <param name="bytes">The packet.</param>
    /// <param name="encoding">The encoding name.</param>
    /// <returns>The text.</returns>
    private static string Decode(byte[] bytes, string encoding) => encoding switch
    {
        Utf16BigEndian => Encoding.BigEndianUnicode.GetString(bytes),
        Utf16LittleEndian => Encoding.Unicode.GetString(bytes),
        Utf32LittleEndian => Encoding.UTF32.GetString(bytes),
        Utf32BigEndian or Utf32 => new UTF32Encoding(true, false).GetString(bytes),
        _ => DecodeUtf16WithMark(bytes),
    };

    /// <summary>Decodes UTF-16 that may start with a byte order mark; without one it is big-endian.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The text.</returns>
    private static string DecodeUtf16WithMark(byte[] bytes)
    {
        if (bytes is [BigEndianMarkEnd, BigEndianMarkStart, ..])
        {
            return Encoding.Unicode.GetString(bytes, MarkLength, bytes.Length - MarkLength);
        }

        return bytes is [BigEndianMarkStart, BigEndianMarkEnd, ..]
            ? Encoding.BigEndianUnicode.GetString(bytes, MarkLength, bytes.Length - MarkLength)
            : Encoding.BigEndianUnicode.GetString(bytes);
    }

    /// <summary>Gets the length of a character reference to a control character at an ampersand.</summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The ampersand's index.</param>
    /// <returns>The reference's length, or 0 when the text there is not such a reference.</returns>
    private static int ControlReferenceLength(string text, int start)
    {
        var index = start + 1;
        if (index >= text.Length || text[index] != '#')
        {
            return 0;
        }

        index++;
        var isHex = index < text.Length && text[index] == 'x';
        if (isHex)
        {
            index++;
        }

        if (!TryReadReference(text, ref index, isHex, out var value))
        {
            return 0;
        }

        return index < text.Length && text[index] == ';' && IsControl((char)value) ? index + 1 - start : 0;
    }

    /// <summary>Reads the digits of a character reference, as many as the toolkit's repair reads.</summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The read position, after <c>&amp;#</c> or <c>&amp;#x</c>.</param>
    /// <param name="isHex">Whether the digits are hexadecimal.</param>
    /// <param name="value">The value read.</param>
    /// <returns><see langword="false"/> when there are too many digits, or no decimal digit.</returns>
    private static bool TryReadReference(string text, ref int index, bool isHex, out int value)
    {
        value = 0;
        var digits = 0;
        var radix = isHex ? HexRadix : DecimalRadix;
        var maxDigits = isHex ? MaxHexDigits : MaxDecimalDigits;
        while (index < text.Length && DigitValue(text[index], isHex) is var digit and >= 0)
        {
            value = (value * radix) + digit;
            digits++;
            index++;
            if (digits > maxDigits)
            {
                return false;
            }
        }

        // A hexadecimal reference may have no digits, which the repair reads as zero.
        return isHex || digits > 0;
    }

    /// <summary>Gets a digit's value.</summary>
    /// <param name="character">The character.</param>
    /// <param name="isHex">Whether hexadecimal digits are read.</param>
    /// <returns>The value, or -1 when the character is not a digit.</returns>
    private static int DigitValue(char character, bool isHex)
    {
        if (char.IsAsciiDigit(character))
        {
            return character - '0';
        }

        return isHex ? HexDigits.IndexOf(char.ToLowerInvariant(character), StringComparison.Ordinal) : -1;
    }

    /// <summary>Determines whether a character is an ASCII control character other than tab, line feed and carriage return.</summary>
    /// <param name="character">The character.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsControl(char character) => (character <= LastControl || character == Delete) && character is not ('\t' or '\n' or '\r');
}
