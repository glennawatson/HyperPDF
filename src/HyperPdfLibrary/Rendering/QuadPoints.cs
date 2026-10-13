// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Rendering;

/// <summary>Reads the quadrilaterals of <c>/QuadPoints</c> (ISO 32000-2 12.5.6.10) without copying them.</summary>
internal static class QuadPoints
{
    /// <summary>The numbers in one quadrilateral.</summary>
    internal const int QuadNumbers = 8;

    /// <summary>The numbers in one point.</summary>
    private const int PointNumbers = 2;

    /// <summary>Gets how many whole quadrilaterals an array holds.</summary>
    /// <param name="points">The <c>/QuadPoints</c> array.</param>
    /// <returns>The count.</returns>
    internal static int Count(PdfArray points) => points.Count / QuadNumbers;

    /// <summary>
    /// Gets the rectangle round one quadrilateral. The standard lists the corners counterclockwise, but common writers
    /// list them in a Z order (top left, top right, bottom left, bottom right). The bounds of the four points are the
    /// same for both orders, so neither needs to be told apart.
    /// </summary>
    /// <param name="points">The array.</param>
    /// <param name="index">The quadrilateral.</param>
    /// <returns>The normalised rectangle.</returns>
    internal static PdfRectangle Rectangle(PdfArray points, int index)
    {
        var start = index * QuadNumbers;
        var left = float.MaxValue;
        var bottom = float.MaxValue;
        var right = float.MinValue;
        var top = float.MinValue;
        for (var i = 0; i < QuadNumbers; i += PointNumbers)
        {
            var x = points.GetSingle(start + i);
            var y = points.GetSingle(start + i + 1);
            left = Math.Min(left, x);
            right = Math.Max(right, x);
            bottom = Math.Min(bottom, y);
            top = Math.Max(top, y);
        }

        return PdfRectangle.FromCorners(left, bottom, right, top);
    }

    /// <summary>Gets the rectangle round every quadrilateral.</summary>
    /// <param name="points">The array.</param>
    /// <returns>The rectangle, or an empty one when there are none.</returns>
    internal static PdfRectangle Bounds(PdfArray points)
    {
        var count = Count(points);
        if (count == 0)
        {
            return default;
        }

        var bounds = Rectangle(points, 0);
        for (var i = 1; i < count; i++)
        {
            bounds = bounds.Union(Rectangle(points, i));
        }

        return bounds;
    }
}
