// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace HyperPdf.Speech.English;

/// <summary>
/// Rewrites written English into the words a person reading it aloud would say: money, times, dates, ordinals,
/// ranges, units, abbreviations, Roman numerals, web addresses and symbols. Citation numbers and table of contents
/// leaders are left out. The phonemizer then reads plain words and numbers.
/// </summary>
internal static partial class TextNormalizer
{
    /// <summary>The name of the regular expression group holding the month.</summary>
    private const string MonthGroup = "month";

    /// <summary>The name of the regular expression group holding the meridiem.</summary>
    private const string MeridiemGroup = "meridiem";

    /// <summary>The name of the regular expression group holding the number.</summary>
    private const string NumberGroup = "number";

    /// <summary>The name of the regular expression group holding the cents.</summary>
    private const string CentsGroup = "cents";

    /// <summary>The name of the regular expression group holding the day.</summary>
    private const string DayGroup = "day";

    /// <summary>The name of the regular expression group holding the year.</summary>
    private const string YearGroup = "year";

    /// <summary>The name of the regular expression group holding the hour.</summary>
    private const string HourGroup = "hour";

    /// <summary>The name of the regular expression group holding the minute.</summary>
    private const string MinuteGroup = "minute";

    /// <summary>The name of the regular expression group holding the amount.</summary>
    private const string AmountGroup = "amount";

    /// <summary>The name of the regular expression group holding the symbol.</summary>
    private const string SymbolGroup = "symbol";

    /// <summary>The name of the regular expression group holding the scale.</summary>
    private const string ScaleGroup = "scale";

    /// <summary>The name of the regular expression group holding the numerator.</summary>
    private const string NumeratorGroup = "numerator";

    /// <summary>The name of the regular expression group holding the denominator.</summary>
    private const string DenominatorGroup = "denominator";

    /// <summary>The name of the regular expression group holding the unit.</summary>
    private const string UnitGroup = "unit";

    /// <summary>The name of the regular expression group holding the name.</summary>
    private const string NameGroup = "name";

    /// <summary>The name of the regular expression group holding the roman.</summary>
    private const string RomanGroup = "roman";

    /// <summary>The name of the regular expression group holding the word.</summary>
    private const string WordGroup = "word";

    /// <summary>The name of the regular expression group holding the from.</summary>
    private const string FromGroup = "from";

    /// <summary>The name of the regular expression group holding the to.</summary>
    private const string ToGroup = "to";

    /// <summary>The name of the regular expression group holding the host.</summary>
    private const string HostGroup = "host";

    /// <summary>The name of the regular expression group holding the path.</summary>
    private const string PathGroup = "path";

    /// <summary>The name of the regular expression group holding the user.</summary>
    private const string UserGroup = "user";

    /// <summary>The name of the regular expression group holding the domain.</summary>
    private const string DomainGroup = "domain";

    /// <summary>The name of the regular expression group holding the abbreviation.</summary>
    private const string AbbreviationGroup = "abbreviation";

    /// <summary>The name of the regular expression group holding the v.</summary>
    private const string VGroup = "v";

    /// <summary>The months in a year.</summary>
    private const int MonthsInYear = 12;

    /// <summary>The last day of the longest month.</summary>
    private const int LastDay = 31;

    /// <summary>How much longer a web address grows when its punctuation is said as words.</summary>
    private const int AddressGrowth = 2;

    /// <summary>The smallest denominator of a fraction said by name.</summary>
    private const int MinDenominator = 2;

    /// <summary>The value of the Roman numeral V.</summary>
    private const int RomanFive = 5;

    /// <summary>The value of the Roman numeral X.</summary>
    private const int RomanTen = 10;

    /// <summary>The value of the Roman numeral L.</summary>
    private const int RomanFifty = 50;

    /// <summary>The value of the Roman numeral C.</summary>
    private const int RomanHundred = 100;

    /// <summary>Full month names, for the date patterns.</summary>
    private const string FullMonths = "January|February|March|April|June|July|August|September|October|November|December";

    /// <summary>Abbreviated month names with their full stops, for the date patterns.</summary>
    private const string ShortMonths = @"Jan\.|Feb\.|Apr\.|Jun\.|Jul\.|Aug\.|Sept?\.|Oct\.|Nov\.|Dec\.";

    /// <summary>The largest numerator or denominator said as a fraction such as "three quarters".</summary>
    private const int MaxFractionPart = 10;

    /// <summary>The latest hour of a clock time.</summary>
    private const int MaxHour = 23;

