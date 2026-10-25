// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Creation;

/// <summary>
/// Text in a box for a <see cref="PdfPageCanvas"/>: wrapped, aligned and justified lines, underlined or struck through,
/// in any device colour. In a tagged document the methods must be inside <see cref="PdfPageCanvas.BeginContent(PdfTagElement)"/>
/// or <see cref="PdfPageCanvas.BeginArtifact(PdfArtifactKind)"/>.
/// </summary>
public static class PdfCanvasTextMixins
{
    /// <summary>How far below the baseline an underline sits, as a fraction of the font size.</summary>
    private const float UnderlineDrop = 0.12F;

    /// <summary>The thickness of an underline or strikeout, as a fraction of the font size.</summary>
    private const float DecorationThickness = 0.06F;

    /// <summary>How far above the baseline a strikeout sits, as a fraction of the font size.</summary>
    private const float StrikeoutRise = 0.3F;

    /// <summary>Half, for centring.</summary>
    private const float Half = 0.5F;

    /// <summary>The width a box without wrapping may have.</summary>
    private const float Unbounded = 1e9F;

    /// <summary>The slack allowed when counting how many lines fit a box, against rounding in the sums.</summary>
    private const float FitSlack = 1e-4F;

    /// <summary>Adds text box methods to a page canvas.</summary>
    /// <param name="canvas">The page canvas.</param>
    extension(ref PdfPageCanvas canvas)
    {
        /// <summary>Draws text in a box, as many lines as fit.</summary>
        /// <param name="style">The font, size, colour and layout.</param>
        /// <param name="box">The box.</param>
        /// <param name="text">The text; line breaks start new lines.</param>
        /// <returns>How many characters of the text were drawn, so the rest can go in the next box; zero when not even one line fits.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="style"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The size, width or line spacing is not positive.</exception>
        /// <exception cref="InvalidOperationException">The document is tagged and no content or artifact is open.</exception>
        public int DrawTextBox(PdfTextStyle style, PdfCanvasBox box, ReadOnlySpan<char> text)
        {
            ArgumentNullException.ThrowIfNull(style);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(style.Size);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(box.Width);
            var format = style.Format;
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(format.LineSpacing);
            var lines = new List<PdfTextLine>();
            PdfTextWrapper.Wrap(style.Font, style.Size, text, format.Wrap ? box.Width : Unbounded, lines);
            var lineHeight = style.Size * format.LineSpacing;
            var fit = Math.Min(lines.Count, (int)MathF.Floor((box.Height / lineHeight) + FitSlack));
            if (fit <= 0)
            {
                return 0;
            }

            canvas.RequireMarked();
            canvas.Content.SaveState();
            style.Color.ApplyFill(ref canvas.Content);
            var block = fit * lineHeight;
            var drop = format.Vertical switch
            {
                PdfVerticalAlignment.Middle => (box.Height - block) * Half,
                PdfVerticalAlignment.Bottom => box.Height - block,
                _ => 0F,
            };
            var top = box.Top - drop;
            for (var i = 0; i < fit; i++)
            {
                DrawLine(ref canvas, style, text, lines[i], new(box.X, top - (i * lineHeight), box.Width, lineHeight));
            }

            canvas.Content.RestoreState();
            return lines[fit - 1].Start + lines[fit - 1].Length;
        }

        /// <summary>Draws one line of text inside a box, aligned in it.</summary>
        /// <param name="style">The font, size, colour and alignment; wrapping and line spacing are ignored.</param>
        /// <param name="box">The box.</param>
        /// <param name="text">The text, without line breaks.</param>
        /// <exception cref="ArgumentNullException"><paramref name="style"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The size, width or height is not positive.</exception>
        /// <exception cref="ArgumentException">The text has a line break.</exception>
        /// <exception cref="InvalidOperationException">The document is tagged and no content or artifact is open.</exception>
        public void DrawAlignedText(PdfTextStyle style, PdfCanvasBox box, ReadOnlySpan<char> text)
        {
            ArgumentNullException.ThrowIfNull(style);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(style.Size);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(box.Height);
            if (text.IndexOfAny('\r', '\n') >= 0)
            {
                throw new ArgumentException("Aligned text must not have line breaks.", nameof(text));
            }

            var single = style with { Format = style.Format with { Wrap = false, LineSpacing = box.Height / style.Size } };
            _ = canvas.DrawTextBox(single, box, text);
        }
    }

