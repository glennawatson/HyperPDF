// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Text;

/// <summary>
/// Finds the character at a point. A character whose box contains the point wins, the first in reading order if boxes
/// overlap. Otherwise, when a tolerance is given, the character whose box is nearest the point wins, provided the point is
/// within the tolerance of the box on both axes; ties go to the earlier character. Generated spaces and line breaks have
/// no extent and are never returned. Blocks of consecutive characters carry their union box, so large pages skip
/// blocks the point cannot reach, and the exact and the nearest search share one pass over the blocks.
/// </summary>
internal static class TextHitTesting
{
    /// <summary>Characters in one indexed block.</summary>
    private const int BlockSize = 32;

    /// <summary>Pages smaller than four blocks use a direct scan.</summary>
    private const int MinimumIndexedCharacters = 128;

    /// <summary>Builds conservative bounds for contiguous groups of characters on larger pages.</summary>
    /// <param name="chars">The page characters in original order.</param>
    /// <returns>The block bounds, or an empty array for a small page.</returns>
    internal static TextHitBlock[] BuildBlocks(ReadOnlySpan<PdfTextChar> chars)
    {
        if (chars.Length < MinimumIndexedCharacters)
        {
            return [];
        }

        var blocks = new TextHitBlock[1 + ((chars.Length - 1) / BlockSize)];
        for (var blockIndex = 0; blockIndex < blocks.Length; blockIndex++)
        {
            var start = blockIndex * BlockSize;
            var count = Math.Min(BlockSize, chars.Length - start);
            blocks[blockIndex] = new(BoundsOf(chars.Slice(start, count)), start, count);
        }

        return blocks;
    }

    /// <summary>Gets the union of all block bounds, used to reject points far from the whole page at once.</summary>
    /// <param name="blocks">The block bounds.</param>
    /// <returns>The union, or an empty box for no blocks.</returns>
    internal static PdfRectangle PageBoundsOf(ReadOnlySpan<TextHitBlock> blocks)
    {
        var bounds = default(PdfRectangle);
        for (var i = 0; i < blocks.Length; i++)
        {
            bounds = i == 0 ? blocks[i].Bounds : bounds.Union(blocks[i].Bounds);
        }

        return bounds;
    }

    /// <summary>Finds the character at a point.</summary>
    /// <param name="chars">The page characters.</param>
    /// <param name="blocks">Bounds of contiguous character blocks, or empty on small pages.</param>
    /// <param name="point">The point in user space.</param>
    /// <param name="toleranceX">How far the point may be from a box horizontally; zero or less for none.</param>
    /// <param name="toleranceY">How far the point may be from a box vertically; zero or less for none.</param>
    /// <returns>The character index, or -1.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Find(ReadOnlySpan<PdfTextChar> chars, ReadOnlySpan<TextHitBlock> blocks, Vector2 point, float toleranceX, float toleranceY) =>
        Find(chars, blocks, blocks.IsEmpty ? default : PageBoundsOf(blocks), point, toleranceX, toleranceY);

