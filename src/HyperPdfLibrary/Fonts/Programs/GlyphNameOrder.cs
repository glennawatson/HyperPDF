// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>Sorts glyph ids by glyph name, then by id, in place and without the comparer objects the framework's sorts make.</summary>
internal static class GlyphNameOrder
{
    /// <summary>Sorts glyph ids by the names a program gives them.</summary>
    /// <param name="program">The font program.</param>
    /// <param name="order">The glyph ids; sorted in place.</param>
    internal static void Sort(FontProgram program, Span<int> order)
    {
        for (var root = (order.Length >> 1) - 1; root >= 0; root--)
        {
            SiftDown(program, order, root, order.Length);
        }

        for (var end = order.Length - 1; end > 0; end--)
        {
            var largest = order[0];
            order[0] = order[end];
            order[end] = largest;
            SiftDown(program, order, 0, end);
        }
    }

    /// <summary>Moves a glyph down a max-heap until both of its children sort before it.</summary>
    /// <param name="program">The font program.</param>
    /// <param name="heap">The heap.</param>
    /// <param name="root">The position of the glyph to move.</param>
    /// <param name="count">The glyphs in the heap.</param>
    private static void SiftDown(FontProgram program, Span<int> heap, int root, int count)
    {
        var glyph = heap[root];
        var parent = root;
        var child = (parent << 1) + 1;
        while (child < count)
        {
            if (child + 1 < count && Compare(program, heap[child], heap[child + 1]) < 0)
            {
                child++;
            }

            if (Compare(program, glyph, heap[child]) >= 0)
            {
                break;
            }

            heap[parent] = heap[child];
            parent = child;
            child = (parent << 1) + 1;
        }

        heap[parent] = glyph;
    }

    /// <summary>Orders two glyphs by name, then by id.</summary>
    /// <param name="program">The font program.</param>
    /// <param name="x">The first glyph.</param>
    /// <param name="y">The second glyph.</param>
    /// <returns>A negative number when the first sorts before the second, positive when after, and zero when they are the same glyph.</returns>
    private static int Compare(FontProgram program, int x, int y)
    {
        var order = program.GetGlyphName(x).SequenceCompareTo(program.GetGlyphName(y));
        return order != 0 ? order : x.CompareTo(y);
    }
}
