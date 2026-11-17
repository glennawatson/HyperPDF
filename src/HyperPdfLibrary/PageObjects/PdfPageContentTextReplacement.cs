// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.PageObjects;

/// <summary>Replaces the text of a text object with new text written in a built-in font, or the bundled font of that look for letters it lacks, at the same place, size and colour.</summary>
public static class PdfPageContentTextReplacement
{
    /// <summary>The line spacing of a one line paragraph, as a multiple of the font size.</summary>
    private const float DefaultLeading = 1.2F;

    /// <summary>The least width a paragraph is wrapped to, as a multiple of the font size, so a tiny paragraph does not wrap one letter to a line.</summary>
    private const float MinimumWidthInSizes = 4F;

    /// <summary>One half.</summary>
    private const float Half = 0.5F;

    /// <summary>The fewest lines of a paragraph from which its alignment can be told.</summary>
    private const int TwoLines = 2;

    /// <summary>How far apart, as a multiple of the font size, the starts, ends or middles of lines may be and still be taken as one place.</summary>
    private const float AlignTolerance = 0.5F;

    /// <summary>How far apart, as a multiple of the font size, the ends of a justified paragraph's lines may be; they are set to the same place, so this only has to cover rounding.</summary>
    private const float JustifiedTolerance = 0.1F;

    /// <summary>The fewest lines from which a paragraph can be told to be justified, as two lines do not show whether the first is stretched.</summary>
    private const int JustifiedLines = 3;

    /// <summary>The most letters of a word measured in a buffer on the stack; a longer word gets an array.</summary>
    private const int StackCodes = 256;

    /// <summary>The first character code outside Latin-1.</summary>
    private const int LatinLimit = 256;

    /// <summary>The width of a letter the standard fonts cannot show, as a multiple of the font size.</summary>
    private const float WideLetter = 0.9F;

    /// <summary>The components of a gray colour.</summary>
    private const int GrayComponents = 1;

    /// <summary>The components of an RGB colour.</summary>
    private const int RgbComponents = 3;

    /// <summary>The components of a CMYK colour.</summary>
    private const int CmykComponents = 4;

    /// <summary>The index of the second colour component.</summary>
    private const int SecondComponent = 1;

    /// <summary>The index of the third colour component.</summary>
    private const int ThirdComponent = 2;

    /// <summary>The index of the fourth colour component.</summary>
    private const int FourthComponent = 3;

    /// <summary>How the lines of a paragraph are lined up.</summary>
    private enum LineAlignment
    {
        /// <summary>Every line starts at the same place.</summary>
        Left = 0,

        /// <summary>Every line ends at the same place.</summary>
        Right = 1,

        /// <summary>Every line is set around the same middle.</summary>
        Centre = 2,

        /// <summary>Every line but the last starts and ends at the same places.</summary>
        Justified = 3,
    }

    /// <summary>Removes the glyphs of a text object and writes new text where the first glyph was.</summary>
    /// <param name="content">The page content that holds the object.</param>
    /// <param name="text">The text object to replace.</param>
    /// <param name="replacement">The new text; empty only removes the old text. Letters the built-in fonts cannot show are written with the bundled font of the same look.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Replace(PdfPageContent content, PdfTextObject text, string replacement) =>
        Replace(content, text, replacement, AppearanceFont.Helvetica, 0F);

    /// <summary>Removes the glyphs of a text object and writes new text where the first glyph was, in a chosen standard font and size.</summary>
    /// <param name="content">The page content that holds the object.</param>
    /// <param name="text">The text object to replace.</param>
    /// <param name="replacement">The new text; empty only removes the old text. Letters the built-in fonts cannot show are written with the bundled font of the same look.</param>
    /// <param name="font">The standard font to write in.</param>
    /// <param name="size">The size in user space units, or zero or less to keep the size of the old text.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Replace(PdfPageContent content, PdfTextObject text, string replacement, AppearanceFont font, float size) =>
        Replace(content, text, replacement, font, size, null);

    /// <summary>Removes the glyphs of a text object and writes new text where the first glyph was, with a font to use for letters the bundled font lacks.</summary>
    /// <param name="content">The page content that holds the object.</param>
    /// <param name="text">The text object to replace.</param>
    /// <param name="replacement">The new text; empty only removes the old text.</param>
    /// <param name="font">The look to write in.</param>
    /// <param name="size">The size in user space units, or zero or less to keep the size of the old text.</param>
    /// <param name="fallback">A font that has the letters the bundled font of that look lacks, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static void Replace(PdfPageContent content, PdfTextObject text, string replacement, AppearanceFont font, float size, PdfFontSource? fallback)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(replacement);
        if (text.GlyphCount == 0)
        {
            return;
        }

