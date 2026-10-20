// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Text;

/// <summary>
/// Finds the underline, strikeout and highlight of each character from the small lines and boxes the page draws. A thin
/// line along a character just under its baseline is an underline; one through the middle of its body is a strikeout; a
/// coloured box that covers the body of the character is a highlight. Only horizontal text is checked, and sizes are
/// measured against the character's own height, so the rules hold at any font size.
/// </summary>
internal static class TextDecorationScan
{
    /// <summary>The thickest a line may be, as a share of the character's height.</summary>
    private const float LineThickness = 0.2F;

    /// <summary>The lowest an underline's middle may be, relative to the baseline, as a share of the character's height.</summary>
    private const float UnderlineLow = -0.5F;

    /// <summary>The highest an underline's middle may be, relative to the baseline.</summary>
    private const float UnderlineHigh = 0.05F;

    /// <summary>The lowest a strikeout's middle may be, relative to the baseline.</summary>
    private const float StrikeLow = 0.12F;

    /// <summary>The highest a strikeout's middle may be, relative to the baseline.</summary>
    private const float StrikeHigh = 0.5F;

    /// <summary>The shortest a highlight may be, as a share of the character's height.</summary>
    private const float HighlightMinHeight = 0.6F;

    /// <summary>The tallest a highlight may be, as a share of the character's height.</summary>
    private const float HighlightMaxHeight = 2.5F;

    /// <summary>The highest a highlight's bottom may be above the baseline, as a share of the character's height.</summary>
    private const float HighlightBottomLimit = 0.15F;

    /// <summary>The lowest a highlight's top may be above the baseline, as a share of the character's height.</summary>
    private const float HighlightTopLimit = 0.45F;

    /// <summary>The share of a character's width that a line or box must cover.</summary>
    private const float CoverShare = 0.5F;

    /// <summary>White, which is the paper and not a highlight.</summary>
    private const uint White = 0xFFFFFF;

    /// <summary>The steepest a baseline may be, in degrees, for the text to be checked.</summary>
    private const float MaxAngle = 1;

    /// <summary>The tallest a mark may be, in points, matching the limit the text device keeps.</summary>
    private const float MarkHeightLimit = 144;

    /// <summary>One half, for finding the middle of a line.</summary>
    private const float Half = 0.5F;

    /// <summary>The divisor that halves a range of indexes.</summary>
    private const int HalfDivisor = 2;

    /// <summary>Finds the decorations of every character.</summary>
    /// <param name="chars">The characters of the page.</param>
    /// <param name="styles">The styles of the page's runs and the run of each character.</param>
    /// <param name="marks">The small lines and boxes the page draws.</param>
    /// <returns>The decorations of each character, or <see langword="null"/> when no character has any.</returns>
    internal static PdfTextDecorations[]? Detect(ReadOnlySpan<PdfTextChar> chars, TextStyleTable styles, List<TextMark> marks)
    {
        if (marks.Count == 0)
        {
            return null;
        }

        var sorted = marks.ToArray();
        Array.Sort(sorted, static (first, second) => first.Rect.Bottom.CompareTo(second.Rect.Bottom));
        PdfTextDecorations[]? found = null;
        for (var i = 0; i < chars.Length; i++)
        {
            var style = (uint)styles.RunOfChar[i] < (uint)styles.Styles.Length ? styles.Styles[styles.RunOfChar[i]] : TextRunStyle.None;
            var decorations = Scan(chars[i], style, sorted);
            if (decorations == PdfTextDecorations.None)
            {
                continue;
            }

            found ??= new PdfTextDecorations[chars.Length];
            found[i] = decorations;
        }

        return found;
    }

