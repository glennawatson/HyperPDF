// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Speech.English;

/// <summary>Says money: "$1,200.50" is "1,200 dollars and 50 cents", "£3 million" is "3 million pounds".</summary>
internal static class MoneyWords
{
    /// <summary>Currency symbols, singular and plural, and their hundredths.</summary>
    private static readonly Dictionary<char, CurrencyWords> Currencies = new()
    {
        ['$'] = new("dollar", "dollars", "cent", "cents"),
        ['£'] = new("pound", "pounds", "penny", "pence"),
        ['€'] = new("euro", "euros", "cent", "cents"),
        ['¥'] = new("yen", "yen", "sen", "sen"),
    };

    /// <summary>Writes the words for a matched amount of money.</summary>
    /// <param name="context">The symbol, the amount, optional cents and an optional scale.</param>
    internal static void Write(in MatchContext context)
    {
        var currency = Currencies[context.Match[0]];
        var rest = context.Match[1..].TrimStart();
        var amountLength = AmountLength(rest);
        var amount = rest[..amountLength];
        rest = rest[amountLength..];
        var cents = default(ReadOnlySpan<char>);
        if (rest.StartsWith('.'))
        {
            var centsLength = MatchText.LeadingDigits(rest[1..]);
            cents = rest.Slice(1, centsLength);
            rest = rest[(1 + centsLength)..];
        }

        var scale = rest.Trim();
        if (!scale.IsEmpty)
        {
            WriteScaled(amount, cents, scale, in currency, context.Output);
            return;
        }

        WriteWhole(amount, cents.IsEmpty ? 0 : MatchText.Parse(cents), in currency, context.Output);
    }

    /// <summary>Counts the digits and thousands commas an amount starts with.</summary>
    /// <param name="text">The text starting at the amount.</param>
    /// <returns>The amount's length.</returns>
    private static int AmountLength(ReadOnlySpan<char> text)
    {
        var count = 0;
        while (count < text.Length && (char.IsAsciiDigit(text[count]) || text[count] == ','))
        {
            count++;
        }

        return count;
    }

    /// <summary>Writes an amount with a scale word: "3.5 million dollars".</summary>
    /// <param name="amount">The amount as written.</param>
    /// <param name="cents">The digits after the point, or empty.</param>
    /// <param name="scale">The scale as written.</param>
    /// <param name="currency">The currency.</param>
    /// <param name="output">Receives the words.</param>
    private static void WriteScaled(ReadOnlySpan<char> amount, ReadOnlySpan<char> cents, ReadOnlySpan<char> scale, in CurrencyWords currency, SpokenText output)
    {
        output.Append(amount);
        if (!cents.IsEmpty)
        {
            output.Append('.');
            output.Append(cents);
        }

        output.Append(' ');
        output.Append(ScaleWord(scale));
        output.Append(' ');
        output.Append(currency.Many);
    }

    /// <summary>Writes an amount with its units and hundredths.</summary>
    /// <param name="amount">The amount as written.</param>
    /// <param name="minor">The hundredths.</param>
    /// <param name="currency">The currency.</param>
    /// <param name="output">Receives the words.</param>
    private static void WriteWhole(ReadOnlySpan<char> amount, int minor, in CurrencyWords currency, SpokenText output)
    {
        var unit = IsWhole(amount, '1') ? currency.One : currency.Many;
        if (minor == 0)
        {
            output.Append(amount);
            output.Append(' ');
            output.Append(unit);
            return;
        }

        var hundredths = minor == 1 ? currency.Cent : currency.Cents;
        if (!IsWhole(amount, '0'))
        {
            output.Append(amount);
            output.Append(' ');
            output.Append(unit);
            output.Append(" and ");
        }

        output.AppendNumber(minor);
        output.Append(' ');
        output.Append(hundredths);
    }

    /// <summary>Determines whether an amount without its commas is a single digit.</summary>
    /// <param name="amount">The amount as written.</param>
    /// <param name="digit">The digit.</param>
    /// <returns><see langword="true"/> when it is just that digit.</returns>
    private static bool IsWhole(ReadOnlySpan<char> amount, char digit)
    {
        var count = 0;
        foreach (var c in amount)
        {
            if (c == ',')
            {
                continue;
            }

            if (c != digit)
            {
                return false;
            }

            count++;
        }

        return count == 1;
    }

    /// <summary>Gets the word for a scale written after money.</summary>
    /// <param name="scale">The scale as written.</param>
    /// <returns>The word.</returns>
    private static string ScaleWord(ReadOnlySpan<char> scale)
    {
        if (scale.Equals("k", StringComparison.OrdinalIgnoreCase) || scale.Equals("thousand", StringComparison.OrdinalIgnoreCase))
        {
            return "thousand";
        }

        if (scale.Equals("m", StringComparison.OrdinalIgnoreCase) || scale.Equals("million", StringComparison.OrdinalIgnoreCase))
        {
            return "million";
        }

        var billion = scale.Equals("bn", StringComparison.OrdinalIgnoreCase) || scale.Equals("b", StringComparison.OrdinalIgnoreCase) || scale.Equals("billion", StringComparison.OrdinalIgnoreCase);
        return billion ? "billion" : "trillion";
    }

    /// <summary>The words a currency is said with.</summary>
    /// <param name="One">One unit.</param>
    /// <param name="Many">Several units.</param>
    /// <param name="Cent">One hundredth.</param>
    /// <param name="Cents">Several hundredths.</param>
    private readonly record struct CurrencyWords(string One, string Many, string Cent, string Cents);
}
