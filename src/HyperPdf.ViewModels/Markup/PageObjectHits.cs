// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;

namespace HyperPdf.ViewModels.Markup;

/// <summary>Finds the object under a point on a page in Edit mode.</summary>
public static class PageObjectHits
{
    /// <summary>How far outside an object's box a point still counts as on it, in points, so thin lines and small text can be picked.</summary>
    private const float Slop = 2;

    /// <summary>Finds the smallest object under a point, so text on a coloured box is picked rather than the box. Of equal sizes, the one drawn last wins.</summary>
    /// <param name="objects">The objects on the page, in drawing order.</param>
    /// <param name="point">The point in page space.</param>
    /// <returns>The index into <paramref name="objects"/>, or -1 when nothing is there.</returns>
    public static int Find(ReadOnlySpan<PageObjectInfo> objects, PagePoint point)
    {
        var best = -1;
        var bestArea = float.MaxValue;
        for (var i = 0; i < objects.Length; i++)
        {
            var bounds = objects[i].Bounds;
            if (point.X < bounds.Left - Slop || point.X > bounds.Right + Slop || point.Y < bounds.Top - Slop || point.Y > bounds.Bottom + Slop)
            {
                continue;
            }

            var area = bounds.Width * bounds.Height;
            if (area > bestArea)
            {
                continue;
            }

            best = i;
            bestArea = area;
        }

        return best;
    }

    /// <summary>Finds the object after an edit that is most like one that was picked before it: the same kind and text, nearest the place it was expected to be.</summary>
    /// <param name="objects">The objects on the page after the edit.</param>
    /// <param name="before">The object picked before the edit.</param>
    /// <param name="expected">Where the object's box should be now.</param>
    /// <returns>The index into <paramref name="objects"/>, or -1 when the object is gone.</returns>
    public static int FindAgain(ReadOnlySpan<PageObjectInfo> objects, PageObjectInfo before, PageRect expected)
    {
        var best = -1;
        var bestDistance = float.MaxValue;
        for (var i = 0; i < objects.Length; i++)
        {
            var item = objects[i];
            if (item.Kind != before.Kind || item.Text != before.Text)
            {
                continue;
            }

            var distance = MathF.Abs(item.Bounds.Left - expected.Left) + MathF.Abs(item.Bounds.Top - expected.Top);
            if (distance >= bestDistance)
            {
                continue;
            }

            best = i;
            bestDistance = distance;
        }

        return best;
    }
}