    /// <summary>Draws one line of a text box.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="style">The font, size, colour and layout.</param>
    /// <param name="text">The whole text.</param>
    /// <param name="line">The line.</param>
    /// <param name="cell">The line's cell: the box's left edge and width, and the top and height of the line.</param>
    private static void DrawLine(ref PdfPageCanvas canvas, PdfTextStyle style, ReadOnlySpan<char> text, PdfTextLine line, in LineCell cell)
    {
        var font = style.Font;
        var size = style.Size;
        var format = style.Format;
        var ascent = font.GetAscent(size);
        var descent = Math.Abs(font.GetDescent(size));
        var baseline = cell.Top - ((cell.Height - (ascent + descent)) * Half) - ascent;
        var slice = text.Slice(line.Start, line.Length);
        var justify = format.Alignment == PdfTextAlignment.Justify && !EndsParagraph(text, line) && slice.Contains(' ');
        var left = cell.Left + format.Alignment switch
        {
            PdfTextAlignment.Center => (cell.Width - line.Width) * Half,
            PdfTextAlignment.Right => cell.Width - line.Width,
            _ => 0F,
        };
        var start = justify ? cell.Left : left;
        var drawn = justify ? cell.Width : line.Width;
        if (justify)
        {
            DrawJustified(ref canvas, style, slice, baseline, cell);
        }
        else
        {
            canvas.DrawText(font, size, left, baseline, slice);
        }

        if (format.Underline)
        {
            FillBar(ref canvas, style.Color, new(start, baseline - (size * UnderlineDrop), drawn, size * DecorationThickness));
        }

        if (format.Strikeout)
        {
            FillBar(ref canvas, style.Color, new(start, baseline + (size * StrikeoutRise), drawn, size * DecorationThickness));
        }
    }

    /// <summary>Draws the words of a line one by one, sharing the spare width between the gaps.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="style">The font, size, colour and layout.</param>
    /// <param name="line">The line's text.</param>
    /// <param name="baseline">The baseline.</param>
    /// <param name="cell">The line's cell, whose left edge and width the words fill.</param>
    private static void DrawJustified(ref PdfPageCanvas canvas, PdfTextStyle style, ReadOnlySpan<char> line, float baseline, in LineCell cell)
    {
        var gaps = 0;
        var wordsWidth = 0F;
        foreach (var range in line.Split(' '))
        {
            var word = line[range];
            if (word.IsEmpty)
            {
                continue;
            }

            wordsWidth += style.Font.Measure(word, style.Size);
            gaps++;
        }

        var share = gaps > 1 ? (cell.Width - wordsWidth) / (gaps - 1) : 0F;
        var x = cell.Left;
        foreach (var range in line.Split(' '))
        {
            var word = line[range];
            if (word.IsEmpty)
            {
                continue;
            }

            canvas.DrawText(style.Font, style.Size, x, baseline, word);
            x += style.Font.Measure(word, style.Size) + share;
        }
    }

    /// <summary>Fills a thin bar, for an underline or strikeout.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="color">The colour.</param>
    /// <param name="bar">The bar.</param>
    private static void FillBar(ref PdfPageCanvas canvas, PdfCanvasColor color, PdfCanvasBox bar)
    {
        canvas.Content.SaveState();
        color.ApplyFill(ref canvas.Content);
        canvas.Content.Rectangle(bar.X, bar.Y, bar.Width, bar.Height);
        canvas.Content.Fill();
        canvas.Content.RestoreState();
    }

    /// <summary>Determines whether a line is the last of its paragraph: it ends the text or is followed by a line break.</summary>
    /// <param name="text">The whole text.</param>
    /// <param name="line">The line.</param>
    /// <returns><see langword="true"/> for the last line of a paragraph.</returns>
    private static bool EndsParagraph(ReadOnlySpan<char> text, PdfTextLine line)
    {
        var end = line.Start + line.Length;
        return end >= text.Length || text[end] is '\r' or '\n';
    }

    /// <summary>The space one line is drawn in.</summary>
    /// <param name="Left">The left edge of the box.</param>
    /// <param name="Top">The top of the line.</param>
    /// <param name="Width">The width of the box.</param>
    /// <param name="Height">The height of the line.</param>
    private readonly record struct LineCell(float Left, float Top, float Width, float Height);
}
