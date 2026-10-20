// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Forms.Scripting.Sandbox;

/// <summary>A value of a form script.</summary>
/// <param name="Kind">What kind of value it is.</param>
/// <param name="Number">The number, or 1 or 0 for a boolean.</param>
/// <param name="Reference">The text of a string, or the object of an object value.</param>
[DebuggerDisplay("SandboxValue: {Kind}")]
internal readonly record struct SandboxValue(SandboxValueKind Kind, double Number, object? Reference)
{
    /// <summary>The largest whole number written without an exponent.</summary>
    private const double MaxPlainInteger = 1e15;

    /// <summary>Gets the value that stands for nothing.</summary>
    internal static SandboxValue Undefined { get; } = new(SandboxValueKind.Undefined, double.NaN, null);

    /// <summary>Gets the null value.</summary>
    internal static SandboxValue Null { get; } = new(SandboxValueKind.Null, 0, null);

    /// <summary>Gets the true value.</summary>
    internal static SandboxValue True { get; } = new(SandboxValueKind.Boolean, 1, null);

    /// <summary>Gets the false value.</summary>
    internal static SandboxValue False { get; } = new(SandboxValueKind.Boolean, 0, null);

    /// <summary>Gets a value indicating whether the value is text.</summary>
    internal bool IsText => Kind == SandboxValueKind.String;

    /// <summary>Gets a value indicating whether the value counts as true in a condition.</summary>
    internal bool IsTruthy => Kind switch
    {
        SandboxValueKind.Boolean => Number is not 0,
        SandboxValueKind.Number => Number is not 0 && !double.IsNaN(Number),
        SandboxValueKind.String => ((string)Reference!).Length > 0,
        SandboxValueKind.Object => true,
        _ => false,
    };

    /// <summary>Makes a number value.</summary>
    /// <param name="number">The number.</param>
    /// <returns>The value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SandboxValue Of(double number) => new(SandboxValueKind.Number, number, null);

    /// <summary>Makes a boolean value.</summary>
    /// <param name="flag">The flag.</param>
    /// <returns>The value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SandboxValue Of(bool flag) => flag ? True : False;

    /// <summary>Makes a text value.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SandboxValue Of(string text) => new(SandboxValueKind.String, 0, text);

    /// <summary>Makes an object value.</summary>
    /// <param name="value">The object.</param>
    /// <returns>The value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SandboxValue Of(SandboxObject value) => new(SandboxValueKind.Object, 0, value);

    /// <summary>Tells whether two numbers are the same number; not a number is never the same as anything.</summary>
    /// <param name="left">The first number.</param>
    /// <param name="right">The second number.</param>
    /// <returns><see langword="true"/> when they are the same.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool SameNumber(double left, double right) => !double.IsNaN(left) && left.CompareTo(right) == 0;

    /// <summary>Writes a number as JavaScript does: whole numbers without a decimal point.</summary>
    /// <param name="number">The number.</param>
    /// <returns>The text.</returns>
    internal static string NumberToText(double number)
    {
        if (double.IsNaN(number))
        {
            return "NaN";
        }

        if (double.IsInfinity(number))
        {
            return number > 0 ? "Infinity" : "-Infinity";
        }

        return Math.Abs(number) < MaxPlainInteger && SameNumber(number, Math.Floor(number))
            ? ((long)number).ToString(CultureInfo.InvariantCulture)
            : number.ToString("R", CultureInfo.InvariantCulture);
    }

    /// <summary>Reads text as a number; empty text is zero and text that is not a number gives not a number.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The number.</returns>
    internal static double TextToNumber(string text)
    {
        var trimmed = text.AsSpan().Trim();
        if (trimmed.IsEmpty)
        {
            return 0;
        }

        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : double.NaN;
    }

    /// <summary>Gets the object of an object value.</summary>
    /// <returns>The object, or <see langword="null"/> when the value is not an object.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal SandboxObject? AsObject() => Reference as SandboxObject;

    /// <summary>Converts the value to a number the way JavaScript does.</summary>
    /// <returns>The number; not a number when the text is not one.</returns>
    internal double ToNumber() => Kind switch
    {
        SandboxValueKind.Null => 0,
        SandboxValueKind.Boolean or SandboxValueKind.Number => Number,
        SandboxValueKind.String => TextToNumber((string)Reference!),
        _ => double.NaN,
    };

    /// <summary>Converts the value to text the way JavaScript does.</summary>
    /// <returns>The text.</returns>
    internal string ToText() => Kind switch
    {
        SandboxValueKind.Null => "null",
        SandboxValueKind.Boolean => Number is not 0 ? "true" : "false",
        SandboxValueKind.Number => NumberToText(Number),
        SandboxValueKind.String => (string)Reference!,
        SandboxValueKind.Object => ((SandboxObject)Reference!).ToText(),
        _ => "undefined",
    };
}
