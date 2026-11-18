// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using static HyperPdf.Core.Documents.LineFrames;

namespace HyperPdf.Core.Documents;

/// <summary>
/// Finds the lines of the paragraph a line of text belongs to: lines of the same size, one under the next, that start at the same
/// place, end at the same place or are centred on the same middle, however the page turns them. A list item is a paragraph too: its
/// first line starts with a marker such as a bullet or a number, which is either in the line's own text or a text of its own beside
/// it, and the lines that follow start further in, under the words rather than the marker.
/// </summary>
public static class ParagraphFinder
{
    /// <summary>How far a line may start from the paragraph's start, as a multiple of the line's height.</summary>
    private const float StartTolerance = 0.6F;

    /// <summary>The widest gap between two lines of a paragraph, as a multiple of the line's height.</summary>
    private const float GapTolerance = 1F;

    /// <summary>How far two lines of a paragraph may overlap, as a multiple of the line's height; lines side by side on one row overlap by the whole height.</summary>
    private const float OverlapTolerance = 0.5F;

    /// <summary>The smallest share of its height a line's height may be of the picked line's.</summary>
    private const float SmallestSize = 0.8F;

    /// <summary>The largest share of its height a line's height may be of the picked line's.</summary>
    private const float LargestSize = 1.25F;

    /// <summary>The furthest in, as a multiple of the line's height, the lines under a list marker may start.</summary>
    private const float LargestHangingIndent = 4F;

    /// <summary>The least share of a line's height that a marker beside it must be on the same row as the line.</summary>
    private const float MarkerRowShare = 0.5F;

    /// <summary>One half.</summary>
    private const float Half = 0.5F;

    /// <summary>The most lines that can start together by chance in a paragraph that is lined up another way.</summary>
    private const int MostByChance = 2;

    /// <summary>The two ways lines are found from the first line of a paragraph: those before it and those after it.</summary>
    private const int TwoSides = 2;

    /// <summary>The most characters a text that is only a marker may have, with the spaces around it.</summary>
    private const int LongestMarkerText = 8;

    /// <summary>The most digits of a number that marks a list item.</summary>
    private const int MostMarkerDigits = 3;

    /// <summary>Characters that stand alone as a list marker, including the bullets the symbol fonts put in the private use area.</summary>
    private const string BulletCharacters = "•●◦‣⁃–—*+-";

    /// <summary>The place of a line the others of its paragraph are lined up by.</summary>
    private enum LineEdge
    {
        /// <summary>Where the line starts, for lines lined up to the left.</summary>
        Start = 0,

        /// <summary>Where the line ends, for lines lined up to the right.</summary>
        End = 1,

        /// <summary>The middle of the line, for lines that are centred.</summary>
        Middle = 2,
    }

    /// <summary>Finds the lines of a paragraph or list item.</summary>
    /// <param name="objects">The objects of the page.</param>
    /// <param name="picked">The line of text that is picked.</param>
    /// <returns>The numbers of the paragraph's lines from the first line on, which include the picked line's own.</returns>
    public static int[] Find(ReadOnlySpan<PageObjectInfo> objects, PageObjectInfo picked)
    {
        if (IsLoneMarker(picked.Text))
        {
            return [picked.Index];
        }

        // Lines lined up to the left are by far the most common. Three lines or more that start together are one paragraph, but one or two may start together by chance in a paragraph
        // that is lined up to the right or the middle, so those are tried too; of the three ways the one that finds the most lines is the paragraph's own, left first so it wins a tie.
        var separate = HasLoneMarker(objects);
        var found = FindLined(objects, picked, LineEdge.Start, separate);
        if (found.Length > MostByChance)
        {
            return ParagraphMates.Add(objects, found, picked);
        }

        var byEnds = FindLined(objects, picked, LineEdge.End, separate);
        if (byEnds.Length > found.Length)
        {
            found = byEnds;
        }

        var byMiddles = FindLined(objects, picked, LineEdge.Middle, separate);

        // A row of the page is often several texts, and the paragraph takes them all.
        return ParagraphMates.Add(objects, byMiddles.Length > found.Length ? byMiddles : found, picked);
    }

