// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Text;

/// <summary>
/// Reads the text inside a rectangle of a page. A character is inside when the middle of its box is. The text of each run of
/// neighbouring characters inside is read in reading order, spaces and line breaks between them included; where the
/// rectangle skips characters, as it does across a column, the pieces are separated by a line break.
/// </summary>
internal static class TextRegions
{
    /// <summary>The line break the page text uses between lines.</summary>
    private const string LineBreak = "\r\n";

    /// <summary>One half, for finding the middle of a box.</summary>
    private const float Half = 0.5F;

    /// <summary>Gets the text inside a rectangle.</summary>
    /// <param name="page">The text page.</param>
    /// <param name="rectangle">The rectangle in user space.</param>
    /// <returns>The text, or an empty string when no character is inside.</returns>
    internal static string Extract(PdfTextPage page, in PdfRectangle rectangle)
    {
        var chars = page.Chars;
        var text = new StringBuilder();
        var first = -1;
        var last = -1;
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i].IsGenerated)
            {
                continue;
            }

            if (IsInside(chars[i], rectangle))
            {
                first = first < 0 ? i : first;
                last = i;
                continue;
            }

            Flush(page, text, first, last);
            first = -1;
        }

        Flush(page, text, first, last);
        return text.ToString();
    }

    /// <summary>Adds the text of a run of characters, after a line break when text came before it.</summary>
    /// <param name="page">The text page.</param>
    /// <param name="text">The text so far.</param>
    /// <param name="first">The first character of the run, or -1 for no run.</param>
    /// <param name="last">The last character of the run.</param>
    private static void Flush(PdfTextPage page, StringBuilder text, int first, int last)
    {
        if (first < 0)
        {
            return;
        }

        var piece = page.GetTextSpan(first, last - first + 1);
        if (piece.IsEmpty)
        {
            return;
        }

        _ = text.Length > 0 ? text.Append(LineBreak) : text;
        _ = text.Append(piece);
    }

    /// <summary>Determines whether the middle of a character's box is inside a rectangle.</summary>
    /// <param name="info">The character.</param>
    /// <param name="rectangle">The rectangle.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsInside(in PdfTextChar info, in PdfRectangle rectangle)
    {
        var box = info.Box.Width > 0 && info.Box.Height > 0 ? info.Box : info.LooseBox;
        var x = (box.Left + box.Right) * Half;
        var y = (box.Bottom + box.Top) * Half;
        return x >= Math.Min(rectangle.Left, rectangle.Right)
            && x <= Math.Max(rectangle.Left, rectangle.Right)
            && y >= Math.Min(rectangle.Bottom, rectangle.Top)
            && y <= Math.Max(rectangle.Bottom, rectangle.Top);
    }
}
