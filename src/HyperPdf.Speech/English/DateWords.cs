// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Speech.English;

/// <summary>Says dates and decades: "2024-03-12" as "March twelfth, two thousand twenty four" and "1990s" as "nineteen nineties".</summary>
internal static class DateWords
{
    /// <summary>The months in a year.</summary>
    private const int MonthsInYear = 12;

    /// <summary>The last day of the longest month.</summary>
    private const int LastDay = 31;

    /// <summary>The digits in a year.</summary>
    private const int YearLength = 4;

    /// <summary>The digits in a month or a day written in a numeric date.</summary>
    private const int PairLength = 2;

    /// <summary>The characters from the start of a date written as year-month-day to its day.</summary>
    private const int DayOffset = YearLength + 1 + PairLength + 1;

    /// <summary>The characters from the start of a date written as year-month-day to its month.</summary>
    private const int MonthOffset = YearLength + 1;

    /// <summary>The months, for dates written as numbers and abbreviated month names.</summary>
    private static readonly string[] Months = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];

    /// <summary>Writes a date written as 2024-03-12.</summary>
    /// <param name="context">The date.</param>
    internal static void WriteIso(in MatchContext context)
    {
        var match = context.Match;
        var output = context.Output;
        var month = MatchText.Parse(match.Slice(MonthOffset, PairLength));
        var day = MatchText.Parse(match.Slice(DayOffset, PairLength));
        if (month is < 1 or > MonthsInYear || day is < 1 or > LastDay)
        {
            output.Append(match);
            return;
        }

        var name = Months[month - 1];
        var year = MatchText.Parse(match[..YearLength]);
        if (context.British)
        {
            output.Append("the ");
            MatchText.AppendOrdinal(output, day);
            output.Append(" of ");
            output.Append(name);
            output.Append(' ');
            MatchText.AppendYear(output, year);
            return;
        }

        output.Append(name);
        output.Append(' ');
        MatchText.AppendOrdinal(output, day);
        output.Append(", ");
        MatchText.AppendYear(output, year);
    }

    /// <summary>Writes a date written day first: "12 March" as "the twelfth of March" (or "March twelfth").</summary>
    /// <param name="context">The day and month.</param>
    internal static void WriteDayMonth(in MatchContext context)
    {
        var match = context.Match;
        var output = context.Output;
        var digits = MatchText.LeadingDigits(match);
        var day = MatchText.Parse(match[..digits]);
        var rest = match[digits..];
        rest = rest[MatchText.LeadingLetters(rest)..].TrimStart();
        if (context.British)
        {
            output.Append("the ");
            MatchText.AppendOrdinal(output, day);
            output.Append(" of ");
            AppendMonthName(output, rest);
            return;
        }

        AppendMonthName(output, rest);
        output.Append(' ');
        MatchText.AppendOrdinal(output, day);
    }

    /// <summary>Writes a date written month first: "March 12" as "March twelfth".</summary>
    /// <param name="context">The month and day.</param>
    internal static void WriteMonthDay(in MatchContext context)
    {
        var match = context.Match;
        var space = MatchText.WhiteSpaceStart(match);
        var day = match[space..].TrimStart();
        AppendMonthName(context.Output, match[..space]);
        context.Output.Append(' ');
        MatchText.AppendOrdinal(context.Output, MatchText.Parse(day[..MatchText.LeadingDigits(day)]));
    }

    /// <summary>Writes a decade: "1990s" as "nineteen nineties".</summary>
    /// <param name="context">The decade.</param>
    internal static void WriteDecade(in MatchContext context)
    {
        var output = context.Output;
        var start = output.Length;
        MatchText.AppendYear(output, MatchText.Parse(context.Match[..YearLength]));
        var words = output.AsSpan()[start..];
        if (!words.EndsWith("y"))
        {
            output.Append('s');
            return;
        }

        output.Truncate(output.Length - 1);
        output.Append("ies");
    }

    /// <summary>Writes a month's full name from its full or abbreviated name.</summary>
    /// <param name="output">Receives the name.</param>
    /// <param name="month">The month as written.</param>
    private static void AppendMonthName(SpokenText output, ReadOnlySpan<char> month)
    {
        var prefix = month.TrimEnd('.');
        foreach (var name in Months)
        {
            if (!name.AsSpan().StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            output.Append(name);
            return;
        }

        output.Append(month);
    }
}
