// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace HyperPdf.Core.Forms.Scripting;

/// <summary>
/// Reads and writes numbers using PDF form function conventions: separator styles 0 (1,234.56), 1 (1234.56),
/// 2 (1.234,56) and 3 (1234,56), negative styles with a minus sign or parentheses, and a currency symbol before or
/// after.
/// </summary>
public static class FormNumbers
{
    /// <summary>The separator style with a comma for the decimal point.</summary>
    private const int FirstCommaDecimalStyle = 2;

    /// <summary>The negative styles shown in parentheses.</summary>
    private const int FirstParenthesesStyle = 2;

    /// <summary>The digits between grouping separators.</summary>
    private const int GroupSize = 3;

    /// <summary>The most decimals written.</summary>
    private const int MaxDecimals = 10;

    /// <summary>Reads a number leniently, as <c>AFMakeNumber</c> does: currency, spaces and grouping are ignored.</summary>
    /// <param name="text">The text, for example <c>$1,234.50</c> or <c>(12)</c>.</param>
    /// <param name="value">The number.</param>
    /// <returns><see langword="true"/> when the text holds a number.</returns>
    public static bool TryParse(string? text, out double value)
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

        var normal = Normalise(digits.ToString());
        if (!double.TryParse(normal, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return false;
        }

        value = negative ? -value : value;
        return true;
    }

    /// <summary>Writes a number.</summary>
    /// <param name="value">The number.</param>
    /// <param name="decimals">The decimals.</param>
    /// <param name="separatorStyle">The separator style, 0 to 3.</param>
    /// <param name="negativeStyle">The negative style, 0 to 3.</param>
    /// <param name="currency">The currency symbol, or empty.</param>
    /// <param name="currencyFirst">Whether the symbol comes before the number.</param>
    /// <returns>The text.</returns>
    public static string Format(double value, int decimals, int separatorStyle, int negativeStyle, string currency, bool currencyFirst)
    {
        ArgumentNullException.ThrowIfNull(currency);
        var places = Math.Clamp(decimals, 0, MaxDecimals);
        var rounded = Math.Round(Math.Abs(value), places, MidpointRounding.AwayFromZero);
        var invariant = rounded.ToString(string.Create(CultureInfo.InvariantCulture, $"F{places}"), CultureInfo.InvariantCulture);
        var number = Separate(invariant, separatorStyle);
        number = currencyFirst ? currency + number : number + currency;
        if (value >= 0 || rounded == 0)
        {
            return number;
        }

        return negativeStyle >= FirstParenthesesStyle ? $"({number})" : $"-{number}";
    }

    /// <summary>Writes a calculation's result plainly, for a field without a format: invariant digits, no trailing zeros.</summary>
    /// <param name="value">The result.</param>
    /// <returns>The text.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string Plain(double value) => Math.Round(value, MaxDecimals).ToString("0.##########", CultureInfo.InvariantCulture);

    /// <summary>Applies a separator style to invariant digits such as <c>1234.56</c>.</summary>
    /// <param name="invariant">The digits with a point.</param>
    /// <param name="style">The style.</param>
    /// <returns>The separated text.</returns>
    private static string Separate(string invariant, int style)
    {
        var point = invariant.IndexOf('.', StringComparison.Ordinal);
        var whole = point < 0 ? invariant : invariant[..point];
        var fraction = point < 0 ? string.Empty : invariant[(point + 1)..];
        var commaDecimal = style >= FirstCommaDecimalStyle;
        if (style is 0 or FirstCommaDecimalStyle)
        {
            whole = Group(whole, commaDecimal ? '.' : ',');
        }

        return fraction.Length == 0 ? whole : $"{whole}{(commaDecimal ? ',' : '.')}{fraction}";
    }

    /// <summary>Puts a separator between each group of three digits.</summary>
    /// <param name="digits">The whole digits.</param>
    /// <param name="separator">The separator.</param>
    /// <returns>The grouped digits.</returns>
    private static string Group(string digits, char separator)
    {
        var builder = new StringBuilder(digits.Length + (digits.Length / GroupSize));
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

    /// <summary>
    /// Turns digits with points and commas into invariant digits: the last mark is the decimal point unless it is
    /// followed by exactly three digits and could be a thousands separator.
    /// </summary>
    /// <param name="digits">The digits and marks.</param>
    /// <returns>The invariant number.</returns>
    private static string Normalise(string digits)
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
