// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using static HyperPdf.Core.Documents.LineFrames;

namespace HyperPdf.Core.Documents;

/// <summary>
/// Adds to the lines of a paragraph the texts that sit on the same row beside them. A line of a real page is often drawn as several texts
/// (a run for each word, a mark drawn apart from its letter, a space of its own), and a paragraph that took only one of them would leave the rest behind.
/// </summary>
internal static class ParagraphMates
{
    /// <summary>How far, as a multiple of the line's height, the middle of a text may be from the middle of a text it shares a row with.</summary>
    private const float RowShare = 0.6F;

    /// <summary>The widest gap, as a multiple of the line's height, between two texts of one row; a wider gap is a column of its own.</summary>
    private const float MateGap = 0.5F;

    /// <summary>How far, as a multiple of the line's height, the places of a text may be from those of another and still be a copy drawn over it.</summary>
    private const float CopyShare = 0.1F;

    /// <summary>One half.</summary>
    private const float Half = 0.5F;

    /// <summary>The first character of the Hebrew block, where the scripts that read right to left begin.</summary>
    private const char FirstRightToLeft = '֐';

    /// <summary>The last character of the Arabic blocks.</summary>
    private const char LastArabic = 'ࣿ';

    /// <summary>The first of the Hebrew and Arabic presentation forms.</summary>
    private const char FirstPresentation = 'יִ';

    /// <summary>The last of the Hebrew and Arabic presentation forms.</summary>
    private const char LastPresentation = '﻿';

    /// <summary>Adds the texts beside the lines of a paragraph.</summary>
    /// <param name="objects">The objects of the page.</param>
    /// <param name="lines">The numbers of the paragraph's lines, from the first line on.</param>
    /// <param name="picked">The line the paragraph was found from, whose size and direction the texts beside it share.</param>
    /// <returns>The numbers of the lines with the numbers of the texts beside each after it, in the order they read; <paramref name="lines"/> itself when no line has any.</returns>
    internal static int[] Add(ReadOnlySpan<PageObjectInfo> objects, int[] lines, in PageObjectInfo picked)
    {
        var frames = ArrayPool<LineFrame>.Shared.Rent(objects.Length + lines.Length);
        var mates = ArrayPool<int>.Shared.Rent(objects.Length);
        var rows = ArrayPool<int>.Shared.Rent(objects.Length);
        try
        {
            FrameLines(objects, lines, frames);
            var found = FindMates(objects, lines, picked, new(frames, mates, rows));
            return found == 0 ? lines : Merge(objects, lines, new(frames, mates, rows), found);
        }
        finally
        {
            ArrayPool<LineFrame>.Shared.Return(frames);
            ArrayPool<int>.Shared.Return(mates);
            ArrayPool<int>.Shared.Return(rows);
        }
    }

    /// <summary>Joins the words of the lines of a paragraph into one text, with one space between the texts; a text that is only spaces adds none.</summary>
    /// <param name="objects">The objects of the page.</param>
    /// <param name="lines">The numbers of the lines and the texts beside them, in the order they read.</param>
    /// <returns>The words.</returns>
    internal static string Join(ReadOnlySpan<PageObjectInfo> objects, int[] lines)
    {
        var words = new StringBuilder();
        foreach (var index in lines)
        {
            var trimmed = Find(objects, index)?.Text.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                continue;
            }

            _ = words.Length > 0 ? words.Append(' ').Append(trimmed) : words.Append(trimmed);
        }

