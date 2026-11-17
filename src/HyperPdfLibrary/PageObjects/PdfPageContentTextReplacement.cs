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

    /// <summary>The most glyphs the list that measures words is sized for at first; a longer line grows it.</summary>
    private const int MostGlyphsOfALine = 128;

    /// <summary>The most lines of a paragraph whose places are held on the stack; a longer paragraph gets an array.</summary>
    private const int StackLines = 32;

    /// <summary>The most a line with no space and no tatweel is drawn wider to reach both edges, as a multiple of its width; more than this misshapes the letters.</summary>
    private const float MostStretch = 1.5F;

    /// <summary>How many numbers are kept for each row while the layout is worked out: where it starts, where it ends and where it sits.</summary>
    private const int ThreePlaces = 3;

    /// <summary>Which run of those numbers holds the baselines, counted from the first run.</summary>
    private const int BaselineRun = 2;

    /// <summary>How far, as a multiple of the font size, the baselines of the texts of one row may be apart; a mark drawn above or below its letter is within it, and the next line is not.</summary>
    private const float SameRowShare = 0.6F;

    /// <summary>How far apart, as a multiple of the font size, the starts, ends or middles of lines may be and still be taken as one place.</summary>
    private const float AlignTolerance = 0.5F;

    /// <summary>How far apart, as a multiple of the font size, the ends of a justified paragraph's lines may be; they are set to the same place, so this only has to cover rounding.</summary>
    private const float JustifiedTolerance = 0.1F;

    /// <summary>The fewest lines from which a paragraph can be told to be justified, as two lines do not show whether the first is stretched.</summary>
    private const int JustifiedLines = 3;

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

        var run = PdfNewTextRun.Create(content, replacement, font, fallback);
        Write(content, new(origin, direction, 0F, 0F), userSize, fill, run);
        run.Finish();
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
        var shape = ShapeOf(ordered, direction, userSize, PdfNewTextMeasure.ReadsRightToLeft(replacement));
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

        WriteLines(content, replacement, shape, new(font, fallback, userSize, fill));
    }

    /// <summary>Wraps new text to the width of a paragraph and writes its lines.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="replacement">The new text.</param>
    /// <param name="shape">The layout of the paragraph.</param>
    /// <param name="style">How the lines are written.</param>
    private static void WriteLines(PdfPageContent content, string replacement, in ParagraphShape shape, in LineStyle style)
    {
        // Words are measured as the font that shows them sets them, so a line of Arabic, Greek or Cyrillic is as wide as it is written and sits where its edge or middle is.
        var source = PdfNewTextRun.SourceFor(content.Document.Objects, replacement, style.Font, style.Fallback);

        // The shaper fills one list for every word and line, which is sized for a long line so it is not grown as the first lines are measured.
        var glyphs = source is null ? null : new List<PdfShapedGlyph>(Math.Min(replacement.Length, MostGlyphsOfALine));
        var meter = new PdfNewTextMeasure.WordMeter(style.Font, source, style.Size, glyphs);
        var wrapped = Wrap(replacement, meter, shape.First.Width, shape.Next.Width);

        // The lines share one embedded font, so the file keeps one subset for the paragraph and not one for each line.
        PdfNewTextRun? shared = null;
        for (var i = 0; i < wrapped.Count; i++)
        {
            if (wrapped[i].Text.Length == 0)
            {
                continue;
            }

            var run = PdfNewTextRun.Create(content, StretchedText(shape, wrapped[i], i, meter), style.Font, style.Fallback, shared);
            Write(content, Place(shape, wrapped[i], i, run.Measure(style.Font, style.Size, glyphs), run.IsEmbedded), style.Size, style.Fill, run);
            if (!run.IsEmbedded || run.Shares(shared))
            {
                continue;
            }

            shared?.Finish();
            shared = run;
        }

        shared?.Finish();
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
            var along = direction * placement.Stretch;
            builder.SetTextMatrix(along.X, along.Y, -direction.Y, direction.X, placement.Origin.X, placement.Origin.Y);
            if (placement.WordSpacing != 0F && !run.IsEmbedded)
            {
                builder.SetWordSpacing(placement.WordSpacing);
            }

            if (placement.CharacterSpacing != 0F)
            {
                builder.SetCharacterSpacing(placement.CharacterSpacing);
            }

            // Word spacing does not reach the two byte codes of an embedded font, so its spaces are widened where the glyphs are written.
            run.Show(ref builder, size, run.IsEmbedded ? placement.WordSpacing : 0F);
            builder.EndText();
            builder.RestoreState();
            PdfPageContentEditing.Append(content, builder.WrittenSpan);
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
    /// <param name="rightToLeft">Whether the new text reads right to left, which sets how a paragraph of one line, or of lines of one width, is lined up.</param>
    /// <returns>The layout. The lines under the first may start further in than it does, as the lines of a list item start under its words and not its marker.</returns>
    private static ParagraphShape ShapeOf(List<PdfTextObject> ordered, Vector2 direction, float userSize, bool rightToLeft)
    {
        // The places of the rows are only read while the layout is worked out, so a paragraph of a few rows keeps them on the stack.
        var most = ordered.Count;
        Span<float> places = most <= StackLines ? stackalloc float[StackLines * ThreePlaces] : new float[most * ThreePlaces];
        var count = GroupRows(ordered, direction, userSize, places);
        var starts = places[..count];
        var ends = places.Slice(most, count);
        var baselines = places.Slice(most * BaselineRun, count);

        var baseline = baselines[0];
        var leading = count > 1 ? (baseline - baselines[^1]) / (count - 1) : userSize * DefaultLeading;
        var alignment = DetectAlignment(starts, ends, userSize, rightToLeft);
        var minimum = userSize * MinimumWidthInSizes;
        return new(direction, alignment, rightToLeft, leading, baseline, SpanOf(alignment, starts, ends, 0, minimum), SpanOf(alignment, starts, ends, count > 1 ? 1 : 0, minimum));
    }

    /// <summary>Works out where each row of a paragraph starts, ends and sits. Texts that share a row, such as the words of a line drawn one by one, are one row.</summary>
    /// <param name="ordered">The texts of the paragraph from the top down.</param>
    /// <param name="direction">The way the lines run.</param>
    /// <param name="userSize">The font size, which sets how far apart the baselines of one row's texts may be.</param>
    /// <param name="places">Room for three runs of numbers, each as long as the texts: the starts of the rows, their ends and their baselines.</param>
    /// <returns>The number of rows.</returns>
    private static int GroupRows(List<PdfTextObject> ordered, Vector2 direction, float userSize, Span<float> places)
    {
        var most = ordered.Count;
        var up = new Vector2(-direction.Y, direction.X);
        var rows = 0;
        var reference = 0F;
        var biggest = 0;
        for (var i = 0; i < most; i++)
        {
            var text = ordered[i];
            var start = Vector2.Dot(text.Glyphs[0].Origin, direction);
            var end = EndAlong(text, direction);
            var baseline = Vector2.Dot(text.Glyphs[0].Origin, up);
            if (rows > 0 && reference - baseline <= userSize * SameRowShare)
            {
                var row = rows - 1;
                places[row] = Math.Min(places[row], start);
                places[most + row] = Math.Max(places[most + row], end);

                // The run with the most glyphs sets where the row sits, as a mark drawn above or below its letter does not.
                if (text.GlyphCount > biggest)
                {
                    places[(most * BaselineRun) + row] = baseline;
                    biggest = text.GlyphCount;
                }

                continue;
            }

            places[rows] = start;
            places[most + rows] = end;
            places[(most * BaselineRun) + rows] = baseline;
            reference = baseline;
            biggest = text.GlyphCount;
            rows++;
        }

        return rows;
    }

    /// <summary>Finds how far along the direction of a line its last glyph reaches.</summary>
    /// <param name="line">The line.</param>
    /// <param name="direction">The way the line runs.</param>
    /// <returns>The furthest reach of the glyphs along the direction.</returns>
    private static float EndAlong(PdfTextObject line, Vector2 direction)
    {
        // A glyph's box is as wide and tall as the page's axes see it, so at a slant its corner reaches past the glyph; the origin and the advance, which run along the baseline, do not.
        var matrix = line.TextMatrix * line.Matrix;
        var scale = new Vector2(matrix.M11, matrix.M12).Length();
        var end = float.NegativeInfinity;
        foreach (var glyph in line.Glyphs)
        {
            end = Math.Max(end, Vector2.Dot(glyph.Origin, direction) + (glyph.Advance * scale));
        }

        return end;
    }

    /// <summary>Tells how the lines of a paragraph are lined up from where they start and end along the line.</summary>
    /// <param name="starts">Where each line starts, from the top line down.</param>
    /// <param name="ends">Where each line ends.</param>
    /// <param name="userSize">The font size, which sets how far places may differ and still count as the same.</param>
    /// <param name="rightToLeft">Whether the paragraph reads right to left, so it lines up to the right when its lines show nothing else.</param>
    /// <returns>The alignment; a paragraph of one line, or of lines of one width, is aligned to the side its text starts on, and one of lines that fit no other shape to the left.</returns>
    private static LineAlignment DetectAlignment(ReadOnlySpan<float> starts, ReadOnlySpan<float> ends, float userSize, bool rightToLeft)
    {
        if (starts.Length < TwoLines)
        {
            return rightToLeft ? LineAlignment.Right : LineAlignment.Left;
        }

        var tolerance = userSize * AlignTolerance;
        var sameStarts = Spread(starts, 0, starts.Length) <= tolerance;
        var sameEnds = Spread(ends, 0, ends.Length) <= tolerance;
        return rightToLeft
            ? DetectRightToLeft(starts, ends, userSize, sameStarts, sameEnds)
            : DetectLeftToRight(starts, ends, userSize, sameStarts, sameEnds);
    }

    /// <summary>Tells how the lines of a left-to-right paragraph are lined up: on the left unless the lines show another shape.</summary>
    /// <param name="starts">Where each line starts, from the top line down.</param>
    /// <param name="ends">Where each line ends.</param>
    /// <param name="userSize">The font size.</param>
    /// <param name="sameStarts">Whether every line starts at the same place.</param>
    /// <param name="sameEnds">Whether every line ends at the same place.</param>
    /// <returns>The alignment.</returns>
    private static LineAlignment DetectLeftToRight(ReadOnlySpan<float> starts, ReadOnlySpan<float> ends, float userSize, bool sameStarts, bool sameEnds)
    {
        var tolerance = userSize * AlignTolerance;

        // Lines of the same width cannot be told apart from left aligned ones, so they count as left aligned.
        if (!sameStarts && !sameEnds && MiddleSpread(starts, ends) <= tolerance)
        {
            return LineAlignment.Centre;
        }

        if (sameEnds && !sameStarts && (starts.Length == TwoLines || Spread(starts, 1, starts.Length) > tolerance))
        {
            return LineAlignment.Right;
        }

        return IsJustified(starts, ends, userSize, false) ? LineAlignment.Justified : LineAlignment.Left;
    }

    /// <summary>Tells how the lines of a right-to-left paragraph are lined up: on the right, where its text starts, unless the lines show another shape.</summary>
    /// <param name="starts">Where each line starts on the page, from the top line down.</param>
    /// <param name="ends">Where each line ends on the page.</param>
    /// <param name="userSize">The font size.</param>
    /// <param name="sameStarts">Whether every line starts at the same place on the page.</param>
    /// <param name="sameEnds">Whether every line ends at the same place on the page.</param>
    /// <returns>The alignment. Lines of one width count as lined up on the right, as they cannot be told apart from it.</returns>
    private static LineAlignment DetectRightToLeft(ReadOnlySpan<float> starts, ReadOnlySpan<float> ends, float userSize, bool sameStarts, bool sameEnds)
    {
        if (sameStarts && sameEnds)
        {
            return LineAlignment.Right;
        }

        if (!sameStarts && !sameEnds)
        {
            return MiddleSpread(starts, ends) <= userSize * AlignTolerance ? LineAlignment.Centre : LineAlignment.Right;
        }

        if (!sameEnds)
        {
            // The lines start together and end apart: the text is lined up on the left.
            return LineAlignment.Left;
        }

        return IsJustified(starts, ends, userSize, true) ? LineAlignment.Justified : LineAlignment.Right;
    }

    /// <summary>Tells whether the lines of a paragraph are stretched to both edges: all but the last line start and end at the same places.</summary>
    /// <param name="starts">Where each line starts.</param>
    /// <param name="ends">Where each line ends.</param>
    /// <param name="userSize">The font size.</param>
    /// <param name="rightToLeft">Whether the paragraph reads right to left, whose last line starts at the right edge and falls short of the left.</param>
    /// <returns><see langword="true"/> for a justified paragraph.</returns>
    private static bool IsJustified(ReadOnlySpan<float> starts, ReadOnlySpan<float> ends, float userSize, bool rightToLeft)
    {
        if (starts.Length < JustifiedLines)
        {
            return false;
        }

        var tolerance = userSize * AlignTolerance;
        var last = starts.Length - 1;

        // Going left to right the lines end together and those under the first start together; going right to left the lines start at the right edge, and stop together at the left except the last.
        return rightToLeft
            ? Spread(starts, 0, last) <= userSize * JustifiedTolerance && Spread(ends, 0, ends.Length) <= tolerance
            : Spread(ends, 0, last) <= userSize * JustifiedTolerance && Spread(starts, 1, starts.Length) <= tolerance;
    }

    /// <summary>Gets how far apart the largest and smallest of the middles of the lines are.</summary>
    /// <param name="starts">Where each line starts.</param>
    /// <param name="ends">Where each line ends.</param>
    /// <returns>The largest middle less the smallest.</returns>
    private static float MiddleSpread(ReadOnlySpan<float> starts, ReadOnlySpan<float> ends)
    {
        var smallest = float.PositiveInfinity;
        var largest = float.NegativeInfinity;
        for (var i = 0; i < starts.Length; i++)
        {
            var middle = (starts[i] + ends[i]) * Half;
            smallest = Math.Min(smallest, middle);
            largest = Math.Max(largest, middle);
        }

        return largest - smallest;
    }

    /// <summary>Gets how far apart the largest and smallest of some places are.</summary>
    /// <param name="values">The places.</param>
    /// <param name="from">The first place to count.</param>
    /// <param name="end">One past the last place to count.</param>
    /// <returns>The largest less the smallest of the places from the first up to the end.</returns>
    private static float Spread(ReadOnlySpan<float> values, int from, int end)
    {
        var smallest = float.PositiveInfinity;
        var largest = float.NegativeInfinity;
        for (var i = from; i < end; i++)
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
    private static LineSpan SpanOf(LineAlignment alignment, ReadOnlySpan<float> starts, ReadOnlySpan<float> ends, int line, float minimum)
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
    private static float Middle(ReadOnlySpan<float> starts, ReadOnlySpan<float> ends)
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
        var direction = shape.Direction;
        var up = new Vector2(-direction.Y, direction.X);
        var baseline = up * (shape.Baseline - (number * shape.Leading));
        if (shape.Alignment != LineAlignment.Justified || line.Final || spare <= 0F)
        {
            return new((direction * StartOf(shape, span, spare)) + baseline, direction, 0F, 0F);
        }

        var spaces = line.Text.AsSpan().Count(' ');
        if (shape.RightToLeft)
        {
            return PlaceRightToLeft(span, new(baseline, direction, width, 0F), spaces > 0 && embedded ? spaces : 0);
        }

        // Spaces take the extra room where the font allows it; otherwise every letter does.
        var origin = (direction * span.Start) + baseline;
        return !embedded && spaces > 0
            ? new(origin, direction, spare / spaces, 0F)
            : new(origin, direction, 0F, spare / Math.Max(line.Text.Length - 1, 1));
    }

    /// <summary>Works out where a line of a justified right-to-left paragraph is written so it reaches both edges. Its letters join up, so they are never spaced out.</summary>
    /// <param name="span">The span the line is set in.</param>
    /// <param name="line">The line: where its baseline is across the page in <c>Origin</c>, the way it runs, and how wide it is as written in <c>WordSpacing</c>.</param>
    /// <param name="spaces">How many spaces of the line can be widened; zero when it has none or its font cannot widen them.</param>
    /// <returns>The place of the line.</returns>
    private static TextPlacement PlaceRightToLeft(in LineSpan span, in TextPlacement line, int spaces)
    {
        var direction = line.Direction;
        var width = line.WordSpacing;

        // What kashida left over goes into the spaces, which an embedded font widens with the adjustments between its glyphs.
        if (spaces > 0)
        {
            return new((direction * span.Start) + line.Origin, direction, (span.Width - width) / spaces, 0F);
        }

        // A line with no space is drawn wider, which keeps its letters joined; it is not drawn out past a share of its width, and what is left sits on the left.
        var stretch = width > 0F ? Math.Min(span.Width / width, MostStretch) : 1F;
        return new((direction * (span.Start + span.Width - (width * stretch))) + line.Origin, direction, 0F, 0F) { Stretch = stretch };
    }

    /// <summary>Gets the words of a line to write: a line of a justified right-to-left paragraph that is not its last is stretched to the width of the paragraph with tatweels.</summary>
    /// <param name="shape">The layout of the paragraph.</param>
    /// <param name="line">The line.</param>
    /// <param name="number">The line's place in the paragraph, from zero.</param>
    /// <param name="meter">How the words are measured.</param>
    /// <returns>The words to write.</returns>
    private static string StretchedText(in ParagraphShape shape, in WrappedLine line, int number, in PdfNewTextMeasure.WordMeter meter) =>
        shape.Alignment == LineAlignment.Justified && shape.RightToLeft && !line.Final
            ? PdfNewTextKashida.Stretch(line.Text, (number == 0 ? shape.First : shape.Next).Width, meter)
            : line.Text;

    /// <summary>Works out where along its direction a line starts.</summary>
    /// <param name="shape">The layout of the paragraph.</param>
    /// <param name="span">The span the line is set in.</param>
    /// <param name="spare">The room left in the span after the line.</param>
    /// <returns>The place of the line's start along the direction.</returns>
    private static float StartOf(in ParagraphShape shape, in LineSpan span, float spare) => shape.Alignment switch
    {
        LineAlignment.Right => span.Start + spare,
        LineAlignment.Centre => span.Start + (spare * Half),
        LineAlignment.Justified when shape.RightToLeft => span.Start + spare,
        _ => span.Start,
    };

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
        // A paragraph has few lines, so an insertion sort does it without a comparison delegate.
        var up = new Vector2(-direction.Y, direction.X);
        for (var i = 1; i < ordered.Count; i++)
        {
            var line = ordered[i];
            var height = Vector2.Dot(line.Glyphs[0].Origin, up);
            var before = i - 1;
            while (before >= 0 && Vector2.Dot(ordered[before].Glyphs[0].Origin, up) < height)
            {
                ordered[before + 1] = ordered[before];
                before--;
            }

            ordered[before + 1] = line;
        }

        return ordered;
    }

    /// <summary>Breaks text into lines no wider than a width, at spaces; a line break in the text always starts a new line.</summary>
    /// <param name="text">The text.</param>
    /// <param name="meter">How the words are written, which measures them.</param>
    /// <param name="firstWidth">The widest the first line may be, in user space units.</param>
    /// <param name="nextWidth">The widest each later line may be, in user space units.</param>
    /// <returns>The lines. A word wider than the width stands on a line of its own.</returns>
    private static List<WrappedLine> Wrap(string text, in PdfNewTextMeasure.WordMeter meter, float firstWidth, float nextWidth)
    {
        // Only the finished lines are made as strings: the words are measured where they stand in the text.
        // The room for the lines is guessed from the text's length, so the list is not grown as the lines are added.
        var lines = new List<WrappedLine>((int)(text.Length * meter.Size * Half / Math.Max(nextWidth, meter.Size)) + TwoLines);
        var space = PdfNewTextMeasure.Advance(meter, " ");
        foreach (var paragraph in text.AsSpan().EnumerateLines())
        {
            WrapParagraph(paragraph, meter, new(firstWidth, nextWidth, space), lines);
        }

        return lines;
    }

    /// <summary>Breaks one line of the text into lines no wider than a width, at spaces.</summary>
    /// <param name="paragraph">The text, which has no line break.</param>
    /// <param name="meter">How the words are written, which measures them.</param>
    /// <param name="widths">How wide the lines may be, and how wide a space is.</param>
    /// <param name="lines">Receives the lines.</param>
    private static void WrapParagraph(ReadOnlySpan<char> paragraph, in PdfNewTextMeasure.WordMeter meter, in WrapWidths widths, List<WrappedLine> lines)
    {
        var position = 0;
        var start = -1;
        var end = 0;
        var width = 0F;
        while (TryReadWord(paragraph, ref position, out var word))
        {
            var wordWidth = PdfNewTextMeasure.Advance(meter, paragraph[word]);
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

    /// <summary>How wide the lines of some wrapped text may be.</summary>
    /// <param name="First">The widest the first line may be.</param>
    /// <param name="Next">The widest each later line may be.</param>
    /// <param name="Space">The width of a space.</param>
    [DebuggerDisplay("WrapWidths: first {First}, next {Next}")]
    private readonly record struct WrapWidths(float First, float Next, float Space);

    /// <summary>How the lines of new text are written.</summary>
    /// <param name="Font">The look.</param>
    /// <param name="Fallback">A font that has the letters the bundled font of that look lacks, or <see langword="null"/>.</param>
    /// <param name="Size">The font size.</param>
    /// <param name="Fill">The colour.</param>
    [DebuggerDisplay("LineStyle: {Font} at {Size}")]
    private readonly record struct LineStyle(AppearanceFont Font, PdfFontSource? Fallback, float Size, PdfPaint Fill);

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
    private readonly record struct TextPlacement(Vector2 Origin, Vector2 Direction, float WordSpacing, float CharacterSpacing)
    {
        /// <summary>Gets how many times wider than they are the glyphs are drawn along the line; 1 draws them as they are.</summary>
        internal float Stretch { get; init; } = 1F;
    }

    /// <summary>How the lines of a paragraph are laid out.</summary>
    /// <param name="Direction">The way the lines run, as a unit vector.</param>
    /// <param name="Alignment">How the lines are lined up.</param>
    /// <param name="RightToLeft">Whether the paragraph reads right to left.</param>
    /// <param name="Leading">The distance from one baseline to the next.</param>
    /// <param name="Baseline">The height of the first line's baseline, measured towards the tops of the glyphs.</param>
    /// <param name="First">Where the first line is set.</param>
    /// <param name="Next">Where each later line is set.</param>
    [DebuggerDisplay("ParagraphShape: {Alignment}, leading {Leading}")]
    private readonly record struct ParagraphShape(Vector2 Direction, LineAlignment Alignment, bool RightToLeft, float Leading, float Baseline, LineSpan First, LineSpan Next);
}
