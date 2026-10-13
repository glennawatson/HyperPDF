// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace HyperPdfLibrary.Objects;

/// <summary>Decodes and encodes PDF text strings: PDFDocEncoding, UTF-16 with a byte order mark, or UTF-8 with one.</summary>
public static class PdfText
{
    /// <summary>The first PDFDocEncoding byte below 0x80 that differs from Latin-1.</summary>
    private const byte LowSpecialFirst = 0x18;

    /// <summary>The last PDFDocEncoding byte below 0x80 that differs from Latin-1.</summary>
    private const byte LowSpecialLast = 0x1F;

    /// <summary>The first PDFDocEncoding byte above 0x7F that differs from Latin-1.</summary>
    private const byte HighSpecialFirst = 0x80;

    /// <summary>The last PDFDocEncoding byte above 0x7F that differs from Latin-1.</summary>
    private const byte HighSpecialLast = 0xA0;

    /// <summary>The first printable ASCII character, which PDFDocEncoding keeps.</summary>
    private const char AsciiFirst = ' ';

    /// <summary>The last printable ASCII character, which PDFDocEncoding keeps.</summary>
    private const char AsciiLast = '~';

    /// <summary>The first character above the euro sign that PDFDocEncoding keeps from Latin-1.</summary>
    private const char LatinFirst = '¡';

    /// <summary>The last Latin-1 character.</summary>
    private const char LatinLast = 'ÿ';

    /// <summary>The soft hyphen, which PDFDocEncoding leaves undefined.</summary>
    private const char SoftHyphen = '­';

    /// <summary>The undefined code at 0x9F, which has a placeholder in the high table.</summary>
    private const char UndefinedHigh = '\u009F';

    /// <summary>The size of a UTF-16 code unit.</summary>
    private const int Utf16Size = 2;

    /// <summary>The most UTF-8 bytes one UTF-16 code unit takes.</summary>
    private const int Utf8BytesPerUnit = 3;

    /// <summary>The characters of PDFDocEncoding bytes 0x18 to 0x1F.</summary>
    private const string LowSpecials = "˘ˇˆ˙˝˛˚˜";

    /// <summary>The characters of PDFDocEncoding bytes 0x80 to 0xA0; 0x9F is undefined and kept as is.</summary>
    private const string HighSpecials =
        "•†‡…—–ƒ⁄‹›−‰„“”‘"
        + "’‚™ﬁﬂŁŒŠŸŽıłœšž\u009F€";

    /// <summary>Gets the UTF-16 big-endian byte order mark.</summary>
    private static ReadOnlySpan<byte> Utf16BigEndianMark => [0xFE, 0xFF];

    /// <summary>Gets the UTF-16 little-endian byte order mark, which some writers use.</summary>
    private static ReadOnlySpan<byte> Utf16LittleEndianMark => [0xFF, 0xFE];

    /// <summary>Gets the UTF-8 byte order mark.</summary>
    private static ReadOnlySpan<byte> Utf8Mark => [0xEF, 0xBB, 0xBF];

