// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Layout;

/// <summary>A rectangle in layout (device independent pixel) space.</summary>
/// <param name="X">The left edge.</param>
/// <param name="Y">The top edge.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
[DebuggerDisplay("LayoutRect: ({X}, {Y}, {Width}, {Height})")]
public readonly record struct LayoutRect(double X, double Y, double Width, double Height)
{
    /// <summary>The sides an edge distance is added to when a rectangle grows, one at each end.</summary>
    private const double Sides = 2;

    /// <summary>Gets the right edge.</summary>
    public double Right => X + Width;

    /// <summary>Gets the bottom edge.</summary>
    public double Bottom => Y + Height;

    /// <summary>Determines whether the rectangle contains a point.</summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <returns><see langword="true"/> when inside.</returns>
    public bool Contains(double x, double y) => x >= X && x < Right && y >= Y && y < Bottom;

    /// <summary>Determines whether the rectangle shares any area with another.</summary>
    /// <param name="other">The other rectangle.</param>
    /// <returns><see langword="true"/> when they overlap.</returns>
    public bool Intersects(in LayoutRect other) => other.X < Right && X < other.Right && other.Y < Bottom && Y < other.Bottom;

    /// <summary>Gets the area the rectangle shares with another.</summary>
    /// <param name="other">The other rectangle.</param>
    /// <returns>The shared area, or an empty rectangle when they do not meet.</returns>
    public LayoutRect Intersect(in LayoutRect other)
    {
        var left = Math.Max(X, other.X);
        var top = Math.Max(Y, other.Y);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);
        return right >= left && bottom >= top ? new(left, top, right - left, bottom - top) : default;
    }

    /// <summary>Gets the rectangle grown by a distance on each side.</summary>
    /// <param name="horizontal">How far the left and right edges move out.</param>
    /// <param name="vertical">How far the top and bottom edges move out.</param>
    /// <returns>The grown rectangle.</returns>
    public LayoutRect Inflate(double horizontal, double vertical) =>
        new(X - horizontal, Y - vertical, Width + (horizontal * Sides), Height + (vertical * Sides));
}
