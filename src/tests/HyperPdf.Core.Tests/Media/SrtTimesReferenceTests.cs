// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.Core.Media;
using HyperPdf.TestAssets;

namespace HyperPdf.Core.Tests.Media;

/// <summary>Tests that <see cref="SrtTimes"/> accepts and reads exactly the lines the general number parser did, including odd and damaged ones.</summary>
public sealed class SrtTimesReferenceTests
{
    /// <summary>The starting state of the number sequence.</summary>
    private const uint Seed = 88_172_645;

    /// <summary>The lines compared.</summary>
    private const int Lines = 60_000;

    /// <summary>The marker between the times.</summary>
    private const string Arrow = "-->";

    /// <summary>The parts of a time: hours, minutes and seconds.</summary>
    private const int TimeParts = 3;

    /// <summary>The parts of a WebVTT time that leaves the hours off: minutes and seconds.</summary>
    private const int WebVttParts = 2;

    /// <summary>The most minutes or seconds a time may have in one part.</summary>
    private const int MaxClockPart = 99;

    /// <summary>The most hours a time may have.</summary>
    private const int MaxHours = 9_999;

    /// <summary>The seconds in a minute.</summary>
    private const int SecondsPerMinute = 60;

    /// <summary>The seconds in an hour.</summary>
    private const int SecondsPerHour = 3_600;

    /// <summary>The milliseconds in a second.</summary>
    private const int MillisecondsPerSecond = 1_000;

    /// <summary>The number of digits in a count of milliseconds.</summary>
    private const int MillisecondDigits = 3;

    /// <summary>The base of the digits of a fraction.</summary>
    private const double DecimalBase = 10;

    /// <summary>Hour, minute and second parts, good and damaged.</summary>
    private static readonly string[] Whole =
    [
        "0", "00", "01", "12", "59", "99", "100", "9999", "10000", "2147483647", "2147483648", "99999999999",
        string.Empty, "-1", "+1", " 1", "1 ", "1a", "a", "١٢",
    ];

    /// <summary>Fractions of a second, good and damaged.</summary>
    private static readonly string[] Fraction = [string.Empty, "5", "50", "500", "5000", "123456789", "2147483647", "2147483648", "1234567890123", "-5", " 5", "5a", "a"];

    /// <summary>The marks between the whole seconds and their fraction.</summary>
    private static readonly string[] Marks = [",", ".", ":", string.Empty];

    /// <summary>What may follow the second time.</summary>
    private static readonly string[] Tails = [string.Empty, " X1:10 X2:20", "   ", " a b", "\t"];

    /// <summary>Verifies a generated line is read the same by the current reader and the number parser it replaced.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TimesMatchTheNumberParser()
    {
        var numbers = new NumberSequence(Seed);
        var differences = 0;
        for (var i = 0; i < Lines; i++)
        {
            var line = $"{Time(numbers)} {Arrow} {Time(numbers)}{Tails[numbers.Next(Tails.Length)]}".Trim();
            var actual = SrtTimes.TryReadTimes(line, out var start, out var end);
            var expected = ReferenceTimes(line, out var expectedStart, out var expectedEnd);
            differences += actual == expected && start == expectedStart && end == expectedEnd ? 0 : 1;
        }

        await Assert.That(differences).IsEqualTo(0);
    }

    /// <summary>Builds one time, now and then with a part left out or an extra colon.</summary>
    /// <param name="numbers">The numbers.</param>
    /// <returns>The text.</returns>
    private static string Time(NumberSequence numbers)
    {
        var colons = numbers.Next(Whole.Length) == 0 ? ":" : string.Empty;
        var clock = $"{Whole[numbers.Next(Whole.Length)]}:{Whole[numbers.Next(Whole.Length)]}:{colons}";
        return $"{clock}{Whole[numbers.Next(Whole.Length)]}{Marks[numbers.Next(Marks.Length)]}{Fraction[numbers.Next(Fraction.Length)]}";
    }

    /// <summary>Reads a line of two times with the general number parser, as the reader did before.</summary>
    /// <param name="line">The line.</param>
    /// <param name="start">Receives the first time.</param>
    /// <param name="end">Receives the second time.</param>
    /// <returns>Whether the line holds two times.</returns>
    private static bool ReferenceTimes(ReadOnlySpan<char> line, out TimeSpan start, out TimeSpan end)
    {
        start = default;
        end = default;
        var arrow = line.IndexOf(Arrow, StringComparison.Ordinal);
        return arrow > 0 && ReferenceTime(line[..arrow].Trim(), out start) && ReferenceTime(FirstWord(line[(arrow + Arrow.Length)..].Trim()), out end);
    }

    /// <summary>Takes the text up to the first space.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The first word.</returns>
    private static ReadOnlySpan<char> FirstWord(ReadOnlySpan<char> text)
    {
        var space = text.IndexOf(' ');
        return space < 0 ? text : text[..space];
    }

    /// <summary>Reads one time with the general number parser.</summary>
    /// <param name="text">The time.</param>
    /// <param name="time">Receives the time.</param>
    /// <returns>Whether the text is a time.</returns>
    private static bool ReferenceTime(ReadOnlySpan<char> text, out TimeSpan time)
    {
        time = default;
        Span<Range> parts = stackalloc Range[TimeParts + 1];
        var count = text.Split(parts, ':');

        // WebVTT leaves the hours off: minutes and seconds only.
        if (count == WebVttParts)
        {
            return ReferenceShortTime(text[parts[0]], text[parts[1]], out time);
        }

        if (count != TimeParts || !ReferenceSeconds(text[parts[2]], out var seconds))
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

    /// <summary>Reads a WebVTT time that has no hours with the general number parser.</summary>
    /// <param name="minutesText">The minutes.</param>
    /// <param name="secondsText">The seconds with an optional fraction.</param>
    /// <param name="time">Receives the time.</param>
    /// <returns>Whether the text is a time.</returns>
    private static bool ReferenceShortTime(ReadOnlySpan<char> minutesText, ReadOnlySpan<char> secondsText, out TimeSpan time)
    {
        time = default;
        if (!int.TryParse(minutesText, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) || minutes > MaxClockPart || !ReferenceSeconds(secondsText, out var seconds))
        {
            return false;
        }

        time = TimeSpan.FromSeconds((minutes * SecondsPerMinute) + seconds);
        return true;
    }

    /// <summary>Reads the seconds of a time with the general number parser.</summary>
    /// <param name="text">The seconds with an optional fraction.</param>
    /// <param name="seconds">Receives the seconds.</param>
    /// <returns>Whether the text is seconds.</returns>
    private static bool ReferenceSeconds(ReadOnlySpan<char> text, out double seconds)
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