    /// <summary>Decodes a text string.</summary>
    /// <param name="bytes">The string's bytes.</param>
    /// <returns>The text.</returns>
    public static string Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(Utf16BigEndianMark))
        {
            return Encoding.BigEndianUnicode.GetString(Utf16Body(bytes));
        }

        if (bytes.StartsWith(Utf16LittleEndianMark))
        {
            return Encoding.Unicode.GetString(Utf16Body(bytes));
        }

        if (bytes.StartsWith(Utf8Mark))
        {
            return Encoding.UTF8.GetString(bytes[Utf8Mark.Length..]);
        }

        // Most strings are plain text, where PDFDocEncoding equals Latin-1.
        return bytes.IndexOfAnyInRange(LowSpecialFirst, LowSpecialLast) < 0 && bytes.IndexOfAnyInRange(HighSpecialFirst, HighSpecialLast) < 0
            ? Encoding.Latin1.GetString(bytes)
            : string.Create(bytes.Length, bytes, static (chars, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                chars[i] = DecodeDocByte(source[i]);
            }
        });
    }

    /// <summary>Decodes one PDFDocEncoding byte.</summary>
    /// <param name="value">The byte.</param>
    /// <returns>The character.</returns>
    public static char DecodeDocByte(byte value) => value switch
    {
        >= LowSpecialFirst and <= LowSpecialLast => LowSpecials[value - LowSpecialFirst],
        >= HighSpecialFirst and <= HighSpecialLast => HighSpecials[value - HighSpecialFirst],
        _ => (char)value,
    };

    /// <summary>Determines whether a header version allows UTF-8 text strings, which PDF 2.0 added.</summary>
    /// <param name="version">The header version, for example "1.7" or "2.0".</param>
    /// <returns><see langword="true"/> for version 2.0 or higher.</returns>
    public static bool SupportsUtf8(string? version) =>
        !string.IsNullOrEmpty(version) && version[0] > '1' && version[0] <= '9';

    /// <summary>Encodes text for a document that predates PDF 2.0: PDFDocEncoding when every character fits, UTF-16 big-endian with a byte order mark otherwise.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The string's bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] Encode(string text) => Encode(text, false);

    /// <summary>Encodes text for a document with the given header version.</summary>
    /// <param name="text">The text.</param>
    /// <param name="version">The header version, for example "1.7" or "2.0".</param>
    /// <returns>The string's bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] Encode(string text, string? version) => Encode(text, SupportsUtf8(version));

    /// <summary>Encodes text as PDFDocEncoding when every character fits, as UTF-8 with a byte order mark when allowed, and as UTF-16 big-endian with a byte order mark otherwise.</summary>
    /// <param name="text">The text.</param>
    /// <param name="allowUtf8">Whether the document is PDF 2.0 or higher and may hold UTF-8 text strings.</param>
    /// <returns>The string's bytes.</returns>
    public static byte[] Encode(string text, bool allowUtf8)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Encode(text.AsSpan(), allowUtf8);
    }

    /// <summary>Encodes characters as PDFDocEncoding when every character fits, as UTF-8 with a byte order mark when allowed, and as UTF-16 big-endian with a byte order mark otherwise.</summary>
    /// <param name="text">The characters.</param>
    /// <param name="allowUtf8">Whether the document is PDF 2.0 or higher and may hold UTF-8 text strings.</param>
    /// <returns>The string's bytes.</returns>
    public static byte[] Encode(ReadOnlySpan<char> text, bool allowUtf8)
    {
        var bytes = new byte[text.Length];
        if (TryEncodeDoc(text, bytes))
        {
            return bytes;
        }

        return allowUtf8 ? EncodeUtf8(text) : EncodeUtf16(text);
    }

    /// <summary>Encodes replacement text the way the string it replaces was encoded.</summary>
    /// <param name="text">The new text.</param>
    /// <param name="previous">The bytes of the string being replaced; empty when there is none.</param>
    /// <param name="version">The header version of the document.</param>
    /// <returns>The string's bytes: UTF-16 stays UTF-16, UTF-8 stays UTF-8, and PDFDocEncoding stays so while the text fits.</returns>
    public static byte[] Encode(string text, ReadOnlySpan<byte> previous, string? version)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (previous.StartsWith(Utf16BigEndianMark))
        {
            return EncodeUtf16(text);
        }

        return previous.StartsWith(Utf8Mark) ? EncodeUtf8(text) : Encode(text, SupportsUtf8(version));
    }

    /// <summary>Encodes text as UTF-8 with a byte order mark.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The string's bytes.</returns>
    public static byte[] EncodeUtf8(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return EncodeUtf8(text.AsSpan());
    }

    /// <summary>Encodes characters as UTF-8 with a byte order mark.</summary>
    /// <param name="text">The characters.</param>
    /// <returns>The string's bytes.</returns>
    public static byte[] EncodeUtf8(ReadOnlySpan<char> text)
    {
        var bytes = new byte[Utf8Mark.Length + Encoding.UTF8.GetByteCount(text)];
        Utf8Mark.CopyTo(bytes);
        _ = Encoding.UTF8.GetBytes(text, bytes.AsSpan(Utf8Mark.Length));
        return bytes;
    }

    /// <summary>Gets the most bytes <see cref="Encode(ReadOnlySpan{char}, bool, Span{byte})"/> writes for some characters.</summary>
    /// <param name="characters">The number of characters.</param>
    /// <returns>The length a destination needs.</returns>
    internal static int MaxEncodedLength(int characters) => Utf8Mark.Length + (characters * Utf8BytesPerUnit);

    /// <summary>Encodes characters as <see cref="Encode(ReadOnlySpan{char}, bool)"/> does, into a buffer.</summary>
    /// <param name="text">The characters.</param>
    /// <param name="allowUtf8">Whether the document is PDF 2.0 or higher and may hold UTF-8 text strings.</param>
    /// <param name="destination">At least <see cref="MaxEncodedLength"/> bytes.</param>
    /// <returns>The bytes written.</returns>
    internal static int Encode(ReadOnlySpan<char> text, bool allowUtf8, Span<byte> destination)
    {
        if (TryEncodeDoc(text, destination))
        {
            return text.Length;
        }

        if (!allowUtf8)
        {
            return EncodeUtf16(text, destination);
        }

        Utf8Mark.CopyTo(destination);
        return Utf8Mark.Length + Encoding.UTF8.GetBytes(text, destination[Utf8Mark.Length..]);
    }

    /// <summary>Counts the characters a text string decodes to.</summary>
    /// <param name="bytes">The string's bytes.</param>
    /// <returns>The length of the text <see cref="Decode(ReadOnlySpan{byte})"/> returns.</returns>
    internal static int GetDecodedLength(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(Utf16BigEndianMark))
        {
            return Encoding.BigEndianUnicode.GetCharCount(Utf16Body(bytes));
        }

        if (bytes.StartsWith(Utf16LittleEndianMark))
        {
            return Encoding.Unicode.GetCharCount(Utf16Body(bytes));
        }

        return bytes.StartsWith(Utf8Mark) ? Encoding.UTF8.GetCharCount(bytes[Utf8Mark.Length..]) : bytes.Length;
    }

    /// <summary>Decodes a text string into characters, without creating a string.</summary>
    /// <param name="bytes">The string's bytes.</param>
    /// <param name="destination">At least <see cref="GetDecodedLength"/> characters.</param>
    /// <returns>The characters written.</returns>
    internal static int Decode(ReadOnlySpan<byte> bytes, Span<char> destination)
    {
        if (bytes.StartsWith(Utf16BigEndianMark))
        {
            return Encoding.BigEndianUnicode.GetChars(Utf16Body(bytes), destination);
        }

        if (bytes.StartsWith(Utf16LittleEndianMark))
        {
            return Encoding.Unicode.GetChars(Utf16Body(bytes), destination);
        }

        if (bytes.StartsWith(Utf8Mark))
        {
            return Encoding.UTF8.GetChars(bytes[Utf8Mark.Length..], destination);
        }

        for (var i = 0; i < bytes.Length; i++)
        {
            destination[i] = DecodeDocByte(bytes[i]);
        }

        return bytes.Length;
    }

    /// <summary>Gets the code units of a UTF-16 text string after its byte order mark, dropping an odd trailing byte.</summary>
    /// <param name="bytes">The string's bytes, starting with a UTF-16 byte order mark.</param>
    /// <returns>The code unit bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ReadOnlySpan<byte> Utf16Body(ReadOnlySpan<byte> bytes) => bytes[Utf16Size..][..((bytes.Length - Utf16Size) & ~1)];

    /// <summary>Encodes text as UTF-16 big-endian with a byte order mark, as PDF versions before 2.0 require for text outside PDFDocEncoding.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The string's bytes.</returns>
    private static byte[] EncodeUtf16(ReadOnlySpan<char> text)
    {
        var bytes = new byte[Utf16Size + (text.Length * Utf16Size)];
        _ = EncodeUtf16(text, bytes);
        return bytes;
    }

    /// <summary>Encodes text as UTF-16 big-endian with a byte order mark into a buffer.</summary>
    /// <param name="text">The text.</param>
    /// <param name="destination">At least two bytes more than twice the characters.</param>
    /// <returns>The bytes written.</returns>
    private static int EncodeUtf16(ReadOnlySpan<char> text, Span<byte> destination)
    {
        Utf16BigEndianMark.CopyTo(destination);
        for (var i = 0; i < text.Length; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(destination[(Utf16Size + (i * Utf16Size))..], text[i]);
        }

        return Utf16Size + (text.Length * Utf16Size);
    }

    /// <summary>Encodes text as PDFDocEncoding when every character has a code.</summary>
    /// <param name="text">The text.</param>
    /// <param name="destination">The bytes, at least as long as the text.</param>
    /// <returns><see langword="true"/> when every character fit.</returns>
    private static bool TryEncodeDoc(ReadOnlySpan<char> text, Span<byte> destination)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (!TryEncodeDocChar(text[i], out destination[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Determines whether PDFDocEncoding gives a character the same code as Latin-1.</summary>
    /// <param name="value">The character.</param>
    /// <returns><see langword="true"/> for printable ASCII, tab, line feed, carriage return and the Latin-1 letters from 0xA1 except the soft hyphen.</returns>
    private static bool IsSameInLatin1(char value) =>
        value is >= AsciiFirst and <= AsciiLast or '\t' or '\n' or '\r'
        || (value is >= LatinFirst and <= LatinLast and not SoftHyphen);

    /// <summary>Encodes one character as PDFDocEncoding.</summary>
    /// <param name="value">The character.</param>
    /// <param name="code">The byte.</param>
    /// <returns><see langword="true"/> when the character has a code.</returns>
    private static bool TryEncodeDocChar(char value, out byte code)
    {
        code = (byte)value;
        if (IsSameInLatin1(value))
        {
            return true;
        }

        var low = LowSpecials.IndexOf(value);
        if (low >= 0)
        {
            code = (byte)(LowSpecialFirst + low);
            return true;
        }

        var high = value == UndefinedHigh ? -1 : HighSpecials.IndexOf(value);
        code = (byte)(HighSpecialFirst + high);
        return high >= 0;
    }
}
