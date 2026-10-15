// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Forms.Scripting;

/// <summary>
/// Reads and writes dates in PDF form date formats, such as <c>dd/mm/yyyy</c> or <c>mmm d, yyyy</c>: in them
/// <c>m</c> is the month and <c>M</c> the minute, the reverse of .NET's patterns.
/// </summary>
internal static class FormDates
{
    /// <summary>The letters that mean the same in PDF form formats and .NET's patterns.</summary>
    private static readonly SearchValues<char> Kept = SearchValues.Create("dyHhst");

    /// <summary>The characters .NET's patterns treat specially, escaped to be written as they are.</summary>
    private static readonly SearchValues<char> Escaped = SearchValues.Create("\\'\"%");

    /// <summary>Common date forms tried when the text is not in the field's format.</summary>
    private static readonly string[] CommonPatterns = ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "d MMM yyyy", "d MMMM yyyy", "MMM d, yyyy", "MMMM d, yyyy"];

    /// <summary>Time forms tried when the text is not in the field's format.</summary>
    private static readonly string[] CommonTimePatterns = ["HH:mm", "H:mm", "h:mm tt", "h:mmtt", "HH:mm:ss", "H:mm:ss", "h:mm:ss tt", "h:mm:sstt"];

    /// <summary>The formats <c>AFDate_Format</c> and <c>AFDate_Keystroke</c> pick by number.</summary>
    private static readonly string[] IndexedDateFormats =
    [
        "m/d", "m/d/yy", "mm/dd/yy", "mm/yy", "d-mmm", "d-mmm-yy", "dd-mmm-yy", "yy-mm-dd", "mmm-yy", "mmmm-yy", "mmm d, yyyy", "mmmm d, yyyy", "m/d/yy h:MM tt", "m/d/yy HH:MM",
    ];

    /// <summary>The formats <c>AFTime_Format</c> and <c>AFTime_Keystroke</c> pick by number.</summary>
    private static readonly string[] IndexedTimeFormats = ["HH:MM", "h:MM tt", "HH:MM:ss", "h:MM:ss tt"];

    /// <summary>Gets the date format a numbered <c>AFDate_Format</c> style stands for.</summary>
    /// <param name="index">The style number.</param>
    /// <returns>The PDF form format; the first style when the number is unknown.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string DateFormatOf(int index) => IndexedDateFormats[(uint)index < (uint)IndexedDateFormats.Length ? index : 0];

    /// <summary>Gets the time format a numbered <c>AFTime_Format</c> style stands for.</summary>
    /// <param name="index">The style number.</param>
    /// <returns>The PDF form format; the first style when the number is unknown.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string TimeFormatOf(int index) => IndexedTimeFormats[(uint)index < (uint)IndexedTimeFormats.Length ? index : 0];

    /// <summary>Reads a time written in the field's format, or failing that in a common form.</summary>
    /// <param name="text">The text.</param>
    /// <param name="format">The field's PDF form format.</param>
    /// <param name="value">The time, on today's date.</param>
    /// <returns><see langword="true"/> when the text is a time.</returns>
    internal static bool TryParseTime(string? text, string format, out DateTime value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.AsSpan().Trim();
        return DateTime.TryParseExact(trimmed, ToPattern(format), CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out value)
            || DateTime.TryParseExact(trimmed, CommonTimePatterns, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out value);
    }

    /// <summary>Converts a PDF form date format to a .NET pattern.</summary>
    /// <param name="format">The PDF form format.</param>
    /// <returns>The .NET pattern.</returns>
    internal static string ToPattern(string format)
    {
        ArgumentNullException.ThrowIfNull(format);
        var predefined = format switch
        {
            "m/d" => "M/d",
            "m/d/yy" => "M/d/yy",
            "mm/dd/yy" => "MM/dd/yy",
            "mm/yy" => "MM/yy",
            "d-mmm" => "d-MMM",
            "d-mmm-yy" => "d-MMM-yy",
            "dd-mmm-yy" => "dd-MMM-yy",
            "yy-mm-dd" => "yy-MM-dd",
            "mmm-yy" => "MMM-yy",
            "mmmm-yy" => "MMMM-yy",
            "mmm d, yyyy" => "MMM d, yyyy",
            "mmmm d, yyyy" => "MMMM d, yyyy",
            "m/d/yy h:MM tt" => "M/d/yy h:mm tt",
            "m/d/yy HH:MM" => "M/d/yy HH:mm",
            "HH:MM" => "HH:mm",
            "h:MM tt" => "h:mm tt",
            "HH:MM:ss" => "HH:mm:ss",
            "h:MM:ss tt" => "h:mm:ss tt",
            _ => null,
        };
        if (predefined is not null)
        {
            return predefined;
        }

        var length = format.Length;
        foreach (var c in format)
        {
            if (c is not ('m' or 'M') && !Kept.Contains(c) && (Escaped.Contains(c) || char.IsAsciiLetter(c)))
            {
                length++;
            }
        }

        return string.Create(length, format, static (pattern, source) =>
        {
            var position = 0;
            foreach (var c in source)
            {
                if (c is 'm' or 'M')
                {
                    // PDF form month and minute letters are reversed in .NET patterns.
                    pattern[position] = c == 'm' ? 'M' : 'm';
                    position++;
                    continue;
                }

                if (!Kept.Contains(c) && (Escaped.Contains(c) || char.IsAsciiLetter(c)))
                {
                    pattern[position] = '\\';
                    position++;
                }

                pattern[position] = c;
                position++;
            }
        });
    }

    /// <summary>Reads a date written in the field's format, or failing that in a common form.</summary>
    /// <param name="text">The text.</param>
    /// <param name="format">The field's PDF form format.</param>
    /// <param name="value">The date.</param>
    /// <returns><see langword="true"/> when the text is a date.</returns>
    internal static bool TryParse(string? text, string format, out DateTime value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.AsSpan().Trim();
        return DateTime.TryParseExact(trimmed, ToPattern(format), CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out value)
            || DateTime.TryParseExact(trimmed, CommonPatterns, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out value)
            || DateTime.TryParse(trimmed, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out value);
    }

    /// <summary>Writes a date in a PDF form format.</summary>
    /// <param name="value">The date.</param>
    /// <param name="format">The PDF form format.</param>
    /// <returns>The text.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string Format(DateTime value, string format) => value.ToString(ToPattern(format), CultureInfo.InvariantCulture);
}
