// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Writing;

/// <summary>
/// Writes PDF tokens as bytes straight into a pooled buffer: names, strings, numbers and references. Nothing here
/// allocates beyond growing the buffer.
/// </summary>
internal static class PdfSyntax
{
    /// <summary>The bytes an octal escape such as <c>\101</c> takes.</summary>
    private const int OctalEscapeLength = 4;

    /// <summary>The bytes a two-character escape such as <c>\n</c> takes.</summary>
    private const int ShortEscapeLength = 2;

    /// <summary>The bytes a <c>#xx</c> name escape takes.</summary>
    private const int NameEscapeLength = 3;

    /// <summary>The bits of one octal digit.</summary>
    private const int OctalBits = 3;

    /// <summary>The shift of the first of three octal digits.</summary>
    private const int HighOctalShift = 6;

    /// <summary>The mask of one octal digit.</summary>
    private const int OctalMask = 7;

    /// <summary>The bits of one hexadecimal digit.</summary>
    private const int HexBits = 4;

    /// <summary>The mask of one hexadecimal digit.</summary>
    private const int HexMask = 0xF;

    /// <summary>The hexadecimal digits a byte takes in a hexadecimal string.</summary>
    private const int HexDigitsPerByte = 2;

    /// <summary>The longest reference: two formatted numbers, two spaces and <c>R</c>, rounded up.</summary>
    private const int MaxReferenceLength = 72;

    /// <summary>The base of decimal digits.</summary>
    private const int Ten = 10;

    /// <summary>The name bytes written as they are: printable ASCII other than delimiters and <c>#</c>.</summary>
    private static readonly SearchValues<byte> NameRegular = SearchValues.Create(
        "!\"$&'*+,-.0123456789:;=?@ABCDEFGHIJKLMNOPQRSTUVWXYZ\\^_`abcdefghijklmnopqrstuvwxyz|~"u8);

    /// <summary>The string bytes written as they are in a literal string: printable ASCII other than <c>( ) \</c>.</summary>
    private static readonly SearchValues<byte> LiteralRegular = SearchValues.Create(
        " !\"#$%&'*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[]^_`abcdefghijklmnopqrstuvwxyz{|}~"u8);

    /// <summary>Gets the upper-case hexadecimal digits.</summary>
    private static ReadOnlySpan<byte> HexDigits => "0123456789ABCDEF"u8;

    /// <summary>Writes a name with its slash, escaping bytes that are not regular characters as <c>#xx</c>.</summary>
    /// <param name="output">The buffer.</param>
    /// <param name="spelling">The name's bytes, unescaped.</param>
    internal static void WriteName(ref PooledBuffer output, scoped ReadOnlySpan<byte> spelling)
    {
        output.WriteByte((byte)'/');
        while (!spelling.IsEmpty)
        {
            var special = spelling.IndexOfAnyExcept(NameRegular);
            if (special < 0)
            {
                output.Write(spelling);
                return;
            }

            output.Write(spelling[..special]);
            var span = output.GetSpan(NameEscapeLength);
            span[0] = (byte)'#';
            span[1] = HexDigits[spelling[special] >> HexBits];
            span[HexDigitsPerByte] = HexDigits[spelling[special] & HexMask];
            output.Advance(NameEscapeLength);
            spelling = spelling[(special + 1)..];
        }
    }

    /// <summary>Writes a string in whichever of the literal and hexadecimal forms is shorter.</summary>
    /// <param name="output">The buffer.</param>
    /// <param name="bytes">The string's bytes.</param>
    internal static void WriteString(ref PooledBuffer output, ReadOnlySpan<byte> bytes)
    {
        if (LiteralLength(bytes) <= (long)bytes.Length * HexDigitsPerByte)
        {
            WriteLiteralString(ref output, bytes);
            return;
        }

        WriteHexString(ref output, bytes);
    }

    /// <summary>Writes a literal string, escaping parentheses, backslashes and bytes outside printable ASCII.</summary>
    /// <param name="output">The buffer.</param>
    /// <param name="bytes">The string's bytes.</param>
    internal static void WriteLiteralString(ref PooledBuffer output, ReadOnlySpan<byte> bytes)
    {
        output.WriteByte((byte)'(');
        while (!bytes.IsEmpty)
        {
            var special = bytes.IndexOfAnyExcept(LiteralRegular);
            if (special < 0)
            {
                output.Write(bytes);
                break;
            }

            output.Write(bytes[..special]);
            WriteEscape(ref output, bytes[special]);
            bytes = bytes[(special + 1)..];
        }

        output.WriteByte((byte)')');
    }

    /// <summary>Writes a hexadecimal string.</summary>
    /// <param name="output">The buffer.</param>
    /// <param name="bytes">The string's bytes.</param>
    internal static void WriteHexString(ref PooledBuffer output, ReadOnlySpan<byte> bytes)
    {
        var length = (bytes.Length * HexDigitsPerByte) + HexDigitsPerByte;
        var span = output.GetSpan(length);
        span[0] = (byte)'<';
        var digits = span[1..];
        for (var i = 0; i < bytes.Length; i++)
        {
            digits[i * HexDigitsPerByte] = HexDigits[bytes[i] >> HexBits];
            digits[(i * HexDigitsPerByte) + 1] = HexDigits[bytes[i] & HexMask];
        }

        span[length - 1] = (byte)'>';
        output.Advance(length);
    }