    /// <summary>Joins the words of the lines a search found into one text, with a space between the lines and none between texts of a row that touch.</summary>
    /// <param name="objects">The objects of the page.</param>
    /// <param name="lines">The numbers the search gave, in order.</param>
    /// <returns>The words.</returns>
    public static string JoinText(ReadOnlySpan<PageObjectInfo> objects, int[] lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        return ParagraphMates.Join(objects, lines);
    }

    /// <summary>Tells whether text starts with a list marker: a bullet, a number or a letter that ends with a full stop or a bracket, followed by a space.</summary>
    /// <param name="text">The text of a line.</param>
    /// <returns><see langword="true"/> when the line is the first line of a list item.</returns>
    public static bool StartsWithMarker(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var trimmed = text.AsSpan().TrimStart();
        var space = trimmed.IndexOf(' ');
        return space > 0 && IsMarker(trimmed[..space]);
    }

    /// <summary>Tells whether an object is a line of text with words that is not only a list marker.</summary>
    /// <param name="item">The object.</param>
    /// <returns><see langword="true"/> for text that has words.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsTextLine(in PageObjectInfo item) => item.Kind == PageObjectKind.Text && item.Text.Length > 0 && !IsLoneMarker(item.Text);

    /// <summary>Tells whether a word is a list marker: a bullet, a number or a letter that ends with a full stop or a bracket.</summary>
    /// <param name="token">The word.</param>
    /// <returns><see langword="true"/> when it marks a list item.</returns>
    private static bool IsMarker(ReadOnlySpan<char> token) =>
        (token.Length == 1 && BulletCharacters.Contains(token[0], StringComparison.Ordinal)) || IsNumberMarker(token) || IsBracketMarker(token);

    /// <summary>Tells whether the whole of a text is one marker, as a bullet or a number is when it is drawn as a text of its own beside the words of its item.</summary>
    /// <param name="text">The text of an object.</param>
    /// <returns><see langword="true"/> when the text is only a marker.</returns>
    private static bool IsLoneMarker(string text)
    {
        // Most texts are far longer than any marker, which saves trimming them.
        if (text.Length > LongestMarkerText)
        {
            return false;
        }

        var trimmed = text.AsSpan().Trim();
        return !trimmed.IsEmpty && trimmed.IndexOf(' ') < 0 && IsMarker(trimmed);
    }

    /// <summary>Tells whether a word is a number followed by a full stop or a bracket, such as <c>12.</c> or <c>3)</c>.</summary>
    /// <param name="token">The word.</param>
    /// <returns><see langword="true"/> when it marks a numbered list item.</returns>
    private static bool IsNumberMarker(ReadOnlySpan<char> token)
    {
        if (token.Length < 2 || token[^1] is not ('.' or ')'))
        {
            return false;
        }

        var digits = token[..^1];
        return digits.Length <= MostMarkerDigits && digits.IndexOfAnyExceptInRange('0', '9') < 0;
    }

    /// <summary>Tells whether a word is a letter or a number in brackets, such as <c>(a)</c> or <c>b)</c>.</summary>
    /// <param name="token">The word.</param>
    /// <returns><see langword="true"/> when it marks a lettered list item.</returns>
    private static bool IsBracketMarker(ReadOnlySpan<char> token)
    {
        if (token.Length < 2 || token[^1] != ')')
        {
            return false;
        }

        var inside = token[0] == '(' ? token[1..^1] : token[..^1];
        return inside.Length is >= 1 and <= MostMarkerDigits && char.IsLetterOrDigit(inside[0]);
    }

