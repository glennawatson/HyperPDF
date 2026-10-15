// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>Groups layout items into lines, and splits each line at the gaps that are wide enough to lie between columns.</summary>
internal static class LayoutLines
{
    /// <summary>The gap inside a line, in line heights, that separates two columns.</summary>
    private const float ColumnGapInLine = 1.2F;

    /// <summary>Groups the items into lines and splits each line at its column gaps.</summary>
    /// <param name="items">The array holding the items; sorted in place into lines.</param>
    /// <param name="count">The number of items.</param>
    /// <param name="lines">Receives the line pieces.</param>
    /// <returns>The number of line pieces.</returns>
    internal static int Build(LayoutItem[] items, int count, LayoutLine[] lines)
    {
        items.AsSpan(0, count).Sort(static (a, b) => a.Bounds.CenterY.CompareTo(b.Bounds.CenterY));
        var lineCount = 0;
        var rowStart = 0;
        var rowBounds = default(PdfViewerRect);
        for (var i = 0; i < count; i++)
        {
            if (i > rowStart && !LayoutGeometry.SameRow(rowBounds, items[i].Bounds))
            {
                Split(items, rowStart, i - rowStart, rowBounds, lines, ref lineCount);
                rowStart = i;
                rowBounds = default;
            }

            rowBounds = rowBounds.Union(items[i].Bounds);
        }

        Split(items, rowStart, count - rowStart, rowBounds, lines, ref lineCount);
        return lineCount;
    }

    /// <summary>Sorts a line left to right and splits it where a gap is wide enough to be between columns.</summary>
    /// <param name="items">The array of items.</param>
    /// <param name="start">The position of the line's first item.</param>
    /// <param name="count">The line's items.</param>
    /// <param name="row">The box around the line.</param>
    /// <param name="lines">Receives the pieces.</param>
    /// <param name="lineCount">The pieces so far; advanced past those added.</param>
    private static void Split(LayoutItem[] items, int start, int count, PdfViewerRect row, LayoutLine[] lines, ref int lineCount)
    {
        items.AsSpan(start, count).Sort(static (a, b) => a.Bounds.Left.CompareTo(b.Bounds.Left));
        var limit = ColumnGapInLine * row.Height;
        var pieceStart = start;
        var pieceBounds = default(PdfViewerRect);
        for (var i = start; i < start + count; i++)
        {
            if (i > pieceStart && items[i].Bounds.Left - pieceBounds.Right > limit)
            {
                lines[lineCount] = new(pieceStart, i - pieceStart, pieceBounds);
                lineCount++;
                pieceStart = i;
                pieceBounds = default;
            }

            pieceBounds = pieceBounds.Union(items[i].Bounds);
        }

        lines[lineCount] = new(pieceStart, start + count - pieceStart, pieceBounds);
        lineCount++;
    }
}
