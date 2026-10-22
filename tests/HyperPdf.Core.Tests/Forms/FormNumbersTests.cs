// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdf.Core.Forms.Scripting;

namespace HyperPdf.Core.Tests.Forms;

/// <summary>Tests that <see cref="FormNumbers"/> reads and writes numbers as the original string based code did.</summary>
public sealed class FormNumbersTests
{
    /// <summary>The separator style with a comma for the decimal point.</summary>
    private const int FirstCommaDecimalStyle = 2;

    /// <summary>The negative styles shown in parentheses.</summary>
    private const int FirstParenthesesStyle = 2;

    /// <summary>The digits between grouping separators.</summary>
    private const int GroupSize = 3;

    /// <summary>The most decimals written.</summary>
    private const int MaxDecimals = 10;

    /// <summary>The separator and negative styles.</summary>
    private const int Styles = 4;

    /// <summary>The longest text tried, longer than the stack buffers.</summary>
    private const int LongDigits = 600;

    /// <summary>A negative amount.</summary>
    private const double Amount = -1234.5;

    /// <summary>The decimals of an amount of money.</summary>
    private const int Cents = 2;

    /// <summary>The numbers typed or shown.</summary>
    private static readonly string[] Texts =
    [
        "1", "12", "123", "1,234", "1.234", "1,234.5", "1.234,5", "1,234,567.891", "1.234.567,891", "$1,234.50", "(12)", "-12.5", "(1,000)",
        "1,23", "1,2345", "0.001", ".5", ",5", "5.", "1 000", "abc", "-", "()", "€ 1.234,56", "1.2.3", "1,2,3", "12,345,678", "999.999", "0",
        "-0", "00012", "1e5", "1,000.", "1.000,", " 42 ", "٣", "12-34",
    ];

    /// <summary>The values written.</summary>
    private static readonly double[] Values =
    [
        0, 1, -1, 0.5, -0.5, 12.345, -12.345, 1234.5678, -1234.5678, 1234567.891, 999.9999, -0.0001, 0.00049, 0.005, 1e15, -1e15, 123_456_789_012_345_678,
        double.MaxValue, double.Epsilon, double.NaN, double.PositiveInfinity,
    ];

    /// <summary>Verifies typed text reads to the same number or failure as the original code gave.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ParsesLikeTheOriginal()
    {
        string[] inputs = [.. Texts, new('1', LongDigits), string.Concat(Enumerable.Repeat("1,234", LongDigits / GroupSize))];
        foreach (var text in inputs)
        {
            var actual = FormNumbers.TryParse(text, out var value);
            var expected = ReferenceTryParse(text, out var reference);

            await Assert.That(actual).IsEqualTo(expected);
            await Assert.That(BitConverter.DoubleToInt64Bits(value)).IsEqualTo(BitConverter.DoubleToInt64Bits(reference));
        }
    }

    /// <summary>Verifies numbers write to the same text as the original code gave, for every style, decimals and currency position.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FormatsLikeTheOriginal()
    {
        foreach (var value in Values)
        {
            for (var decimals = -1; decimals <= MaxDecimals + 1; decimals++)
            {
                for (var separator = 0; separator < Styles; separator++)
                {
                    for (var negative = 0; negative < Styles; negative++)
                    {
                        await Assert.That(FormNumbers.Format(value, decimals, separator, negative, "€", true)).IsEqualTo(ReferenceFormat(value, decimals, separator, negative, "€", true));
                        await Assert.That(FormNumbers.Format(value, decimals, separator, negative, " USD", false)).IsEqualTo(ReferenceFormat(value, decimals, separator, negative, " USD", false));
                    }
                }
            }
        }
    }

    /// <summary>Verifies a currency symbol longer than the stack buffer is written whole.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FormatsWithALongCurrencyName()
    {
        var currency = new string('x', LongDigits);

        await Assert.That(FormNumbers.Format(Amount, Cents, 0, FirstParenthesesStyle, currency, false)).IsEqualTo(ReferenceFormat(Amount, Cents, 0, FirstParenthesesStyle, currency, false));
    }

