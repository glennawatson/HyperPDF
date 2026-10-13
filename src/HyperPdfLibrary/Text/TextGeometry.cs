// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Text;

/// <summary>Matrix and rectangle arithmetic for text layout.</summary>
internal static class TextGeometry
{
    /// <summary>The difference below which two text state values are the same.</summary>
    private const float SameValueTolerance = 1E-6F;

    /// <summary>Determines whether two text state values are the same, allowing for float rounding.</summary>
    /// <param name="first">The first value.</param>
    /// <param name="second">The second value.</param>
    /// <returns><see langword="true"/> when they match.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Same(float first, float second) => MathF.Abs(first - second) <= SameValueTolerance;

    /// <summary>Inverts a matrix, giving the identity when it cannot be inverted.</summary>
    /// <param name="matrix">The matrix.</param>
    /// <returns>The inverse.</returns>
    internal static Matrix3x2 Invert(Matrix3x2 matrix) => Matrix3x2.Invert(matrix, out var inverse) ? inverse : Matrix3x2.Identity;

    /// <summary>Transforms a point.</summary>
    /// <param name="matrix">The matrix.</param>
    /// <param name="point">The point.</param>
    /// <returns>The transformed point.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector2 Transform(Matrix3x2 matrix, Vector2 point) => Vector2.Transform(point, matrix);

    /// <summary>Gets the bounding box of a rectangle after a matrix.</summary>
    /// <param name="matrix">The matrix.</param>
    /// <param name="rectangle">The rectangle.</param>
    /// <returns>The normalised bounds.</returns>
    internal static PdfRectangle TransformRect(Matrix3x2 matrix, in PdfRectangle rectangle)
    {
        var a = Vector2.Transform(new(rectangle.Left, rectangle.Top), matrix);
        var b = Vector2.Transform(new(rectangle.Left, rectangle.Bottom), matrix);
        var c = Vector2.Transform(new(rectangle.Right, rectangle.Top), matrix);
        var d = Vector2.Transform(new(rectangle.Right, rectangle.Bottom), matrix);
        var min = Vector2.Min(Vector2.Min(a, b), Vector2.Min(c, d));
        var max = Vector2.Max(Vector2.Max(a, b), Vector2.Max(c, d));
        return new(min.X, min.Y, max.X, max.Y);
    }

    /// <summary>Normalises a rectangle so left is not past right and bottom is not above top.</summary>
    /// <param name="rectangle">The rectangle.</param>
    /// <returns>The normalised rectangle.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfRectangle Normalize(in PdfRectangle rectangle) =>
        PdfRectangle.FromCorners(rectangle.Left, rectangle.Bottom, rectangle.Right, rectangle.Top);

    /// <summary>Gets the union of two rectangles after normalising both.</summary>
    /// <param name="first">The first rectangle.</param>
    /// <param name="second">The second rectangle.</param>
    /// <returns>The union.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfRectangle Union(in PdfRectangle first, in PdfRectangle second) => Normalize(first).Union(Normalize(second));

    /// <summary>Determines whether a rectangle has no area.</summary>
    /// <param name="rectangle">The rectangle.</param>
    /// <returns><see langword="true"/> when the rectangle is empty.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsEmpty(in PdfRectangle rectangle) => rectangle.Left >= rectangle.Right || rectangle.Bottom >= rectangle.Top;

    /// <summary>Determines whether a point is inside or on the edge of a rectangle.</summary>
    /// <param name="rectangle">The rectangle.</param>
    /// <param name="point">The point.</param>
    /// <returns><see langword="true"/> when the point is inside.</returns>
    internal static bool Contains(in PdfRectangle rectangle, Vector2 point)
    {
        var box = Normalize(rectangle);
        return point.X <= box.Right && point.X >= box.Left && point.Y <= box.Top && point.Y >= box.Bottom;
    }
}