    /// <summary>Finds the character at a point, rejecting points outside the page bounds without visiting a block.</summary>
    /// <param name="chars">The page characters.</param>
    /// <param name="blocks">Bounds of contiguous character blocks, or empty on small pages.</param>
    /// <param name="pageBounds">The union of the block bounds; ignored when there are no blocks.</param>
    /// <param name="point">The point in user space.</param>
    /// <param name="toleranceX">How far the point may be from a box horizontally; zero or less for none.</param>
    /// <param name="toleranceY">How far the point may be from a box vertically; zero or less for none.</param>
    /// <returns>The character index, or -1.</returns>
    internal static int Find(ReadOnlySpan<PdfTextChar> chars, ReadOnlySpan<TextHitBlock> blocks, in PdfRectangle pageBounds, Vector2 point, float toleranceX, float toleranceY)
    {
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y))
        {
            return -1;
        }

        var reach = new Reach(toleranceX > 0 ? toleranceX : 0, toleranceY > 0 ? toleranceY : 0);
        var best = new Nearest(-1, double.PositiveInfinity);
        if (blocks.IsEmpty)
        {
            var direct = Scan(chars, 0, chars.Length, point, reach, ref best);
            return direct >= 0 ? direct : best.Index;
        }

        return ReachContains(pageBounds, reach, point) ? FindInBlocks(chars, blocks, point, reach, ref best) : -1;
    }

    /// <summary>Visits the blocks the point can reach, once, returning an exact hit at once and otherwise the nearest character.</summary>
    /// <param name="chars">The page characters.</param>
    /// <param name="blocks">The block bounds.</param>
    /// <param name="point">The point.</param>
    /// <param name="reach">The reach on each axis.</param>
    /// <param name="best">The nearest character so far.</param>
    /// <returns>The character index, or -1.</returns>
    private static int FindInBlocks(ReadOnlySpan<PdfTextChar> chars, ReadOnlySpan<TextHitBlock> blocks, Vector2 point, Reach reach, ref Nearest best)
    {
        foreach (var block in blocks)
        {
            if (!ReachContains(block.Bounds, reach, point))
            {
                continue;
            }

            var exact = Scan(chars, block.Start, block.Count, point, reach, ref best);
            if (exact >= 0)
            {
                return exact;
            }
        }

        return best.Index;
    }

    /// <summary>Checks whether a point is inside a normalized box grown by the reach on each side.</summary>
    /// <param name="bounds">The normalized box.</param>
    /// <param name="reach">The reach.</param>
    /// <param name="point">The point.</param>
    /// <returns>Whether the point is inside the grown box.</returns>
    private static bool ReachContains(in PdfRectangle bounds, Reach reach, Vector2 point) =>
        point.X <= bounds.Right + reach.X && point.X >= bounds.Left - reach.X && point.Y <= bounds.Top + reach.Y && point.Y >= bounds.Bottom - reach.Y;

    /// <summary>
    /// Scans a range once, returning the first character whose box contains the point and otherwise keeping the nearest
    /// character within reach.
    /// </summary>
    /// <param name="chars">The page characters.</param>
    /// <param name="start">The first index.</param>
    /// <param name="count">The number of characters.</param>
    /// <param name="point">The point.</param>
    /// <param name="reach">The reach on each axis.</param>
    /// <param name="best">The nearest character so far.</param>
    /// <returns>The index of the first character containing the point, or -1.</returns>
    private static int Scan(ReadOnlySpan<PdfTextChar> chars, int start, int count, Vector2 point, Reach reach, ref Nearest best)
    {
        var nearestWanted = reach.X > 0 || reach.Y > 0;
        for (var i = start; i < start + count; i++)
        {
            if (chars[i].Kind == PdfTextCharKind.Generated)
            {
                continue;
            }

            var box = TextGeometry.Normalize(chars[i].Box);
            if (point.X <= box.Right && point.X >= box.Left && point.Y <= box.Top && point.Y >= box.Bottom)
            {
                return i;
            }

            if (nearestWanted)
            {
                Consider(i, box, point, reach, ref best);
            }
        }

        return -1;
    }

    /// <summary>Keeps a character when it is within reach and nearer than the best so far.</summary>
    /// <param name="index">The character index.</param>
    /// <param name="box">The normalized box of the character.</param>
    /// <param name="point">The point.</param>
    /// <param name="reach">The reach on each axis.</param>
    /// <param name="best">The nearest character so far.</param>
    private static void Consider(int index, in PdfRectangle box, Vector2 point, Reach reach, ref Nearest best)
    {
        var dx = MathF.Max(MathF.Max(box.Left - point.X, point.X - box.Right), 0);
        var dy = MathF.Max(MathF.Max(box.Bottom - point.Y, point.Y - box.Top), 0);
        if (!(dx <= reach.X && dy <= reach.Y))
        {
            return;
        }

        var squared = ((double)dx * dx) + ((double)dy * dy);
        if (squared < best.Squared)
        {
            best = new(index, squared);
        }
    }

    /// <summary>Gets the union box of a block, or unbounded bounds when a box has a nonfinite coordinate.</summary>
    /// <param name="block">The block's characters.</param>
    /// <returns>The bounds.</returns>
    private static PdfRectangle BoundsOf(ReadOnlySpan<PdfTextChar> block)
    {
        var bounds = default(PdfRectangle);
        for (var i = 0; i < block.Length; i++)
        {
            var box = block[i].Box;
            if (!IsFinite(box))
            {
                return new(float.NegativeInfinity, float.NegativeInfinity, float.PositiveInfinity, float.PositiveInfinity);
            }

            bounds = i == 0 ? TextGeometry.Normalize(box) : bounds.Union(TextGeometry.Normalize(box));
        }

        return bounds;
    }

    /// <summary>Checks whether every box edge is finite.</summary>
    /// <param name="box">The box.</param>
    /// <returns>Whether all four edges are finite.</returns>
    private static bool IsFinite(in PdfRectangle box) =>
        float.IsFinite(box.Left) && float.IsFinite(box.Bottom) && float.IsFinite(box.Right) && float.IsFinite(box.Top);

    /// <summary>How far a point may be from a box on each axis.</summary>
    /// <param name="X">The horizontal reach.</param>
    /// <param name="Y">The vertical reach.</param>
    private readonly record struct Reach(float X, float Y);

    /// <summary>The nearest character found so far.</summary>
    /// <param name="Index">The character index, or -1.</param>
    /// <param name="Squared">The squared distance from the point to its box.</param>
    private readonly record struct Nearest(int Index, double Squared);
}