    /// <summary>The latest minute of a clock time.</summary>
    private const int MaxMinute = 59;

    /// <summary>Minutes below this are said with "oh", as in "nine oh five".</summary>
    private const int TenMinutes = 10;

    /// <summary>The months, for dates written as numbers and abbreviated month names.</summary>
    private static readonly string[] Months = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];

    /// <summary>Abbreviations said as words, matched with their full stop.</summary>
    private static readonly FrozenDictionary<string, string> Abbreviations = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Dr."] = "Doctor",
        ["Mr."] = "Mister",
        ["Mrs."] = "Missus",
        ["Ms."] = "Miz",
        ["Prof."] = "Professor",
        ["e.g."] = "for example",
        ["i.e."] = "that is",
        ["etc."] = "et cetera",
        ["vs."] = "versus",
        ["cf."] = "compare",
        ["approx."] = "approximately",
        ["Approx."] = "Approximately",
        ["Fig."] = "Figure",
        ["fig."] = "figure",
        ["Figs."] = "Figures",
        ["Eq."] = "Equation",
        ["eq."] = "equation",
        ["Vol."] = "Volume",
        ["vol."] = "volume",
        ["Ch."] = "Chapter",
        ["Sec."] = "Section",
        ["Dept."] = "Department",
        ["Univ."] = "University",
        ["Inc."] = "Incorporated",
        ["Ltd."] = "Limited",
        ["Corp."] = "Corporation",
        ["Jr."] = "Junior",
        ["Sr."] = "Senior",
        ["Gen."] = "General",
        ["Gov."] = "Governor",
        ["Capt."] = "Captain",
        ["Lt."] = "Lieutenant",
        ["Mt."] = "Mount",
        ["Ave."] = "Avenue",
        ["Rd."] = "Road",
        ["Jan."] = "January",
        ["Feb."] = "February",
        ["Apr."] = "April",
        ["Jun."] = "June",
        ["Jul."] = "July",
        ["Aug."] = "August",
        ["Sep."] = "September",
        ["Sept."] = "September",
        ["Oct."] = "October",
        ["Nov."] = "November",
        ["Dec."] = "December",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Units said after a number, singular and plural.</summary>
    private static readonly FrozenDictionary<string, (string One, string Many)> Units = new Dictionary<string, (string One, string Many)>(StringComparer.Ordinal)
    {
        ["km/h"] = ("kilometre per hour", "kilometres per hour"),
        ["mph"] = ("mile per hour", "miles per hour"),
        ["km"] = ("kilometre", "kilometres"),
        ["cm"] = ("centimetre", "centimetres"),
        ["mm"] = ("millimetre", "millimetres"),
        ["nm"] = ("nanometre", "nanometres"),
        ["µm"] = ("micrometre", "micrometres"),
        ["m"] = ("metre", "metres"),
        ["kg"] = ("kilogram", "kilograms"),
        ["mg"] = ("milligram", "milligrams"),
        ["g"] = ("gram", "grams"),
        ["lb"] = ("pound", "pounds"),
        ["lbs"] = ("pound", "pounds"),
        ["ml"] = ("millilitre", "millilitres"),
        ["mL"] = ("millilitre", "millilitres"),
        ["ft"] = ("foot", "feet"),
        ["°C"] = ("degree Celsius", "degrees Celsius"),
        ["°F"] = ("degree Fahrenheit", "degrees Fahrenheit"),
        ["°"] = ("degree", "degrees"),
        ["KB"] = ("kilobyte", "kilobytes"),
        ["kB"] = ("kilobyte", "kilobytes"),
        ["MB"] = ("megabyte", "megabytes"),
        ["GB"] = ("gigabyte", "gigabytes"),
        ["TB"] = ("terabyte", "terabytes"),
        ["Hz"] = ("hertz", "hertz"),
        ["kHz"] = ("kilohertz", "kilohertz"),
        ["MHz"] = ("megahertz", "megahertz"),
        ["GHz"] = ("gigahertz", "gigahertz"),
        ["ms"] = ("millisecond", "milliseconds"),
        ["kW"] = ("kilowatt", "kilowatts"),
        ["kWh"] = ("kilowatt hour", "kilowatt hours"),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Currency symbols, singular and plural, and their hundredths.</summary>
    private static readonly FrozenDictionary<char, (string One, string Many, string Cent, string Cents)> Currencies = new Dictionary<char, (string One, string Many, string Cent, string Cents)>
    {
        ['$'] = ("dollar", "dollars", "cent", "cents"),
        ['£'] = ("pound", "pounds", "penny", "pence"),
        ['€'] = ("euro", "euros", "cent", "cents"),
        ['¥'] = ("yen", "yen", "sen", "sen"),
    }.ToFrozenDictionary();

    /// <summary>Fractions said by name.</summary>
    private static readonly FrozenDictionary<int, (string One, string Many)> Denominators = new Dictionary<int, (string One, string Many)>
    {
        [2] = ("half", "halves"),
        [3] = ("third", "thirds"),
        [4] = ("quarter", "quarters"),
    }.ToFrozenDictionary();

    /// <summary>Symbols said as words.</summary>
    private static readonly FrozenDictionary<string, string> Symbols = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["±"] = " plus or minus ",
        ["×"] = " times ",
        ["÷"] = " divided by ",
        ["≈"] = " approximately ",
        ["≤"] = " less than or equal to ",
        ["≥"] = " greater than or equal to ",
        ["≠"] = " not equal to ",
        ["<"] = " less than ",
        [">"] = " greater than ",
        ["→"] = " to ",
        ["§§"] = " sections ",
        ["§"] = " section ",
        ["¶"] = " paragraph ",
        ["©"] = " copyright ",
        ["®"] = " ",
        ["™"] = " ",
        ["w/o"] = " without ",
        ["w/"] = " with ",
        ["24/7"] = " twenty four seven ",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Normalizes one sentence or passage.</summary>
    /// <param name="text">The text as written.</param>
    /// <param name="british">Whether to say dates the British way: the twelfth of March.</param>
    /// <returns>The text as it is said.</returns>
    internal static string Normalize(string text, bool british)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = CitationPattern().Replace(text, string.Empty);
        result = LeaderPattern().Replace(result, ", ");

        // E-mail addresses first: their domain would otherwise be read as a web address.
        result = EmailPattern().Replace(result, static m => $"{SayAddress(m.Groups[UserGroup].Value)} at {SayAddress(m.Groups[DomainGroup].Value)}");
        result = UrlPattern().Replace(result, static m => SayAddress(m.Groups[HostGroup].Value + m.Groups[PathGroup].Value));
        result = CurrencyPattern().Replace(result, SayMoney);
        result = IsoDatePattern().Replace(result, m => SayIsoDate(m, british));
        result = DayMonthPattern().Replace(result, m => SayDayMonth(m, british));
        result = MonthDayPattern().Replace(result, static m => $"{MonthName(m.Groups[MonthGroup].Value)} {NumberWords.Ordinal(Parse(m.Groups[DayGroup].Value))}");
        result = ClockPattern().Replace(result, SayClock);
        result = HourMeridiemPattern().Replace(result, static m => $"{m.Groups[HourGroup].Value} {Meridiem(m.Groups[MeridiemGroup].Value)}");
        result = DecadePattern().Replace(result, SayDecade);
        result = OrdinalPattern().Replace(result, static m => NumberWords.Ordinal(Parse(m.Groups[NumberGroup].Value)));
        result = VersionPattern().Replace(result, SayVersion);
        result = RangePattern().Replace(result, static m => $"{m.Groups[FromGroup].Value} to {m.Groups[ToGroup].Value}");
        result = FractionPattern().Replace(result, SayFraction);
        result = UnitPattern().Replace(result, SayUnit);
        result = PagePattern().Replace(result, static m => $"{(m.Groups[AbbreviationGroup].Value is "pp." or "Pp." ? "pages" : "page")} {m.Groups[NumberGroup].Value}");
        result = NumberSignPattern().Replace(result, static m => $"number {m.Groups[NumberGroup].Value}");
        result = NegativePattern().Replace(result, static m => $"{m.Groups["before"].Value}minus {m.Groups[NumberGroup].Value}");
        result = ApproximatePattern().Replace(result, static m => $"about {m.Groups[NumberGroup].Value}");
        result = SaintPattern().Replace(result, static m => m.Groups[NameGroup].Success ? $"Saint {m.Groups[NameGroup].Value}" : "Street");
        result = AbbreviationPattern().Replace(result, static m => Abbreviations.TryGetValue(m.Value, out var words) ? words : m.Value);
        result = RomanAfterWordPattern().Replace(result, static m => $"{m.Groups[WordGroup].Value} {NumberWords.Cardinal(Roman(m.Groups[RomanGroup].Value))}");
        result = RegnalPattern().Replace(result, static m => $"{m.Groups[NameGroup].Value} the {NumberWords.Ordinal(Roman(m.Groups[RomanGroup].Value))}");
        result = SymbolPattern().Replace(result, static m => Symbols[m.Value]);
        return SpacePattern().Replace(result, " ").Trim();
    }

    /// <summary>Says money: "$1,200.50" is "1,200 dollars and 50 cents", "£3 million" is "3 million pounds".</summary>
    /// <param name="match">The match.</param>
    /// <returns>The words.</returns>
    private static string SayMoney(Match match)
    {
        var currency = Currencies[match.Groups[SymbolGroup].Value[0]];
        var amount = match.Groups[AmountGroup].Value;
        var scale = match.Groups[ScaleGroup].Success ? ScaleWord(match.Groups[ScaleGroup].Value) : null;
        var minor = match.Groups[CentsGroup].Success ? Parse(match.Groups[CentsGroup].Value) : 0;
        if (scale is not null)
        {
            var decimals = match.Groups[CentsGroup].Success ? $".{match.Groups[CentsGroup].Value}" : string.Empty;
            return $"{amount}{decimals} {scale} {currency.Many}";
        }

        var whole = amount.Replace(",", string.Empty, StringComparison.Ordinal);
        var unit = whole == "1" ? currency.One : currency.Many;
        if (minor == 0)
        {
            return $"{amount} {unit}";
        }

        var hundredths = minor == 1 ? currency.Cent : currency.Cents;
        return whole == "0" ? $"{minor} {hundredths}" : $"{amount} {unit} and {minor} {hundredths}";
    }

    /// <summary>Says a version number: "v2.1.3" is "version 2 point 1 point 3".</summary>
    /// <param name="match">The match.</param>
    /// <returns>The words.</returns>
    private static string SayVersion(Match match)
    {
        var prefix = match.Groups[VGroup].Success ? "version " : string.Empty;
        return prefix + match.Groups[NumberGroup].Value.Replace(".", " point ", StringComparison.Ordinal);
    }

    /// <summary>Says a date written as 2024-03-12.</summary>
    /// <param name="match">The match.</param>
    /// <param name="british">Whether British.</param>
    /// <returns>The words.</returns>
    private static string SayIsoDate(Match match, bool british)
    {
        var month = Parse(match.Groups[MonthGroup].Value);
        var day = Parse(match.Groups[DayGroup].Value);
        if (month is < 1 or > MonthsInYear || day is < 1 or > LastDay)
        {
            return match.Value;
        }

        var year = NumberWords.SayYear(Parse(match.Groups[YearGroup].Value));
        var name = Months[month - 1];
        var ordinal = NumberWords.Ordinal(day);
        return british ? $"the {ordinal} of {name} {year}" : $"{name} {ordinal}, {year}";
    }

    /// <summary>Says a date written day first: "12 March" is "the twelfth of March" (or "March twelfth").</summary>
    /// <param name="match">The match.</param>
    /// <param name="british">Whether British.</param>
    /// <returns>The words.</returns>
    private static string SayDayMonth(Match match, bool british)
    {
        var ordinal = NumberWords.Ordinal(Parse(match.Groups[DayGroup].Value));
        var month = MonthName(match.Groups[MonthGroup].Value);
        return british ? $"the {ordinal} of {month}" : $"{month} {ordinal}";
    }

    /// <summary>Says a clock time: "10:30" is "ten thirty", "9:05" "nine oh five", "10:00" "ten o'clock".</summary>
    /// <param name="match">The match.</param>
    /// <returns>The words.</returns>
    private static string SayClock(Match match)
    {
        var hour = Parse(match.Groups[HourGroup].Value);
        var minute = Parse(match.Groups[MinuteGroup].Value);
        if (hour > MaxHour || minute > MaxMinute)
        {
            return match.Value;
        }

        var meridiem = match.Groups[MeridiemGroup].Success ? $" {Meridiem(match.Groups[MeridiemGroup].Value)}" : string.Empty;
        var minutes = minute switch
        {
            0 when meridiem.Length == 0 => " o'clock",
            0 => string.Empty,
            < TenMinutes => $" oh {NumberWords.Cardinal(minute)}",
            _ => $" {NumberWords.Cardinal(minute)}",
        };
        return $"{NumberWords.Cardinal(hour)}{minutes}{meridiem}";
    }

    /// <summary>Says a decade: "1990s" is "nineteen nineties".</summary>
    /// <param name="match">The match.</param>
    /// <returns>The words.</returns>
    private static string SayDecade(Match match)
    {
        var words = NumberWords.SayYear(Parse(match.Groups[YearGroup].Value));
        if (words.EndsWith("hundred", StringComparison.Ordinal) || words.EndsWith("thousand", StringComparison.Ordinal))
        {
            return $"{words}s";
        }

        return words.EndsWith('y') ? $"{words[..^1]}ies" : $"{words}s";
    }

    /// <summary>Says a simple fraction: "3/4" is "three quarters"; others are "five over eight".</summary>
    /// <param name="match">The match.</param>
    /// <returns>The words.</returns>
    private static string SayFraction(Match match)
    {
        var numerator = Parse(match.Groups[NumeratorGroup].Value);
        var denominator = Parse(match.Groups[DenominatorGroup].Value);
        if (numerator is < 1 or > MaxFractionPart || denominator is < MinDenominator or > MaxFractionPart)
        {
            return match.Value;
        }

        if (Denominators.TryGetValue(denominator, out var name))
        {
            return numerator == 1 ? $"one {name.One}" : $"{NumberWords.Cardinal(numerator)} {name.Many}";
        }

        var ordinal = NumberWords.Ordinal(denominator);
        return numerator == 1 ? $"one {ordinal}" : $"{NumberWords.Cardinal(numerator)} {ordinal}s";
    }

    /// <summary>Says a measurement: "5 km" is "5 kilometres", "1 kg" "1 kilogram".</summary>
    /// <param name="match">The match.</param>
    /// <returns>The words.</returns>
    private static string SayUnit(Match match)
    {
        var number = match.Groups[NumberGroup].Value;
        var unit = Units[match.Groups[UnitGroup].Value];
        return $"{number} {(number == "1" ? unit.One : unit.Many)}";
    }

    /// <summary>Says a web address or e-mail part: dots, slashes and dashes as words.</summary>
    /// <param name="address">The address without its scheme.</param>
    /// <returns>The words.</returns>
    private static string SayAddress(string address)
    {
        var builder = new StringBuilder(address.Length * AddressGrowth);
        foreach (var c in address.TrimEnd('/'))
        {
            _ = c switch
            {
                '.' => builder.Append(" dot "),
                '/' => builder.Append(" slash "),
                '-' => builder.Append(" dash "),
                '_' => builder.Append(" underscore "),
                _ => builder.Append(c),
            };
        }

        return builder.ToString();
    }

    /// <summary>Gets the word for a scale written after money.</summary>
    /// <param name="scale">The scale as written.</param>
    /// <returns>The word.</returns>
    private static string ScaleWord(string scale) => scale.ToLowerInvariant() switch
    {
        "k" or "thousand" => "thousand",
        "m" or "million" => "million",
        "bn" or "b" or "billion" => "billion",
        _ => "trillion",
    };

    /// <summary>Gets a month's full name from its full or abbreviated name.</summary>
    /// <param name="month">The month as written.</param>
    /// <returns>The full name.</returns>
    private static string MonthName(string month)
    {
        foreach (var name in Months)
        {
            if (name.StartsWith(month.TrimEnd('.'), StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }

        return month;
    }

    /// <summary>Says a.m. or p.m. as its letters.</summary>
    /// <param name="meridiem">The letter a or p.</param>
    /// <returns>The letters, spaced so each is spelled.</returns>
    private static string Meridiem(string meridiem) => char.ToLowerInvariant(meridiem[0]) == 'a' ? "A M" : "P M";

    /// <summary>Reads a Roman numeral.</summary>
    /// <param name="roman">The numeral, in capitals.</param>
    /// <returns>Its value.</returns>
    private static int Roman(string roman)
    {
        var total = 0;
        var previous = 0;
        for (var i = roman.Length - 1; i >= 0; i--)
        {
            var value = roman[i] switch
            {
                'I' => 1,
                'V' => RomanFive,
                'X' => RomanTen,
                'L' => RomanFifty,
                _ => RomanHundred,
            };
            total += value < previous ? -value : value;
            previous = Math.Max(previous, value);
        }

        return total;
    }

    /// <summary>Parses digits.</summary>
    /// <param name="digits">The digits.</param>
    /// <returns>The number.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static int Parse(string digits) => int.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);

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
    [GeneratedRegex(@"\b(?<day>[1-9]|[12]\d|3[01])(?:st|nd|rd|th)?\s+(?<month>May|" + FullMonths + "|" + ShortMonths + ")")]
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
    [GeneratedRegex(@"\b(?<hour>1[0-2]|[1-9])\s?(?<meridiem>[AaPp])\.?[Mm]\b\.?")]
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
