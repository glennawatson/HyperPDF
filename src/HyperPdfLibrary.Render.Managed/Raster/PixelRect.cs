// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Render.Managed.Raster;

/// <summary>A rectangle of whole pixels.</summary>
/// <param name="Left">The first column.</param>
/// <param name="Top">The first line.</param>
/// <param name="Right">One past the last column.</param>
/// <param name="Bottom">One past the last line.</param>
[DebuggerDisplay("PixelRect: {Left},{Top} to {Right},{Bottom}")]
internal readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    /// <summary>The farthest coordinate kept. No surface is larger, and sums of two such values still fit an integer.</summary>
    private const float Limit = 1 << 28;

    /// <summary>Gets the width, 0 when the rectangle is empty.</summary>
    internal int Width => Math.Max(0, Right - Left);

    /// <summary>Gets the height, 0 when the rectangle is empty.</summary>
    internal int Height => Math.Max(0, Bottom - Top);

    /// <summary>Gets a value indicating whether the rectangle holds no pixel.</summary>
    internal bool IsEmpty => Right <= Left || Bottom <= Top;

    /// <summary>Finds the whole pixels that a rectangle with fractional edges touches.</summary>
    /// <param name="left">The left edge.</param>
    /// <param name="top">The top edge.</param>
    /// <param name="right">The right edge.</param>
    /// <param name="bottom">The bottom edge.</param>
    /// <returns>The pixels, empty when an edge is not a number.</returns>
    internal static PixelRect Around(float left, float top, float right, float bottom) =>
        float.IsFinite(left + top + right + bottom)
            ? new(ToPixel(MathF.Floor(left)), ToPixel(MathF.Floor(top)), ToPixel(MathF.Ceiling(right)), ToPixel(MathF.Ceiling(bottom)))
            : default;

    /// <summary>Finds the pixels two rectangles share.</summary>
    /// <param name="other">The other rectangle.</param>
    /// <returns>The shared pixels, which may be none.</returns>
    internal PixelRect Intersect(in PixelRect other) =>
        new(Math.Max(Left, other.Left), Math.Max(Top, other.Top), Math.Min(Right, other.Right), Math.Min(Bottom, other.Bottom));

    /// <summary>Turns a coordinate into a pixel index, kept well inside the range of an integer.</summary>
    /// <param name="value">The coordinate.</param>
    /// <returns>The index.</returns>
    private static int ToPixel(float value) => (int)Math.Clamp(value, -Limit, Limit);
}
