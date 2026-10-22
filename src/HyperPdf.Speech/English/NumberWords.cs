// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Speech.English;

/// <summary>
/// Says numbers as English words: 42 as "forty two", 1984 as "nineteen eighty four", 3.5 as "three point five". Each word is
/// handed to a sink as it is found; the words are fixed strings, so saying a number allocates nothing.
/// </summary>
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

    /// <summary>The word for one hundred.</summary>
    private const string HundredWord = "hundred";

    /// <summary>The scales a number is split into: ones, thousands, millions, billions and trillions.</summary>
    private const int ScaleCount = 5;

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

    /// <summary>The ordinal of each word a number can end in.</summary>
    private static readonly Dictionary<string, string> Ordinals = BuildOrdinals();

    /// <summary>Says a number written with digits, an optional decimal point and thousands commas.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="digits">The number as written.</param>
    /// <param name="sink">Receives the words.</param>
    internal static void Say<TSink>(ReadOnlySpan<char> digits, ref TSink sink)
        where TSink : struct, IWordSink
    {
        var point = digits.IndexOf('.');
        var whole = point < 0 ? digits : digits[..point];
        if (TryReadWhole(whole, out var value, out var digitCount))
        {
            var isYear = point < 0 && digitCount == YearDigits && whole.IndexOf(',') < 0 && value is >= FirstPairedYear and <= LastPairedYear;
            if (isYear)
            {
                Year((int)value, ref sink);
            }
            else
            {
                Cardinal(value, ref sink);
            }
        }

        if (point < 0 || point + 1 >= digits.Length)
        {
            return;
        }

        AppendDigits(digits[(point + 1)..], ref sink);
    }

    /// <summary>Says a whole number.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="value">The number.</param>
    /// <param name="sink">Receives the words.</param>
    internal static void Cardinal<TSink>(long value, ref TSink sink)
        where TSink : struct, IWordSink
    {
        if (value == 0)
        {
            sink.Add(Ones[0]);
            return;
        }

        Span<int> groups = stackalloc int[ScaleCount];
        var count = 0;
        while (value > 0 && count < ScaleCount)
        {
            groups[count] = (int)(value % Thousand);
            value /= Thousand;
            count++;
        }

        for (var scale = count - 1; scale >= 0; scale--)
        {
            if (groups[scale] == 0)
            {
                continue;
            }

            UnderThousand(groups[scale], ref sink);
            if (scale > 0)
            {
                sink.Add(Scales[scale]);
            }
        }
    }

    /// <summary>Says a whole number as an ordinal: first, twelfth, twenty first, one hundredth.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="value">The number.</param>
    /// <param name="sink">Receives the words.</param>
    internal static void Ordinal<TSink>(long value, ref TSink sink)
        where TSink : struct, IWordSink
    {
        var last = new OrdinalLastWord<TSink>(sink);
        Cardinal(value, ref last);
        last.Finish();
        sink = last.Inner;
    }

    /// <summary>Says a year the way people say years: 1999 as nineteen ninety-nine and 2005 as two thousand five.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="year">The year.</param>
    /// <param name="sink">Receives the words.</param>
    internal static void SayYear<TSink>(int year, ref TSink sink)
        where TSink : struct, IWordSink
    {
        if (year is >= FirstPairedYear and <= LastPairedYear)
        {
            Year(year, ref sink);
        }
        else
        {
            Cardinal(year, ref sink);
        }
    }

    /// <summary>Gets the ordinal of the last word of a cardinal: "one" is "first", "twenty" is "twentieth", "hundred" is "hundredth".</summary>
    /// <param name="word">One of the words a number can end in.</param>
    /// <returns>The ordinal word.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string OrdinalOf(string word) => Ordinals[word];

    /// <summary>Builds the ordinal of every word a number can end in.</summary>
    /// <returns>The ordinals by cardinal word.</returns>
    private static Dictionary<string, string> BuildOrdinals()
    {
        var ordinals = new Dictionary<string, string>(StringComparer.Ordinal);
        AddOrdinals(ordinals, Ones);
        AddOrdinals(ordinals, Tens);
        AddOrdinals(ordinals, Scales);
        AddOrdinals(ordinals, [HundredWord]);
        return ordinals;
    }

    /// <summary>Adds the ordinal of each word in a list.</summary>
    /// <param name="ordinals">Receives the ordinals by cardinal word.</param>
    /// <param name="words">The cardinal words; empty ones are skipped.</param>
    private static void AddOrdinals(Dictionary<string, string> ordinals, string[] words)
    {
        foreach (var word in words)
        {
            if (word.Length > 0)
            {
                ordinals[word] = OrdinalWord(word);
            }
        }
    }

    /// <summary>Makes a cardinal word an ordinal.</summary>
    /// <param name="word">The cardinal.</param>
    /// <returns>The ordinal.</returns>
    private static string OrdinalWord(string word) => word switch
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
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="fraction">The digits.</param>
    /// <param name="sink">Receives the words.</param>
    private static void AppendDigits<TSink>(ReadOnlySpan<char> fraction, ref TSink sink)
        where TSink : struct, IWordSink
    {
        sink.Add("point");
        foreach (var c in fraction)
        {
            if (char.IsAsciiDigit(c))
            {
                sink.Add(Ones[c - '0']);
            }
        }
    }

    /// <summary>Says a year in pairs, as people do: 1984 as "nineteen eighty four", 1900 as "nineteen hundred".</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="year">The year.</param>
    /// <param name="sink">Receives the words.</param>
    private static void Year<TSink>(int year, ref TSink sink)
        where TSink : struct, IWordSink
    {
        if (year is >= MillenniumStart and <= MillenniumEnd)
        {
            Cardinal(year, ref sink);
            return;
        }

        var high = year / Hundred;
        var low = year % Hundred;
        UnderHundred(high, ref sink);
        if (low == 0)
        {
            sink.Add(HundredWord);
        }
        else if (low < Ten)
        {
            sink.Add("oh");
            sink.Add(Ones[low]);
        }
        else
        {
            UnderHundred(low, ref sink);
        }
    }

    /// <summary>Says 1 to 999.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="value">The number.</param>
    /// <param name="sink">Receives the words.</param>
    private static void UnderThousand<TSink>(int value, ref TSink sink)
        where TSink : struct, IWordSink
    {
        var hundreds = value / Hundred;
        var rest = value % Hundred;
        if (hundreds > 0)
        {
            sink.Add(Ones[hundreds]);
            sink.Add(HundredWord);
        }

        if (hundreds == 0 || rest > 0)
        {
            UnderHundred(rest, ref sink);
        }
    }

    /// <summary>Says 0 to 99.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="value">The number.</param>
    /// <param name="sink">Receives the words.</param>
    private static void UnderHundred<TSink>(int value, ref TSink sink)
        where TSink : struct, IWordSink
    {
        if (value < Twenty)
        {
            sink.Add(Ones[value]);
            return;
        }

        sink.Add(Tens[value / Ten]);
        var ones = value % Ten;
        if (ones != 0)
        {
            sink.Add(Ones[ones]);
        }
    }
}
