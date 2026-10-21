// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Layout;

/// <summary>Finds which corner resize handle of a rectangle on the canvas a pointer is on.</summary>
public static class ResizeHandles
{
    /// <summary>Finds whether a point is on one of the four corner handles of a canvas rectangle.</summary>
    /// <param name="area">The rectangle, in canvas units.</param>
    /// <param name="x">The pointer's canvas x.</param>
    /// <param name="y">The pointer's canvas y.</param>
    /// <param name="reach">How far from a corner the pointer may be, in canvas units, on each axis.</param>
    /// <param name="opposite">When a handle is on, the opposite corner, which stays put while resizing.</param>
    /// <returns><see langword="true"/> when a handle was on.</returns>
    public static bool TryGrab(LayoutRect area, double x, double y, double reach, out CanvasPoint opposite)
    {
        var left = area.X;
        var top = area.Y;
        var right = area.X + area.Width;
        var bottom = area.Y + area.Height;

        // Top-left, top-right, bottom-right, then bottom-left, each with the corner across from it.
        if (IsWithin(x, y, left, top, reach))
        {
            opposite = new(right, bottom);
            return true;
        }

        if (IsWithin(x, y, right, top, reach))
        {
            opposite = new(left, bottom);
            return true;
        }

        if (IsWithin(x, y, right, bottom, reach))
        {
            opposite = new(left, top);
            return true;
        }

        if (IsWithin(x, y, left, bottom, reach))
        {
            opposite = new(right, top);
            return true;
        }

        opposite = default;
        return false;
    }

    /// <summary>Determines whether a point is within a distance of a corner on both axes.</summary>
    /// <param name="x">The point's x.</param>
    /// <param name="y">The point's y.</param>
    /// <param name="cornerX">The corner's x.</param>
    /// <param name="cornerY">The corner's y.</param>
    /// <param name="reach">The distance.</param>
    /// <returns><see langword="true"/> when the point is within reach.</returns>
    private static bool IsWithin(double x, double y, double cornerX, double cornerY, double reach) =>
        Math.Abs(x - cornerX) <= reach && Math.Abs(y - cornerY) <= reach;
}
