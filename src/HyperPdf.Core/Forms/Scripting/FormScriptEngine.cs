// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace HyperPdf.Core.Forms.Scripting;

/// <summary>
/// Runs the recognised form scripts: formats a value for display, checks what is typed, validates a value against its
/// range, and calculates fields from others. Each is plain C#, so a form gets the behaviour its author meant without
/// any of its JavaScript being executed.
/// </summary>
public static class FormScriptEngine
{
    /// <summary>The default decimals of a number format.</summary>
    private const int DefaultDecimals = 2;

    /// <summary>The date format used when a date script gives none.</summary>
    private const string DefaultDateFormat = "mm/dd/yyyy";

    /// <summary>The time format used when a time script gives none.</summary>
    private const string DefaultTimeFormat = "HH:MM";

    /// <summary>The position of the negative style of <c>AFNumber_Format</c>.</summary>
    private const int NegativeArgument = 2;

    /// <summary>A percentage is the value times a hundred.</summary>
    private const double Hundred = 100;

    /// <summary>The <c>AFNumber_Format</c> argument positions: decimals, separator style, negative style, unused, currency, currency first.</summary>
    private const int CurrencyArgument = 4;

    /// <summary>The position of the currency-first flag.</summary>
    private const int CurrencyFirstArgument = 5;

    /// <summary>The position of the lower limit of <c>AFRange_Validate</c>.</summary>
    private const int LowerLimitArgument = 1;

    /// <summary>The position of the upper limit's flag.</summary>
    private const int UpperFlagArgument = 2;

    /// <summary>The position of the upper limit.</summary>
    private const int UpperLimitArgument = 3;

    /// <summary>The characters a number may hold besides currency symbols.</summary>
    private static readonly SearchValues<char> NumberCharacters = SearchValues.Create("0123456789 .,-+()%\u00A0");

    /// <summary>The patterns of <c>AFSpecial_Format</c>: zip code, zip+4, phone number and social security number; # is a digit.</summary>
    private static readonly string[] SpecialPatterns = ["#####", "#####-####", "(###) ###-####", "###-##-####"];

