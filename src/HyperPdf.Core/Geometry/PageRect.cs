// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Geometry;

/// <summary>A rectangle in unrotated page space, in points, with the origin at the top-left corner and Y growing downwards.</summary>
/// <param name="Left">The left edge.</param>
/// <param name="Top">The top edge.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
[DebuggerDisplay("PageRect: ({Left}, {Top}, {Width}, {Height})")]
public readonly record struct PageRect(float Left, float Top, float Width, float Height)
{
    /// <summary>Gets the right edge.</summary>
    public float Right => Left + Width;

    /// <summary>Gets the bottom edge.</summary>
    public float Bottom => Top + Height;

    /// <summary>Creates a rectangle from its edges, normalising inverted edges.</summary>
    /// <param name="left">The left edge.</param>
    /// <param name="top">The top edge.</param>
    /// <param name="right">The right edge.</param>
    /// <param name="bottom">The bottom edge.</param>
    /// <returns>The rectangle.</returns>
    public static PageRect FromEdges(float left, float top, float right, float bottom)
    {
        var l = MathF.Min(left, right);
        var t = MathF.Min(top, bottom);
        return new(l, t, MathF.Abs(right - left), MathF.Abs(bottom - top));
    }

    /// <summary>Determines whether a point lies inside the rectangle.</summary>
    /// <param name="point">The point.</param>
    /// <returns><see langword="true"/> when the point is inside.</returns>
    public bool Contains(PagePoint point) => point.X >= Left && point.X <= Right && point.Y >= Top && point.Y <= Bottom;

    /// <summary>Returns the smallest rectangle containing both rectangles.</summary>
    /// <param name="other">The other rectangle.</param>
    /// <returns>The union.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PageRect Union(PageRect other) =>
        FromEdges(MathF.Min(Left, other.Left), MathF.Min(Top, other.Top), MathF.Max(Right, other.Right), MathF.Max(Bottom, other.Bottom));
}