    /// <summary>Finds the lines of the paragraph the picked line is in when its lines are lined up by one place.</summary>
    /// <param name="objects">The objects of the page.</param>
    /// <param name="picked">The picked line.</param>
    /// <param name="edge">The place of each line the lines are lined up by.</param>
    /// <param name="separate">Whether the page has any text that is only a marker, so the lines beside which one stands are worth looking for.</param>
    /// <returns>The numbers of the lines from the first line on.</returns>
    private static int[] FindLined(ReadOnlySpan<PageObjectInfo> objects, in PageObjectInfo picked, LineEdge edge, bool separate)
    {
        // Only lines lined up to the left start a list item with a marker.
        var head = edge == LineEdge.Start ? FindMarkerAbove(objects, picked) ?? picked : picked;

        // The lines over the head are found nearest first into the first half of a borrowed array and those under it into the second half, so the answer is the only array made.
        var half = objects.Length + 1;
        var room = ArrayPool<int>.Shared.Rent(half * TwoSides);
        try
        {
            var above = 0;
            if (head.Index == picked.Index && !StartsItem(objects, head, edge, separate))
            {
                // A plain paragraph grows both ways from the picked line, stopping at the first line of a list item.
                above = Grow(objects, head, new(edge, ItemAnchor(head, edge), false, separate), room.AsSpan(0, half));
            }

            var below = Grow(objects, head, new(edge, ContinuationAnchor(objects, head, edge), true, separate), room.AsSpan(half, half));
            var lines = new int[above + 1 + below];
            for (var i = 0; i < above; i++)
            {
                lines[i] = room[above - 1 - i];
            }

            lines[above] = head.Index;
            room.AsSpan(half, below).CopyTo(lines.AsSpan(above + 1));
            return lines;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(room);
        }
    }

    /// <summary>Finds the marker line of a list item that a continuation line belongs to, when the marker is in the text of its first line.</summary>
    /// <param name="objects">The objects of the page.</param>
    /// <param name="picked">The picked line.</param>
    /// <returns>The marker line when the picked line is a continuation of a list item; otherwise <see langword="null"/>.</returns>
    private static PageObjectInfo? FindMarkerAbove(ReadOnlySpan<PageObjectInfo> objects, in PageObjectInfo picked)
    {
        var current = picked;
        var height = FrameOf(picked).Height;
        while (!StartsWithMarker(current.Text))
        {
            // The line over may start at the same place or further left, as far as a list marker's width.
            if (Nearest(objects, picked, current, new(LineEdge.Start, false, -height * LargestHangingIndent, height * StartTolerance)) is not { } line)
            {
                return null;
            }

            var indent = FrameOf(current).Start - FrameOf(line).Start;
            if (StartsWithMarker(line.Text) && indent > height * StartTolerance)
            {
                return line;
            }

            current = line;
        }

        return null;
    }

    /// <summary>Finds the line right over or under a line that is the same size and whose place falls within a range from the same place of the line.</summary>
    /// <param name="objects">The objects of the page.</param>
    /// <param name="sizeLine">The line whose height sets the size the line must be, and whose direction it must have.</param>
    /// <param name="current">The line to look from.</param>
    /// <param name="reach">Which way to look and how far from the current line's place the line's place may be.</param>
    /// <returns>The nearest such line, or <see langword="null"/> when there is none.</returns>
    private static PageObjectInfo? Nearest(ReadOnlySpan<PageObjectInfo> objects, in PageObjectInfo sizeLine, in PageObjectInfo current, in Reach reach)
    {
        PageObjectInfo? best = null;
        var bestGap = float.PositiveInfinity;
        var sizeFrame = FrameOf(sizeLine);
        var from = FrameOf(current);
        for (var i = 0; i < objects.Length; i++)
        {
            var candidate = objects[i];
            if (!IsTextLine(candidate) || candidate.Index == current.Index || !SameWay(candidate, sizeLine))
            {
                continue;
            }

            var frame = FrameOf(candidate);
            var gap = reach.Down ? frame.Top - from.Bottom : from.Top - frame.Bottom;
            var indent = FrameAnchor(frame, reach.Edge) - FrameAnchor(from, reach.Edge);
            if (gap >= bestGap || !IsClose(sizeFrame, frame, gap) || indent < reach.Lowest || indent > reach.Highest)
            {
                continue;
            }

            best = candidate;
            bestGap = gap;
        }

        return best;
    }

