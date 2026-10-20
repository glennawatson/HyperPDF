// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Forms.Scripting.Sandbox;

/// <summary>
/// Writes text as <c>util.printf</c> does: <c>%d</c>, <c>%f</c>, <c>%s</c>, <c>%x</c>, widths, precisions and the <c>,0</c> to
/// <c>,3</c> thousands styles. Numbers are written into stack space, so the only allocation is the result.
/// </summary>
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

    /// <summary>The characters of stack space for a number; a longer number is written on the heap.</summary>
    private const int Scratch = 384;

    /// <summary>The most characters a number can gain from thousands separators and a sign.</summary>
    private const int Growth = 140;

    /// <summary>The precisions whose .NET format is made once.</summary>
    private const int CachedFormats = 21;

    /// <summary>The characters that can come before a width: the flags.</summary>
    private static readonly SearchValues<char> FlagCharacters = SearchValues.Create("-0+ ,");

    /// <summary>The .NET fixed-point formats for the precisions 0 to 20.</summary>
    private static readonly string[] FixedFormats = MakeFormats();

    /// <summary>Writes the arguments into the format.</summary>
    /// <param name="format">The format text.</param>
    /// <param name="arguments">The values to write.</param>
    /// <returns>The text.</returns>
    internal static string Format(string format, ReadOnlySpan<SandboxValue> arguments)
    {
        var output = new DefaultInterpolatedStringHandler(format.Length, 0, CultureInfo.InvariantCulture);
        var next = 0;
        var at = 0;
        while (at < format.Length)
        {
            if (format[at] != '%')
            {
                output.AppendFormatted(format[at]);
                at++;
                continue;
            }

            var spec = ReadSpec(format, ref at);
            var argument = spec.Conversion != '%' && next < arguments.Length ? arguments[next] : SandboxValue.Undefined;
            next += spec.Conversion == '%' ? 0 : 1;
            Write(ref output, spec, argument);
        }

        return output.ToStringAndClear();
    }

    /// <summary>Makes the fixed-point formats once.</summary>
    /// <returns>The formats, by precision.</returns>
    private static string[] MakeFormats()
    {
        var formats = new string[CachedFormats];
        for (var i = 0; i < formats.Length; i++)
        {
            formats[i] = string.Create(CultureInfo.InvariantCulture, $"F{i}");
        }

        return formats;
    }

    /// <summary>Gets the .NET fixed-point format for a precision.</summary>
    /// <param name="precision">The decimals.</param>
    /// <returns>The format.</returns>
    private static string FixedFormat(int precision) => precision < FixedFormats.Length ? FixedFormats[precision] : string.Create(CultureInfo.InvariantCulture, $"F{precision}");

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

    /// <summary>Writes one value as its conversion asks, padded to the width.</summary>
    /// <param name="output">Receives the text.</param>
    /// <param name="spec">The conversion.</param>
    /// <param name="argument">The value.</param>
    private static void Write(ref DefaultInterpolatedStringHandler output, PrintfSpec spec, SandboxValue argument)
    {
        Span<char> plain = stackalloc char[Scratch];
        Span<char> grouped = stackalloc char[Scratch + Growth];
        var body = Body(spec, argument, plain, grouped);
        Pad(ref output, spec, body);
    }

    /// <summary>Makes the text of one value, before padding.</summary>
    /// <param name="spec">The conversion.</param>
    /// <param name="argument">The value.</param>
    /// <param name="plain">Space for the number.</param>
    /// <param name="grouped">Space for the number with thousands separators.</param>
    /// <returns>The text, in one of the spaces or the value's own text.</returns>
    private static ReadOnlySpan<char> Body(PrintfSpec spec, SandboxValue argument, Span<char> plain, Span<char> grouped)
    {
        switch (spec.Conversion)
        {
            case 'd' or 'i':
            {
                return Number(spec, Math.Truncate(argument.ToNumber()), "F0", plain, grouped);
            }

            case 'f':
            {
                return Number(spec, argument.ToNumber(), FixedFormat(spec.Precision < 0 ? DefaultPrecision : spec.Precision), plain, grouped);
            }

            case 'x' or 'X':
            {
                return ((long)Math.Truncate(argument.ToNumber())).TryFormat(plain, out var written, spec.Conversion == 'x' ? "x" : "X", CultureInfo.InvariantCulture)
                    ? plain[..written]
                    : default;
            }

            case 's':
            {
                var text = argument.ToText();
                return spec.Precision >= 0 && spec.Precision < text.Length ? text.AsSpan(0, spec.Precision) : text;
            }

            case '%':
            {
                return "%";
            }

            default:
            {
                return default;
            }
        }
    }

    /// <summary>Writes a number in a fixed-point format, with a sign and thousands separators when asked.</summary>
    /// <param name="spec">The conversion.</param>
    /// <param name="value">The number.</param>
    /// <param name="format">The .NET format.</param>
    /// <param name="plain">Space for the number.</param>
    /// <param name="grouped">Space for the number with thousands separators.</param>
    /// <returns>The text.</returns>
    private static ReadOnlySpan<char> Number(PrintfSpec spec, double value, string format, Span<char> plain, Span<char> grouped)
    {
        // The number starts one place in, so a plus sign can go in front of it.
        if (!value.TryFormat(plain[1..], out var written, format, CultureInfo.InvariantCulture))
        {
            return value.ToString(format, CultureInfo.InvariantCulture);
        }

        var start = spec.Plus && plain[1] != '-' ? 0 : 1;
        plain[0] = '+';
        var number = plain.Slice(start, written + 1 - start);
        return spec.Separator is >= CommaPointStyle and <= NoneCommaStyle && number.Length + Growth <= grouped.Length ? Separate(spec.Separator, number, grouped) : number;
    }

    /// <summary>Applies a <c>,0</c> to <c>,3</c> thousands style to a plain number.</summary>
    /// <param name="style">The style, 0 to 3.</param>
    /// <param name="number">The number written with a point for decimals, perhaps with a sign.</param>
    /// <param name="destination">Space for the result.</param>
    /// <returns>The number with the style's separators.</returns>
    private static ReadOnlySpan<char> Separate(int style, ReadOnlySpan<char> number, Span<char> destination)
    {
        var point = number.IndexOf('.');
        var written = GroupWhole(style, point < 0 ? number : number[..point], destination);
        if (point >= 0)
        {
            destination[written] = style >= PointCommaStyle ? ',' : '.';
            written++;
            number[(point + 1)..].CopyTo(destination[written..]);
            written += number.Length - point - 1;
        }

        return destination[..written];
    }

    /// <summary>Writes the whole part of a number, with a separator between each group of three digits when the style asks.</summary>
    /// <param name="style">The style, 0 to 3.</param>
    /// <param name="whole">The whole part, perhaps with a sign.</param>
    /// <param name="destination">Space for the result.</param>
    /// <returns>The characters written.</returns>
    private static int GroupWhole(int style, ReadOnlySpan<char> whole, Span<char> destination)
    {
        var signs = SignLength(whole);
        var groups = style is CommaPointStyle or PointCommaStyle;
        var thousands = style == CommaPointStyle ? ',' : '.';
        var written = 0;
        for (var i = 0; i < whole.Length; i++)
        {
            if (groups && i > signs && (whole.Length - i) % GroupSize == 0)
            {
                destination[written] = thousands;
                written++;
            }

            destination[written] = whole[i];
            written++;
        }

        return written;
    }

    /// <summary>Writes text padded to the width.</summary>
    /// <param name="output">Receives the text.</param>
    /// <param name="spec">The conversion.</param>
    /// <param name="body">The text.</param>
    private static void Pad(ref DefaultInterpolatedStringHandler output, PrintfSpec spec, scoped ReadOnlySpan<char> body)
    {
        var missing = Math.Max(spec.Width - body.Length, 0);
        if (spec.Left)
        {
            output.AppendFormatted(body);
            Fill(ref output, ' ', missing);
            return;
        }

        // Zeros go after a sign, never before it.
        var zeros = spec.Zero && spec.Conversion != 's';
        var sign = zeros ? SignLength(body) : 0;
        output.AppendFormatted(body[..sign]);
        Fill(ref output, zeros ? '0' : ' ', missing);
        output.AppendFormatted(body[sign..]);
    }

    /// <summary>Gets the length of the sign at the start of a number.</summary>
    /// <param name="number">The text.</param>
    /// <returns>1 when the text starts with a sign, otherwise 0.</returns>
    private static int SignLength(ReadOnlySpan<char> number) => !number.IsEmpty && number[0] is '-' or '+' ? 1 : 0;

    /// <summary>Writes a character several times.</summary>
    /// <param name="output">Receives the text.</param>
    /// <param name="c">The character.</param>
    /// <param name="count">How many times.</param>
    private static void Fill(ref DefaultInterpolatedStringHandler output, char c, int count)
    {
        for (var i = 0; i < count; i++)
        {
            output.AppendFormatted(c);
        }
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
