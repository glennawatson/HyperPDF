// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Writes a page label's text, its prefix followed by the page number in the range's numbering style.</summary>
internal static class PageLabelText
{
    /// <summary>The letters in the alphabet styles.</summary>
    private const int LetterCount = 26;

    /// <summary>
    /// The largest number written in Roman numerals (ISO 32000-2 12.4.2 /S /R and /r). Roman numerals have no symbol above
    /// M, so larger numbers repeat it; this cap keeps a label to about a hundred letters. Larger numbers fall back to digits.
    /// </summary>
    private const int MaxRoman = 100_000;

    /// <summary>
    /// The longest run of one letter written for /S /A and /a (a to z, then aa to zz, and so on). A longer run is no
    /// use to a reader, so larger numbers fall back to digits.
    /// </summary>
    private const int MaxLetters = 100;

    /// <summary>The bytes each Roman symbol takes in <see cref="RomanSymbols"/>.</summary>
    private const int SymbolWidth = 2;

    /// <summary>The longest decimal form of an <see cref="int"/>, "-2147483648".</summary>
    private const int MaxDecimalLength = 11;

    /// <summary>The distance from a lower-case ASCII letter to its upper-case form.</summary>
    private const int CaseOffset = 'a' - 'A';

    /// <summary>Fills the second byte of a one-letter Roman symbol.</summary>
    private const byte NoLetter = (byte)' ';

    /// <summary>Gets the values of the Roman numeral symbols, largest first.</summary>
    private static ReadOnlySpan<int> RomanValues => [0x3E8, 0x384, 0x1F4, 0x190, 0x64, 0x5A, 0x32, 0x28, 0x0A, 0x09, 0x05, 0x04, 0x01];

    /// <summary>Gets the lower-case Roman numeral symbols matching <see cref="RomanValues"/>, two bytes each.</summary>
    private static ReadOnlySpan<byte> RomanSymbols => "m cmd cdc xcl xlx ixv ivi "u8;

    /// <summary>Creates a label as one string, with no intermediate text.</summary>
    /// <param name="prefix">The /P text string's bytes, or empty.</param>
    /// <param name="number">The page's number within its range.</param>
    /// <param name="style">The one-letter /S style: D, R, r, A or a; any other value writes no number.</param>
    /// <returns>The label, or <see langword="null"/> when it would be empty.</returns>
    internal static string? Create(ReadOnlySpan<byte> prefix, int number, byte style)
    {
        var prefixLength = PdfText.GetDecodedLength(prefix);
        var length = prefixLength + PageLabelText.GetNumberLength(number, style);
        return length == 0
            ? null
            : string.Create(length, new LabelParts(prefix, prefixLength, number, style), static (destination, parts) =>
            {
                _ = PdfText.Decode(parts.Prefix, destination[..parts.PrefixLength]);
                PageLabelText.WriteNumber(destination[parts.PrefixLength..], parts.Number, parts.Style);
            });
    }

    /// <summary>Counts the characters of a number written in a style.</summary>
    /// <param name="number">The number.</param>
    /// <param name="style">The style.</param>
    /// <returns>The character count.</returns>
    private static int GetNumberLength(int number, byte style) => style switch
    {
        (byte)'D' => PageLabelText.GetDecimalLength(number),
        (byte)'R' or (byte)'r' => PageLabelText.GetRomanOrDigitsLength(number),
        (byte)'A' or (byte)'a' => PageLabelText.GetLettersOrDigitsLength(number),
        _ => 0,
    };

    /// <summary>Counts the characters of a Roman numeral, or of digits above the cap; non-positive numbers write nothing.</summary>
    /// <param name="number">The number.</param>
    /// <returns>The character count.</returns>
    private static int GetRomanOrDigitsLength(int number)
    {
        if (number <= 0)
        {
            return 0;
        }

        return number <= PageLabelText.MaxRoman ? PageLabelText.GetRomanLength(number) : PageLabelText.GetDecimalLength(number);
    }

    /// <summary>Counts the characters of a run of letters, or of digits above the cap; non-positive numbers write nothing.</summary>
    /// <param name="number">The number.</param>
    /// <returns>The character count.</returns>
    private static int GetLettersOrDigitsLength(int number)
    {
        if (number <= 0)
        {
            return 0;
        }

        return PageLabelText.FitsLetters(number) ? PageLabelText.GetLetterLength(number) : PageLabelText.GetDecimalLength(number);
    }

    /// <summary>Determines whether a positive number is written as a run of letters rather than digits.</summary>
    /// <param name="number">The number.</param>
    /// <returns><see langword="true"/> when the run is no longer than the cap.</returns>
    private static bool FitsLetters(int number) => ((number - 1) / PageLabelText.LetterCount) + 1 <= PageLabelText.MaxLetters;