        return words.ToString();
    }

    /// <summary>Tells whether a text reads right to left, going by which way most of its letters read.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when most of its letters are Hebrew or Arabic.</returns>
    internal static bool ReadsRightToLeft(string text)
    {
        var balance = 0;
        foreach (var letter in text)
        {
            if (letter is >= FirstRightToLeft and <= LastArabic or >= FirstPresentation and <= LastPresentation)
            {
                balance++;
            }
            else if (char.IsLetter(letter))
            {
                balance--;
            }
        }

        return balance > 0;
    }

    /// <summary>Finds an object of the page by its number.</summary>
    /// <param name="objects">The objects of the page.</param>
    /// <param name="index">The number.</param>
    /// <returns>The object; <see langword="null"/> when the page has none.</returns>
    private static PageObjectInfo? Find(ReadOnlySpan<PageObjectInfo> objects, int index)
    {
        for (var i = 0; i < objects.Length; i++)
        {
            if (objects[i].Index == index)
            {
                return objects[i];
            }
        }

        return null;
    }

    /// <summary>Puts the frame of each line of a paragraph in the place of the line.</summary>
    /// <param name="objects">The objects of the page.</param>
    /// <param name="lines">The numbers of the lines.</param>
    /// <param name="frames">Receives the frames, in the order of the lines.</param>
    private static void FrameLines(ReadOnlySpan<PageObjectInfo> objects, int[] lines, LineFrame[] frames)
    {
        for (var i = 0; i < objects.Length; i++)
        {
            var place = Array.IndexOf(lines, objects[i].Index);
            if (place >= 0)
            {
                frames[place] = FrameOf(objects[i]);
            }
        }
    }

    /// <summary>Finds the texts that sit on a row of the paragraph, beside a line or beside a text that is beside one.</summary>
    /// <param name="objects">The objects of the page.</param>
    /// <param name="lines">The numbers of the lines.</param>
    /// <param name="picked">The line whose size and direction the texts share.</param>
    /// <param name="room">Where the frames of the lines are, and where the numbers and rows of the texts found are put.</param>
    /// <returns>How many texts were found.</returns>
    private static int FindMates(ReadOnlySpan<PageObjectInfo> objects, int[] lines, in PageObjectInfo picked, in MateRoom room)
    {
        var height = FrameOf(picked).Height;
        var found = 0;
        bool grew;
        do
        {
            grew = false;
            for (var i = 0; i < objects.Length; i++)
            {
                var candidate = objects[i];
                if (IsKnown(lines, room.Mates, found, candidate.Index) || !SameWay(candidate, picked) || !ParagraphFinder.IsTextLine(candidate))
                {
                    continue;
                }

                var frame = FrameOf(candidate);
                var slot = RowSlot(room.Frames, lines.Length + found, frame, height);
                if (slot < 0)
                {
                    continue;
                }

                room.Mates[found] = candidate.Index;
                room.Frames[lines.Length + found] = frame;
                room.Rows[found] = slot < lines.Length ? slot : room.Rows[slot - lines.Length];
                found++;
                grew = true;
            }
        }
        while (grew);

        return found;
    }

    /// <summary>Tells whether a text is one of the lines or one of the texts found already.</summary>
    /// <param name="lines">The numbers of the lines.</param>
    /// <param name="mates">The numbers of the texts found.</param>
    /// <param name="found">How many texts were found.</param>
    /// <param name="index">The number of the text.</param>
    /// <returns><see langword="true"/> when it is one of them.</returns>
    private static bool IsKnown(int[] lines, int[] mates, int found, int index) => Array.IndexOf(lines, index) >= 0 || Array.IndexOf(mates, index, 0, found) >= 0;

    /// <summary>Finds a framed text that a text sits beside on one row.</summary>
    /// <param name="frames">The frames of the lines and of the texts found.</param>
    /// <param name="total">How many frames are in use.</param>
    /// <param name="frame">The frame of the text.</param>
    /// <param name="height">The height of a line, which sets how far apart the middles of a row's texts may be and how wide a gap may be.</param>
    /// <returns>The place of the frame the text is beside; -1 when it is beside none.</returns>
    private static int RowSlot(LineFrame[] frames, int total, in LineFrame frame, float height)
    {
        var middle = frame.Top + frame.Bottom;
        var slot = -1;
        for (var i = 0; i < total; i++)
        {
            var other = frames[i];

            // A text drawn again right over another, as a halo or a shadow is, is a copy of it and not part of its row.
            if (IsCopy(frame, other, height))
            {
                return -1;
            }

            var gap = Math.Max(frame.Start - other.End, other.Start - frame.End);
            if (slot < 0 && Math.Abs((middle - (other.Top + other.Bottom)) * Half) <= height * RowShare && gap <= height * MateGap)
            {
                slot = i;
            }
        }

        return slot;
    }

    /// <summary>Tells whether a framed text is drawn over another in the same place.</summary>
    /// <param name="frame">The frame of the text.</param>
    /// <param name="other">The frame of the other text.</param>
    /// <param name="height">The height of a line, which sets how far off the places may be.</param>
    /// <returns><see langword="true"/> when both have the same places to within a small part of the height.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsCopy(in LineFrame frame, in LineFrame other, float height)
    {
        var tolerance = height * CopyShare;
        return Math.Abs(frame.Start - other.Start) <= tolerance && Math.Abs(frame.End - other.End) <= tolerance && Math.Abs(frame.Top - other.Top) <= tolerance;
    }

    /// <summary>Lists each line with the texts beside it after it, in the order they read.</summary>
    /// <param name="objects">The objects of the page.</param>
    /// <param name="lines">The numbers of the lines.</param>
    /// <param name="room">The frames of the lines and the texts found, and the numbers and rows of the texts.</param>
    /// <param name="found">How many texts were found.</param>
    /// <returns>The numbers of the lines and the texts.</returns>
    private static int[] Merge(ReadOnlySpan<PageObjectInfo> objects, int[] lines, in MateRoom room, int found)
    {
        var result = new int[lines.Length + found];
        var slots = ArrayPool<int>.Shared.Rent(result.Length);
        try
        {
            var at = 0;
            for (var line = 0; line < lines.Length; line++)
            {
                at = ListRow(objects, lines, line, room, new(slots, at, found));
            }

            for (var i = 0; i < result.Length; i++)
            {
                result[i] = slots[i] < lines.Length ? lines[slots[i]] : room.Mates[slots[i] - lines.Length];
            }

            return result;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(slots);
        }
    }

    /// <summary>Lists a line and the texts beside it, in the order they read.</summary>
    /// <param name="objects">The objects of the page.</param>
    /// <param name="lines">The numbers of the lines.</param>
    /// <param name="line">The place of the line among the lines.</param>
    /// <param name="room">The frames of the lines and the texts found, and the numbers and rows of the texts.</param>
    /// <param name="output">The places listed so far, and where the next goes.</param>
    /// <returns>Where the next place after the row goes.</returns>
    private static int ListRow(ReadOnlySpan<PageObjectInfo> objects, int[] lines, int line, in MateRoom room, in RowOutput output)
    {
        var slots = output.Slots;
        var start = output.At;
        var at = start;
        slots[at] = line;
        at++;
        for (var mate = 0; mate < output.Found; mate++)
        {
            if (room.Rows[mate] != line)
            {
                continue;
            }

            slots[at] = lines.Length + mate;
            at++;
        }

        if (at - start <= 1)
        {
            return at;
        }

        SortRow(slots.AsSpan(start, at - start), room.Frames, ReadsRightToLeft(TextOf(objects, lines[line])));
        return at;
    }

    /// <summary>Puts the texts of a row in the order they read: left to right, or right to left for text that reads that way.</summary>
    /// <param name="row">The places of the frames of the row's texts.</param>
    /// <param name="frames">The frames.</param>
    /// <param name="rightToLeft">Whether the row reads right to left.</param>
    private static void SortRow(Span<int> row, LineFrame[] frames, bool rightToLeft)
    {
        for (var i = 1; i < row.Length; i++)
        {
            var slot = row[i];
            var key = KeyOf(frames[slot], rightToLeft);
            var before = i - 1;
            while (before >= 0 && KeyOf(frames[row[before]], rightToLeft) > key)
            {
                row[before + 1] = row[before];
                before--;
            }

            row[before + 1] = slot;
        }
    }

    /// <summary>Gets the place a text sorts by in its row.</summary>
    /// <param name="frame">The frame of the text.</param>
    /// <param name="rightToLeft">Whether the row reads right to left, so the text furthest along comes first.</param>
    /// <returns>The sort key.</returns>
    private static float KeyOf(in LineFrame frame, bool rightToLeft) => rightToLeft ? -frame.Start : frame.Start;

    /// <summary>Gets the words of a text of the page.</summary>
    /// <param name="objects">The objects of the page.</param>
    /// <param name="index">The number of the text.</param>
    /// <returns>The words; empty when the page has no such text.</returns>
    private static string TextOf(ReadOnlySpan<PageObjectInfo> objects, int index)
    {
        for (var i = 0; i < objects.Length; i++)
        {
            if (objects[i].Index == index)
            {
                return objects[i].Text;
            }
        }

        return string.Empty;
    }

    /// <summary>The room the search for texts uses.</summary>
    /// <param name="Frames">The frames of the lines, in their order, then those of the texts found.</param>
    /// <param name="Mates">The numbers of the texts found.</param>
    /// <param name="Rows">The line each text found is on the row of.</param>
    private readonly record struct MateRoom(LineFrame[] Frames, int[] Mates, int[] Rows);

    /// <summary>Where the places of a merged list are put.</summary>
    /// <param name="Slots">The places listed so far.</param>
    /// <param name="At">Where the next place goes.</param>
    /// <param name="Found">How many texts were found beside the lines.</param>
    private readonly record struct RowOutput(int[] Slots, int At, int Found);
}
