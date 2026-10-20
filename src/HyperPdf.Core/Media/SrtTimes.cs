// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace HyperPdf.Core.Media;

/// <summary>Reads the times of SubRip captions.</summary>
internal static class SrtTimes
{
    /// <summary>The marker between a caption's start and end times.</summary>
    private const string Arrow = "-->";

    /// <summary>The number of parts in a time: hours, minutes and seconds.</summary>
    private const int TimeParts = 3;

    /// <summary>The most hours a caption time may have, so a damaged value cannot overflow a time span.</summary>
    private const int MaxHours = 9_999;

    /// <summary>The most minutes or seconds a caption time may have in one part.</summary>
    private const int MaxClockPart = 99;

    /// <summary>The seconds in a minute.</summary>
    private const int SecondsPerMinute = 60;

    /// <summary>The seconds in an hour.</summary>
    private const int SecondsPerHour = 3_600;

    /// <summary>The milliseconds in a second.</summary>
    private const int MillisecondsPerSecond = 1_000;

    /// <summary>The number of digits in a count of milliseconds.</summary>
    private const int MillisecondDigits = 3;

    /// <summary>The base of the decimal digits of a fraction of a second.</summary>
    private const double DecimalBase = 10;

    /// <summary>Reads a line that holds the start time, the arrow and the end time.</summary>
    /// <param name="line">The line.</param>
    /// <param name="start">Receives the first time.</param>
    /// <param name="end">Receives the second time.</param>
    /// <returns><see langword="true"/> when the line holds two times.</returns>
    internal static bool TryReadTimes(ReadOnlySpan<char> line, out TimeSpan start, out TimeSpan end)
    {
        start = default;
        end = default;
        var arrow = line.IndexOf(Arrow, StringComparison.Ordinal);
        return arrow > 0 && TryReadTime(line[..arrow].Trim(), out start) && TryReadTime(FirstWord(line[(arrow + Arrow.Length)..].Trim()), out end);
    }

    /// <summary>Takes the text up to the first space, which drops the position settings some files add after the end time.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The first word.</returns>
    private static ReadOnlySpan<char> FirstWord(ReadOnlySpan<char> text)
    {
        var space = text.IndexOf(' ');
        return space < 0 ? text : text[..space];
    }

    /// <summary>Reads a time made of hours, minutes and seconds with a comma or point before the fraction.</summary>
    /// <param name="text">The time.</param>
    /// <param name="time">Receives the time.</param>
    /// <returns><see langword="true"/> when the text is a time.</returns>
    private static bool TryReadTime(ReadOnlySpan<char> text, out TimeSpan time)
    {
        time = default;
        Span<Range> parts = stackalloc Range[TimeParts + 1];
        if (text.Split(parts, ':') != TimeParts || !TryReadSeconds(text[parts[2]], out var seconds))
        {
            return false;
        }

        if (!int.TryParse(text[parts[0]], NumberStyles.None, CultureInfo.InvariantCulture, out var hours)
            || !int.TryParse(text[parts[1]], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
            || hours > MaxHours
            || minutes > MaxClockPart)
        {
            return false;
        }

        time = TimeSpan.FromSeconds((hours * (double)SecondsPerHour) + (minutes * SecondsPerMinute) + seconds);
        return true;
    }

    /// <summary>Reads the seconds of a time, such as <c>02,500</c>.</summary>
    /// <param name="text">The seconds with an optional fraction.</param>
    /// <param name="seconds">Receives the seconds.</param>
    /// <returns><see langword="true"/> when the text is seconds.</returns>
    private static bool TryReadSeconds(ReadOnlySpan<char> text, out double seconds)
    {
        seconds = 0;
        var comma = text.IndexOfAny(',', '.');
        var whole = comma < 0 ? text : text[..comma];
        var fraction = comma < 0 ? default : text[(comma + 1)..];
        var millis = 0;
        if (!int.TryParse(whole, NumberStyles.None, CultureInfo.InvariantCulture, out var wholeSeconds)
            || wholeSeconds > MaxClockPart
            || (!fraction.IsEmpty && !int.TryParse(fraction, NumberStyles.None, CultureInfo.InvariantCulture, out millis)))
        {
            return false;
        }

        seconds = wholeSeconds + (fraction.IsEmpty ? 0 : millis * Math.Pow(DecimalBase, MillisecondDigits - fraction.Length) / MillisecondsPerSecond);
        return true;
    }
}
