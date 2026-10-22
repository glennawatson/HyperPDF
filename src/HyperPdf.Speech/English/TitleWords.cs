// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Speech.English;

/// <summary>Says abbreviations, "St.", Roman numerals after words, and symbols as words.</summary>
internal static class TitleWords
{
    /// <summary>The value of the Roman numeral V.</summary>
    private const int RomanFive = 5;

    /// <summary>The value of the Roman numeral X.</summary>
    private const int RomanTen = 10;

    /// <summary>The value of the Roman numeral L.</summary>
    private const int RomanFifty = 50;

    /// <summary>The value of the Roman numeral C.</summary>
    private const int RomanHundred = 100;

    /// <summary>The length of "St.".</summary>
    private const int SaintLength = 3;

    /// <summary>Abbreviations said as words, matched with their full stop.</summary>
    private static readonly Dictionary<string, string> Abbreviations = new(StringComparer.Ordinal)
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
    };

    /// <summary>Symbols said as words.</summary>
    private static readonly Dictionary<string, string> Symbols = new(StringComparer.Ordinal)
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
    };

    /// <summary>Writes a comma and a space, for a table of contents leader.</summary>
    /// <param name="context">The leader and the spoken text it is replaced in.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteLeader(in MatchContext context) => context.Output.Append(", ");

    /// <summary>Writes "St." as Saint before a name and as Street otherwise.</summary>
    /// <param name="context">The abbreviation, and the name when there is one.</param>
    internal static void WriteSaint(in MatchContext context)
    {
        if (context.Match.Length <= SaintLength)
        {
            context.Output.Append("Street");
            return;
        }

        context.Output.Append("Saint ");
        context.Output.Append(context.Match[SaintLength..].TrimStart());
    }

    /// <summary>Writes an abbreviation as the words it stands for.</summary>
    /// <param name="context">The abbreviation with its full stop.</param>
    internal static void WriteAbbreviation(in MatchContext context)
    {
        if (Abbreviations.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(context.Match, out var words))
        {
            context.Output.Append(words);
        }
        else
        {
            context.Output.Append(context.Match);
        }
    }

    /// <summary>Writes a symbol as words.</summary>
    /// <param name="context">The symbol.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteSymbol(in MatchContext context) =>
        context.Output.Append(Symbols.GetAlternateLookup<ReadOnlySpan<char>>()[context.Match]);

    /// <summary>Writes a Roman numeral after a word that numbers things as a number: "Chapter IV" as "Chapter four".</summary>
    /// <param name="context">The word and the numeral.</param>
    internal static void WriteNumbered(in MatchContext context)
    {
        var match = context.Match;
        var space = MatchText.WhiteSpaceStart(match);
        context.Output.Append(match[..space]);
        context.Output.Append(' ');
        MatchText.AppendCardinal(context.Output, Roman(match[space..].TrimStart()));
    }

    /// <summary>Writes a Roman numeral after a monarch's or pope's name as an ordinal: "Henry VIII" as "Henry the eighth".</summary>
    /// <param name="context">The name and the numeral.</param>
    internal static void WriteRegnal(in MatchContext context)
    {
        var match = context.Match;
        var space = MatchText.WhiteSpaceStart(match);
        context.Output.Append(match[..space]);
        context.Output.Append(" the ");
        MatchText.AppendOrdinal(context.Output, Roman(match[space..].TrimStart()));
    }

    /// <summary>Reads a Roman numeral.</summary>
    /// <param name="roman">The numeral, in capitals.</param>
    /// <returns>Its value.</returns>
    private static int Roman(ReadOnlySpan<char> roman)
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
}
