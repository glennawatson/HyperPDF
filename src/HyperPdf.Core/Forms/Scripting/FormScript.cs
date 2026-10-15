// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace HyperPdf.Core.Forms.Scripting;

/// <summary>
/// A form field script recognised as one of built-in PDF form functions, with its arguments. Only these calls,
/// and simplified field notation calculations, are understood; any other JavaScript is not run, so a document cannot
/// run code on the reader's computer.
/// </summary>
/// <param name="Function">The function.</param>
/// <param name="Arguments">The literal arguments in order: numbers, strings and booleans as written.</param>
/// <param name="Fields">The field names a calculation reads.</param>
/// <param name="Expression">The simplified field notation of an expression calculation, otherwise empty.</param>
[DebuggerDisplay("FormScript: {Function}({Arguments.Count} arguments)")]
public sealed partial record FormScript(FormScriptFunction Function, IReadOnlyList<string> Arguments, IReadOnlyList<string> Fields, string Expression)
{
    /// <summary>The built-in form functions the engine runs, by name.</summary>
    private static readonly Dictionary<string, FormScriptFunction> Functions = new(StringComparer.Ordinal)
    {
        ["AFNumber_Format"] = FormScriptFunction.Number,
        ["AFNumber_Keystroke"] = FormScriptFunction.Number,
        ["AFPercent_Format"] = FormScriptFunction.Percent,
        ["AFPercent_Keystroke"] = FormScriptFunction.Percent,
        ["AFDate_Format"] = FormScriptFunction.Date,
        ["AFDate_Keystroke"] = FormScriptFunction.Date,
        ["AFDate_FormatEx"] = FormScriptFunction.Date,
        ["AFDate_KeystrokeEx"] = FormScriptFunction.Date,
        ["AFTime_Format"] = FormScriptFunction.Time,
        ["AFTime_Keystroke"] = FormScriptFunction.Time,
        ["AFTime_FormatEx"] = FormScriptFunction.Time,
        ["AFTime_KeystrokeEx"] = FormScriptFunction.Time,
        ["AFSpecial_Format"] = FormScriptFunction.Special,
        ["AFSpecial_Keystroke"] = FormScriptFunction.Special,
        ["AFRange_Validate"] = FormScriptFunction.Range,
        ["AFSimple_Calculate"] = FormScriptFunction.Simple,
    };

    /// <summary>Gets the script used when there is none, or it is not understood.</summary>
    public static FormScript None { get; } = new(FormScriptFunction.Unknown, [], [], string.Empty);

    /// <summary>Recognises a script.</summary>
    /// <param name="javaScript">The field action's JavaScript.</param>
    /// <returns>The recognised script, or <see cref="None"/>.</returns>
    public static FormScript Parse(string? javaScript)
    {
        if (string.IsNullOrWhiteSpace(javaScript))
        {
            return None;
        }

        // PDF form editors write simplified field notation between these markers, ahead of the JavaScript it generates.
        var notation = NotationPattern().Match(javaScript);
        return notation.Success
            ? new(FormScriptFunction.Expression, [], [], notation.Groups["expression"].Value.Trim())
            : FindCall(javaScript);
    }

