// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

namespace HyperPdfLibrary.Composition;

/// <summary>Writes whole numbers as decimal, Roman numerals or letters.</summary>
internal static class NumberText
{
    /// <summary>The letters in the alphabet.</summary>
    private const int AlphabetLength = 26;

    /// <summary>The values of the Roman numeral symbols, largest first.</summary>
    private static readonly int[] RomanValues = [1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1];

    /// <summary>The Roman numeral symbols, in the order of <see cref="RomanValues"/>.</summary>
    private static readonly string[] RomanSymbols = ["M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I"];

    /// <summary>Writes a number.</summary>
    /// <param name="value">The number; numbers below 1 are written as decimals whatever the format.</param>
    /// <param name="format">How to write it.</param>
    /// <returns>The text.</returns>
    internal static string Format(int value, PdfNumberFormat format)
    {
        var decimalOnly = value < 1 || format == PdfNumberFormat.Decimal;
        return decimalOnly ? value.ToString(CultureInfo.InvariantCulture) : FormatWords(value, format);
    }

    /// <summary>Writes a number, 1 or more, as Roman numerals or letters.</summary>
    /// <param name="value">The number.</param>
    /// <param name="format">The format, which is not decimal.</param>
    /// <returns>The text.</returns>
    private static string FormatWords(int value, PdfNumberFormat format) => format switch
    {
        PdfNumberFormat.LowerRoman => FormatRoman(value).ToLowerInvariant(),
        PdfNumberFormat.UpperRoman => FormatRoman(value),
        PdfNumberFormat.LowerAlpha => FormatAlpha(value, 'a'),
        _ => FormatAlpha(value, 'A'),
    };

    /// <summary>Writes a number as Roman numerals.</summary>
    /// <param name="value">The number, 1 or more.</param>
    /// <returns>The numerals in capitals.</returns>
    private static string FormatRoman(int value)
    {
        var text = new StringBuilder();
        for (var i = 0; i < RomanValues.Length; i++)
        {
            while (value >= RomanValues[i])
            {
                _ = text.Append(RomanSymbols[i]);
                value -= RomanValues[i];
            }
        }

        return text.ToString();
    }

    /// <summary>Writes a number as letters: a to z, then aa to zz, then aaa.</summary>
    /// <param name="value">The number, 1 or more.</param>
    /// <param name="first">The first letter of the alphabet in the wanted case.</param>
    /// <returns>The letters.</returns>
    private static string FormatAlpha(int value, char first) =>
        new((char)(first + ((value - 1) % AlphabetLength)), ((value - 1) / AlphabetLength) + 1);
}