    /// <summary>The original parse, kept as the reference.</summary>
    /// <param name="text">The text.</param>
    /// <param name="value">The number.</param>
    /// <returns>Whether the text holds a number.</returns>
    private static bool ReferenceTryParse(string? text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var negative = text.Contains('-', StringComparison.Ordinal) || (text.Contains('(', StringComparison.Ordinal) && text.Contains(')', StringComparison.Ordinal));
        var digits = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (char.IsAsciiDigit(c) || c is '.' or ',')
            {
                _ = digits.Append(c);
            }
        }

        if (!double.TryParse(ReferenceNormalise(digits.ToString()), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return false;
        }

        value = negative ? -value : value;
        return true;
    }

    /// <summary>The original format, kept as the reference.</summary>
    /// <param name="value">The number.</param>
    /// <param name="decimals">The decimals.</param>
    /// <param name="separatorStyle">The separator style.</param>
    /// <param name="negativeStyle">The negative style.</param>
    /// <param name="currency">The currency symbol.</param>
    /// <param name="currencyFirst">Whether the symbol comes first.</param>
    /// <returns>The text.</returns>
    private static string ReferenceFormat(double value, int decimals, int separatorStyle, int negativeStyle, string currency, bool currencyFirst)
    {
        var places = Math.Clamp(decimals, 0, MaxDecimals);
        var rounded = Math.Round(Math.Abs(value), places, MidpointRounding.AwayFromZero);
        var invariant = rounded.ToString(string.Create(CultureInfo.InvariantCulture, $"F{places}"), CultureInfo.InvariantCulture);
        var number = ReferenceSeparate(invariant, separatorStyle);
        number = currencyFirst ? currency + number : number + currency;
        if (value >= 0 || rounded == 0)
        {
            return number;
        }

        return negativeStyle >= FirstParenthesesStyle ? $"({number})" : $"-{number}";
    }

    /// <summary>The original separator step.</summary>
    /// <param name="invariant">The digits with a point.</param>
    /// <param name="style">The style.</param>
    /// <returns>The separated text.</returns>
    private static string ReferenceSeparate(string invariant, int style)
    {
        var point = invariant.IndexOf('.', StringComparison.Ordinal);
        var whole = point < 0 ? invariant : invariant[..point];
        var fraction = point < 0 ? string.Empty : invariant[(point + 1)..];
        var commaDecimal = style >= FirstCommaDecimalStyle;
        if (style is 0 or FirstCommaDecimalStyle)
        {
            whole = ReferenceGroup(whole, commaDecimal ? '.' : ',');
        }

        return fraction.Length == 0 ? whole : $"{whole}{(commaDecimal ? ',' : '.')}{fraction}";
    }

    /// <summary>The original grouping.</summary>
    /// <param name="digits">The whole digits.</param>
    /// <param name="separator">The separator.</param>
    /// <returns>The grouped digits.</returns>
    private static string ReferenceGroup(string digits, char separator)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < digits.Length; i++)
        {
            if (i > 0 && (digits.Length - i) % GroupSize == 0)
            {
                _ = builder.Append(separator);
            }

            _ = builder.Append(digits[i]);
        }

        return builder.ToString();
    }

    /// <summary>The original normalisation.</summary>
    /// <param name="digits">The digits and marks.</param>
    /// <returns>The invariant number.</returns>
    private static string ReferenceNormalise(string digits)
    {
        var lastPoint = digits.LastIndexOf('.');
        var lastComma = digits.LastIndexOf(',');
        var decimalAt = Math.Max(lastPoint, lastComma);
        if (decimalAt < 0)
        {
            return digits;
        }

        var mark = digits[decimalAt];
        var single = digits.AsSpan().Count(mark) == 1;
        var isDecimal = (lastPoint >= 0 && lastComma >= 0) || digits.Length - decimalAt - 1 != GroupSize || (single && mark == '.');
        if (!isDecimal)
        {
            return digits.Replace(",", string.Empty, StringComparison.Ordinal).Replace(".", string.Empty, StringComparison.Ordinal);
        }

        var whole = digits[..decimalAt].Replace(",", string.Empty, StringComparison.Ordinal).Replace(".", string.Empty, StringComparison.Ordinal);
        return $"{whole}.{digits[(decimalAt + 1)..]}";
    }
}