    /// <summary>Determines whether <see cref="Parse"/> would recognise a script, without creating the script.</summary>
    /// <param name="javaScript">The field action's JavaScript.</param>
    /// <returns><see langword="true"/> when the script is field notation or calls a built-in form function.</returns>
    public static bool IsRecognised(ReadOnlySpan<char> javaScript)
    {
        if (javaScript.IsWhiteSpace())
        {
            return false;
        }

        if (NotationPattern().IsMatch(javaScript))
        {
            return true;
        }

        var lookup = Functions.GetAlternateLookup<ReadOnlySpan<char>>();
        foreach (var match in CallPattern().EnumerateMatches(javaScript))
        {
            var call = javaScript.Slice(match.Index, match.Length);
            if (lookup.TryGetValue(call[..CallNameLength(call)], out var function) && function != FormScriptFunction.Unknown)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets an argument as a number, or a fallback when it is missing or not a number.</summary>
    /// <param name="index">The argument's position.</param>
    /// <param name="fallback">The fallback.</param>
    /// <returns>The number.</returns>
    public double Number(int index, double fallback) =>
        index < Arguments.Count && double.TryParse(Arguments[index], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : fallback;

    /// <summary>Gets an argument as text, or a fallback when it is missing.</summary>
    /// <param name="index">The argument's position.</param>
    /// <param name="fallback">The fallback.</param>
    /// <returns>The text.</returns>
    public string Text(int index, string fallback) => index < Arguments.Count ? Arguments[index] : fallback;

    /// <summary>Gets an argument as a flag: true, or a non-zero number.</summary>
    /// <param name="index">The argument's position.</param>
    /// <returns>The flag; false when missing.</returns>
    public bool Flag(int index) => index < Arguments.Count && Arguments[index] is "true" or not ("false" or "0" or "");

    /// <summary>
    /// Finds the first call that sets a field's behaviour. Helpers such as <c>AFMergeChange</c> and <c>AFParseDateEx</c>
    /// can come before it, and are skipped.
    /// </summary>
    /// <param name="javaScript">The field action's JavaScript.</param>
    /// <returns>The recognised script, or <see cref="None"/>.</returns>
    private static FormScript FindCall(string javaScript)
    {
        foreach (var call in (IEnumerable<Match>)CallPattern().Matches(javaScript))
        {
            var name = call.Groups["name"].Value;
            var function = FunctionOf(name);
            if (function != FormScriptFunction.Unknown)
            {
                return ReadCall(function, name, call.Groups["args"].Value);
            }
        }

        return None;
    }

    /// <summary>Gets the length of the function name a call match starts with.</summary>
    /// <param name="call">The text of a <see cref="CallPattern"/> match.</param>
    /// <returns>The name's length.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CallNameLength(ReadOnlySpan<char> call) => call[..call.IndexOf('(')].TrimEnd().Length;

    /// <summary>Reads the arguments of a recognised call.</summary>
    /// <param name="function">The function.</param>
    /// <param name="name">The function's name.</param>
    /// <param name="text">The text between the call's parentheses.</param>
    /// <returns>The script.</returns>
    private static FormScript ReadCall(FormScriptFunction function, string name, string text)
    {
        var arguments = new List<string>();
        var fields = new List<string>();
        ReadArguments(text, arguments, fields);
        if (function == FormScriptFunction.Simple && fields.Count == 0 && arguments.Count > 1)
        {
            // The fields can also be one comma-separated string.
            fields.AddRange(arguments[1].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        }

        ResolveIndexedFormat(name, arguments);
        return new(function, arguments, fields, string.Empty);
    }

    /// <summary>Reads the first argument as a whole number.</summary>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The number; 0 when there is none.</returns>
    private static int ReadIndex(List<string> arguments) =>
        arguments.Count > 0 && int.TryParse(arguments[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;

    /// <summary>
    /// Replaces the number that <c>AFDate_Format</c>, <c>AFDate_Keystroke</c>, <c>AFTime_Format</c> and <c>AFTime_Keystroke</c>
    /// take with the format it stands for, as the engine only deals in format strings.
    /// </summary>
    /// <param name="name">The function's name.</param>
    /// <param name="arguments">The arguments, changed in place.</param>
    private static void ResolveIndexedFormat(string name, List<string> arguments)
    {
        if (IndexedFormat(name, ReadIndex(arguments)) is not { } format)
        {
            return;
        }

        arguments.Clear();
        arguments.Add(format);
    }

    /// <summary>Gets the format a numbered style of a date or time function stands for.</summary>
    /// <param name="name">The function's name.</param>
    /// <param name="index">The style number.</param>
    /// <returns>The format; <see langword="null"/> for functions that do not pick a format by number.</returns>
    private static string? IndexedFormat(string name, int index) => name switch
    {
        "AFDate_Format" or "AFDate_Keystroke" => FormDates.DateFormatOf(index),
        "AFTime_Format" or "AFTime_Keystroke" => FormDates.TimeFormatOf(index),
        _ => null,
    };

    /// <summary>Maps a built-in form function's name to what it does.</summary>
    /// <param name="name">The function's name.</param>
    /// <returns>The function, or unknown.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FormScriptFunction FunctionOf(string name) => Functions.GetValueOrDefault(name, FormScriptFunction.Unknown);

    /// <summary>Splits a call's arguments into literals, gathering the strings of a <c>new Array(...)</c> as field names.</summary>
    /// <param name="text">The text between the call's parentheses.</param>
    /// <param name="arguments">Receives the literals.</param>
    /// <param name="fields">Receives the field names of an array.</param>
    private static void ReadArguments(string text, List<string> arguments, List<string> fields)
    {
        foreach (var token in (IEnumerable<Match>)TokenPattern().Matches(text))
        {
            if (token.Groups["array"].Success)
            {
                foreach (var name in (IEnumerable<Match>)StringPattern().Matches(token.Groups["array"].Value))
                {
                    fields.Add(Unescape(name.Groups["body"].Value));
                }

                arguments.Add(string.Empty);
            }
            else if (token.Groups["string"].Success)
            {
                arguments.Add(Unescape(token.Groups["string"].Value));
            }
            else
            {
                arguments.Add(token.Groups["literal"].Value);
            }
        }
    }

    /// <summary>Removes JavaScript string escapes.</summary>
    /// <param name="text">The escaped text.</param>
    /// <returns>The text.</returns>
    private static string Unescape(string text)
    {
        if (!text.Contains('\\', StringComparison.Ordinal))
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length)
            {
                i++;
            }

            _ = builder.Append(text[i]);
        }

        return builder.ToString();
    }

    /// <summary>Matches simplified field notation between field notation markers.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"BVCALC(?<expression>.*?)EVCALC", RegexOptions.Singleline)]
    private static partial Regex NotationPattern();

    /// <summary>Matches the first call of a built-in form function.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"\b(?<name>AF\w+)\s*\((?<args>(?:[^()""']|""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'|\((?:[^()]|""[^""]*"")*\))*)\)")]
    private static partial Regex CallPattern();

    /// <summary>Matches one argument: an array of strings, a string, or a bare literal.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"new\s+Array\s*\((?<array>[^)]*)\)|""(?<string>(?:\\.|[^""\\])*)""|'(?<string>(?:\\.|[^'\\])*)'|(?<literal>[^,\s][^,]*?)(?=\s*(?:,|$))")]
    private static partial Regex TokenPattern();

    /// <summary>Matches a string literal.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"""(?<body>(?:\\.|[^""\\])*)""|'(?<body>(?:\\.|[^'\\])*)'")]
    private static partial Regex StringPattern();
}