    /// <summary>Formats a value for display, as the field's format script asks.</summary>
    /// <param name="format">The format script.</param>
    /// <param name="value">The value.</param>
    /// <returns>The formatted value, or the value unchanged when it cannot be formatted.</returns>
    public static string Format(FormScript format, string value)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(value);
        return value.Length == 0 ? value : format.Function switch
        {
            FormScriptFunction.Number when FormNumbers.TryParse(value, out var number) => FormatNumber(format, number),
            FormScriptFunction.Percent when FormNumbers.TryParse(value.Replace("%", string.Empty, StringComparison.Ordinal), out var number) =>
                FormNumbers.Format(number * Hundred, (int)format.Number(0, DefaultDecimals), (int)format.Number(1, 0), 0, "%", false),
            FormScriptFunction.Date when FormDates.TryParse(value, format.Text(0, DefaultDateFormat), out var date) => FormDates.Format(date, format.Text(0, DefaultDateFormat)),
            FormScriptFunction.Time when FormDates.TryParseTime(value, format.Text(0, DefaultTimeFormat), out var time) => FormDates.Format(time, format.Text(0, DefaultTimeFormat)),
            FormScriptFunction.Special => FormatSpecial((int)format.Number(0, 0), value),
            _ => value,
        };
    }

    /// <summary>Reads a date as <c>AFParseDateEx</c> does: in the given format first, then in common forms.</summary>
    /// <param name="text">The text.</param>
    /// <param name="format">The PDF form date format, for example <c>dd/mm/yyyy</c>.</param>
    /// <param name="value">The date.</param>
    /// <returns><see langword="true"/> when the text is a date.</returns>
    public static bool TryParseDate(string? text, string format, out DateTimeOffset value)
    {
        ArgumentNullException.ThrowIfNull(format);
        var parsed = FormDates.TryParse(text, format, out var date);

        // Form dates carry no zone, so the value is the date as written, at midnight or the time given.
        value = parsed ? new(DateTime.SpecifyKind(date, DateTimeKind.Unspecified), TimeSpan.Zero) : default;
        return parsed;
    }

    /// <summary>
    /// Merges a typed change into a field's value, as <c>AFMergeChange</c> does: the change replaces the selected text, or
    /// goes at the caret when nothing is selected. The positions are clamped to the value.
    /// </summary>
    /// <param name="value">The value before the change.</param>
    /// <param name="selectionStart">The start of the selection.</param>
    /// <param name="selectionEnd">The end of the selection.</param>
    /// <param name="change">The typed text.</param>
    /// <returns>The value with the change in it.</returns>
    public static string MergeChange(string value, int selectionStart, int selectionEnd, string change)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(change);
        var start = Math.Clamp(Math.Min(selectionStart, selectionEnd), 0, value.Length);
        var end = Math.Clamp(Math.Max(selectionStart, selectionEnd), start, value.Length);
        return string.Concat(value.AsSpan(0, start), change, value.AsSpan(end));
    }

    /// <summary>Checks a value typed into a field, as its keystroke script asks.</summary>
    /// <param name="keystroke">The keystroke script.</param>
    /// <param name="value">The whole value typed.</param>
    /// <returns><see langword="true"/> when it is acceptable; unknown scripts accept everything.</returns>
    public static bool Accepts(FormScript keystroke, string value)
    {
        ArgumentNullException.ThrowIfNull(keystroke);
        ArgumentNullException.ThrowIfNull(value);
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return keystroke.Function switch
        {
            FormScriptFunction.Number or FormScriptFunction.Percent => FormNumbers.TryParse(value.Replace("%", string.Empty, StringComparison.Ordinal), out _) && IsNumeric(value),
            FormScriptFunction.Date => FormDates.TryParse(value, keystroke.Text(0, DefaultDateFormat), out _),
            FormScriptFunction.Time => FormDates.TryParseTime(value, keystroke.Text(0, DefaultTimeFormat), out _),
            FormScriptFunction.Special => Digits(value).Length == Digits(SpecialPattern((int)keystroke.Number(0, 0))).Length,
            _ => true,
        };
    }

    /// <summary>Validates a value, as the field's validate script asks.</summary>
    /// <param name="validate">The validate script.</param>
    /// <param name="value">The value.</param>
    /// <param name="message">Why the value is refused, or empty.</param>
    /// <returns><see langword="true"/> when the value is valid; unknown scripts accept everything.</returns>
    public static bool Validate(FormScript validate, string value, out string message)
    {
        ArgumentNullException.ThrowIfNull(validate);
        message = string.Empty;
        if (validate.Function != FormScriptFunction.Range || !FormNumbers.TryParse(value, out var number))
        {
            return true;
        }

        var hasLower = validate.Flag(0);
        var hasUpper = validate.Flag(UpperFlagArgument);
        var lower = validate.Number(LowerLimitArgument, double.MinValue);
        var upper = validate.Number(UpperLimitArgument, double.MaxValue);
        if ((!hasLower || number >= lower) && (!hasUpper || number <= upper))
        {
            return true;
        }

        message = (hasLower, hasUpper) switch
        {
            (true, true) => UiText.Format("The value must be between {0} and {1}.", lower, upper),
            (true, false) => UiText.Format("The value must be at least {0}.", lower),
            _ => UiText.Format("The value must be at most {0}.", upper),
        };
        return false;
    }

    /// <summary>Calculates a field's value from others, as its calculate script asks.</summary>
    /// <param name="calculate">The calculate script.</param>
    /// <param name="valueOf">Gets another field's value by name, or <see langword="null"/> when there is no such field.</param>
    /// <param name="result">The result.</param>
    /// <returns><see langword="true"/> when the script is understood and gives a number.</returns>
    public static bool TryCalculate(FormScript calculate, Func<string, string?> valueOf, out double result)
    {
        ArgumentNullException.ThrowIfNull(calculate);
        ArgumentNullException.ThrowIfNull(valueOf);
        result = 0;
        return calculate.Function switch
        {
            FormScriptFunction.Simple => TrySimple(calculate, valueOf, out result),
            FormScriptFunction.Expression => FormExpression.TryEvaluate(calculate.Expression, name => FormNumbers.TryParse(valueOf(name), out var value) ? value : 0, out result),
            _ => false,
        };
    }

    /// <summary>Writes a number as <c>AFNumber_Format</c>'s arguments ask.</summary>
    /// <param name="format">The format script.</param>
    /// <param name="number">The number.</param>
    /// <returns>The text.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string FormatNumber(FormScript format, double number) =>
        FormNumbers.Format(
            number,
            (int)format.Number(0, DefaultDecimals),
            (int)format.Number(1, 0),
            (int)format.Number(NegativeArgument, 0),
            format.Text(CurrencyArgument, string.Empty),
            format.Flag(CurrencyFirstArgument));

    /// <summary>Runs <c>AFSimple_Calculate</c>: SUM, AVG, PRD, MIN or MAX of the named fields, empty fields counting as zero.</summary>
    /// <param name="calculate">The script.</param>
    /// <param name="valueOf">Gets a field's value.</param>
    /// <param name="result">The result.</param>
    /// <returns><see langword="true"/> when the operation is known and there are fields.</returns>
    private static bool TrySimple(FormScript calculate, Func<string, string?> valueOf, out double result)
    {
        result = 0;
        if (calculate.Fields.Count == 0)
        {
            return false;
        }

        double sum = 0;
        double product = 1;
        var min = double.MaxValue;
        var max = double.MinValue;
        foreach (var name in calculate.Fields)
        {
            var value = FormNumbers.TryParse(valueOf(name), out var parsed) ? parsed : 0;
            sum += value;
            product *= value;
            min = Math.Min(min, value);
            max = Math.Max(max, value);
        }

        var operation = calculate.Text(0, string.Empty).ToUpperInvariant();
        result = operation switch
        {
            "SUM" => sum,
            "AVG" => sum / calculate.Fields.Count,
            "PRD" => product,
            "MIN" => min,
            "MAX" => max,
            _ => double.NaN,
        };
        return !double.IsNaN(result);
    }

    /// <summary>Writes digits into a special format: zip code, zip+4, phone or social security number.</summary>
    /// <param name="style">The style, 0 to 3.</param>
    /// <param name="value">The value.</param>
    /// <returns>The formatted value, or the value unchanged when it has the wrong number of digits.</returns>
    private static string FormatSpecial(int style, string value)
    {
        var pattern = SpecialPattern(style);
        var digits = Digits(value);
        if (digits.Length != Digits(pattern).Length)
        {
            return value;
        }

        var builder = new StringBuilder(pattern.Length);
        var next = 0;
        foreach (var c in pattern)
        {
            if (c != '#')
            {
                _ = builder.Append(c);
                continue;
            }

            _ = builder.Append(digits[next]);
            next++;
        }

        return builder.ToString();
    }

    /// <summary>Gets a special format's pattern, the zip code for an unknown style.</summary>
    /// <param name="style">The style.</param>
    /// <returns>The pattern.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string SpecialPattern(int style) => SpecialPatterns[(uint)style < (uint)SpecialPatterns.Length ? style : 0];

    /// <summary>Keeps only the digits, and the # placeholders of a pattern.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The digits.</returns>
    private static string Digits(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (char.IsAsciiDigit(c) || c == '#')
            {
                _ = builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>Determines whether text holds only what a number may: digits, separators, a sign, currency or parentheses.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when it could be a number.</returns>
    private static bool IsNumeric(string text)
    {
        foreach (var c in text)
        {
            if (!NumberCharacters.Contains(c) && char.GetUnicodeCategory(c) != UnicodeCategory.CurrencySymbol)
            {
                return false;
            }
        }

        return true;
    }
}
