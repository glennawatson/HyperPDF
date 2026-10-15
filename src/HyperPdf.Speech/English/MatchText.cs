// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;

namespace HyperPdf.Speech.English;

/// <summary>Reads the parts of a matched piece of text, and writes numbers as words into spoken text, without making strings.</summary>
internal static class MatchText
{
    /// <summary>Counts the ASCII digits a text starts with.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The number of leading digits.</returns>
    internal static int LeadingDigits(ReadOnlySpan<char> text)
    {
        var count = 0;
        while (count < text.Length && char.IsAsciiDigit(text[count]))
        {
            count++;
        }

        return count;
    }

    /// <summary>Counts the ASCII digits a text ends with.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The number of trailing digits.</returns>
    internal static int TrailingDigits(ReadOnlySpan<char> text)
    {
        var count = 0;
        while (count < text.Length && char.IsAsciiDigit(text[text.Length - 1 - count]))
        {
            count++;
        }

        return count;
    }

    /// <summary>Counts the ASCII letters a text starts with.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The number of leading letters.</returns>
    internal static int LeadingLetters(ReadOnlySpan<char> text)
    {
        var count = 0;
        while (count < text.Length && char.IsAsciiLetter(text[count]))
        {
            count++;
        }

        return count;
    }

    /// <summary>Finds the first white space character.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The index, or the text's length when there is none.</returns>
    internal static int WhiteSpaceStart(ReadOnlySpan<char> text)
    {
        var index = 0;
        while (index < text.Length && !char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return index;
    }

    /// <summary>Parses digits.</summary>
    /// <param name="digits">The digits.</param>
    /// <returns>The number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Parse(ReadOnlySpan<char> digits) => int.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);

    /// <summary>Writes a whole number as words.</summary>
    /// <param name="output">The spoken text.</param>
    /// <param name="value">The number.</param>
    internal static void AppendCardinal(SpokenText output, long value)
    {
        var words = new SpokenWords(output, output.Length);
        NumberWords.Cardinal(value, ref words);
    }

    /// <summary>Writes a whole number as ordinal words.</summary>
    /// <param name="output">The spoken text.</param>
    /// <param name="value">The number.</param>
    internal static void AppendOrdinal(SpokenText output, long value)
    {
        var words = new SpokenWords(output, output.Length);
        NumberWords.Ordinal(value, ref words);
    }

    /// <summary>Writes a year as words.</summary>
    /// <param name="output">The spoken text.</param>
    /// <param name="year">The year.</param>
    internal static void AppendYear(SpokenText output, int year)
    {
        var words = new SpokenWords(output, output.Length);
        NumberWords.SayYear(year, ref words);
    }
}
