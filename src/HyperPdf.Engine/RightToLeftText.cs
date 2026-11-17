// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.PageObjects;

namespace HyperPdf.Engine;

/// <summary>Puts the words of a right-to-left text object in the order they were typed. A page stores them in the order they are drawn, which is the reverse.</summary>
internal static class RightToLeftText
{
    /// <summary>The first character of the Hebrew block; nothing below it is a right-to-left letter.</summary>
    private const int FirstRightToLeft = 0x0590;

    /// <summary>The end of the Arabic and related blocks.</summary>
    private const int ArabicEnd = 0x08FF;

    /// <summary>The start of the Hebrew and Arabic presentation forms.</summary>
    private const int PresentationStart = 0xFB1D;

    /// <summary>The end of the Arabic presentation forms.</summary>
    private const int PresentationEnd = 0xFEFF;

    /// <summary>Gets the words of a text object in the order they were typed.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based index of the page the object is on.</param>
    /// <param name="run">The text object.</param>
    /// <returns>
    /// The words in typed order: for text with right-to-left letters, what the page's text layer reads in the object's area when it has the same letters, which also puts mixed
    /// directions in order, and otherwise the words reversed when the whole line is right-to-left. Text with none is returned as it is.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string Typed(PdfDocument document, int pageIndex, PdfTextObject run) => Typed(document, pageIndex, run.Text, run.Bounds);

    /// <summary>Gets the words of a text in the order they were typed, for a text drawn in pieces whose words and area are those of all its pieces.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based index of the page the text is on.</param>
    /// <param name="drawn">The words in the order they are drawn.</param>
    /// <param name="area">The area the text covers.</param>
    /// <returns>The words in typed order, as for a single text object.</returns>
    internal static string Typed(PdfDocument document, int pageIndex, string drawn, PdfRectangle area)
    {
        if (!HasRightToLeftLetter(drawn))
        {
            return drawn;
        }

        var fromPage = PdfDocumentText.GetTextPage(document, pageIndex).GetTextInRectangle(area).Trim();
        return SameLetters(fromPage, drawn) ? fromPage : ToTyped(drawn);
    }

    /// <summary>Gets the typed order of a text object's words when it is a line of right-to-left letters, with spaces, digits and punctuation.</summary>
    /// <param name="drawn">The words in the order they are drawn.</param>
    /// <returns>The words in typed order; text with a left-to-right letter in it, or with no right-to-left letter, is returned as it is.</returns>
    internal static string ToTyped(string drawn)
    {
        if (!HasRightToLeftOnly(drawn))
        {
            return drawn;
        }

        var elements = new List<string>();
        var parts = StringInfo.GetTextElementEnumerator(drawn);
        while (parts.MoveNext())
        {
            elements.Add((string)parts.Current);
        }

        elements.Reverse();
        var typed = string.Concat(elements).ToCharArray();

        // Digits are read left to right inside right-to-left text, so each run of them is turned back.
        var start = 0;
        while (start < typed.Length)
        {
            if (!char.IsAsciiDigit(typed[start]))
            {
                start++;
                continue;
            }

            var end = start;
            while (end < typed.Length && char.IsAsciiDigit(typed[end]))
            {
                end++;
            }

            Array.Reverse(typed, start, end - start);
            start = end;
        }

        return new(typed);
    }

    /// <summary>Tells whether text has a character from a right-to-left block.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when it has one.</returns>
    private static bool HasRightToLeftLetter(string text)
    {
        foreach (var character in text)
        {
            if (character >= FirstRightToLeft && (character <= ArabicEnd || (character >= PresentationStart && character <= PresentationEnd)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tells whether two texts hold the same letters, whatever their order and the white space between them.</summary>
    /// <param name="first">The first text.</param>
    /// <param name="second">The second text.</param>
    /// <returns><see langword="true"/> when they do.</returns>
    private static bool SameLetters(string first, string second)
    {
        var a = Letters(first);
        var b = Letters(second);
        return a.AsSpan().SequenceEqual(b);
    }

    /// <summary>Gets the characters of a text that are not white space or controls, in code order.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The characters.</returns>
    private static char[] Letters(string text)
    {
        var letters = new List<char>(text.Length);
        foreach (var character in text)
        {
            if (!char.IsWhiteSpace(character) && !char.IsControl(character))
            {
                letters.Add(character);
            }
        }

        var sorted = letters.ToArray();
        Array.Sort(sorted);
        return sorted;
    }

    /// <summary>Tells whether text has a right-to-left letter and no letter that reads left to right.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> for a line of right-to-left words.</returns>
    private static bool HasRightToLeftOnly(string text)
    {
        var found = false;
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value < FirstRightToLeft)
            {
                if (Rune.IsLetter(rune))
                {
                    return false;
                }

                continue;
            }

            if (rune.Value <= ArabicEnd || (rune.Value >= PresentationStart && rune.Value <= PresentationEnd))
            {
                found = true;
            }
            else if (Rune.IsLetter(rune))
            {
                return false;
            }
        }

        return found;
    }
}
