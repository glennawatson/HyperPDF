// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>Joins the text of layout items into the text of a line, with spaces where words are apart.</summary>
internal static class LayoutText
{
    /// <summary>The gap between items, in line heights, that separates words.</summary>
    private const float WordGap = 0.15F;

    /// <summary>Gets the most characters the text of some items can take: all the items' text and a space before each.</summary>
    /// <param name="items">The items.</param>
    /// <returns>The length of a text buffer that always suffices.</returns>
    internal static int MaxLength(ReadOnlySpan<LayoutItem> items)
    {
        var length = items.Length;
        foreach (var item in items)
        {
            length += item.TextLength;
        }

        return length;
    }

    /// <summary>Determines whether a space must go before text so it does not run into what came before.</summary>
    /// <param name="written">The text so far.</param>
    /// <param name="next">The next text.</param>
    /// <returns><see langword="true"/> when neither side has white space.</returns>
    internal static bool NeedsSpace(ReadOnlySpan<char> written, ReadOnlySpan<char> next) =>
        !written.IsEmpty && !char.IsWhiteSpace(written[^1]) && !next.IsEmpty && !char.IsWhiteSpace(next[0]);

    /// <summary>Appends the text of a line of items, with spaces at word gaps and before the line when text comes before it.</summary>
    /// <param name="text">The items' text.</param>
    /// <param name="items">The items.</param>
    /// <param name="start">The position of the first item.</param>
    /// <param name="count">The number of items, left to right.</param>
    /// <param name="height">The line height that word gaps are measured against.</param>
    /// <param name="destination">The text so far.</param>
    /// <param name="length">The characters of text written; advanced past those added.</param>
    internal static void Append(string text, LayoutItem[] items, int start, int count, float height, char[] destination, ref int length)
    {
        var limit = WordGap * height;
        var previousRight = float.NaN;
        for (var i = start; i < start + count; i++)
        {
            var item = items[i];
            var next = text.AsSpan(item.TextStart, item.TextLength);
            var separated = float.IsNaN(previousRight) || item.Bounds.Left - previousRight > limit;
            if (separated && NeedsSpace(destination.AsSpan(0, length), next))
            {
                destination[length] = ' ';
                length++;
            }

            next.CopyTo(destination.AsSpan(length));
            length += next.Length;
            previousRight = item.Bounds.Right;
        }
    }

    /// <summary>Appends the text of items in reading order, which may cover several lines.</summary>
    /// <param name="text">The items' text.</param>
    /// <param name="items">The items.</param>
    /// <param name="start">The position of the first item.</param>
    /// <param name="count">The number of items, sorted by <see cref="LayoutGeometry.SortReading"/>.</param>
    /// <param name="destination">The text so far.</param>
    /// <param name="length">The characters of text written; advanced past those added.</param>
    internal static void AppendRows(string text, LayoutItem[] items, int start, int count, char[] destination, ref int length)
    {
        var rowStart = start;
        var row = default(PdfViewerRect);
        for (var i = start; i < start + count; i++)
        {
            if (i > rowStart && !LayoutGeometry.SameRow(row, items[i].Bounds))
            {
                Append(text, items, rowStart, i - rowStart, row.Height, destination, ref length);
                rowStart = i;
                row = default;
            }

            row = row.Union(items[i].Bounds);
        }

        if (count > 0)
        {
            Append(text, items, rowStart, start + count - rowStart, row.Height, destination, ref length);
        }
    }
}