    /// <summary>Writes a number: an integer when it is whole, otherwise up to six decimals.</summary>
    /// <param name="output">The buffer.</param>
    /// <param name="number">The number.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteNumber(ref PooledBuffer output, double number) =>
        output.Advance(PdfNumber.Format(number, output.GetSpan(PdfNumber.MaxFormattedLength)));

    /// <summary>Writes a real number copied from a value, with up to ten decimals so small values survive.</summary>
    /// <param name="output">The buffer.</param>
    /// <param name="number">The number.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WritePreciseNumber(ref PooledBuffer output, double number) =>
        output.Advance(PdfNumber.FormatPrecise(number, output.GetSpan(PdfNumber.MaxFormattedLength)));

    /// <summary>Writes an integer.</summary>
    /// <param name="output">The buffer.</param>
    /// <param name="number">The integer.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteInteger(ref PooledBuffer output, long number) =>
        output.Advance(PdfNumber.Format(number, output.GetSpan(PdfNumber.MaxFormattedLength)));

    /// <summary>Writes a reference, <c>n g R</c>.</summary>
    /// <param name="output">The buffer.</param>
    /// <param name="number">The object number.</param>
    /// <param name="generation">The generation.</param>
    internal static void WriteReference(ref PooledBuffer output, int number, int generation)
    {
        var span = output.GetSpan(MaxReferenceLength);
        var written = PdfNumber.Format(number, span);
        span[written] = (byte)' ';
        written++;
        written += PdfNumber.Format(generation, span[written..]);
        span[written] = (byte)' ';
        span[written + 1] = (byte)'R';
        output.Advance(written + ShortEscapeLength);
    }

    /// <summary>Writes an integer padded with leading zeros, as cross-reference table entries need.</summary>
    /// <param name="output">The buffer.</param>
    /// <param name="number">The non-negative integer.</param>
    /// <param name="digits">The width.</param>
    internal static void WritePadded(ref PooledBuffer output, long number, int digits)
    {
        var span = output.GetSpan(digits);
        for (var i = digits - 1; i >= 0; i--)
        {
            span[i] = (byte)('0' + (number % Ten));
            number /= Ten;
        }

        output.Advance(digits);
    }

    /// <summary>Works out how long a string is in literal form.</summary>
    /// <param name="bytes">The string's bytes.</param>
    /// <returns>The length, without the parentheses.</returns>
    private static long LiteralLength(ReadOnlySpan<byte> bytes)
    {
        long length = bytes.Length;
        var special = bytes.IndexOfAnyExcept(LiteralRegular);
        while (special >= 0)
        {
            length += EscapeLength(bytes[special]) - 1;
            bytes = bytes[(special + 1)..];
            special = bytes.IndexOfAnyExcept(LiteralRegular);
        }

        return length;
    }

    /// <summary>Gets the length of a byte's escape in a literal string.</summary>
    /// <param name="value">The byte.</param>
    /// <returns>The bytes the escape takes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int EscapeLength(byte value) => ShortEscape(value) == 0 ? OctalEscapeLength : ShortEscapeLength;

    /// <summary>Gets the letter of a byte's short escape.</summary>
    /// <param name="value">The byte.</param>
    /// <returns>The letter, or zero when the byte needs an octal escape.</returns>
    private static byte ShortEscape(byte value) => value switch
    {
        (byte)'(' or (byte)')' or (byte)'\\' => value,
        (byte)'\n' => (byte)'n',
        (byte)'\r' => (byte)'r',
        (byte)'\t' => (byte)'t',
        (byte)'\b' => (byte)'b',
        (byte)'\f' => (byte)'f',
        _ => 0,
    };

    /// <summary>Writes one escaped byte of a literal string.</summary>
    /// <param name="output">The buffer.</param>
    /// <param name="value">The byte.</param>
    private static void WriteEscape(ref PooledBuffer output, byte value)
    {
        var letter = ShortEscape(value);
        if (letter != 0)
        {
            var pair = output.GetSpan(ShortEscapeLength);
            pair[0] = (byte)'\\';
            pair[1] = letter;
            output.Advance(ShortEscapeLength);
            return;
        }

        // Always three digits, so a following digit is never read as part of the escape.
        var span = output.GetSpan(OctalEscapeLength);
        span[0] = (byte)'\\';
        span[1] = (byte)('0' + (value >> HighOctalShift));
        span[ShortEscapeLength] = (byte)('0' + ((value >> OctalBits) & OctalMask));
        span[OctalEscapeLength - 1] = (byte)('0' + (value & OctalMask));
        output.Advance(OctalEscapeLength);
    }
}
