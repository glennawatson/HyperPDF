// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>Shared geometry for the layout analysis: which items share a line, how boxes overlap and what text is a number.</summary>
internal static class LayoutGeometry
{
    /// <summary>How far, as a share of height, two items' centres may differ and still sit on one line.</summary>
    internal const float LineTolerance = 0.5F;

    /// <summary>The divisor that finds a middle.</summary>
    internal const int Halves = 2;

    /// <summary>Determines whether a box sits on the same line as the boxes gathered into a row.</summary>
    /// <param name="row">The box around the row so far.</param>
    /// <param name="item">The box.</param>
    /// <returns><see langword="true"/> when their centres are close for their heights.</returns>
    internal static bool SameRow(PdfViewerRect row, PdfViewerRect item) =>
        Math.Abs(item.CenterY - row.CenterY) <= LineTolerance * Math.Min(item.Height, row.Height);

    /// <summary>Gets how far two spans overlap.</summary>
    /// <param name="startA">The first span's start.</param>
    /// <param name="endA">The first span's end.</param>
    /// <param name="startB">The second span's start.</param>
    /// <param name="endB">The second span's end.</param>
    /// <returns>The overlap; zero when they are apart.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Overlap(float startA, float endA, float startB, float endB) =>
        Math.Max(0F, Math.Min(endA, endB) - Math.Max(startA, startB));

    /// <summary>Determines whether a point lies in a box grown by a margin.</summary>
    /// <param name="box">The box.</param>
    /// <param name="x">The point's horizontal position.</param>
    /// <param name="y">The point's vertical position.</param>
    /// <param name="margin">The margin.</param>
    /// <returns><see langword="true"/> when the point is inside.</returns>
    internal static bool Contains(PdfViewerRect box, float x, float y, float margin) =>
        x >= box.Left - margin && x <= box.Right + margin && y >= box.Top - margin && y <= box.Bottom + margin;

    /// <summary>Determines whether the text of some items is a number, such as a price or a percentage: it has digits and no letters.</summary>
    /// <param name="text">The items' text.</param>
    /// <param name="items">The items.</param>
    /// <returns><see langword="true"/> for a number.</returns>
    internal static bool IsNumeric(string text, ReadOnlySpan<LayoutItem> items)
    {
        var digits = 0;
        foreach (var item in items)
        {
            foreach (var character in text.AsSpan(item.TextStart, item.TextLength))
            {
                if (char.IsLetter(character))
                {
                    return false;
                }

                digits += char.IsDigit(character) ? 1 : 0;
            }
        }

        return digits > 0;
    }

    /// <summary>Sorts items into reading order: lines top to bottom, and each line left to right.</summary>
    /// <param name="items">The items.</param>
    internal static void SortReading(Span<LayoutItem> items)
    {
        if (items.Length < Halves)
        {
            return;
        }

        items.Sort(static (a, b) => a.Bounds.CenterY.CompareTo(b.Bounds.CenterY));
        var start = 0;
        var row = default(PdfViewerRect);
        for (var i = 0; i < items.Length; i++)
        {
            if (i > start && !SameRow(row, items[i].Bounds))
            {
                items[start..i].Sort(static (a, b) => a.Bounds.Left.CompareTo(b.Bounds.Left));
                start = i;
                row = default;
            }

            row = row.Union(items[i].Bounds);
        }

        items[start..].Sort(static (a, b) => a.Bounds.Left.CompareTo(b.Bounds.Left));
    }
}
