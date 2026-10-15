// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Forms;

/// <summary>Splits field text into lines.</summary>
internal static class TextLayout
{
    /// <summary>One thousand: glyph widths are in thousandths of an em.</summary>
    private const float GlyphUnits = 1000;

    /// <summary>Gets the first line of text: everything before the first line feed.</summary>
    /// <param name="text">The encoded text.</param>
    /// <param name="size">The font size.</param>
    /// <returns>The line.</returns>
    internal static TextLine FirstLine(FormCodes text, float size)
    {
        var feed = text.IndexOfLineFeed(0);
        var length = feed < 0 ? text.Length : feed;
        return new(0, length, Width(text, 0, text.TrimmedLength(0, length), size));
    }

    /// <summary>Splits text at its line feeds and wraps lines that are wider than the space allows, at spaces where possible.</summary>
    /// <param name="text">The encoded text.</param>
    /// <param name="size">The font size.</param>
    /// <param name="maxWidth">The widest a line may be, in points.</param>
    /// <param name="lines">Receives the lines.</param>
    /// <param name="cancellationToken">A token that cancels the layout; it is checked for every paragraph.</param>
    internal static void Wrap(FormCodes text, float size, float maxWidth, ref TextLines lines, CancellationToken cancellationToken)
    {
        var start = 0;
        while (start <= text.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var feed = text.IndexOfLineFeed(start);
            var end = feed < 0 ? text.Length : feed;
            WrapParagraph(text, start, end, size, maxWidth, ref lines);
            start = end + 1;
        }
    }

    /// <summary>Wraps the text between two line feeds.</summary>
    /// <param name="text">The encoded text.</param>
    /// <param name="paragraphStart">The first unit.</param>
    /// <param name="paragraphEnd">One past the last unit.</param>
    /// <param name="size">The font size.</param>
    /// <param name="maxWidth">The widest a line may be.</param>
    /// <param name="lines">Receives the lines.</param>
    private static void WrapParagraph(FormCodes text, int paragraphStart, int paragraphEnd, float size, float maxWidth, ref TextLines lines)
    {
        var start = paragraphStart;
        var width = 0F;
        var breakAt = -1;
        for (var i = paragraphStart; i < paragraphEnd; i++)
        {
            var advance = text.GetWidth(i) * size / GlyphUnits;
            if (width + advance > maxWidth && i > start)
            {
                start = Break(text, start, i, breakAt, size, ref lines);
                width = Width(text, start, i - start, size);
                breakAt = -1;
            }

            width += advance;
            breakAt = text.GetKind(i) == FormCodes.Space ? i : breakAt;
        }

        Add(text, start, paragraphEnd, size, ref lines);
    }

    /// <summary>Ends a line before the unit that no longer fits.</summary>
    /// <param name="text">The encoded text.</param>
    /// <param name="start">The line's first unit.</param>
    /// <param name="current">The unit that does not fit.</param>
    /// <param name="breakAt">The last space on the line, or -1.</param>
    /// <param name="size">The font size.</param>
    /// <param name="lines">Receives the line.</param>
    /// <returns>The first unit of the next line.</returns>
    private static int Break(FormCodes text, int start, int current, int breakAt, float size, ref TextLines lines)
    {
        if (breakAt > start)
        {
            Add(text, start, breakAt, size, ref lines);
            return breakAt + 1;
        }

        Add(text, start, current, size, ref lines);
        return current;
    }

    /// <summary>Adds a line.</summary>
    /// <param name="text">The encoded text.</param>
    /// <param name="start">The first unit.</param>
    /// <param name="end">One past the last unit.</param>
    /// <param name="size">The font size.</param>
    /// <param name="lines">Receives the line.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Add(FormCodes text, int start, int end, float size, ref TextLines lines) =>
        lines.Add(new(start, end - start, Width(text, start, text.TrimmedLength(start, end - start), size)));

    /// <summary>Measures a run of units.</summary>
    /// <param name="text">The encoded text.</param>
    /// <param name="start">The first unit.</param>
    /// <param name="length">The number of units.</param>
    /// <param name="size">The font size.</param>
    /// <returns>The width in points.</returns>
    private static float Width(FormCodes text, int start, int length, float size) => text.Measure(start, length) * size / GlyphUnits;
}
