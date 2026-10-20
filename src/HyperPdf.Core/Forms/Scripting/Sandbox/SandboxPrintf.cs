// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace HyperPdf.Core.Forms.Scripting.Sandbox;

/// <summary>Writes text as <c>util.printf</c> does: <c>%d</c>, <c>%f</c>, <c>%s</c>, <c>%x</c>, widths, precisions and the <c>,0</c> to <c>,3</c> thousands styles.</summary>
internal static class SandboxPrintf
{
    /// <summary>The digits in a group of thousands.</summary>
    private const int GroupSize = 3;

    /// <summary>The precision of <c>%f</c> when none is given.</summary>
    private const int DefaultPrecision = 6;

    /// <summary>The separator style with a comma between thousands and a point for decimals.</summary>
    private const int CommaPointStyle = 0;

    /// <summary>The separator style with a point between thousands and a comma for decimals.</summary>
    private const int PointCommaStyle = 2;

    /// <summary>The separator style with no thousands separator and a comma for decimals.</summary>
    private const int NoneCommaStyle = 3;

    /// <summary>The largest width or precision read, so a script cannot ask for huge text.</summary>
    private const int MaxWidth = 200;

    /// <summary>The base of the numbers in a format.</summary>
    private const int DecimalBase = 10;

    /// <summary>The characters that can come before a width: the flags.</summary>
    private static readonly SearchValues<char> FlagCharacters = SearchValues.Create("-0+ ,");

    /// <summary>Writes the arguments into the format.</summary>
    /// <param name="format">The format text.</param>
    /// <param name="arguments">The values to write.</param>
    /// <returns>The text.</returns>
    internal static string Format(string format, ReadOnlySpan<SandboxValue> arguments)
    {
        var text = new StringBuilder(format.Length);
        var next = 0;
        var at = 0;
        while (at < format.Length)
        {
            if (format[at] != '%')
            {
                _ = text.Append(format[at]);
                at++;
                continue;
            }

            var spec = ReadSpec(format, ref at);
            var argument = spec.Conversion != '%' && next < arguments.Length ? arguments[next] : SandboxValue.Undefined;
            next += spec.Conversion == '%' ? 0 : 1;
            _ = text.Append(Pad(spec, Convert(spec, argument)));
        }

        return text.ToString();
    }

    /// <summary>Reads one conversion, from its <c>%</c> to its letter.</summary>
    /// <param name="format">The format text.</param>
    /// <param name="at">The position of the <c>%</c>, moved past the conversion.</param>
    /// <returns>The conversion.</returns>
    private static PrintfSpec ReadSpec(string format, ref int at)
    {
        at++;
        var spec = new PrintfSpec(false, false, false, -1, 0, -1, '%');
        while (at < format.Length && FlagCharacters.Contains(format[at]))
        {
            spec = AddFlag(spec, format, ref at);
        }

        var width = ReadNumber(format, ref at);
        var precision = at < format.Length && format[at] == '.' ? ReadPrecision(format, ref at) : -1;
        var conversion = at < format.Length ? format[at] : '%';
        at++;
        return spec with { Width = width, Precision = precision, Conversion = conversion };
    }

    /// <summary>Reads one flag: <c>-</c>, <c>0</c>, <c>+</c>, a space, or a comma with its style digit.</summary>
    /// <param name="spec">The conversion so far.</param>
    /// <param name="format">The format text.</param>
    /// <param name="at">The position of the flag, moved past it.</param>
    /// <returns>The conversion with the flag set.</returns>
    private static PrintfSpec AddFlag(PrintfSpec spec, string format, ref int at)
    {
        var flag = format[at];
        at++;
        if (flag == ',' && at < format.Length && char.IsAsciiDigit(format[at]))
        {
            var style = format[at] - '0';
            at++;
            return spec with { Separator = style };
        }

        return flag switch
        {
            '-' => spec with { Left = true },
            '0' => spec with { Zero = true },
            '+' => spec with { Plus = true },
            _ => spec,
        };
    }

    /// <summary>Reads the number after a <c>.</c>.</summary>
    /// <param name="format">The format text.</param>
    /// <param name="at">The position of the point, moved past the number.</param>
    /// <returns>The precision.</returns>
    private static int ReadPrecision(string format, ref int at)
    {
        at++;
        return Math.Max(ReadNumber(format, ref at), 0);
    }

    /// <summary>Reads a whole number.</summary>
    /// <param name="format">The format text.</param>
    /// <param name="at">The position, moved past the digits.</param>
    /// <returns>The number; zero when there are no digits.</returns>
    private static int ReadNumber(string format, ref int at)
    {
        var value = 0;
        while (at < format.Length && char.IsAsciiDigit(format[at]))
        {
            value = Math.Min((value * DecimalBase) + (format[at] - '0'), MaxWidth);
            at++;
        }

        return value;
    }

