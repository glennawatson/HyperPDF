// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace HyperPdf.Speech.English;

/// <summary>Says numbers as English words: 42 as "forty two", 1984 as "nineteen eighty four", 3.5 as "three point five".</summary>
internal static class NumberWords
{
    /// <summary>The number ten.</summary>
    private const int Ten = 10;

    /// <summary>Twenty, where the tens words start.</summary>
    private const int Twenty = 20;

    /// <summary>One hundred.</summary>
    private const int Hundred = 100;

    /// <summary>One thousand.</summary>
    private const long Thousand = 1_000;

    /// <summary>The digits in a year.</summary>
    private const int YearDigits = 4;

    /// <summary>The first year read in pairs, such as "eleven hundred".</summary>
    private const int FirstPairedYear = 1100;

    /// <summary>The last year read in pairs.</summary>
    private const int LastPairedYear = 2099;

    /// <summary>The years read as "two thousand", "two thousand and nine".</summary>
    private const int MillenniumStart = 2000;

    /// <summary>The last year of the "two thousand ..." decade.</summary>
    private const int MillenniumEnd = 2009;

    /// <summary>The words for 0 to 19.</summary>
    private static readonly string[] Ones =
    [
        "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
        "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen",
    ];

    /// <summary>The words for the tens from twenty.</summary>
    private static readonly string[] Tens = [string.Empty, string.Empty, "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];

    /// <summary>The scale words, from thousand up.</summary>
    private static readonly string[] Scales = [string.Empty, "thousand", "million", "billion", "trillion"];

    /// <summary>Says a number written with digits, an optional decimal point and thousands commas.</summary>
    /// <param name="digits">The number as written.</param>
    /// <returns>The words, separated by spaces.</returns>
    internal static string Say(ReadOnlySpan<char> digits)
    {
        var builder = new StringBuilder();
        var point = digits.IndexOf('.');
        var whole = point < 0 ? digits : digits[..point];
        if (TryReadWhole(whole, out var value, out var digitCount))
        {
            var isYear = point < 0 && digitCount == YearDigits && whole.IndexOf(',') < 0 && value is >= FirstPairedYear and <= LastPairedYear;
            Append(builder, isYear ? Year((int)value) : Cardinal(value));
        }

        if (point >= 0 && point + 1 < digits.Length)
        {
            AppendDigits(builder, digits[(point + 1)..]);
        }

        return builder.ToString();
    }

    /// <summary>Says a whole number.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The words.</returns>
    internal static string Cardinal(long value)
    {
        if (value == 0)
        {
            return Ones[0];
        }

        var builder = new StringBuilder();
        var scale = 0;
        var parts = new Stack<string>();
        while (value > 0 && scale < Scales.Length)
        {
            var group = (int)(value % Thousand);
            if (group > 0)
            {
                parts.Push(scale == 0 ? UnderThousand(group) : $"{UnderThousand(group)} {Scales[scale]}");
            }

            value /= Thousand;
            scale++;
        }

        foreach (var part in parts)
        {
            Append(builder, part);
        }

        return builder.ToString();
    }

    /// <summary>Says a whole number as an ordinal: first, twelfth, twenty first, one hundredth.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The words.</returns>
    internal static string Ordinal(long value)
    {
        var cardinal = Cardinal(value);
        var last = cardinal.LastIndexOf(' ') + 1;
        var word = cardinal[last..];
        var ordinal = word switch
        {
            "one" => "first",
            "two" => "second",
            "three" => "third",
            "five" => "fifth",
            "eight" => "eighth",
            "nine" => "ninth",
            "twelve" => "twelfth",
            _ when word.EndsWith('y') => $"{word[..^1]}ieth",
            _ => $"{word}th",
        };
        return cardinal[..last] + ordinal;
    }

    /// <summary>Says a year the way people say years: 1999 as nineteen ninety-nine and 2005 as two thousand five.</summary>
    /// <param name="year">The year.</param>
    /// <returns>The words.</returns>
    internal static string SayYear(int year) => year is >= FirstPairedYear and <= LastPairedYear ? Year(year) : Cardinal(year);

    /// <summary>Reads the whole part of a number, skipping thousands commas.</summary>
    /// <param name="whole">The digits and commas.</param>
    /// <param name="value">The number.</param>
    /// <param name="digitCount">How many digits it has.</param>
    /// <returns><see langword="true"/> when there is a number that fits.</returns>
    private static bool TryReadWhole(ReadOnlySpan<char> whole, out long value, out int digitCount)
    {
        value = 0;
        digitCount = 0;
        foreach (var c in whole)
        {
            if (!char.IsAsciiDigit(c))
            {
                continue;
            }

            if (value > (long.MaxValue / Ten) - Ten)
            {
                return false;
            }

            value = (value * Ten) + (c - '0');
            digitCount++;
        }

        return digitCount > 0;
    }

    /// <summary>Says the digits after a decimal point one by one.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="fraction">The digits.</param>
    private static void AppendDigits(StringBuilder builder, ReadOnlySpan<char> fraction)
    {
        Append(builder, "point");
        foreach (var c in fraction)
        {
            if (char.IsAsciiDigit(c))
            {
                Append(builder, Ones[c - '0']);
            }
        }
    }

    /// <summary>Says a year in pairs, as people do: 1984 as "nineteen eighty four", 1900 as "nineteen hundred".</summary>
    /// <param name="year">The year.</param>
    /// <returns>The words.</returns>
    private static string Year(int year)
    {
        if (year is >= MillenniumStart and <= MillenniumEnd)
        {
            return Cardinal(year);
        }

        var high = year / Hundred;
        var low = year % Hundred;
        return low switch
        {
            0 => $"{UnderHundred(high)} hundred",
            < Ten => $"{UnderHundred(high)} oh {Ones[low]}",
            _ => $"{UnderHundred(high)} {UnderHundred(low)}",
        };
    }

    /// <summary>Says 1 to 999.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The words.</returns>
    private static string UnderThousand(int value)
    {
        var hundreds = value / Hundred;
        var rest = value % Hundred;
        if (hundreds == 0)
        {
            return UnderHundred(rest);
        }

        return rest == 0 ? $"{Ones[hundreds]} hundred" : $"{Ones[hundreds]} hundred {UnderHundred(rest)}";
    }

    /// <summary>Says 0 to 99.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The words.</returns>
    private static string UnderHundred(int value)
    {
        if (value < Twenty)
        {
            return Ones[value];
        }

        var ones = value % Ten;
        return ones == 0 ? Tens[value / Ten] : $"{Tens[value / Ten]} {Ones[ones]}";
    }

    /// <summary>Appends words with a space between.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="words">The words.</param>
    private static void Append(StringBuilder builder, string words)
    {
        if (builder.Length > 0)
        {
            _ = builder.Append(' ');
        }

        _ = builder.Append(words);
    }
}
