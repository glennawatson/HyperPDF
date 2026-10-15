// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace HyperPdfLibrary.Creation;

/// <summary>
/// Breaks text into lines that fit a width. Lines break at spaces, and at every line break in the text; a word wider than
/// the line breaks between text elements (what a reader sees as one character). Widths come from the font, so shaped
/// text such as ligatures and kerning is measured as it will be drawn.
/// </summary>
public static class PdfTextWrapper
{
    /// <summary>The characters in a carriage return followed by a line feed.</summary>
    private const int CarriageReturnLineFeed = 2;

    /// <summary>Breaks text into lines.</summary>
    /// <param name="font">The font the text is drawn in.</param>
    /// <param name="size">The font size in points.</param>
    /// <param name="text">The text.</param>
    /// <param name="maxWidth">The widest a line may be, in points; a single text element wider than this still gets a line of its own.</param>
    /// <param name="lines">Receives the lines; it is not cleared first. A blank line in the text gives a line of length zero.</param>
    /// <exception cref="ArgumentNullException"><paramref name="font"/> or <paramref name="lines"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> or <paramref name="maxWidth"/> is not positive.</exception>
    public static void Wrap(PdfCanvasFont font, float size, ReadOnlySpan<char> text, float maxWidth, List<PdfTextLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var sink = new ListLineSink(lines);
        Wrap(font, size, text, maxWidth, ref sink);
    }

    /// <summary>Breaks text into lines, sending them to a sink.</summary>
    /// <typeparam name="TSink">The sink's type.</typeparam>
    /// <param name="font">The font the text is drawn in.</param>
    /// <param name="size">The font size in points.</param>
    /// <param name="text">The text.</param>
    /// <param name="maxWidth">The widest a line may be, in points.</param>
    /// <param name="lines">Receives the lines.</param>
    /// <exception cref="ArgumentNullException"><paramref name="font"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> or <paramref name="maxWidth"/> is not positive.</exception>
    internal static void Wrap<TSink>(PdfCanvasFont font, float size, ReadOnlySpan<char> text, float maxWidth, ref TSink lines)
        where TSink : struct, ILineSink, allows ref struct
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxWidth);
        var start = 0;
        while (start <= text.Length)
        {
            var lineEnd = text[start..].IndexOfAny('\r', '\n');
            var paragraphEnd = lineEnd < 0 ? text.Length : start + lineEnd;
            WrapParagraph(font, size, text, start, paragraphEnd, maxWidth, ref lines);
            start = SkipBreak(text, paragraphEnd);
            if (paragraphEnd == text.Length)
            {
                break;
            }
        }
    }

    /// <summary>Skips a line break.</summary>
    /// <param name="text">The text.</param>
    /// <param name="position">The position of the break, or the end of the text.</param>
    /// <returns>The position after the break; a carriage return followed by a line feed counts as one.</returns>
    private static int SkipBreak(ReadOnlySpan<char> text, int position)
    {
        if (position >= text.Length)
        {
            return position;
        }

        return text[position] == '\r' && position + 1 < text.Length && text[position + 1] == '\n' ? position + CarriageReturnLineFeed : position + 1;
    }

    /// <summary>Breaks one paragraph, which has no line breaks, into lines.</summary>
    /// <typeparam name="TSink">The sink's type.</typeparam>
    /// <param name="font">The font.</param>
    /// <param name="size">The font size.</param>
    /// <param name="text">The whole text.</param>
    /// <param name="start">The paragraph's first character.</param>
    /// <param name="end">The index after the paragraph's last character.</param>
    /// <param name="maxWidth">The widest a line may be.</param>
    /// <param name="lines">Receives the lines.</param>
    private static void WrapParagraph<TSink>(PdfCanvasFont font, float size, ReadOnlySpan<char> text, int start, int end, float maxWidth, ref TSink lines)
        where TSink : struct, ILineSink, allows ref struct
    {
        var lineStart = start;
        var lineEnd = start;
        var lineWidth = 0F;
        var position = start;
        while (position < end)
        {
            var wordEnd = FindWordEnd(text, position, end);
            var candidate = font.Measure(text[lineStart..wordEnd], size);
            if (candidate <= maxWidth || lineEnd == lineStart)
            {
                lineEnd = wordEnd;
                lineWidth = candidate;
                position = SkipSpaces(text, wordEnd, end);
                continue;
            }

            BreakOverlong(font, size, text, new(lineStart, lineEnd - lineStart, lineWidth), maxWidth, ref lines);
            lineStart = position;
            lineEnd = position;
            lineWidth = 0F;
        }

        BreakOverlong(font, size, text, new(lineStart, lineEnd - lineStart, lineWidth), maxWidth, ref lines);
    }

    /// <summary>Adds a line, first splitting it between text elements when it is wider than the limit.</summary>
    /// <typeparam name="TSink">The sink's type.</typeparam>
    /// <param name="font">The font.</param>
    /// <param name="size">The font size.</param>
    /// <param name="text">The whole text.</param>
    /// <param name="line">The line.</param>
    /// <param name="maxWidth">The widest a line may be.</param>
    /// <param name="lines">Receives the lines.</param>
    private static void BreakOverlong<TSink>(PdfCanvasFont font, float size, ReadOnlySpan<char> text, PdfTextLine line, float maxWidth, ref TSink lines)
        where TSink : struct, ILineSink, allows ref struct
    {
        if (line.Width <= maxWidth || line.Length == 0)
        {
            lines.Add(line);
            return;
        }

        var start = line.Start;
        var end = line.Start + line.Length;
        var chunkStart = start;
        var chunkEnd = start;
        var chunkWidth = 0F;
        var offset = start;
        while (offset < end)
        {
            var next = offset + StringInfo.GetNextTextElementLength(text[offset..end]);
            var width = font.Measure(text[chunkStart..next], size);
            if (width > maxWidth && chunkEnd > chunkStart)
            {
                lines.Add(new(chunkStart, chunkEnd - chunkStart, chunkWidth));
                chunkStart = offset;
                width = font.Measure(text[offset..next], size);
            }

            chunkEnd = next;
            chunkWidth = width;
            offset = next;
        }

        lines.Add(new(chunkStart, chunkEnd - chunkStart, chunkWidth));
    }

    /// <summary>Finds where the word that starts at a position ends.</summary>
    /// <param name="text">The text.</param>
    /// <param name="position">The word's first character.</param>
    /// <param name="end">The index after the last character that may be read.</param>
    /// <returns>The index of the space after the word, or <paramref name="end"/>.</returns>
    private static int FindWordEnd(ReadOnlySpan<char> text, int position, int end)
    {
        var space = text[position..end].IndexOf(' ');
        return space < 0 ? end : position + space;
    }

    /// <summary>Skips spaces.</summary>
    /// <param name="text">The text.</param>
    /// <param name="position">The first position to look at.</param>
    /// <param name="end">The index after the last character that may be read.</param>
    /// <returns>The index of the first character that is not a space.</returns>
    private static int SkipSpaces(ReadOnlySpan<char> text, int position, int end)
    {
        while (position < end && text[position] == ' ')
        {
            position++;
        }

        return position;
    }
}