    /// <summary>Finds the decorations of one character.</summary>
    /// <param name="info">The character.</param>
    /// <param name="style">The style of the character's run.</param>
    /// <param name="marks">The marks, sorted by the bottom of their bounds.</param>
    /// <returns>The decorations.</returns>
    private static PdfTextDecorations Scan(in PdfTextChar info, in TextRunStyle style, TextMark[] marks)
    {
        var height = info.LooseBox.Height;
        if (info.Kind == PdfTextCharKind.Generated || height <= 0 || info.LooseBox.Width <= 0 || MathF.Abs(style.Angle) > MaxAngle)
        {
            return PdfTextDecorations.None;
        }

        var baseline = info.Origin.Y;
        var found = PdfTextDecorations.None;
        var lowest = baseline - (HighlightMaxHeight * height);
        for (var i = FirstAtOrAbove(marks, lowest); i < marks.Length && marks[i].Rect.Bottom <= baseline + (StrikeHigh * height); i++)
        {
            found |= Classify(marks[i], info, style.FillColor);
        }

        return found;
    }

    /// <summary>Decides what one mark is to one character.</summary>
    /// <param name="mark">The mark.</param>
    /// <param name="info">The character.</param>
    /// <param name="fill">The character's fill colour.</param>
    /// <returns>The decoration the mark makes, or none.</returns>
    private static PdfTextDecorations Classify(in TextMark mark, in PdfTextChar info, uint fill)
    {
        var box = info.LooseBox;
        var covered = MathF.Min(mark.Rect.Right, box.Right) - MathF.Max(mark.Rect.Left, box.Left);
        if (covered < CoverShare * box.Width)
        {
            return PdfTextDecorations.None;
        }

        return mark.Rect.Height <= LineThickness * box.Height ? ClassifyLine(mark.Rect, info) : ClassifyBox(mark, info, fill);
    }

    /// <summary>Decides whether a thin line is an underline or a strikeout of a character, from where its middle is.</summary>
    /// <param name="line">The line's bounds.</param>
    /// <param name="info">The character.</param>
    /// <returns>The decoration the line makes, or none.</returns>
    private static PdfTextDecorations ClassifyLine(in PdfRectangle line, in PdfTextChar info)
    {
        var middle = (((line.Bottom + line.Top) * Half) - info.Origin.Y) / info.LooseBox.Height;
        if (middle is >= UnderlineLow and <= UnderlineHigh)
        {
            return PdfTextDecorations.Underline;
        }

        return middle is >= StrikeLow and <= StrikeHigh ? PdfTextDecorations.Strikeout : PdfTextDecorations.None;
    }

    /// <summary>Decides whether a coloured box is a highlight of a character: as tall as a line of text, covering its body, and not the paper's colour.</summary>
    /// <param name="mark">The box.</param>
    /// <param name="info">The character.</param>
    /// <param name="fill">The character's fill colour.</param>
    /// <returns>Highlight, or none.</returns>
    private static PdfTextDecorations ClassifyBox(in TextMark mark, in PdfTextChar info, uint fill)
    {
        var height = info.LooseBox.Height;
        var baseline = info.Origin.Y;
        var thickness = mark.Rect.Height;
        var isBox = thickness >= HighlightMinHeight * height && thickness <= HighlightMaxHeight * height;
        var covers = mark.Rect.Bottom <= baseline + (HighlightBottomLimit * height) && mark.Rect.Top >= baseline + (HighlightTopLimit * height);
        return isBox && covers && mark.Color != White && mark.Color != fill ? PdfTextDecorations.Highlight : PdfTextDecorations.None;
    }

    /// <summary>Finds the first mark whose bottom is at or above a level, by binary search over marks sorted by their bottoms.</summary>
    /// <param name="marks">The marks, sorted by the bottom of their bounds.</param>
    /// <param name="level">The level.</param>
    /// <returns>The index, or the number of marks when none is that high.</returns>
    private static int FirstAtOrAbove(TextMark[] marks, float level)
    {
        var low = 0;
        var high = marks.Length;
        while (low < high)
        {
            var middle = low + ((high - low) / HalfDivisor);
            if (marks[middle].Rect.Bottom < level - MarkHeightLimit)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }
}