    /// <summary>Tells where the lines under a line are lined up: for lines lined up to the left under a list marker, further in than it, under the words.</summary>
    /// <param name="objects">The objects of the page.</param>
    /// <param name="head">The first line of the paragraph.</param>
    /// <param name="edge">The place of each line the lines are lined up by.</param>
    /// <returns>The place of the following lines.</returns>
    private static float ContinuationAnchor(ReadOnlySpan<PageObjectInfo> objects, in PageObjectInfo head, LineEdge edge)
    {
        if (edge != LineEdge.Start || !StartsWithMarker(head.Text))
        {
            return ItemAnchor(head, edge);
        }

        var height = FrameOf(head).Height;
        return Nearest(objects, head, head, new(LineEdge.Start, true, height * StartTolerance, height * LargestHangingIndent)) is { } line ? FrameOf(line).Start : FrameOf(head).Start;
    }

    /// <summary>Adds the lines that follow one another from a line, going down or up, that are lined up at a given place.</summary>
    /// <param name="objects">The objects of the page.</param>
    /// <param name="head">The line the paragraph is found from, which sets the size.</param>
    /// <param name="step">The place the lines are lined up at, the way they are lined up and which way to go.</param>
    /// <param name="found">Receives the numbers of the lines, in the order they are found; it holds as many as the page has objects.</param>
    /// <returns>How many lines were found.</returns>
    private static int Grow(ReadOnlySpan<PageObjectInfo> objects, in PageObjectInfo head, in GrowStep step, Span<int> found)
    {
        var count = 0;
        var current = head;
        while (Next(objects, head, current, step) is { } next)
        {
            // A line that opens a list item ends the paragraph: going down it is the next item's, and going up it is this item's first line.
            if (step.Edge == LineEdge.Start && step.Separate && HasMarkerBeside(objects, next))
            {
                if (!step.Down)
                {
                    found[count] = next.Index;
                    count++;
                }

                return count;
            }

            if (step.Edge == LineEdge.Start && StartsWithMarker(next.Text))
            {
                return count;
            }

            found[count] = next.Index;
            count++;
            current = next;
        }

        return count;
    }

    /// <summary>Finds the line right under or over a line that belongs to the same paragraph.</summary>
    /// <param name="objects">The objects of the page.</param>
    /// <param name="head">The first line, which sets the size.</param>
    /// <param name="current">The line to look from.</param>
    /// <param name="step">Where the lines of the paragraph are lined up and which way to look.</param>
    /// <returns>The nearest such line, or <see langword="null"/> when the paragraph ends.</returns>
    private static PageObjectInfo? Next(ReadOnlySpan<PageObjectInfo> objects, in PageObjectInfo head, in PageObjectInfo current, in GrowStep step)
    {
        var reach = FrameOf(head).Height * StartTolerance;
        var shift = step.Anchor - ItemAnchor(current, step.Edge);
        return Nearest(objects, head, current, new(step.Edge, step.Down, shift - reach, shift + reach));
    }

    /// <summary>Tells whether a line is the first line of a list item, by a marker in its text or a text of its own beside it.</summary>
    /// <param name="objects">The objects of the page.</param>
    /// <param name="line">The line.</param>
    /// <param name="edge">The place the lines are lined up by; only lines lined up to the left start a list item.</param>
    /// <param name="separate">Whether the page has any text that is only a marker.</param>
    /// <returns><see langword="true"/> when the line opens a list item.</returns>
    private static bool StartsItem(ReadOnlySpan<PageObjectInfo> objects, in PageObjectInfo line, LineEdge edge, bool separate) =>
        edge == LineEdge.Start && (StartsWithMarker(line.Text) || (separate && HasMarkerBeside(objects, line)));

