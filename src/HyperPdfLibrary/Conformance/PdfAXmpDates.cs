// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Reads XMP dates (the ISO 8601 subset of XMP Specification Part 1, 8.2.1.2) and PDF dates (ISO 32000-1, 7.9.4) as
/// veraPDF's toolkit reads them, so the PDF/A-1 date crosswalk compares the same instants.
/// </summary>
internal static class PdfAXmpDates
{
    /// <summary>The largest year.</summary>
    private const int MaxYear = 9999;

    /// <summary>The largest month.</summary>
    private const int MaxMonth = 12;

    /// <summary>The largest day.</summary>
    private const int MaxDay = 31;

    /// <summary>The largest hour.</summary>
    private const int MaxHour = 23;

    /// <summary>The largest minute or second.</summary>
    private const int MaxMinute = 59;

    /// <summary>The largest fraction of a second read, in its written digits.</summary>
    private const int MaxFraction = 999_999_999;

    /// <summary>The number of digits in a nanosecond count.</summary>
    private const int NanosecondDigits = 9;

    /// <summary>The nanoseconds in a millisecond.</summary>
    private const int NanosecondsPerMillisecond = 1_000_000;

    /// <summary>The radix of decimal digits.</summary>
    private const int DecimalRadix = 10;

    /// <summary>The characters that may follow the year or month.</summary>
    private const string AfterDatePart = "-";

    /// <summary>The characters that may follow the day.</summary>
    private const string AfterDay = "T";

    /// <summary>The characters that may follow the minutes.</summary>
    private const string AfterMinute = ":Z+-";

    /// <summary>The characters that may follow the whole seconds.</summary>
    private const string AfterSecond = ".Z+-";

    /// <summary>The characters that start a time zone.</summary>
    private const string ZoneStart = "Z+-";

    /// <summary>The characters that may follow the time zone hours.</summary>
    private const string AfterZoneHour = ":";

    /// <summary>Determines whether text is an XMP date.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when it reads as one.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsDate(string text) => TryParse(text, out _);

    /// <summary>Reads an XMP date.</summary>
    /// <param name="text">The text.</param>
    /// <param name="date">The date.</param>
    /// <returns><see langword="true"/> when the text reads as a date.</returns>
    internal static bool TryParse(string text, out PdfAXmpDate date)
    {
        date = default;
        if (text.Length == 0)
        {
            return true;
        }

        var position = text[0] == '-' ? 1 : 0;
        if (!TryReadDate(text, ref position, out date))
        {
            return false;
        }

        if (position >= text.Length)
        {
            return true;
        }

        position++;
        return TryReadTime(text, ref position, ref date);
    }

