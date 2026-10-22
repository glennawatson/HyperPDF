// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Speech.English;

/// <summary>Says measurements, fractions, ranges, versions, page references and signs before numbers.</summary>
internal static class MeasureWords
{
    /// <summary>The smallest denominator of a fraction said by name.</summary>
    private const int MinDenominator = 2;

    /// <summary>The largest numerator or denominator said as a fraction such as "three quarters".</summary>
    private const int MaxFractionPart = 10;

    /// <summary>Units said after a number, singular and plural.</summary>
    private static readonly Dictionary<string, UnitWords> Units = new(StringComparer.Ordinal)
    {
        ["km/h"] = new("kilometre per hour", "kilometres per hour"),
        ["mph"] = new("mile per hour", "miles per hour"),
        ["km"] = new("kilometre", "kilometres"),
        ["cm"] = new("centimetre", "centimetres"),
        ["mm"] = new("millimetre", "millimetres"),
        ["nm"] = new("nanometre", "nanometres"),
        ["µm"] = new("micrometre", "micrometres"),
        ["m"] = new("metre", "metres"),
        ["kg"] = new("kilogram", "kilograms"),
        ["mg"] = new("milligram", "milligrams"),
        ["g"] = new("gram", "grams"),
        ["lb"] = new("pound", "pounds"),
        ["lbs"] = new("pound", "pounds"),
        ["ml"] = new("millilitre", "millilitres"),
        ["mL"] = new("millilitre", "millilitres"),
        ["ft"] = new("foot", "feet"),
        ["°C"] = new("degree Celsius", "degrees Celsius"),
        ["°F"] = new("degree Fahrenheit", "degrees Fahrenheit"),
        ["°"] = new("degree", "degrees"),
        ["KB"] = new("kilobyte", "kilobytes"),
        ["kB"] = new("kilobyte", "kilobytes"),
        ["MB"] = new("megabyte", "megabytes"),
        ["GB"] = new("gigabyte", "gigabytes"),
        ["TB"] = new("terabyte", "terabytes"),
        ["Hz"] = new("hertz", "hertz"),
        ["kHz"] = new("kilohertz", "kilohertz"),
        ["MHz"] = new("megahertz", "megahertz"),
        ["GHz"] = new("gigahertz", "gigahertz"),
        ["ms"] = new("millisecond", "milliseconds"),
        ["kW"] = new("kilowatt", "kilowatts"),
        ["kWh"] = new("kilowatt hour", "kilowatt hours"),
    };

    /// <summary>Fractions said by name.</summary>
    private static readonly Dictionary<int, UnitWords> Denominators = new() { [2] = new("half", "halves"), [3] = new("third", "thirds"), [4] = new("quarter", "quarters") };

    /// <summary>Writes a measurement: "5 km" as "5 kilometres", "1 kg" as "1 kilogram".</summary>
    /// <param name="context">The number and the unit.</param>
    internal static void WriteUnit(in MatchContext context)
    {
        var match = context.Match;
        var length = 0;
        while (length < match.Length && (char.IsAsciiDigit(match[length]) || match[length] is '.' or ','))
        {
            length++;
        }

        var number = match[..length];
        var unit = Units.GetAlternateLookup<ReadOnlySpan<char>>()[match[length..].TrimStart()];
        context.Output.Append(number);
        context.Output.Append(' ');
        context.Output.Append(number.SequenceEqual("1") ? unit.One : unit.Many);
    }

    /// <summary>Writes a simple fraction: "3/4" as "three quarters"; others as "five over eight".</summary>
    /// <param name="context">The fraction.</param>
    internal static void WriteFraction(in MatchContext context)
    {
        var match = context.Match;
        var output = context.Output;
        var slash = match.IndexOf('/');
        var numerator = MatchText.Parse(match[..slash]);
        var denominator = MatchText.Parse(match[(slash + 1)..]);
        if (numerator is < 1 or > MaxFractionPart || denominator is < MinDenominator or > MaxFractionPart)
        {
            output.Append(match);
            return;
        }

        if (numerator == 1)
        {
            output.Append("one ");
        }
        else
        {
            MatchText.AppendCardinal(output, numerator);
            output.Append(' ');
        }

        if (Denominators.TryGetValue(denominator, out var name))
        {
            output.Append(numerator == 1 ? name.One : name.Many);
            return;
        }

        MatchText.AppendOrdinal(output, denominator);
        if (numerator != 1)
        {
            output.Append('s');
        }
    }

    /// <summary>Writes a range between two numbers: "10-20" as "10 to 20".</summary>
    /// <param name="context">The range.</param>
    internal static void WriteRange(in MatchContext context)
    {
        var match = context.Match;
        context.Output.Append(match[..MatchText.LeadingDigits(match)]);
        context.Output.Append(" to ");
        context.Output.Append(match[^MatchText.TrailingDigits(match)..]);
    }

    /// <summary>Writes a version number: "v2.1.3" as "version 2 point 1 point 3".</summary>
    /// <param name="context">The version.</param>
    internal static void WriteVersion(in MatchContext context)
    {
        var number = context.Match;
        if (number[0] is 'v' or 'V')
        {
            context.Output.Append("version ");
            number = number[1..];
        }

        foreach (var c in number)
        {
            if (c == '.')
            {
                context.Output.Append(" point ");
            }
            else
            {
                context.Output.Append(c);
            }
        }
    }

    /// <summary>Writes a page reference: "p. 7" as "page 7" and "pp. 12" as "pages 12".</summary>
    /// <param name="context">The abbreviation and the number.</param>
    internal static void WritePage(in MatchContext context)
    {
        var match = context.Match;
        context.Output.Append(match.StartsWith("pp.") || match.StartsWith("Pp.") ? "pages " : "page ");
        context.Output.Append(match[^MatchText.TrailingDigits(match)..]);
    }

    /// <summary>Writes a number sign or "No." before a number: "#5" as "number 5".</summary>
    /// <param name="context">The sign and the number.</param>
    internal static void WriteNumberSign(in MatchContext context)
    {
        context.Output.Append("number ");
        context.Output.Append(context.Match[^MatchText.TrailingDigits(context.Match)..]);
    }

    /// <summary>Writes a minus sign before a number as a word: "-5" as "minus 5".</summary>
    /// <param name="context">What comes before the sign, the sign and the first digit.</param>
    internal static void WriteNegative(in MatchContext context)
    {
        var match = context.Match;
        if (match[0] is not ('-' or '−'))
        {
            context.Output.Append(match[0]);
        }

        context.Output.Append("minus ");
        context.Output.Append(match[^1]);
    }

    /// <summary>Writes a tilde before a number as a word: "~5" as "about 5".</summary>
    /// <param name="context">The tilde and the first digit.</param>
    internal static void WriteApproximate(in MatchContext context)
    {
        context.Output.Append("about ");
        context.Output.Append(context.Match[^1]);
    }

    /// <summary>The words for one and for several.</summary>
    /// <param name="One">One.</param>
    /// <param name="Many">Several.</param>
    private readonly record struct UnitWords(string One, string Many);
}