    /// <summary>Tells whether any object of the page is a text that is only a marker.</summary>
    /// <param name="objects">The objects of the page.</param>
    /// <returns><see langword="true"/> when a bullet or number stands on its own.</returns>
    private static bool HasLoneMarker(ReadOnlySpan<PageObjectInfo> objects)
    {
        for (var i = 0; i < objects.Length; i++)
        {
            var item = objects[i];
            if (item.Kind == PageObjectKind.Text && IsLoneMarker(item.Text))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tells whether a text that is only a marker stands just before a line on the same row, as the bullet or number of a list item is drawn on its own.</summary>
    /// <param name="objects">The objects of the page.</param>
    /// <param name="line">The line.</param>
    /// <returns><see langword="true"/> when a marker stands beside the line.</returns>
    private static bool HasMarkerBeside(ReadOnlySpan<PageObjectInfo> objects, in PageObjectInfo line)
    {
        var frame = FrameOf(line);
        var height = frame.Height;
        for (var i = 0; i < objects.Length; i++)
        {
            var candidate = objects[i];
            if (candidate.Kind != PageObjectKind.Text || !SameWay(candidate, line) || candidate.Text.Length > LongestMarkerText)
            {
                continue;
            }

            // The place is checked before the text, as most short texts are not beside the line.
            var marker = FrameOf(candidate);
            var room = frame.Start - marker.End;
            var sharedRow = Math.Min(marker.Bottom, frame.Bottom) - Math.Max(marker.Top, frame.Top);
            if (room >= -height * OverlapTolerance && room <= height * LargestHangingIndent && sharedRow >= height * MarkerRowShare && IsLoneMarker(candidate.Text))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tells whether a line is the same size as a first line and close enough to the line before it.</summary>
    /// <param name="head">The first line, which sets the size.</param>
    /// <param name="candidate">The line that may follow or precede.</param>
    /// <param name="gap">The space between the lines; negative when they overlap.</param>
    /// <returns><see langword="true"/> when the candidate is the same size and close.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsClose(in LineFrame head, in LineFrame candidate, float gap)
    {
        var height = head.Height;
        var sameSize = candidate.Height >= height * SmallestSize && candidate.Height <= height * LargestSize;
        return sameSize && gap >= -height * OverlapTolerance && gap <= height * GapTolerance;
    }

    /// <summary>Gets the place of a line that lines are lined up by.</summary>
    /// <param name="item">The line.</param>
    /// <param name="edge">Which place.</param>
    /// <returns>The place along the way the line reads.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ItemAnchor(in PageObjectInfo item, LineEdge edge) => FrameAnchor(FrameOf(item), edge);

    /// <summary>Gets the place of a framed line that lines are lined up by.</summary>
    /// <param name="frame">The line.</param>
    /// <param name="edge">Which place.</param>
    /// <returns>The place along the way the line reads.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float FrameAnchor(in LineFrame frame, LineEdge edge) => edge switch
    {
        LineEdge.Start => frame.Start,
        LineEdge.End => frame.End,
        _ => (frame.Start + frame.End) * Half,
    };

    /// <summary>Which way to look for a line and how far its place may be from the current line's.</summary>
    /// <param name="Edge">The place of each line the lines are lined up by.</param>
    /// <param name="Down">Whether to look at the lines that follow rather than the ones before.</param>
    /// <param name="Lowest">The least the line's place may be on from the current line's; negative for before it.</param>
    /// <param name="Highest">The most the line's place may be on from the current line's.</param>
    private readonly record struct Reach(LineEdge Edge, bool Down, float Lowest, float Highest);

    /// <summary>How the lines of a paragraph are followed from a line.</summary>
    /// <param name="Edge">The place of each line the lines are lined up by.</param>
    /// <param name="Anchor">Where the lines are lined up.</param>
    /// <param name="Down">Whether to follow the lines that come after rather than the ones before.</param>
    /// <param name="Separate">Whether the page has any text that is only a marker.</param>
    private readonly record struct GrowStep(LineEdge Edge, float Anchor, bool Down, bool Separate);
}
