// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// Finds running headers, running footers and page numbers: lines in the top or bottom margin of a page that repeat on
/// other pages, or read as a page number. They are pagination artifacts (ISO 32000-2 14.8.2.2.2), not part of the
/// reading order. Texts compare with each run of digits replaced by <c>#</c>, so "Page 3" and "Page 4" are the same text.
/// </summary>
internal static class LayoutPagination
{
    /// <summary>The share of the page height, at the top and the bottom, that holds headers and footers.</summary>
    private const float BandShare = 0.09F;

    /// <summary>The character that stands for a run of digits in a compared text.</summary>
    private const char DigitMark = '#';

    /// <summary>The compared texts that are page numbers.</summary>
    private static readonly string[] PageNumberForms =
    [
        "#", "page #", "page # of #", "# of #", "# / #", "#/#", "p. #", "p #", "- # -", "– # –", "— # —", "[#]", "(#)",
    ];

    /// <summary>Takes the pagination lines out of a page's lines.</summary>
    /// <param name="text">The items' text.</param>
    /// <param name="items">The items.</param>
    /// <param name="lines">The page's lines; the pagination lines are removed.</param>
    /// <param name="lineCount">The number of lines; updated.</param>
    /// <param name="pageHeight">The page height; zero when unknown, which finds nothing.</param>
    /// <param name="profile">What repeats across the document, or <see langword="null"/> to go by page numbers alone.</param>
    /// <param name="found">Receives the pagination lines, in the order the page lists them.</param>
    internal static void Extract(string text, LayoutItem[] items, LayoutLine[] lines, ref int lineCount, float pageHeight, PdfPaginationProfile? profile, List<PaginationLine> found)
    {
        if (pageHeight <= 0)
        {
            return;
        }

        var kept = 0;
        for (var i = 0; i < lineCount; i++)
        {
            var line = lines[i];
            var band = line.Kind == LayoutLineKind.Text ? Band(line, pageHeight) : 0;
            var kind = band == 0 ? PdfPaginationKind.None : Classify(text, items, line, band < 0, profile);
            if (kind == PdfPaginationKind.None)
            {
                lines[kept] = line;
                kept++;
            }
            else
            {
                found.Add(new(line, kind));
            }
        }

        lineCount = kept;
    }