    /// <summary>Gets the instant an XMP date names; a date without a time zone is local time.</summary>
    /// <param name="date">The date.</param>
    /// <returns>The instant, or <see langword="null"/> when it is out of range.</returns>
    internal static DateTimeOffset? Instant(in PdfAXmpDate date)
    {
        // The toolkit keeps whole milliseconds, dropping the rest of the fraction.
        var milliseconds = date.Nanosecond / NanosecondsPerMillisecond;
        try
        {
            var wall = new DateTime(date.Year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)
                .AddMonths(date.Month - 1)
                .AddDays(date.Day - 1)
                .AddHours(date.Hour)
                .AddMinutes(date.Minute)
                .AddSeconds(date.Second)
                .AddMilliseconds(milliseconds);
            return new DateTimeOffset(wall, date.Offset ?? TimeZoneInfo.Local.GetUtcOffset(wall));
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>Reads a PDF date string, such as <c>D:20240131120000+10'00'</c>, to an instant.</summary>
    /// <param name="text">The decoded string.</param>
    /// <returns>The instant, or <see langword="null"/> when the string is not a valid date.</returns>
    internal static DateTimeOffset? ParsePdfDate(string? text)
    {
        if (text is null || !text.StartsWith("D:", StringComparison.Ordinal))
        {
            return null;
        }

        if (text.EndsWith('\''))
        {
            if (text.EndsWith("''", StringComparison.Ordinal))
            {
                return null;
            }

            text = text[..^1];
        }

        return PdfAPdfDateReader.Parse(text);
    }

    /// <summary>Reads the year, month and day.</summary>
    /// <param name="text">The text.</param>
    /// <param name="position">The read position.</param>
    /// <param name="date">The date read.</param>
    /// <returns><see langword="true"/> when they read.</returns>
    private static bool TryReadDate(string text, ref int position, out PdfAXmpDate date)
    {
        date = default;
        if (!TryReadField(text, ref position, MaxYear, AfterDatePart, out var year))
        {
            return false;
        }

        date = new(Math.Min(Math.Abs(year), MaxYear), 0, 0, 0, 0, 0, 0, null);
        if (position >= text.Length)
        {
            return true;
        }

        position++;
        if (!TryReadField(text, ref position, MaxMonth, AfterDatePart, out var month))
        {
            return false;
        }

        date = date with { Month = Math.Max(month, 1) };
        if (position >= text.Length)
        {
            return true;
        }

        position++;
        var hasDay = TryReadField(text, ref position, MaxDay, AfterDay, out var day);
        date = date with { Day = Math.Max(day, 1) };
        return hasDay;
    }

    /// <summary>Reads the time after <c>T</c> and its time zone.</summary>
    /// <param name="text">The text.</param>
    /// <param name="position">The read position, after <c>T</c>.</param>
    /// <param name="date">The date, to which the time is added.</param>
    /// <returns><see langword="true"/> when the time reads.</returns>
    private static bool TryReadTime(string text, ref int position, ref PdfAXmpDate date)
    {
        if (!TryReadField(text, ref position, MaxHour, null, out var hour))
        {
            return false;
        }

        date = date with { Hour = hour };
        if (position < text.Length && text[position] == ':')
        {
            position++;
            if (!TryReadField(text, ref position, MaxMinute, AfterMinute, out var minute))
            {
                return false;
            }

            date = date with { Minute = minute };
        }

        if (position >= text.Length)
        {
            return true;
        }

        // Seconds may come next; otherwise only a time zone may.
        var hasSeconds = text[position] == ':';
        var seconds = hasSeconds ? TryReadSeconds(text, ref position, ref date) : ZoneStart.Contains(text[position], StringComparison.Ordinal);
        return seconds && TryReadZone(text, ref position, ref date);
    }

    /// <summary>Reads the seconds and any fraction.</summary>
    /// <param name="text">The text.</param>
    /// <param name="position">The read position, on the colon.</param>
    /// <param name="date">The date being read.</param>
    /// <returns><see langword="true"/> when they read.</returns>
    private static bool TryReadSeconds(string text, ref int position, ref PdfAXmpDate date)
    {
        position++;
        if (!TryReadField(text, ref position, MaxMinute, AfterSecond, out var second))
        {
            return false;
        }

        date = date with { Second = second };
        if (position >= text.Length || text[position] != '.')
        {
            return true;
        }

        position++;
        var start = position;
        if (!TryReadField(text, ref position, MaxFraction, ZoneStart, out var fraction))
        {
            return false;
        }

        for (var digits = position - start; digits > NanosecondDigits; digits--)
        {
            fraction /= DecimalRadix;
        }

        for (var digits = position - start; digits < NanosecondDigits; digits++)
        {
            fraction = unchecked(fraction * DecimalRadix);
        }

        date = date with { Nanosecond = fraction };
        return true;
    }

    /// <summary>Reads the time zone: <c>Z</c>, or a sign, hours and optional minutes, with nothing after it.</summary>
    /// <param name="text">The text.</param>
    /// <param name="position">The read position.</param>
    /// <param name="date">The date being read.</param>
    /// <returns><see langword="true"/> when the zone reads.</returns>
    private static bool TryReadZone(string text, ref int position, ref PdfAXmpDate date)
    {
        if (position >= text.Length)
        {
            return true;
        }

        var offset = TimeSpan.Zero;
        var sign = text[position];
        position++;
        if (sign != 'Z')
        {
            if (!TryReadField(text, ref position, MaxHour, AfterZoneHour, out var hours))
            {
                return false;
            }

            var minutes = 0;
            if (position < text.Length)
            {
                position++;
                if (!TryReadField(text, ref position, MaxMinute, null, out minutes))
                {
                    return false;
                }
            }

            offset = (sign == '-' ? -1 : 1) * new TimeSpan(hours, minutes, 0);
        }

        date = date with { Offset = offset };
        return position >= text.Length;
    }

    /// <summary>Reads a number field, capped as the toolkit caps it, and checks the character after it.</summary>
    /// <param name="text">The text.</param>
    /// <param name="position">The read position.</param>
    /// <param name="max">The cap.</param>
    /// <param name="allowedNext">The characters that may follow the field, or <see langword="null"/> for any.</param>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when at least one digit was read and an allowed character, or the end, follows.</returns>
    private static bool TryReadField(string text, ref int position, int max, string? allowedNext, out int value)
    {
        value = 0;
        var start = position;
        while (position < text.Length && char.IsAsciiDigit(text[position]))
        {
            // Java's int arithmetic wraps on overflow; the toolkit then caps or floors the result.
            value = unchecked((value * DecimalRadix) + (text[position] - '0'));
            position++;
        }

        if (position == start)
        {
            return false;
        }

        value = value > max ? max : Math.Max(value, 0);
        return allowedNext is null || position >= text.Length || allowedNext.Contains(text[position], StringComparison.Ordinal);
    }
}
