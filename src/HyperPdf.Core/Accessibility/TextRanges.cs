// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Accessibility;

/// <summary>
/// Moves and resizes a range of text by a unit, the way a UI Automation text range does, so the Windows and macOS bridges
/// share one tested set of rules and only translate the results.
/// </summary>
public static class TextRanges
{
    /// <summary>The number of UTF-16 code units in a character outside the basic plane.</summary>
    private const int SurrogatePairLength = 2;

    /// <summary>Finds the unit that holds an offset.</summary>
    /// <param name="text">The text.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="unit">The size of unit.</param>
    /// <returns>The unit; empty at the end of the text.</returns>
    public static TextSpan Expand(ReadOnlySpan<char> text, int offset, TextRangeUnit unit)
    {
        if (unit is TextRangeUnit.Document or TextRangeUnit.Page)
        {
            return new(0, text.Length);
        }

        return unit == TextRangeUnit.Character ? ExpandCharacter(text, offset) : TextBoundaries.At(text, offset, ToGranularity(unit));
    }

    /// <summary>Moves a range by whole units. The range becomes one unit, and the count says how far it got.</summary>
    /// <param name="text">The text.</param>
    /// <param name="range">The range.</param>
    /// <param name="unit">The size of unit.</param>
    /// <param name="count">How many units to move; negative moves back.</param>
    /// <returns>The new range and how many units it moved.</returns>
    public static TextMove Move(ReadOnlySpan<char> text, TextSpan range, TextRangeUnit unit, int count)
    {
        if (unit is TextRangeUnit.Document or TextRangeUnit.Page || text.IsEmpty)
        {
            return new(new(0, text.Length), 0);
        }

        var position = Expand(text, Math.Min(range.Start, text.Length - 1), unit).Start;
        var moved = 0;
        while (moved < count && position < text.Length)
        {
            var next = Expand(text, position, unit).End;
            if (next >= text.Length)
            {
                break;
            }

            position = next;
            moved++;
        }

        while (moved > count && position > 0)
        {
            position = StartBefore(text, position, unit);
            moved--;
        }

        return new(Expand(text, position, unit), moved);
    }

    /// <summary>Moves one end of a range by whole units. When the start passes the end, or the end passes the start, the range collapses to the end that moved.</summary>
    /// <param name="text">The text.</param>
    /// <param name="range">The range.</param>
    /// <param name="moveEnd">Whether the end moves; <see langword="false"/> moves the start.</param>
    /// <param name="unit">The size of unit.</param>
    /// <param name="count">How many units to move; negative moves back.</param>
    /// <returns>The new range and how many units the end moved.</returns>
    public static TextMove MoveEndpoint(ReadOnlySpan<char> text, TextSpan range, bool moveEnd, TextRangeUnit unit, int count)
    {
        var offset = Math.Clamp(moveEnd ? range.End : range.Start, 0, text.Length);
        var step = StepOffset(text, offset, unit, count);
        return new(Collapse(range, moveEnd, step.Range.Start), step.Moved);
    }

    /// <summary>Moves an offset by whole units; an offset moving forward stops at the end of the text, which counts as a unit boundary.</summary>
    /// <param name="text">The text.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="unit">The size of unit.</param>
    /// <param name="count">How many units to move; negative moves back.</param>
    /// <returns>The new offset as an empty range, and how many units it moved.</returns>
    private static TextMove StepOffset(ReadOnlySpan<char> text, int offset, TextRangeUnit unit, int count)
    {
        if (text.IsEmpty || count == 0)
        {
            return new(new(offset, offset), 0);
        }

        if (unit is TextRangeUnit.Document or TextRangeUnit.Page)
        {
            var edge = count > 0 ? text.Length : 0;
            return new(new(edge, edge), edge == offset ? 0 : Math.Sign(count));
        }

        return count > 0 ? StepForward(text, offset, unit, count) : StepBack(text, offset, unit, count);
    }

    /// <summary>Moves an offset forward to the starts of the units after it.</summary>
    /// <param name="text">The text.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="unit">The size of unit.</param>
    /// <param name="count">How many units to move, above zero.</param>
    /// <returns>The new offset as an empty range, and how many units it moved.</returns>
    private static TextMove StepForward(ReadOnlySpan<char> text, int offset, TextRangeUnit unit, int count)
    {
        var moved = 0;
        while (moved < count && offset < text.Length)
        {
            var next = Expand(text, offset, unit).End;
            if (next <= offset)
            {
                break;
            }

            offset = next;
            moved++;
        }

        return new(new(offset, offset), moved);
    }

    /// <summary>Moves an offset back to the starts of the units before it.</summary>
    /// <param name="text">The text.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="unit">The size of unit.</param>
    /// <param name="count">How many units to move, below zero.</param>
    /// <returns>The new offset as an empty range, and how many units it moved.</returns>
    private static TextMove StepBack(ReadOnlySpan<char> text, int offset, TextRangeUnit unit, int count)
    {
        var moved = 0;
        while (moved > count && offset > 0)
        {
            var inside = Expand(text, Math.Min(offset, text.Length - 1), unit).Start;
            offset = inside < offset ? inside : StartBefore(text, offset, unit);
            moved--;
        }

        return new(new(offset, offset), moved);
    }

    /// <summary>Joins a moved end and the end that stayed, collapsing the range if they crossed.</summary>
    /// <param name="range">The range before the move.</param>
    /// <param name="moveEnd">Whether the end moved.</param>
    /// <param name="offset">Where the end that moved now is.</param>
    /// <returns>The new range.</returns>
    private static TextSpan Collapse(TextSpan range, bool moveEnd, int offset) =>
        moveEnd
            ? new(Math.Min(range.Start, offset), offset)
            : new(offset, Math.Max(range.End, offset));

    /// <summary>Gets the unit that holds a character, keeping a surrogate pair together.</summary>
    /// <param name="text">The text.</param>
    /// <param name="offset">The offset.</param>
    /// <returns>The character, or the pair.</returns>
    private static TextSpan ExpandCharacter(ReadOnlySpan<char> text, int offset)
    {
        if (text.IsEmpty || offset < 0 || offset >= text.Length)
        {
            return new(text.Length, text.Length);
        }

        var start = char.IsLowSurrogate(text[offset]) && offset > 0 && char.IsHighSurrogate(text[offset - 1]) ? offset - 1 : offset;
        var paired = char.IsHighSurrogate(text[start]) && start + 1 < text.Length && char.IsLowSurrogate(text[start + 1]);
        return new(start, start + (paired ? SurrogatePairLength : 1));
    }

    /// <summary>Finds the start of the unit before the one that starts at an offset.</summary>
    /// <param name="text">The text.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="unit">The size of unit.</param>
    /// <returns>The start of the earlier unit.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int StartBefore(ReadOnlySpan<char> text, int offset, TextRangeUnit unit) =>
        Expand(text, Math.Max(0, offset - 1), unit).Start;

    /// <summary>Maps a unit to the size the boundary finder understands.</summary>
    /// <param name="unit">The unit, never a document or page.</param>
    /// <returns>The size.</returns>
    private static TextGranularity ToGranularity(TextRangeUnit unit) =>
        unit switch
        {
            TextRangeUnit.Word => TextGranularity.Word,
            TextRangeUnit.Line => TextGranularity.Line,
            _ => TextGranularity.Paragraph,
        };
}