    /// <summary>Writes a number in a style into a span sized by <see cref="GetNumberLength"/>.</summary>
    /// <param name="destination">The characters to fill.</param>
    /// <param name="number">The number.</param>
    /// <param name="style">The style.</param>
    private static void WriteNumber(Span<char> destination, int number, byte style)
    {
        switch (style)
        {
            case (byte)'D':
            {
                _ = number.TryFormat(destination, out _, default, CultureInfo.InvariantCulture);
                break;
            }

            case (byte)'R' or (byte)'r' when number > PageLabelText.MaxRoman:
            case (byte)'A' or (byte)'a' when number > 0 && !PageLabelText.FitsLetters(number):
            {
                _ = number.TryFormat(destination, out _, default, CultureInfo.InvariantCulture);
                break;
            }

            case (byte)'R' or (byte)'r':
            {
                PageLabelText.WriteRoman(destination, number, style == (byte)'R');
                break;
            }

            case (byte)'A' or (byte)'a':
            {
                PageLabelText.WriteLetters(destination, number, style == (byte)'A');
                break;
            }
        }
    }

    /// <summary>Counts the characters of a decimal number.</summary>
    /// <param name="number">The number.</param>
    /// <returns>The character count.</returns>
    private static int GetDecimalLength(int number)
    {
        Span<char> digits = stackalloc char[PageLabelText.MaxDecimalLength];
        _ = number.TryFormat(digits, out var written, default, CultureInfo.InvariantCulture);
        return written;
    }

    /// <summary>Counts the characters of a number in Roman numerals; zero and negative numbers write nothing.</summary>
    /// <param name="number">The number, already wrapped.</param>
    /// <returns>The character count.</returns>
    private static int GetRomanLength(int number)
    {
        var length = 0;
        for (var i = 0; i < PageLabelText.RomanValues.Length && number > 0; i++)
        {
            length += number / PageLabelText.RomanValues[i] * PageLabelText.GetSymbolLength(i);
            number %= PageLabelText.RomanValues[i];
        }

        return length;
    }

    /// <summary>Writes a number in Roman numerals.</summary>
    /// <param name="destination">The characters to fill.</param>
    /// <param name="number">The number, already wrapped.</param>
    /// <param name="upperCase">Whether to use upper-case symbols.</param>
    private static void WriteRoman(Span<char> destination, int number, bool upperCase)
    {
        var offset = 0;
        var caseOffset = upperCase ? PageLabelText.CaseOffset : 0;
        for (var i = 0; i < PageLabelText.RomanValues.Length && number > 0; i++)
        {
            var count = number / PageLabelText.RomanValues[i];
            number %= PageLabelText.RomanValues[i];
            var first = (char)(PageLabelText.RomanSymbols[i * PageLabelText.SymbolWidth] - caseOffset);
            if (PageLabelText.GetSymbolLength(i) == 1)
            {
                destination.Slice(offset, count).Fill(first);
                offset += count;
                continue;
            }

            // A two-letter symbol such as "cm" is used at most once, because the larger symbols take the rest.
            if (count == 0)
            {
                continue;
            }

            destination[offset] = first;
            destination[offset + 1] = (char)(PageLabelText.RomanSymbols[(i * PageLabelText.SymbolWidth) + 1] - caseOffset);
            offset += PageLabelText.SymbolWidth;
        }
    }

    /// <summary>Gets the letters in one Roman symbol.</summary>
    /// <param name="index">The symbol's index in <see cref="RomanValues"/>.</param>
    /// <returns>1 or 2.</returns>
    private static int GetSymbolLength(int index) =>
        PageLabelText.RomanSymbols[(index * PageLabelText.SymbolWidth) + 1] == PageLabelText.NoLetter ? 1 : PageLabelText.SymbolWidth;

    /// <summary>Counts the letters of a number in the alphabet styles: a..z, then aa..zz, and so on.</summary>
    /// <param name="number">The number; zero and negative numbers write nothing.</param>
    /// <returns>The letter count.</returns>
    private static int GetLetterLength(int number) =>
        number <= 0 ? 0 : ((number - 1) / PageLabelText.LetterCount) + 1;

    /// <summary>Writes a number in the alphabet styles.</summary>
    /// <param name="destination">The characters to fill, sized by <see cref="GetLetterLength"/>.</param>
    /// <param name="number">The number.</param>
    /// <param name="upperCase">Whether to use upper-case letters.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteLetters(Span<char> destination, int number, bool upperCase) =>
        destination.Fill((char)((upperCase ? 'A' : 'a') + ((number - 1) % PageLabelText.LetterCount)));

    /// <summary>The parts of a label written by <see cref="Create"/>.</summary>
    /// <param name="prefix">The prefix's text string bytes.</param>
    /// <param name="prefixLength">The prefix's decoded length.</param>
    /// <param name="number">The page number.</param>
    /// <param name="style">The numbering style.</param>
    private readonly ref struct LabelParts(ReadOnlySpan<byte> prefix, int prefixLength, int number, byte style)
    {
        /// <summary>Gets the prefix's text string bytes.</summary>
        internal ReadOnlySpan<byte> Prefix { get; } = prefix;

        /// <summary>Gets the prefix's decoded length.</summary>
        internal int PrefixLength { get; } = prefixLength;

        /// <summary>Gets the page number.</summary>
        internal int Number { get; } = number;

        /// <summary>Gets the numbering style.</summary>
        internal byte Style { get; } = style;
    }
}
