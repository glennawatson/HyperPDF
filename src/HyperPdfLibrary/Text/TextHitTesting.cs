// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Text;

/// <summary>
/// Finds the character at a point. A character whose box contains the point wins, the first in reading order if boxes
/// overlap. Otherwise, when a tolerance is given, the character whose box is nearest the point wins, provided the point is
/// within the tolerance of the box on both axes; ties go to the earlier character. Generated spaces and line breaks have
/// no extent and are never returned. Blocks of consecutive characters carry their union box, so large pages skip
/// blocks the point cannot reach.
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

    /// <summary>Finds the character at a point.</summary>
    /// <param name="chars">The page characters.</param>
    /// <param name="blocks">Bounds of contiguous character blocks, or empty on small pages.</param>
    /// <param name="point">The point in user space.</param>
    /// <param name="toleranceX">How far the point may be from a box horizontally; zero or less for none.</param>
    /// <param name="toleranceY">How far the point may be from a box vertically; zero or less for none.</param>
    /// <returns>The character index, or -1.</returns>
    internal static int Find(ReadOnlySpan<PdfTextChar> chars, ReadOnlySpan<TextHitBlock> blocks, Vector2 point, float toleranceX, float toleranceY)
    {
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y))
        {
            return -1;
        }

        var reachX = toleranceX > 0 ? toleranceX : 0;
        var reachY = toleranceY > 0 ? toleranceY : 0;
        var exact = FindContaining(chars, blocks, point);
        return exact >= 0 || (reachX <= 0 && reachY <= 0) ? exact : FindNearest(chars, blocks, point, reachX, reachY);
    }

    /// <summary>Finds the first character whose box contains a point.</summary>
    /// <param name="chars">The page characters.</param>
    /// <param name="blocks">The block bounds.</param>
    /// <param name="point">The point.</param>
    /// <returns>The index, or -1.</returns>
    private static int FindContaining(ReadOnlySpan<PdfTextChar> chars, ReadOnlySpan<TextHitBlock> blocks, Vector2 point)
    {
        if (blocks.IsEmpty)
        {
            return ScanContaining(chars, 0, chars.Length, point);
        }

        foreach (var block in blocks)
        {
            if (!TextGeometry.Contains(block.Bounds, point))
            {
                continue;
            }

            var hit = ScanContaining(chars, block.Start, block.Count, point);
            if (hit >= 0)
            {
                return hit;
            }
        }

        return -1;
    }

    /// <summary>Scans a range for the first character containing a point.</summary>
    /// <param name="chars">The page characters.</param>
    /// <param name="start">The first index.</param>
    /// <param name="count">The number of characters.</param>
    /// <param name="point">The point.</param>
    /// <returns>The index, or -1.</returns>
    private static int ScanContaining(ReadOnlySpan<PdfTextChar> chars, int start, int count, Vector2 point)
    {
        for (var i = start; i < start + count; i++)
        {
            if (chars[i].Kind != PdfTextCharKind.Generated && TextGeometry.Contains(chars[i].Box, point))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Finds the character whose box is nearest a point, within the reach on both axes.</summary>
    /// <param name="chars">The page characters.</param>
    /// <param name="blocks">The block bounds.</param>
    /// <param name="point">The point.</param>
    /// <param name="reachX">The horizontal reach.</param>
    /// <param name="reachY">The vertical reach.</param>
    /// <returns>The index, or -1.</returns>
    private static int FindNearest(ReadOnlySpan<PdfTextChar> chars, ReadOnlySpan<TextHitBlock> blocks, Vector2 point, float reachX, float reachY)
    {
        var best = new Nearest(-1, double.PositiveInfinity);
        if (blocks.IsEmpty)
        {
            ScanNearest(chars, 0, chars.Length, point, reachX, reachY, ref best);
            return best.Index;
        }

        foreach (var block in blocks)
        {
            var bounds = block.Bounds;
            var reach = new PdfRectangle(bounds.Left - reachX, bounds.Bottom - reachY, bounds.Right + reachX, bounds.Top + reachY);
            if (TextGeometry.Contains(reach, point))
            {
                ScanNearest(chars, block.Start, block.Count, point, reachX, reachY, ref best);
            }
        }

        return best.Index;
    }

    /// <summary>Scans a range, keeping the nearest character within reach.</summary>
    /// <param name="chars">The page characters.</param>
    /// <param name="start">The first index.</param>
    /// <param name="count">The number of characters.</param>
    /// <param name="point">The point.</param>
    /// <param name="reachX">The horizontal reach.</param>
    /// <param name="reachY">The vertical reach.</param>
    /// <param name="best">The nearest character so far.</param>
    private static void ScanNearest(ReadOnlySpan<PdfTextChar> chars, int start, int count, Vector2 point, float reachX, float reachY, ref Nearest best)
    {
        for (var i = start; i < start + count; i++)
        {
            if (chars[i].Kind == PdfTextCharKind.Generated)
            {
                continue;
            }

            var box = TextGeometry.Normalize(chars[i].Box);
            var dx = MathF.Max(MathF.Max(box.Left - point.X, point.X - box.Right), 0);
            var dy = MathF.Max(MathF.Max(box.Bottom - point.Y, point.Y - box.Top), 0);
            if (!(dx <= reachX && dy <= reachY))
            {
                continue;
            }

            var squared = ((double)dx * dx) + ((double)dy * dy);
            if (squared < best.Squared)
            {
                best = new(i, squared);
            }
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

    /// <summary>The nearest character found so far.</summary>
    /// <param name="Index">The character index, or -1.</param>
    /// <param name="Squared">The squared distance from the point to its box.</param>
    private readonly record struct Nearest(int Index, double Squared);
}