    /// <summary>Determines whether any line of text sits in the top or the bottom margin, so a page without one skips the search.</summary>
    /// <param name="lines">The page's lines.</param>
    /// <param name="lineCount">The number of lines.</param>
    /// <param name="pageHeight">The page height.</param>
    /// <returns><see langword="true"/> when a line is in a margin.</returns>
    internal static bool HasMarginLine(LayoutLine[] lines, int lineCount, float pageHeight)
    {
        for (var i = 0; i < lineCount; i++)
        {
            if (lines[i].Kind == LayoutLineKind.Text && Band(lines[i], pageHeight) != 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tells which margin of the page a line sits in.</summary>
    /// <param name="line">The line.</param>
    /// <param name="pageHeight">The page height.</param>
    /// <returns>-1 for the top margin, 1 for the bottom margin, 0 for neither.</returns>
    internal static int Band(in LayoutLine line, float pageHeight)
    {
        if (line.Bounds.CenterY < pageHeight * BandShare)
        {
            return -1;
        }

        return line.Bounds.CenterY > pageHeight * (1 - BandShare) ? 1 : 0;
    }

    /// <summary>Writes the compared form of a text: lower case, each run of digits one <c>#</c>, white space collapsed and trimmed.</summary>
    /// <param name="text">The text.</param>
    /// <param name="destination">Receives the compared text; at least as long as <paramref name="text"/>.</param>
    /// <returns>The length of the compared text.</returns>
    internal static int Compare(ReadOnlySpan<char> text, Span<char> destination)
    {
        var length = 0;
        foreach (var character in text)
        {
            var mapped = Map(character);
            if (Skips(destination[..length], mapped))
            {
                continue;
            }

            destination[length] = mapped;
            length++;
        }

        return length > 0 && destination[length - 1] == ' ' ? length - 1 : length;
    }

    /// <summary>Gets the compared form of a line's text, in the buffer of a line's text.</summary>
    /// <param name="text">The items' text.</param>
    /// <param name="items">The items.</param>
    /// <param name="line">The line.</param>
    /// <param name="buffer">Room for twice the line's text: the text, then its compared form.</param>
    /// <returns>The compared form, a slice of <paramref name="buffer"/>.</returns>
    internal static ReadOnlySpan<char> Key(string text, LayoutItem[] items, in LayoutLine line, char[] buffer)
    {
        var half = buffer.Length / LayoutGeometry.Halves;
        var length = 0;
        LayoutText.Append(text, items, line.Start, line.Count, line.Bounds.Height, buffer, ref length);
        var compared = Compare(buffer.AsSpan(0, length), buffer.AsSpan(half));
        return buffer.AsSpan(half, compared);
    }

    /// <summary>Gets the size of the buffer <see cref="Key"/> needs.</summary>
    /// <param name="items">The items.</param>
    /// <param name="line">The line.</param>
    /// <returns>The buffer length.</returns>
    internal static int KeyBufferLength(LayoutItem[] items, in LayoutLine line) =>
        LayoutText.MaxLength(items.AsSpan(line.Start, line.Count)) * LayoutGeometry.Halves;

    /// <summary>Maps a character to its compared form: digits to <c>#</c>, white space to a space, letters to lower case.</summary>
    /// <param name="character">The character.</param>
    /// <returns>The mapped character.</returns>
    private static char Map(char character)
    {
        if (char.IsDigit(character))
        {
            return DigitMark;
        }

        return char.IsWhiteSpace(character) ? ' ' : char.ToLowerInvariant(character);
    }

    /// <summary>Determines whether a mapped character adds nothing: a repeated digit mark or space, or a space at the start.</summary>
    /// <param name="written">The compared text so far.</param>
    /// <param name="mapped">The mapped character.</param>
    /// <returns><see langword="true"/> to leave the character out.</returns>
    private static bool Skips(ReadOnlySpan<char> written, char mapped)
    {
        if (mapped is not (DigitMark or ' '))
        {
            return false;
        }

        return written.IsEmpty ? mapped == ' ' : written[^1] == mapped;
    }

    /// <summary>Decides what a line in a margin is.</summary>
    /// <param name="text">The items' text.</param>
    /// <param name="items">The items.</param>
    /// <param name="line">The line.</param>
    /// <param name="top">Whether the line is in the top margin.</param>
    /// <param name="profile">What repeats across the document, or <see langword="null"/>.</param>
    /// <returns>The kind of pagination artifact, or none.</returns>
    private static PdfPaginationKind Classify(string text, LayoutItem[] items, in LayoutLine line, bool top, PdfPaginationProfile? profile)
    {
        var buffer = ArrayPool<char>.Shared.Rent(KeyBufferLength(items, line));
        try
        {
            var key = Key(text, items, line, buffer);
            if (IsPageNumber(key))
            {
                return PdfPaginationKind.PageNumber;
            }

            if (profile?.Contains(key, top) != true)
            {
                return PdfPaginationKind.None;
            }

            return top ? PdfPaginationKind.Header : PdfPaginationKind.Footer;
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    /// <summary>Determines whether a compared text is a page number, such as "#", "page #" or "# of #".</summary>
    /// <param name="key">The compared text.</param>
    /// <returns><see langword="true"/> for a page number.</returns>
    private static bool IsPageNumber(ReadOnlySpan<char> key)
    {
        foreach (var form in PageNumberForms)
        {
            if (key.SequenceEqual(form))
            {
                return true;
            }
        }

        return false;
    }
}
