// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace HyperPdf.Speech.English;

/// <summary>
/// Rewrites written English into the words a person reading it aloud would say: money, times, dates, ordinals,
/// ranges, units, abbreviations, Roman numerals, web addresses and symbols. Citation numbers and table of contents
/// leaders are left out. The phonemizer then reads plain words and numbers. Each step finds its matches in the text so
/// far and writes the result into a reused buffer, so nothing is allocated once the buffers have grown.
/// </summary>
internal static partial class TextNormalizer
{
    /// <summary>Full month names, for the date patterns.</summary>
    private const string FullMonths = "January|February|March|April|June|July|August|September|October|November|December";

    /// <summary>Abbreviated month names with their full stops, for the date patterns.</summary>
    private const string ShortMonths = @"Jan\.|Feb\.|Apr\.|Jun\.|Jul\.|Aug\.|Sept?\.|Oct\.|Nov\.|Dec\.";

    /// <summary>Starts for the two patterns whose generated generic range scanner boxes characters.</summary>
    private static readonly SearchValues<char> NonZeroDigits = SearchValues.Create("123456789");

    /// <summary>The steps, in the order they run.</summary>
    private static readonly Step[] Steps = BuildSteps();

    /// <summary>Normalizes one sentence or passage.</summary>
    /// <param name="text">The text as written.</param>
    /// <param name="british">Whether to say dates the British way: the twelfth of March.</param>
    /// <returns>The text as it is said.</returns>
    internal static string Normalize(string text, bool british)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Length == 0 ? string.Empty : Normalize(text.AsSpan(), british, new()).ToString();
    }

    /// <summary>Normalizes one sentence or passage into reused buffers.</summary>
    /// <param name="text">The text as written.</param>
    /// <param name="british">Whether to say dates the British way: the twelfth of March.</param>
    /// <param name="buffers">The buffers; the result lives in them.</param>
    /// <returns>The text as it is said; valid until the buffers are used again.</returns>
    internal static ReadOnlySpan<char> Normalize(ReadOnlySpan<char> text, bool british, NormalizerBuffers buffers)
    {
        ArgumentNullException.ThrowIfNull(buffers);
        if (text.IsEmpty)
        {
            return text;
        }

        // Each step that changes the text writes into the buffer the last one did not.
        var current = text;
        var second = false;
        foreach (var step in Steps)
        {
            var target = second ? buffers.Second : buffers.First;
            target.Clear();
            if (!step.Apply(current, british, target))
            {
                continue;
            }

            current = target.AsSpan();
            second = !second;
        }

        return current.Trim();
    }

    /// <summary>Builds the steps. Email addresses come before web addresses: their domain would otherwise be read as one.</summary>
    /// <returns>The steps.</returns>
    private static Step[] BuildSteps() =>
    [
        new(CitationPattern(), null),
        new(LeaderPattern(), TitleWords.WriteLeader),
        new(EmailPattern(), AddressWords.WriteEmail),
        new(UrlPattern(), AddressWords.WriteUrl),
        new(CurrencyPattern(), MoneyWords.Write),
        new(IsoDatePattern(), DateWords.WriteIso),
        new(DayMonthPattern(), DateWords.WriteDayMonth, NonZeroDigits),
        new(MonthDayPattern(), DateWords.WriteMonthDay),
        new(ClockPattern(), ClockWords.Write),
        new(HourMeridiemPattern(), ClockWords.WriteHour, NonZeroDigits),
        new(DecadePattern(), DateWords.WriteDecade),
        new(OrdinalPattern(), WriteOrdinal),
        new(VersionPattern(), MeasureWords.WriteVersion),
        new(RangePattern(), MeasureWords.WriteRange),
        new(FractionPattern(), MeasureWords.WriteFraction),
        new(UnitPattern(), MeasureWords.WriteUnit),
        new(PagePattern(), MeasureWords.WritePage),
        new(NumberSignPattern(), MeasureWords.WriteNumberSign),
        new(NegativePattern(), MeasureWords.WriteNegative),
        new(ApproximatePattern(), MeasureWords.WriteApproximate),
        new(SaintPattern(), TitleWords.WriteSaint),
        new(AbbreviationPattern(), TitleWords.WriteAbbreviation),
        new(RomanAfterWordPattern(), TitleWords.WriteNumbered),
        new(RegnalPattern(), TitleWords.WriteRegnal),
        new(SymbolPattern(), TitleWords.WriteSymbol),
        new(SpacePattern(), WriteSpace),
    ];

    /// <summary>Writes an ordinal written with digits as words: "21st" as "twenty first".</summary>
    /// <param name="context">The ordinal.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteOrdinal(in MatchContext context) =>
        MatchText.AppendOrdinal(context.Output, MatchText.Parse(context.Match[..MatchText.LeadingDigits(context.Match)]));

    /// <summary>Writes a run of white space as one space.</summary>
    /// <param name="context">The white space.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteSpace(in MatchContext context) => context.Output.Append(' ');

    /// <summary>Citation numbers in brackets, such as [12] or [3, 4–6], and superscript note marks.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"\s?\[\d+(?:\s*[,–-]\s*\d+)*\]|[¹²³⁰⁴-⁹]+")]
    private static partial Regex CitationPattern();

    /// <summary>Table of contents leaders: four or more dots, spaced or not.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"(?:\s*\.){4,}\s*")]
    private static partial Regex LeaderPattern();

    /// <summary>A web address.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"\b(?:https?://)?(?:www\.)?(?<host>[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)*\.(?:com|org|net|edu|gov|io|dev|uk|au|de|fr|info|co)\b)(?<path>/[^\s,;)]*[^\s.,;)])?")]
    private static partial Regex UrlPattern();

    /// <summary>An e-mail address.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"\b(?<user>[A-Za-z0-9._%+-]+)@(?<domain>[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)+)\b")]
    private static partial Regex EmailPattern();

    /// <summary>Money: a currency symbol, an amount, optional cents and an optional scale.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"(?<symbol>[$£€¥])\s?(?<amount>\d{1,3}(?:,\d{3})+|\d+)(?:\.(?<cents>\d{1,2}))?(?:\s?(?<scale>thousand|million|billion|trillion|bn|[kKmMbB])\b)?")]
    private static partial Regex CurrencyPattern();

    /// <summary>A date written as year-month-day.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"\b(?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2})\b")]
    private static partial Regex IsoDatePattern();

    /// <summary>A day before a month name.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"\G\b(?<day>[1-9]|[12]\d|3[01])(?:st|nd|rd|th)?\s+(?<month>May|" + FullMonths + "|" + ShortMonths + ")")]
    private static partial Regex DayMonthPattern();

    /// <summary>A month name before a day.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"(?<month>\b(?:" + FullMonths + @")|\b(?:" + ShortMonths + @"))\s+(?<day>[1-9]|[12]\d|3[01])(?:st|nd|rd|th)?\b(?![\d:])")]
    private static partial Regex MonthDayPattern();

    /// <summary>A clock time with optional a.m. or p.m.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"\b(?<hour>\d{1,2}):(?<minute>\d{2})(?!\d)(?:\s?(?<meridiem>[AaPp])\.?\s?[Mm]\b\.?)?")]
    private static partial Regex ClockPattern();

    /// <summary>An hour followed by a.m. or p.m.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"\G\b(?<hour>1[0-2]|[1-9])\s?(?<meridiem>[AaPp])\.?[Mm]\b\.?")]
    private static partial Regex HourMeridiemPattern();

    /// <summary>A decade, such as 1990s.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"\b(?<year>1[1-9]\d0|20\d0)s\b")]
    private static partial Regex DecadePattern();

    /// <summary>An ordinal written with digits, such as 21st.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"\b(?<number>\d{1,6})(?:st|nd|rd|th)\b")]
    private static partial Regex OrdinalPattern();

    /// <summary>A version number, such as v2.1.3 or 10.4.1.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"\b(?<v>[vV](?=\d))?(?<number>\d+\.\d+\.\d+(?:\.\d+)*)\b|\b(?<v>[vV])(?<number>\d+\.\d+)\b")]
    private static partial Regex VersionPattern();

    /// <summary>A range between two numbers, written with a dash.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"(?<![\d.,-])(?<from>\d{1,4})\s?[–—-]\s?(?<to>\d{1,4})(?![\d-])")]
    private static partial Regex RangePattern();

    /// <summary>A simple fraction, not part of a date.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"(?<![\d/])(?<numerator>\d{1,2})/(?<denominator>\d{1,2})(?![\d/])")]
    private static partial Regex FractionPattern();

    /// <summary>A number followed by a unit.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"(?<number>\d+(?:[.,]\d+)?)\s?(?<unit>km/h|mph|kWh|kW|km|cm|mm|nm|µm|kg|mg|lbs|lb|mL|ml|ft|°C|°F|°|KB|kB|MB|GB|TB|kHz|MHz|GHz|Hz|ms|m|g)(?![A-Za-z0-9])")]
    private static partial Regex UnitPattern();

    /// <summary>A page reference: p. 7 or pp. 12 to 15.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"\b(?<abbreviation>pp?\.|Pp?\.)\s?(?<number>\d+)")]
    private static partial Regex PagePattern();

    /// <summary>A number sign or "No." before a number.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"(?:#|\bNo\.|\bno\.)\s?(?<number>\d+)")]
    private static partial Regex NumberSignPattern();

    /// <summary>A minus sign before a number.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"(?<before>^|[\s(=])[-−](?<number>\d)")]
    private static partial Regex NegativePattern();

    /// <summary>A tilde before a number, meaning about.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"~\s?(?<number>\d)")]
    private static partial Regex ApproximatePattern();

    /// <summary>"St." before a name is Saint; otherwise Street.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"\bSt\.(?:\s+(?<name>[A-Z][a-z]+))?")]
    private static partial Regex SaintPattern();

    /// <summary>An abbreviation with its full stop.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"(?<![\w.])(?:Dr|Mrs|Mr|Ms|Prof|e\.g|i\.e|etc|vs|cf|[Aa]pprox|Figs|[Ff]ig|[Ee]q|[Vv]ol|Ch|Sec|Dept|Univ|Inc|Ltd|Corp"
        + "|Jr|Sr|Gen|Gov|Capt|Lt|Mt|Ave|Rd|Jan|Feb|Apr|Jun|Jul|Aug|Sept|Sep|Oct|Nov|Dec)\\.")]
    private static partial Regex AbbreviationPattern();

    /// <summary>A Roman numeral after a word that numbers things: Chapter IV, World War II.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"\b(?<word>Chapter|Part|Volume|Book|Section|Appendix|Act|Scene|Phase|Stage|Type|Class|War|Article|Annex|Schedule|Level|Grade|Title|Figure|Table)\s+(?<roman>[IVXLC]+)\b")]
    private static partial Regex RomanAfterWordPattern();

    /// <summary>A Roman numeral after a monarch's or pope's name: Henry VIII, Elizabeth II.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"\b(?<name>Henry|Edward|George|Elizabeth|Charles|Louis|William|Richard|James|Mary|John|Paul|Benedict|Pius|Leo|Napoleon|Philip"
        + @"|Frederick|Alexander|Nicholas|Peter|Catherine|Ferdinand|Gregory|Innocent|Clement|Urban|Victor|Ludwig|Gustav|Christian|Olaf|Harald|Haakon|Carl)\s+(?<roman>[IVX]{2,}|V|X)\b")]
    private static partial Regex RegnalPattern();

    /// <summary>Symbols said as words.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"§§|w/o|w/|24/7|[±×÷≈≤≥≠<>→§¶©®™]")]
    private static partial Regex SymbolPattern();

    /// <summary>Runs of white space.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex SpacePattern();
}
