// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Reading;

/// <summary>Questions about the text of a line: whether it is a page number, a caption, a list item or a footnote.</summary>
internal static class ReadingText
{
    /// <summary>The longest text taken for a page number.</summary>
    private const int MaxPageNumberLength = 16;

    /// <summary>The longest number that starts a numbered list item.</summary>
    private const int MaxItemDigits = 3;

    /// <summary>Characters that start a bulleted list item.</summary>
    private static readonly SearchValues<char> Bullets = SearchValues.Create("•◦▪▫‣⁃∙●○■□–—-*➢►▶✓✔");

    /// <summary>Marks that start a footnote, superscript digits included.</summary>
    private static readonly SearchValues<char> FootnoteMarks = SearchValues.Create("*†‡§¶¹²³⁰⁴⁵⁶⁷⁸⁹");

    /// <summary>Words that start a caption.</summary>
    private static readonly string[] CaptionWords = ["figure", "fig.", "table", "chart", "listing", "plate", "exhibit"];

    /// <summary>Determines whether a character belongs to a right-to-left script: Hebrew, Arabic, Syriac or Thaana.</summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for a right-to-left letter.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsRightToLeft(char c) => c is (>= '֐' and <= 'ࣿ') or (>= 'יִ' and <= '﷿') or (>= 'ﹰ' and <= '﻿');

    /// <summary>Determines whether a line is only a page number, such as "12", "Page 3 of 9", "- 4 -" or "xii".</summary>
    /// <param name="text">The line.</param>
    /// <returns><see langword="true"/> for a page number.</returns>
    internal static bool IsPageNumber(ReadOnlySpan<char> text)
    {
        var span = text.Trim().Trim("-–—|·•[]() ");
        if (span.IsEmpty || span.Length > MaxPageNumberLength)
        {
            return false;
        }

        if (span.StartsWith("page", StringComparison.OrdinalIgnoreCase))
        {
            span = span["page".Length..].TrimStart();
        }

        var digits = 0;
        foreach (var c in span)
        {
            if (char.IsAsciiDigit(c))
            {
                digits++;
            }
            else if (!char.IsWhiteSpace(c) && c != '/' && !"of".Contains(char.ToLowerInvariant(c), StringComparison.Ordinal))
            {
                return digits == 0 && IsRomanNumeral(span);
            }
        }

        return digits > 0;
    }

    /// <summary>Writes a margin line's signature: lower case, digits as <c>#</c>, single spaces.</summary>
    /// <param name="text">The line.</param>
    /// <param name="destination">The buffer, at least as long as the text.</param>
    /// <returns>The characters written.</returns>
    internal static int WriteSignature(ReadOnlySpan<char> text, Span<char> destination)
    {
        var length = 0;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                if (length > 0 && destination[length - 1] != ' ')
                {
                    destination[length] = ' ';
                    length++;
                }
            }
            else
            {
                destination[length] = char.IsDigit(c) ? '#' : char.ToLowerInvariant(c);
                length++;
            }
        }

        while (length > 0 && char.IsWhiteSpace(destination[length - 1]))
        {
            length--;
        }

        return length;
    }

    /// <summary>Determines whether a line starts a caption, such as "Figure 3." or "Table 2:".</summary>
    /// <param name="text">The line.</param>
    /// <returns><see langword="true"/> for a caption.</returns>
    internal static bool IsCaption(ReadOnlySpan<char> text)
    {
        foreach (var word in CaptionWords)
        {
            if (text.Length > word.Length + 1 && text.StartsWith(word, StringComparison.OrdinalIgnoreCase) && StartsCaptionLabel(text[word.Length..].TrimStart()))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether a line starts a list item: a bullet, or a number or letter followed by a full stop or bracket.</summary>
    /// <param name="text">The line.</param>
    /// <returns><see langword="true"/> for a list item.</returns>
    internal static bool StartsItem(ReadOnlySpan<char> text) => text.Length > 1 && (StartsWithBullet(text) || StartsWithItemNumber(text.TrimStart('(')));

    /// <summary>Determines whether a line starts a footnote: a number, symbol or superscript mark.</summary>
    /// <param name="text">The line.</param>
    /// <returns><see langword="true"/> for a footnote start.</returns>
    internal static bool StartsFootnote(ReadOnlySpan<char> text) => !text.IsEmpty && (char.IsAsciiDigit(text[0]) || FootnoteMarks.Contains(text[0]));

    /// <summary>Determines whether text ends like a sentence.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when it ends with a full stop, question or exclamation mark.</returns>
    internal static bool EndsSentence(ReadOnlySpan<char> text) => !text.IsEmpty && text[^1] is '.' or '?' or '!' or ':';

    /// <summary>Determines whether text is a small roman numeral, as front matter is numbered.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> for roman numerals.</returns>
    private static bool IsRomanNumeral(ReadOnlySpan<char> text) => !text.IsEmpty && text.IndexOfAnyExcept("ivxlcIVXLC") < 0;

    /// <summary>Determines whether text starts with a caption's number or letter, such as "3" or "A.".</summary>
    /// <param name="rest">The text after the caption word.</param>
    /// <returns><see langword="true"/> for a label.</returns>
    private static bool StartsCaptionLabel(ReadOnlySpan<char> rest) =>
        !rest.IsEmpty && (char.IsAsciiDigit(rest[0]) || (char.IsAsciiLetterUpper(rest[0]) && rest.Length > 1 && rest[1] is '.' or ':'));

    /// <summary>Determines whether a line starts with a bullet followed by a space.</summary>
    /// <param name="text">The line, at least two characters.</param>
    /// <returns><see langword="true"/> for a bullet.</returns>
    private static bool StartsWithBullet(ReadOnlySpan<char> text) => Bullets.Contains(text[0]) && (text[1] == ' ' || (!Bullets.Contains(text[1]) && !char.IsLetterOrDigit(text[1])));

    /// <summary>Determines whether text starts with an item number such as "3. ", "12) " or "b. ".</summary>
    /// <param name="marker">The text, without an opening bracket.</param>
    /// <returns><see langword="true"/> for an item number.</returns>
    private static bool StartsWithItemNumber(ReadOnlySpan<char> marker)
    {
        var length = marker.Length > 1 && char.IsAsciiLetterLower(marker[0]) ? 1 : CountDigits(marker);
        return length > 0 && marker.Length > length + 1 && marker[length] is '.' or ')' && marker[length + 1] == ' ';
    }

    /// <summary>Counts the leading digits of an item number.</summary>
    /// <param name="marker">The text.</param>
    /// <returns>The digits, at most <see cref="MaxItemDigits"/>.</returns>
    private static int CountDigits(ReadOnlySpan<char> marker)
    {
        var length = 0;
        while (length < marker.Length && length < MaxItemDigits && char.IsAsciiDigit(marker[length]))
        {
            length++;
        }

        return length;
    }
}
