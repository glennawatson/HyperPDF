// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;

namespace HyperPdf.ViewModels.Markup;

/// <summary>Works out where objects must move to line up or to be spread evenly.</summary>
public static class PageObjectLayout
{
    /// <summary>Half of a length.</summary>
    private const float Half = 0.5F;

    /// <summary>The most objects whose order is kept on the stack while they are spread.</summary>
    private const int StackLimit = 64;

    /// <summary>The fewest objects that can be spread, since the two outer ones stay where they are.</summary>
    private const int SpreadMinimum = 3;

    /// <summary>Gets the box around several boxes.</summary>
    /// <param name="boxes">The boxes.</param>
    /// <returns>The box around them, or an empty box when there are none.</returns>
    public static PageRect Union(ReadOnlySpan<PageRect> boxes)
    {
        if (boxes.IsEmpty)
        {
            return default;
        }

        var left = boxes[0].Left;
        var top = boxes[0].Top;
        var right = boxes[0].Right;
        var bottom = boxes[0].Bottom;
        for (var i = 1; i < boxes.Length; i++)
        {
            left = MathF.Min(left, boxes[i].Left);
            top = MathF.Min(top, boxes[i].Top);
            right = MathF.Max(right, boxes[i].Right);
            bottom = MathF.Max(bottom, boxes[i].Bottom);
        }

        return PageRect.FromEdges(left, top, right, bottom);
    }

    /// <summary>Gets how far each box moves so the boxes line up.</summary>
    /// <param name="boxes">The boxes.</param>
    /// <param name="alignment">The edge or middle to line up on.</param>
    /// <param name="moves">Receives how far each box moves; it must be as long as <paramref name="boxes"/>.</param>
    public static void Align(ReadOnlySpan<PageRect> boxes, PageObjectAlignment alignment, Span<PagePoint> moves)
    {
        var around = Union(boxes);
        for (var i = 0; i < boxes.Length; i++)
        {
            var box = boxes[i];
            moves[i] = alignment switch
            {
                PageObjectAlignment.Left => new(around.Left - box.Left, 0),
                PageObjectAlignment.Centre => new(around.Left + (around.Width * Half) - (box.Left + (box.Width * Half)), 0),
                PageObjectAlignment.Right => new(around.Right - box.Right, 0),
                PageObjectAlignment.Top => new(0, around.Top - box.Top),
                PageObjectAlignment.Middle => new(0, around.Top + (around.Height * Half) - (box.Top + (box.Height * Half))),
                _ => new(0, around.Bottom - box.Bottom),
            };
        }
    }

    /// <summary>Gets how far each box moves so the gaps between the boxes are equal, with the two outermost boxes staying put.</summary>
    /// <param name="boxes">The boxes.</param>
    /// <param name="across">True to spread from left to right, false to spread from top to bottom.</param>
    /// <param name="moves">Receives how far each box moves; it must be as long as <paramref name="boxes"/>.</param>
    public static void Spread(ReadOnlySpan<PageRect> boxes, bool across, Span<PagePoint> moves)
    {
        moves.Clear();
        if (boxes.Length < SpreadMinimum)
        {
            return;
        }

        // A group is a handful of objects, so its order lives on the stack; a larger one gets an array.
        Span<int> storage = stackalloc int[StackLimit];
        var order = boxes.Length <= StackLimit ? storage[..boxes.Length] : new int[boxes.Length];
        Place(boxes, across, order, moves);
    }

    /// <summary>Works out the moves that spread boxes evenly.</summary>
    /// <param name="boxes">The boxes.</param>
    /// <param name="across">True to spread from left to right, false to spread from top to bottom.</param>
    /// <param name="order">Space for the numbers of the boxes, as long as <paramref name="boxes"/>.</param>
    /// <param name="moves">Receives how far each box moves.</param>
    private static void Place(ReadOnlySpan<PageRect> boxes, bool across, Span<int> order, Span<PagePoint> moves)
    {
        var sizes = 0F;
        for (var i = 0; i < order.Length; i++)
        {
            order[i] = i;
            sizes += across ? boxes[i].Width : boxes[i].Height;
        }

        SortByStart(order, boxes, across);
        var first = boxes[order[0]];
        var last = boxes[order[^1]];
        var span = across ? last.Right - first.Left : last.Bottom - first.Top;
        var gap = (span - sizes) / (boxes.Length - 1);
        var next = across ? first.Right + gap : first.Bottom + gap;
        for (var i = 1; i < order.Length - 1; i++)
        {
            var box = boxes[order[i]];
            var move = next - (across ? box.Left : box.Top);
            moves[order[i]] = across ? new(move, 0) : new(0, move);
            next += (across ? box.Width : box.Height) + gap;
        }
    }

    /// <summary>Sorts box numbers by where their boxes start.</summary>
    /// <param name="order">The numbers, sorted in place.</param>
    /// <param name="boxes">The boxes.</param>
    /// <param name="across">True to sort by left edge, false by top edge.</param>
    private static void SortByStart(Span<int> order, ReadOnlySpan<PageRect> boxes, bool across)
    {
        // An insertion sort: the group is a handful of objects, and a span cannot be captured by a comparison.
        for (var i = 1; i < order.Length; i++)
        {
            var moving = order[i];
            var start = across ? boxes[moving].Left : boxes[moving].Top;
            var at = i - 1;
            while (at >= 0 && (across ? boxes[order[at]].Left : boxes[order[at]].Top) > start)
            {
                order[at + 1] = order[at];
                at--;
            }

            order[at + 1] = moving;
        }
    }
}
