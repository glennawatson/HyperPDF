// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Speech.English;

/// <summary>Says clock times: "10:30" as "ten thirty", "9:05" as "nine oh five", "10:00" as "ten o'clock".</summary>
internal static class ClockWords
{
    /// <summary>The latest hour of a clock time.</summary>
    private const int MaxHour = 23;

    /// <summary>The latest minute of a clock time.</summary>
    private const int MaxMinute = 59;

    /// <summary>Minutes below this are said with "oh", as in "nine oh five".</summary>
    private const int TenMinutes = 10;

    /// <summary>The digits in the minutes of a clock time.</summary>
    private const int MinuteDigits = 2;

    /// <summary>Writes a clock time with an optional a.m. or p.m.</summary>
    /// <param name="context">The time.</param>
    internal static void Write(in MatchContext context)
    {
        var match = context.Match;
        var output = context.Output;
        var colon = match.IndexOf(':');
        var hour = MatchText.Parse(match[..colon]);
        var minute = MatchText.Parse(match.Slice(colon + 1, MinuteDigits));
        if (hour > MaxHour || minute > MaxMinute)
        {
            output.Append(match);
            return;
        }

        var tail = match[(colon + 1 + MinuteDigits)..].TrimStart();
        MatchText.AppendCardinal(output, hour);
        if (minute == 0)
        {
            output.Append(tail.IsEmpty ? " o'clock" : string.Empty);
        }
        else
        {
            output.Append(minute < TenMinutes ? " oh " : " ");
            MatchText.AppendCardinal(output, minute);
        }

        if (tail.IsEmpty)
        {
            return;
        }

        output.Append(' ');
        output.Append(Meridiem(tail[0]));
    }

    /// <summary>Writes an hour followed by a.m. or p.m.</summary>
    /// <param name="context">The hour and the letters.</param>
    internal static void WriteHour(in MatchContext context)
    {
        var match = context.Match;
        var digits = MatchText.LeadingDigits(match);
        context.Output.Append(match[..digits]);
        context.Output.Append(' ');
        context.Output.Append(Meridiem(match[digits..].TrimStart()[0]));
    }

    /// <summary>Says a.m. or p.m. as its letters.</summary>
    /// <param name="letter">The letter a or p.</param>
    /// <returns>The letters, spaced so each is spelled.</returns>
    private static string Meridiem(char letter) => char.ToLowerInvariant(letter) == 'a' ? "A M" : "P M";
}