    /// <summary>Writes one value as its conversion asks.</summary>
    /// <param name="spec">The conversion.</param>
    /// <param name="argument">The value.</param>
    /// <returns>The text, without padding.</returns>
    private static string Convert(PrintfSpec spec, SandboxValue argument) => spec.Conversion switch
    {
        'd' or 'i' => Signed(spec, Separate(spec, Math.Truncate(argument.ToNumber()).ToString("F0", CultureInfo.InvariantCulture))),
        'f' => Signed(spec, Separate(spec, argument.ToNumber().ToString(FixedFormat(spec.Precision < 0 ? DefaultPrecision : spec.Precision), CultureInfo.InvariantCulture))),
        'x' => ((long)Math.Truncate(argument.ToNumber())).ToString("x", CultureInfo.InvariantCulture),
        'X' => ((long)Math.Truncate(argument.ToNumber())).ToString("X", CultureInfo.InvariantCulture),
        's' => spec.Precision >= 0 && spec.Precision < argument.ToText().Length ? argument.ToText()[..spec.Precision] : argument.ToText(),
        '%' => "%",
        _ => string.Empty,
    };

    /// <summary>Makes the .NET format of a number with a fixed count of decimals.</summary>
    /// <param name="precision">The decimals.</param>
    /// <returns>The format.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string FixedFormat(int precision) => string.Create(CultureInfo.InvariantCulture, $"F{precision}");

    /// <summary>Puts a plus sign in front of a non-negative number when the conversion asks.</summary>
    /// <param name="spec">The conversion.</param>
    /// <param name="digits">The number as text.</param>
    /// <returns>The text.</returns>
    private static string Signed(PrintfSpec spec, string digits) => spec.Plus && !digits.StartsWith('-') ? $"+{digits}" : digits;

    /// <summary>Applies a <c>,0</c> to <c>,3</c> thousands style to a plain number.</summary>
    /// <param name="spec">The conversion.</param>
    /// <param name="number">The number written with a point for decimals.</param>
    /// <returns>The number with the style's separators.</returns>
    private static string Separate(PrintfSpec spec, string number)
    {
        if (spec.Separator < 0 || spec.Separator > NoneCommaStyle)
        {
            return number;
        }

        var point = number.IndexOf('.', StringComparison.Ordinal);
        var whole = point < 0 ? number : number[..point];
        var fraction = point < 0 ? string.Empty : number[(point + 1)..];
        var groups = spec.Separator is CommaPointStyle or PointCommaStyle ? Group(whole, spec.Separator == CommaPointStyle ? ',' : '.') : whole;
        var decimalMark = spec.Separator >= PointCommaStyle ? ',' : '.';
        return fraction.Length == 0 ? groups : groups + decimalMark + fraction;
    }

    /// <summary>Puts a separator between each group of three digits.</summary>
    /// <param name="whole">The whole part, with an optional minus sign.</param>
    /// <param name="separator">The separator.</param>
    /// <returns>The grouped text.</returns>
    private static string Group(string whole, char separator)
    {
        var sign = whole.StartsWith('-') ? 1 : 0;
        var text = new StringBuilder(whole.Length + (whole.Length / GroupSize));
        for (var i = 0; i < whole.Length; i++)
        {
            if (i > sign && (whole.Length - i) % GroupSize == 0)
            {
                _ = text.Append(separator);
            }

            _ = text.Append(whole[i]);
        }

        return text.ToString();
    }

    /// <summary>Pads a converted value to the width.</summary>
    /// <param name="spec">The conversion.</param>
    /// <param name="text">The converted value.</param>
    /// <returns>The padded text.</returns>
    private static string Pad(PrintfSpec spec, string text)
    {
        if (text.Length >= spec.Width)
        {
            return text;
        }

        return spec.Left ? text.PadRight(spec.Width) : text.PadLeft(spec.Width, spec.Zero && spec.Conversion != 's' ? '0' : ' ');
    }

    /// <summary>One conversion of a format: <c>%-08.2f</c> and so on.</summary>
    /// <param name="Left">Whether the text is padded on the right.</param>
    /// <param name="Zero">Whether numbers are padded with zeros.</param>
    /// <param name="Plus">Whether a sign is always written.</param>
    /// <param name="Separator">The thousands style from 0 to 3, or -1 for none.</param>
    /// <param name="Width">The smallest width.</param>
    /// <param name="Precision">The decimals of a number or the length of a string, or -1.</param>
    /// <param name="Conversion">The letter.</param>
    private readonly record struct PrintfSpec(bool Left, bool Zero, bool Plus, int Separator, int Width, int Precision, char Conversion);
}
