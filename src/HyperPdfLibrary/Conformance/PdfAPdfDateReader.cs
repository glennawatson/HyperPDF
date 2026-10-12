// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace HyperPdfLibrary.Conformance;

/// <summary>Reads the fields of a PDF date string (ISO 32000-1, 7.9.4) after <c>D:</c>, as veraPDF's parser reads them.</summary>
internal static class PdfAPdfDateReader
{
    /// <summary>The offset of the year.</summary>
    private const int YearAt = 2;

    /// <summary>The digits in a year.</summary>
    private const int YearDigits = 4;

    /// <summary>The offset of the month.</summary>
    private const int MonthAt = 6;

    /// <summary>The offset of the day.</summary>
    private const int DayAt = 8;

    /// <summary>The offset of the hour.</summary>
    private const int HourAt = 10;

    /// <summary>The offset of the minute.</summary>
    private const int MinuteAt = 12;

    /// <summary>The offset of the second.</summary>
    private const int SecondAt = 14;

    /// <summary>The offset of the time zone sign.</summary>
    private const int SignAt = 16;

    /// <summary>The offset of the time zone hours.</summary>
    private const int ZoneHourAt = 17;

    /// <summary>The offset of the apostrophe after the zone hours.</summary>
    private const int ApostropheAt = 19;

    /// <summary>The offset of the time zone minutes.</summary>
    private const int ZoneMinuteAt = 20;

    /// <summary>The longest date string.</summary>
    private const int MaxLength = 22;

    /// <summary>The digits in every field but the year.</summary>
    private const int FieldDigits = 2;

    /// <summary>The largest zone hour Java accepts in a custom zone.</summary>
    private const int MaxZoneHour = 23;

    /// <summary>The largest zone minute Java accepts in a custom zone.</summary>
    private const int MaxZoneMinute = 59;

    /// <summary>The radix of decimal digits.</summary>
    private const int DecimalRadix = 10;

    /// <summary>Reads the fields.</summary>
    /// <param name="text">The date string, starting with <c>D:</c> and without a trailing apostrophe.</param>
    /// <returns>The instant, or <see langword="null"/> when the string is not a valid date.</returns>
    internal static DateTimeOffset? Parse(string text)
    {
        if (text.Length > MaxLength || !TryField(text, YearAt, YearDigits, true, out var year))
        {
            return null;
        }

        if (!TryField(text, MonthAt, FieldDigits, false, out var month)
            || !TryField(text, DayAt, FieldDigits, false, out var day)
            || !TryTime(text, out var time)
            || !TryZone(text, out var offset))
        {
            return null;
        }

        return Instant(year, text.Length > MonthAt ? month - 1 : 0, text.Length > DayAt ? day : 1, time, offset);
    }

    /// <summary>Reads the hours, minutes and seconds.</summary>
    /// <param name="text">The date string.</param>
    /// <param name="time">The time of day.</param>
    /// <returns><see langword="true"/> when the fields are valid or absent.</returns>
    private static bool TryTime(string text, out TimeSpan time)
    {
        time = TimeSpan.Zero;
        if (!TryField(text, HourAt, FieldDigits, false, out var hour)
            || !TryField(text, MinuteAt, FieldDigits, false, out var minute)
            || !TryField(text, SecondAt, FieldDigits, false, out var second))
        {
            return false;
        }

        time = new(hour, minute, second);
        return true;
    }

    /// <summary>Reads the time zone.</summary>
    /// <param name="text">The date string.</param>
    /// <param name="offset">The offset.</param>
    /// <returns><see langword="true"/> when the zone is valid or absent.</returns>
    private static bool TryZone(string text, out TimeSpan offset)
    {
        offset = TimeSpan.Zero;
        if (text.Length <= SignAt)
        {
            return true;
        }

        var sign = text[SignAt];
        if (sign is not ('Z' or '+' or '-') || !TryZoneFields(text, out var hours, out var minutes))
        {
            return false;
        }

        offset = ZoneOffset(sign, hours, minutes);
        return sign != 'Z' || (hours == 0 && minutes == 0);
    }

    /// <summary>Gets a zone offset from its sign, hours and minutes.</summary>
    /// <param name="sign">The sign: <c>Z</c>, <c>+</c> or <c>-</c>.</param>
    /// <param name="hours">The hours.</param>
    /// <param name="minutes">The minutes.</param>
    /// <returns>The offset; zero for <c>Z</c>, and for hours or minutes Java cannot make a zone from, as it falls back to GMT.</returns>
    private static TimeSpan ZoneOffset(char sign, int hours, int minutes)
    {
        if (sign == 'Z' || hours > MaxZoneHour || minutes > MaxZoneMinute)
        {
            return TimeSpan.Zero;
        }

        var magnitude = new TimeSpan(hours, minutes, 0);
        return sign == '-' ? -magnitude : magnitude;
    }

    /// <summary>Reads the time zone hours and minutes and the apostrophe between them.</summary>
    /// <param name="text">The date string.</param>
    /// <param name="hours">The hours.</param>
    /// <param name="minutes">The minutes.</param>
    /// <returns><see langword="true"/> when they are valid or absent.</returns>
    private static bool TryZoneFields(string text, out int hours, out int minutes)
    {
        minutes = 0;
        return TryField(text, ZoneHourAt, FieldDigits, false, out hours)
            && (text.Length <= ApostropheAt || text[ApostropheAt] == '\'')
            && TryField(text, ZoneMinuteAt, FieldDigits, false, out minutes);
    }

    /// <summary>Reads a fixed-width field when the string reaches it.</summary>
    /// <param name="text">The date string.</param>
    /// <param name="at">The field's offset.</param>
    /// <param name="digits">The field's width.</param>
    /// <param name="required">Whether the field must be present.</param>
    /// <param name="value">The value, or 0 when the field is absent.</param>
    /// <returns><see langword="false"/> when the field is present but not all digits.</returns>
    private static bool TryField(string text, int at, int digits, bool required, out int value)
    {
        value = 0;
        if (!required && text.Length <= at)
        {
            return true;
        }

        if (at + digits > text.Length)
        {
            return false;
        }

        for (var i = at; i < at + digits; i++)
        {
            var digit = CharUnicodeInfo.GetDecimalDigitValue(text[i]);
            if (digit < 0)
            {
                return false;
            }

            value = (value * DecimalRadix) + digit;
        }

        return true;
    }

    /// <summary>Gets an instant from date fields, rolling over out-of-range values as Java's lenient calendar does.</summary>
    /// <param name="year">The year.</param>
    /// <param name="monthIndex">The zero-based month.</param>
    /// <param name="day">The day.</param>
    /// <param name="time">The time of day.</param>
    /// <param name="offset">The zone offset.</param>
    /// <returns>The instant, or <see langword="null"/> when out of range.</returns>
    private static DateTimeOffset? Instant(int year, int monthIndex, int day, TimeSpan time, TimeSpan offset)
    {
        try
        {
            var wall = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified).AddMonths(monthIndex).AddDays(day - 1).Add(time);
            return new DateTimeOffset(wall, offset);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