        var origin = text.Glyphs[0].Origin;
        var direction = DirectionOf(text);
        var userSize = size > 0 ? size : GetUserSize(text);
        var fill = text.FillPaint;
        for (var i = 0; i < text.GlyphCount; i++)
        {
            _ = text.RemoveGlyph(i);
        }

        if (replacement.Length == 0)
        {
            return;
        }

        Write(content, new(origin, direction, 0F, 0F), userSize, fill, PdfNewTextRun.Create(content, replacement, font, fallback));
    }

    /// <summary>
    /// Removes the glyphs of the lines of a paragraph and writes new text in their place, wrapped to the width the paragraph had. The
    /// first line keeps its place, and the lines that follow are as far apart as the old ones were, so a longer text takes more lines
    /// and a shorter one takes fewer. A line break in the new text starts a new line. The new lines run the way the old ones did, at
    /// any angle, and are lined up the way the old ones were: to the left, to the right, centred, or stretched to both edges.
    /// </summary>
    /// <param name="content">The page content that holds the lines.</param>
    /// <param name="lines">The text objects of the paragraph's lines, in any order.</param>
    /// <param name="replacement">The new text; empty only removes the old text.</param>
    /// <param name="font">The look to write in.</param>
    /// <param name="size">The size in user space units, or zero or less to keep the size of the first line.</param>
    /// <param name="fallback">A font that has the letters the bundled font of that look lacks, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">No line has any text.</exception>
    public static void ReplaceParagraph(PdfPageContent content, IReadOnlyList<PdfTextObject> lines, string replacement, AppearanceFont font, float size, PdfFontSource? fallback)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(replacement);
        var direction = DirectionOf(FindFirstLine(lines));
        var ordered = OrderLines(lines, direction);
        var first = ordered[0];
        var userSize = size > 0 ? size : GetUserSize(first);
        var shape = ShapeOf(ordered, direction, userSize);
        var fill = first.FillPaint;
        foreach (var line in ordered)
        {
            for (var i = 0; i < line.GlyphCount; i++)
            {
                _ = line.RemoveGlyph(i);
            }
        }

        if (replacement.Length == 0)
        {
            return;
        }

        var wrapped = Wrap(replacement, font, userSize, shape.First.Width, shape.Next.Width);
        for (var i = 0; i < wrapped.Count; i++)
        {
            if (wrapped[i].Text.Length == 0)
            {
                continue;
            }

            var run = PdfNewTextRun.Create(content, wrapped[i].Text, font, fallback);
            Write(content, Place(shape, wrapped[i], i, Measure(font, wrapped[i].Text, userSize), run.IsEmbedded), userSize, fill, run);
        }
    }

    /// <summary>Gets the way the text of an object runs on the page, as a unit vector; a text that has no size across it runs to the right.</summary>
    /// <param name="text">The text object.</param>
    /// <returns>The direction the baseline runs in, in user space.</returns>
    private static Vector2 DirectionOf(PdfTextObject text)
    {
        var matrix = text.TextMatrix * text.Matrix;
        var along = new Vector2(matrix.M11, matrix.M12);
        var length = along.Length();
        return length > 0 ? along / length : Vector2.UnitX;
    }

    /// <summary>Finds the first of the lines that has text, which sets the way the paragraph runs and its size.</summary>
    /// <param name="lines">The lines of a paragraph.</param>
    /// <returns>The first line with text.</returns>
    /// <exception cref="ArgumentException">No line has any text.</exception>
    private static PdfTextObject FindFirstLine(IReadOnlyList<PdfTextObject> lines)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].GlyphCount > 0)
            {
                return lines[i];
            }
        }

        throw new ArgumentException("A paragraph needs a line with text.", nameof(lines));
    }

    /// <summary>Gets the size the text is drawn at in user space.</summary>
    /// <param name="text">The text object.</param>
    /// <returns>The size in user space units.</returns>
    private static float GetUserSize(PdfTextObject text)
    {
        var matrix = text.TextMatrix * text.Matrix;
        var scale = MathF.Sqrt((matrix.M21 * matrix.M21) + (matrix.M22 * matrix.M22));
        return text.FontSize * (scale > 0 ? scale : 1);
    }

    /// <summary>Appends the new text to the page content.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="placement">Where the baseline starts, which way it runs, and how far apart to space the letters.</param>
    /// <param name="size">The font size.</param>
    /// <param name="fill">The colour to write in.</param>
    /// <param name="run">The text and its font.</param>
    private static void Write(PdfPageContent content, in TextPlacement placement, float size, PdfPaint fill, PdfNewTextRun run)
    {
        var builder = default(PdfContentBuilder);
        try
        {
            builder.SaveState();
            SetFill(ref builder, fill);
            builder.BeginText();
            run.SelectFont(ref builder, size);
            var direction = placement.Direction;
            builder.SetTextMatrix(direction.X, direction.Y, -direction.Y, direction.X, placement.Origin.X, placement.Origin.Y);
            if (placement.WordSpacing != 0F)
            {
                builder.SetWordSpacing(placement.WordSpacing);
            }

            if (placement.CharacterSpacing != 0F)
            {
                builder.SetCharacterSpacing(placement.CharacterSpacing);
            }

            run.Show(ref builder, size);
            builder.EndText();
            builder.RestoreState();
            PdfPageContentEditing.Append(content, builder.WrittenSpan);
            run.Finish();
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Sets the fill colour from a device paint, or black when the paint is not a device colour.</summary>
    /// <param name="builder">The output.</param>
    /// <param name="paint">The paint.</param>
    private static void SetFill(ref PdfContentBuilder builder, PdfPaint paint)
    {
        var c = paint.Components;
        if (!paint.IsDeviceColor)
        {
            builder.SetFillGray(0);
        }
        else if (c.Length == GrayComponents)
        {
            builder.SetFillGray(c[0]);
        }
        else if (c.Length == RgbComponents)
        {
            builder.SetFillRgb(c[0], c[SecondComponent], c[ThirdComponent]);
        }
        else if (c.Length == CmykComponents)
        {
            builder.SetFillCmyk(c[0], c[SecondComponent], c[ThirdComponent], c[FourthComponent]);
        }
        else
        {
            builder.SetFillGray(0);
        }
    }

    /// <summary>Works out how the lines of a paragraph are laid out: how far apart they are, how they are lined up, where each starts and how wide each may be.</summary>
    /// <param name="ordered">The lines with text, from the top down.</param>
    /// <param name="direction">The way the lines run.</param>
    /// <param name="userSize">The font size the new text is written at.</param>
    /// <returns>The layout. The lines under the first may start further in than it does, as the lines of a list item start under its words and not its marker.</returns>
    private static ParagraphShape ShapeOf(List<PdfTextObject> ordered, Vector2 direction, float userSize)
    {
        var up = new Vector2(-direction.Y, direction.X);
        var count = ordered.Count;
        var starts = new float[count];
        var ends = new float[count];
        for (var i = 0; i < count; i++)
        {
            starts[i] = Vector2.Dot(ordered[i].Glyphs[0].Origin, direction);
            ends[i] = EndAlong(ordered[i], direction);
        }

        var baseline = Vector2.Dot(ordered[0].Glyphs[0].Origin, up);
        var leading = count > 1 ? (baseline - Vector2.Dot(ordered[^1].Glyphs[0].Origin, up)) / (count - 1) : userSize * DefaultLeading;
        var alignment = DetectAlignment(starts, ends, userSize);
        var minimum = userSize * MinimumWidthInSizes;
        return new(direction, alignment, leading, baseline, SpanOf(alignment, starts, ends, 0, minimum), SpanOf(alignment, starts, ends, count > 1 ? 1 : 0, minimum));
    }

    /// <summary>Finds how far along the direction of a line its last glyph reaches.</summary>
    /// <param name="line">The line.</param>
    /// <param name="direction">The way the line runs.</param>
    /// <returns>The furthest reach of the glyphs along the direction.</returns>
    private static float EndAlong(PdfTextObject line, Vector2 direction)
    {
        var end = float.NegativeInfinity;
        foreach (var glyph in line.Glyphs)
        {
            // The corner of the glyph's box that reaches furthest along the direction.
            var box = glyph.Box;
            end = Math.Max(end, ((direction.X >= 0 ? box.Right : box.Left) * direction.X) + ((direction.Y >= 0 ? box.Top : box.Bottom) * direction.Y));
        }

        return end;
    }

    /// <summary>Tells how the lines of a paragraph are lined up from where they start and end along the line.</summary>
    /// <param name="starts">Where each line starts, from the top line down.</param>
    /// <param name="ends">Where each line ends.</param>
    /// <param name="userSize">The font size, which sets how far places may differ and still count as the same.</param>
    /// <returns>The alignment; a paragraph of one line, or of lines that fit no other shape, is aligned to the left.</returns>
    private static LineAlignment DetectAlignment(float[] starts, float[] ends, float userSize)
    {
        if (starts.Length < TwoLines)
        {
            return LineAlignment.Left;
        }

        var tolerance = userSize * AlignTolerance;
        var sameStarts = Spread(starts, 0) <= tolerance;
        var sameEnds = Spread(ends, 0) <= tolerance;

        // Lines of the same width cannot be told apart from left aligned ones, so they count as left aligned.
        if (!sameStarts && !sameEnds && Spread(Middles(starts, ends), 0) <= tolerance)
        {
            return LineAlignment.Centre;
        }

        if (sameEnds && !sameStarts && (starts.Length == TwoLines || Spread(starts, 1) > tolerance))
        {
            return LineAlignment.Right;
        }

        return IsJustified(starts, ends, userSize) ? LineAlignment.Justified : LineAlignment.Left;
    }

    /// <summary>Tells whether the lines of a paragraph are stretched to both edges: every line but the last ends at the same place, and the lines under the first start together.</summary>
    /// <param name="starts">Where each line starts.</param>
    /// <param name="ends">Where each line ends.</param>
    /// <param name="userSize">The font size.</param>
    /// <returns><see langword="true"/> for a justified paragraph.</returns>
    private static bool IsJustified(float[] starts, float[] ends, float userSize) =>
        starts.Length >= JustifiedLines
        && Spread(ends[..^1], 0) <= userSize * JustifiedTolerance
        && Spread(starts, 1) <= userSize * AlignTolerance;

    /// <summary>Gets the middle of each line.</summary>
    /// <param name="starts">Where each line starts.</param>
    /// <param name="ends">Where each line ends.</param>
    /// <returns>The middles.</returns>
    private static float[] Middles(float[] starts, float[] ends)
    {
        var middles = new float[starts.Length];
        for (var i = 0; i < middles.Length; i++)
        {
            middles[i] = (starts[i] + ends[i]) * Half;
        }

        return middles;
    }

    /// <summary>Gets how far apart the largest and smallest of some places are.</summary>
    /// <param name="values">The places.</param>
    /// <param name="from">The first place to count.</param>
    /// <returns>The largest less the smallest of the places from there on.</returns>
    private static float Spread(float[] values, int from)
    {
        var smallest = float.PositiveInfinity;
        var largest = float.NegativeInfinity;
        for (var i = from; i < values.Length; i++)
        {
            smallest = Math.Min(smallest, values[i]);
            largest = Math.Max(largest, values[i]);
        }

        return largest - smallest;
    }

    /// <summary>Works out where a line may start along its direction and how wide it may be.</summary>
    /// <param name="alignment">How the lines are lined up.</param>
    /// <param name="starts">Where each old line starts.</param>
    /// <param name="ends">Where each old line ends.</param>
    /// <param name="line">The old line the new line follows in place: the first, or the second for every line under the first.</param>
    /// <param name="minimum">The least width.</param>
    /// <returns>The span the line is set in.</returns>
    private static LineSpan SpanOf(LineAlignment alignment, float[] starts, float[] ends, int line, float minimum)
    {
        var right = float.NegativeInfinity;
        var left = float.PositiveInfinity;
        for (var i = 0; i < starts.Length; i++)
        {
            right = Math.Max(right, ends[i]);
            left = Math.Min(left, starts[i]);
        }

        if (alignment == LineAlignment.Right)
        {
            // The lines share a right edge, so the span reaches back as far as the longest of them does.
            var width = Math.Max(right - left, minimum);
            return new(right - width, width);
        }

        if (alignment == LineAlignment.Centre)
        {
            // The lines share a middle, so the span reaches as far from it as the widest of them does.
            var middle = Middle(starts, ends);
            var half = Math.Max(Math.Max(middle - left, right - middle), minimum * Half);
            return new(middle - half, half + half);
        }

        return new(starts[line], Math.Max(right - starts[line], minimum));
    }

    /// <summary>Gets the middle the lines of a centred paragraph are set around.</summary>
    /// <param name="starts">Where each line starts.</param>
    /// <param name="ends">Where each line ends.</param>
    /// <returns>The mean of the middles of the lines.</returns>
    private static float Middle(float[] starts, float[] ends)
    {
        var total = 0F;
        for (var i = 0; i < starts.Length; i++)
        {
            total += (starts[i] + ends[i]) * Half;
        }

        return total / starts.Length;
    }

    /// <summary>Works out where a wrapped line is written.</summary>
    /// <param name="shape">The layout of the paragraph.</param>
    /// <param name="line">The line.</param>
    /// <param name="number">The line's place in the paragraph, from zero.</param>
    /// <param name="width">How wide the line is as written.</param>
    /// <param name="embedded">Whether the line is in an embedded font, whose spaces cannot be stretched on their own.</param>
    /// <returns>The place and direction of the line, and how far its letters are spaced.</returns>
    private static TextPlacement Place(in ParagraphShape shape, in WrappedLine line, int number, float width, bool embedded)
    {
        var span = number == 0 ? shape.First : shape.Next;
        var spare = span.Width - width;
        var start = shape.Alignment switch
        {
            LineAlignment.Right => span.Start + spare,
            LineAlignment.Centre => span.Start + (spare * Half),
            _ => span.Start,
        };

        var direction = shape.Direction;
        var up = new Vector2(-direction.Y, direction.X);
        var origin = (direction * start) + (up * (shape.Baseline - (number * shape.Leading)));
        if (shape.Alignment != LineAlignment.Justified || line.Final || spare <= 0F)
        {
            return new(origin, direction, 0F, 0F);
        }

        // Spaces take the extra room where the font allows it; otherwise every letter does.
        var spaces = line.Text.AsSpan().Count(' ');
        return !embedded && spaces > 0
            ? new(origin, direction, spare / spaces, 0F)
            : new(origin, direction, 0F, spare / Math.Max(line.Text.Length - 1, 1));
    }

    /// <summary>Keeps the lines that have text and puts the top one first.</summary>
    /// <param name="lines">The lines of a paragraph.</param>
    /// <param name="direction">The way the lines run.</param>
    /// <returns>The lines with text, from the top of the page down, as seen along their direction.</returns>
    /// <exception cref="ArgumentException">No line has any text.</exception>
    private static List<PdfTextObject> OrderLines(IReadOnlyList<PdfTextObject> lines, Vector2 direction)
    {
        var ordered = new List<PdfTextObject>(lines.Count);
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].GlyphCount > 0)
            {
                ordered.Add(lines[i]);
            }
        }

        if (ordered.Count == 0)
        {
            throw new ArgumentException("A paragraph needs a line with text.", nameof(lines));
        }

        // The side the glyphs point to is up, so the line with the largest height along it is the highest.
        var up = new Vector2(-direction.Y, direction.X);
        ordered.Sort((a, b) => Vector2.Dot(b.Glyphs[0].Origin, up).CompareTo(Vector2.Dot(a.Glyphs[0].Origin, up)));
        return ordered;
    }

    /// <summary>Breaks text into lines no wider than a width, at spaces; a line break in the text always starts a new line.</summary>
    /// <param name="text">The text.</param>
    /// <param name="font">The look the text is written in.</param>
    /// <param name="size">The font size in user space units.</param>
    /// <param name="firstWidth">The widest the first line may be, in user space units.</param>
    /// <param name="nextWidth">The widest each later line may be, in user space units.</param>
    /// <returns>The lines. A word wider than the width stands on a line of its own.</returns>
    private static List<WrappedLine> Wrap(string text, AppearanceFont font, float size, float firstWidth, float nextWidth)
    {
        // Only the finished lines are made as strings: the words are measured where they stand in the text.
        var lines = new List<WrappedLine>();
        var space = Measure(font, " ", size);
        foreach (var paragraph in text.AsSpan().EnumerateLines())
        {
            WrapParagraph(paragraph, font, size, new(firstWidth, nextWidth, space), lines);
        }

        return lines;
    }

    /// <summary>Breaks one line of the text into lines no wider than a width, at spaces.</summary>
    /// <param name="paragraph">The text, which has no line break.</param>
    /// <param name="font">The look the text is written in.</param>
    /// <param name="size">The font size in user space units.</param>
    /// <param name="widths">How wide the lines may be, and how wide a space is.</param>
    /// <param name="lines">Receives the lines.</param>
    private static void WrapParagraph(ReadOnlySpan<char> paragraph, AppearanceFont font, float size, in WrapWidths widths, List<WrappedLine> lines)
    {
        var position = 0;
        var start = -1;
        var end = 0;
        var width = 0F;
        while (TryReadWord(paragraph, ref position, out var word))
        {
            var wordWidth = Measure(font, paragraph[word], size);
            var joined = width + (widths.Space * (word.Start.Value - end)) + wordWidth;
            if (start >= 0 && joined <= (lines.Count == 0 ? widths.First : widths.Next))
            {
                width = joined;
                end = word.End.Value;
                continue;
            }

            if (start >= 0)
            {
                lines.Add(new(new(paragraph[start..end]), false));
            }

            start = word.Start.Value;
            end = word.End.Value;
            width = wordWidth;
        }

        lines.Add(new(start >= 0 ? new(paragraph[start..end]) : string.Empty, true));
    }

    /// <summary>Finds the next word of some text: the letters between spaces.</summary>
    /// <param name="text">The text.</param>
    /// <param name="position">Where to look from; moved past the word.</param>
    /// <param name="word">Receives where the word is.</param>
    /// <returns><see langword="true"/> when there is another word.</returns>
    private static bool TryReadWord(ReadOnlySpan<char> text, ref int position, out Range word)
    {
        var skip = text[position..].IndexOfAnyExcept(' ');
        if (skip < 0)
        {
            position = text.Length;
            word = default;
            return false;
        }

        var begin = position + skip;
        var length = text[begin..].IndexOf(' ');
        var finish = length < 0 ? text.Length : begin + length;
        position = finish;
        word = begin..finish;
        return true;
    }

    /// <summary>Measures text written in a standard font; the bundled fonts have the same widths, and a letter outside Latin-1 counts as a wide one.</summary>
    /// <param name="font">The look.</param>
    /// <param name="text">The text.</param>
    /// <param name="size">The font size.</param>
    /// <returns>The width in user space units.</returns>
    private static float Measure(AppearanceFont font, ReadOnlySpan<char> text, float size)
    {
        Span<byte> codes = text.Length <= StackCodes ? stackalloc byte[StackCodes] : new byte[text.Length];
        var count = 0;
        var wide = 0;
        foreach (var letter in text)
        {
            if (letter < LatinLimit)
            {
                codes[count] = (byte)letter;
                count++;
            }
            else
            {
                wide++;
            }
        }

        return (wide * size * WideLetter) + AppearanceFontMetrics.MeasureAdvance(font, codes[..count], size);
    }

    /// <summary>How wide the lines of some wrapped text may be.</summary>
    /// <param name="First">The widest the first line may be.</param>
    /// <param name="Next">The widest each later line may be.</param>
    /// <param name="Space">The width of a space.</param>
    [DebuggerDisplay("WrapWidths: first {First}, next {Next}")]
    private readonly record struct WrapWidths(float First, float Next, float Space);

    /// <summary>Where a line may start along its direction and how wide it may be.</summary>
    /// <param name="Start">Where the span starts, along the direction of the line.</param>
    /// <param name="Width">How wide the span is.</param>
    [DebuggerDisplay("LineSpan: {Start} for {Width}")]
    private readonly record struct LineSpan(float Start, float Width);

    /// <summary>A line of wrapped text.</summary>
    /// <param name="Text">The words of the line.</param>
    /// <param name="Final">Whether the line is the last of its paragraph, which a justified paragraph leaves unstretched.</param>
    [DebuggerDisplay("WrappedLine: {Text}")]
    private readonly record struct WrappedLine(string Text, bool Final);

    /// <summary>Where a line is written and how its letters are spaced.</summary>
    /// <param name="Origin">Where the baseline starts, in user space.</param>
    /// <param name="Direction">The way the baseline runs, as a unit vector.</param>
    /// <param name="WordSpacing">The room added to each space.</param>
    /// <param name="CharacterSpacing">The room added after each letter.</param>
    [DebuggerDisplay("TextPlacement: {Origin} towards {Direction}")]
    private readonly record struct TextPlacement(Vector2 Origin, Vector2 Direction, float WordSpacing, float CharacterSpacing);

    /// <summary>How the lines of a paragraph are laid out.</summary>
    /// <param name="Direction">The way the lines run, as a unit vector.</param>
    /// <param name="Alignment">How the lines are lined up.</param>
    /// <param name="Leading">The distance from one baseline to the next.</param>
    /// <param name="Baseline">The height of the first line's baseline, measured towards the tops of the glyphs.</param>
    /// <param name="First">Where the first line is set.</param>
    /// <param name="Next">Where each later line is set.</param>
    [DebuggerDisplay("ParagraphShape: {Alignment}, leading {Leading}")]
    private readonly record struct ParagraphShape(Vector2 Direction, LineAlignment Alignment, float Leading, float Baseline, LineSpan First, LineSpan Next);
}
