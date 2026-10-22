// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;

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

    /// <summary>The characters typed text is read in on the stack; longer text is read in a rented array.</summary>
    private const int StackText = 128;

    /// <summary>The copies of the text kept side by side while it is read: the digits found and the number made from them.</summary>
    private const int Copies = 2;

    /// <summary>The characters a double formatted with a fixed number of decimals can need: 309 digits, a point and the decimals.</summary>
    private const int MaxInvariantCharacters = 328;

    /// <summary>The characters a formatted number is built in on the stack; a longer one is built in a rented array.</summary>
    private const int StackNumber = 256;

    /// <summary>The characters a formatted number can have besides its digits, separators and currency: a sign or two parentheses, and a decimal mark.</summary>
    private const int SignAndMarkCharacters = 4;

    /// <summary>The fixed point format of each count of decimals.</summary>
    private static readonly string[] FixedFormats = ["F0", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10"];

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
        char[]? rented = null;
        var space = text.Length <= StackText ? stackalloc char[StackText * Copies] : (rented = ArrayPool<char>.Shared.Rent(text.Length * Copies));
        try
        {
            var digits = space[..CopyDigits(text, space)];
            var normal = space.Slice(digits.Length, digits.Length)[..Normalise(digits, space.Slice(digits.Length, digits.Length))];
            if (!double.TryParse(normal, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return false;
            }

            value = negative ? -value : value;
            return true;
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
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
        Span<char> invariant = stackalloc char[MaxInvariantCharacters];
        _ = rounded.TryFormat(invariant, out var written, FixedFormats[places], CultureInfo.InvariantCulture);
        var capacity = currency.Length + written + (written / GroupSize) + SignAndMarkCharacters;
        char[]? rented = null;
        var buffer = capacity <= StackNumber ? stackalloc char[StackNumber] : (rented = ArrayPool<char>.Shared.Rent(capacity));
        try
        {
            var negative = !(value >= 0 || rounded == 0);
            return new(buffer[..Build(buffer, invariant[..written], separatorStyle, negative, negative && negativeStyle >= FirstParenthesesStyle, currency, currencyFirst)]);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Writes a calculation's result plainly, for a field without a format: invariant digits, no trailing zeros.</summary>
    /// <param name="value">The result.</param>
    /// <returns>The text.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string Plain(double value) => Math.Round(value, MaxDecimals).ToString("0.##########", CultureInfo.InvariantCulture);

    /// <summary>Copies the digits, points and commas of a text.</summary>
    /// <param name="text">The text.</param>
    /// <param name="destination">Receives them; at least as long as the text.</param>
    /// <returns>The characters copied.</returns>
    private static int CopyDigits(string text, Span<char> destination)
    {
        var count = 0;
        foreach (var c in text)
        {
            if (!char.IsAsciiDigit(c) && c is not ('.' or ','))
            {
                continue;
            }

            destination[count] = c;
            count++;
        }

        return count;
    }

    /// <summary>Writes a formatted number: sign or parenthesis, currency, the separated digits, currency, closing parenthesis.</summary>
    /// <param name="buffer">Receives the text.</param>
    /// <param name="invariant">The digits with a point.</param>
    /// <param name="separatorStyle">The separator style.</param>
    /// <param name="negative">Whether the number is shown as negative.</param>
    /// <param name="parentheses">Whether a negative number is shown in parentheses.</param>
    /// <param name="currency">The currency symbol.</param>
    /// <param name="currencyFirst">Whether the symbol comes before the number.</param>
    /// <returns>The characters written.</returns>
    private static int Build(Span<char> buffer, ReadOnlySpan<char> invariant, int separatorStyle, bool negative, bool parentheses, string currency, bool currencyFirst)
    {
        var length = 0;
        if (negative)
        {
            buffer[length] = parentheses ? '(' : '-';
            length++;
        }

        if (currencyFirst)
        {
            currency.CopyTo(buffer[length..]);
            length += currency.Length;
        }

        length += Separate(invariant, separatorStyle, buffer[length..]);
        if (!currencyFirst)
        {
            currency.CopyTo(buffer[length..]);
            length += currency.Length;
        }

        if (parentheses)
        {
            buffer[length] = ')';
            length++;
        }

        return length;
    }

    /// <summary>Applies a separator style to invariant digits such as <c>1234.56</c>.</summary>
    /// <param name="invariant">The digits with a point.</param>
    /// <param name="style">The style.</param>
    /// <param name="destination">Receives the separated text.</param>
    /// <returns>The characters written.</returns>
    private static int Separate(ReadOnlySpan<char> invariant, int style, Span<char> destination)
    {
        var point = invariant.IndexOf('.');
        var whole = point < 0 ? invariant : invariant[..point];
        var fraction = point < 0 ? default : invariant[(point + 1)..];
        var commaDecimal = style >= FirstCommaDecimalStyle;
        var length = style is 0 or FirstCommaDecimalStyle ? Group(whole, commaDecimal ? '.' : ',', destination) : CopyTo(whole, destination);
        if (fraction.IsEmpty)
        {
            return length;
        }

        destination[length] = commaDecimal ? ',' : '.';
        length++;
        fraction.CopyTo(destination[length..]);
        return length + fraction.Length;
    }

    /// <summary>Copies characters to the start of a span.</summary>
    /// <param name="source">The characters.</param>
    /// <param name="destination">Receives them.</param>
    /// <returns>The characters written.</returns>
    private static int CopyTo(ReadOnlySpan<char> source, Span<char> destination)
    {
        source.CopyTo(destination);
        return source.Length;
    }

    /// <summary>Puts a separator between each group of three digits.</summary>
    /// <param name="digits">The whole digits.</param>
    /// <param name="separator">The separator.</param>
    /// <param name="destination">Receives the grouped digits.</param>
    /// <returns>The characters written.</returns>
    private static int Group(ReadOnlySpan<char> digits, char separator, Span<char> destination)
    {
        var length = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            if (i > 0 && (digits.Length - i) % GroupSize == 0)
            {
                destination[length] = separator;
                length++;
            }

            destination[length] = digits[i];
            length++;
        }

        return length;
    }

    /// <summary>
    /// Turns digits with points and commas into invariant digits: the last mark is the decimal point unless it is
    /// followed by exactly three digits and could be a thousands separator.
    /// </summary>
    /// <param name="digits">The digits and marks.</param>
    /// <param name="destination">Receives the invariant number; at least as long as <paramref name="digits"/>.</param>
    /// <returns>The characters written.</returns>
    private static int Normalise(ReadOnlySpan<char> digits, Span<char> destination)
    {
        var lastPoint = digits.LastIndexOf('.');
        var lastComma = digits.LastIndexOf(',');
        var decimalAt = Math.Max(lastPoint, lastComma);
        if (decimalAt < 0)
        {
            return CopyTo(digits, destination);
        }

        var mark = digits[decimalAt];
        var single = digits.Count(mark) == 1;
        var isDecimal = (lastPoint >= 0 && lastComma >= 0) || digits.Length - decimalAt - 1 != GroupSize || (single && mark == '.');
        if (!isDecimal)
        {
            return CopyWithoutMarks(digits, destination);
        }

        var length = CopyWithoutMarks(digits[..decimalAt], destination);
        destination[length] = '.';
        length++;
        return length + CopyTo(digits[(decimalAt + 1)..], destination[length..]);
    }

    /// <summary>Copies digits without their points and commas.</summary>
    /// <param name="digits">The digits and marks.</param>
    /// <param name="destination">Receives the digits.</param>
    /// <returns>The characters written.</returns>
    private static int CopyWithoutMarks(ReadOnlySpan<char> digits, Span<char> destination)
    {
        var length = 0;
        foreach (var c in digits)
        {
            if (c is '.' or ',')
            {
                continue;
            }

            destination[length] = c;
            length++;
        }

        return length;
    }
}
