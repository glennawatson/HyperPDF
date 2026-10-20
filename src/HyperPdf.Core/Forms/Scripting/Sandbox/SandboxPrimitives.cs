// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace HyperPdf.Core.Forms.Scripting.Sandbox;

/// <summary>The properties and methods of text and numbers in a form script, and the global functions such as <c>parseInt</c>.</summary>
internal static class SandboxPrimitives
{
    /// <summary>The most decimals <c>toFixed</c> writes.</summary>
    private const int MaxDecimals = 20;

    /// <summary>Reads a property of a value that is not an object.</summary>
    /// <param name="target">The value.</param>
    /// <param name="name">The property.</param>
    /// <returns>The value; undefined when there is none.</returns>
    internal static SandboxValue Get(SandboxValue target, string name) =>
        target.IsText && name == "length" ? SandboxValue.Of(((string)target.Reference!).Length) : SandboxValue.Undefined;

    /// <summary>Calls a method of a value that is not an object.</summary>
    /// <param name="target">The value.</param>
    /// <param name="name">The method.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The result.</returns>
    /// <exception cref="SandboxException">There is no such method.</exception>
    internal static SandboxValue Call(SandboxValue target, string name, SandboxValue[] arguments) => name switch
    {
        "toString" => SandboxValue.Of(target.ToText()),
        "toFixed" => SandboxValue.Of(ToFixed(target.ToNumber(), Number(arguments, 0))),
        _ when target.IsText => CallText((string)target.Reference!, name, arguments),
        _ => throw new SandboxException($"The script calls {name}, which form scripts cannot use."),
    };

    /// <summary>Calls a global function such as <c>parseInt</c>.</summary>
    /// <param name="name">The function.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The result.</returns>
    /// <exception cref="SandboxException">There is no such function.</exception>
    internal static SandboxValue CallGlobal(string name, SandboxValue[] arguments)
    {
        var first = arguments.Length > 0 ? arguments[0] : SandboxValue.Undefined;
        return name switch
        {
            "parseFloat" => SandboxValue.Of(ParseLeading(first.ToText(), allowPoint: true)),
            "parseInt" => SandboxValue.Of(Math.Truncate(ParseLeading(first.ToText(), allowPoint: false))),
            "Number" => SandboxValue.Of(first.ToNumber()),
            "String" => SandboxValue.Of(first.ToText()),
            "Boolean" => SandboxValue.Of(first.IsTruthy),
            "isNaN" => SandboxValue.Of(double.IsNaN(first.ToNumber())),
            _ => throw new SandboxException($"The script calls {name}, which form scripts cannot use."),
        };
    }

    /// <summary>Gets one character as text.</summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The position.</param>
    /// <returns>The character, or empty text when the position is outside the text.</returns>
    internal static string CharAt(string text, int index) => (uint)index < (uint)text.Length ? text[index].ToString() : string.Empty;

    /// <summary>Calls a method of text.</summary>
    /// <param name="text">The text.</param>
    /// <param name="name">The method.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The result.</returns>
    /// <exception cref="SandboxException">There is no such method.</exception>
    private static SandboxValue CallText(string text, string name, SandboxValue[] arguments) => name switch
    {
        "toUpperCase" => SandboxValue.Of(text.ToUpperInvariant()),
        "toLowerCase" => SandboxValue.Of(text.ToLowerInvariant()),
        "trim" => SandboxValue.Of(text.Trim()),
        "indexOf" => SandboxValue.Of(text.IndexOf(arguments.Length > 0 ? arguments[0].ToText() : "undefined", StringComparison.Ordinal)),
        "charAt" => SandboxValue.Of(CharAt(text, (int)Number(arguments, 0))),
        "substring" => SandboxValue.Of(Substring(text, Number(arguments, 0), arguments.Length > 1 ? Number(arguments, 1) : text.Length)),
        "split" => Split(text, arguments.Length > 0 ? arguments[0].ToText() : string.Empty),
        _ => throw new SandboxException($"The script calls {name}, which form scripts cannot use."),
    };

    /// <summary>Gets an argument as a number, with not a number counting as zero.</summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="index">The position.</param>
    /// <returns>The number.</returns>
    private static double Number(SandboxValue[] arguments, int index)
    {
        var number = index < arguments.Length ? arguments[index].ToNumber() : 0;
        return double.IsNaN(number) ? 0 : number;
    }

    /// <summary>Writes a number with a fixed count of decimals.</summary>
    /// <param name="number">The number.</param>
    /// <param name="decimals">The decimals.</param>
    /// <returns>The text.</returns>
    private static string ToFixed(double number, double decimals)
    {
        var places = (int)Math.Clamp(decimals, 0, MaxDecimals);
        return double.IsFinite(number)
            ? Math.Round(number, places, MidpointRounding.AwayFromZero).ToString(string.Create(CultureInfo.InvariantCulture, $"F{places}"), CultureInfo.InvariantCulture)
            : SandboxValue.NumberToText(number);
    }

    /// <summary>Reads the number at the start of text, ignoring what follows it.</summary>
    /// <param name="text">The text.</param>
    /// <param name="allowPoint">Whether a decimal point and exponent belong to the number.</param>
    /// <returns>The number; not a number when the text does not start with one.</returns>
    private static double ParseLeading(string text, bool allowPoint)
    {
        var span = text.AsSpan().TrimStart();
        var length = 0;
        while (length < span.Length && BelongsToNumber(span[length], length == 0, allowPoint))
        {
            length++;
        }

        return double.TryParse(span[..length], NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : double.NaN;
    }

    /// <summary>Determines whether a character can be part of the number at the start of text.</summary>
    /// <param name="c">The character.</param>
    /// <param name="first">Whether it is the first character, which may be a sign.</param>
    /// <param name="allowPoint">Whether a decimal point and exponent belong to the number.</param>
    /// <returns><see langword="true"/> when it can.</returns>
    private static bool BelongsToNumber(char c, bool first, bool allowPoint) =>
        char.IsAsciiDigit(c) || (first && c is '-' or '+') || (allowPoint && c is '.' or 'e' or 'E');

    /// <summary>Gets part of text between two positions, as JavaScript's <c>substring</c> does.</summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The first position.</param>
    /// <param name="end">The last position.</param>
    /// <returns>The part.</returns>
    private static string Substring(string text, double start, double end)
    {
        var from = (int)Math.Clamp(Math.Min(start, end), 0, text.Length);
        var to = (int)Math.Clamp(Math.Max(start, end), 0, text.Length);
        return text[from..to];
    }

    /// <summary>Splits text into an array.</summary>
    /// <param name="text">The text.</param>
    /// <param name="separator">The separator; empty splits into characters.</param>
    /// <returns>The array.</returns>
    private static SandboxValue Split(string text, string separator)
    {
        var array = new SandboxArray();
        var parts = separator.Length == 0 ? SplitCharacters(text) : text.Split(separator);
        for (var i = 0; i < parts.Length; i++)
        {
            array.SetAt(i, SandboxValue.Of(parts[i]));
        }

        return SandboxValue.Of(array);
    }

    /// <summary>Splits text into one-character strings.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The characters.</returns>
    private static string[] SplitCharacters(string text)
    {
        var parts = new string[text.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            parts[i] = text[i].ToString();
        }

        return parts;
    }
}
