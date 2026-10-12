// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Rendering;

/// <summary>
/// The part of a page a snapshot renders: the scale in pixels per point and the pixel window of the rendered page.
/// Snapshots are rendered at twice the screen's resolution or 200 dots per inch, whichever is sharper, up to a size
/// limit so a large area never needs a huge image.
/// </summary>
/// <param name="Scale">The pixels per point.</param>
/// <param name="OffsetX">The left edge of the window in the rendered page, in pixels.</param>
/// <param name="OffsetY">The top edge of the window in the rendered page, in pixels.</param>
/// <param name="Width">The window's width in pixels.</param>
/// <param name="Height">The window's height in pixels.</param>
[DebuggerDisplay("SnapshotRegion: {Width} x {Height} at x{Scale}")]
public readonly record struct SnapshotRegion(float Scale, int OffsetX, int OffsetY, int Width, int Height)
{
    /// <summary>The resolution of a snapshot, in dots per inch, when the screen is coarser.</summary>
    public static readonly double MinDotsPerInch = 200;

    /// <summary>How many times the screen's resolution a snapshot is rendered at.</summary>
    public static readonly double ScreenMultiple = 2;

    /// <summary>The most pixels a snapshot may have: 4096 by 4096.</summary>
    public static readonly double MaxPixels = 4096D * 4096D;

    /// <summary>The smallest area, in device independent pixels, worth taking a picture of.</summary>
    public static readonly double MinSize = 4;

    /// <summary>The points in an inch.</summary>
    private const double PointsPerInch = 72;

    /// <summary>Gets a value indicating whether there is nothing to render.</summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>Works out the region to render for an area of a page on screen.</summary>
    /// <param name="left">The area's left edge from the page's left edge, in device independent pixels.</param>
    /// <param name="top">The area's top edge from the page's top edge, in device independent pixels.</param>
    /// <param name="width">The area's width, in device independent pixels.</param>
    /// <param name="height">The area's height, in device independent pixels.</param>
    /// <param name="layoutScale">The device independent pixels per point the page is shown at.</param>
    /// <param name="renderScaling">The screen's device pixels per device independent pixel.</param>
    /// <returns>The region, or an empty one when the area is too small.</returns>
    public static SnapshotRegion Create(double left, double top, double width, double height, double layoutScale, double renderScaling)
    {
        if (width < MinSize || height < MinSize || !(layoutScale > 0))
        {
            return default;
        }

        var scale = Math.Max(layoutScale * Math.Max(renderScaling, 1) * ScreenMultiple, MinDotsPerInch / PointsPerInch);
        var pixels = width / layoutScale * scale * (height / layoutScale) * scale;
        if (pixels > MaxPixels)
        {
            scale *= Math.Sqrt(MaxPixels / pixels);
        }

        scale = Math.Min(scale, TileGrid.MaxScale);
        var ratio = scale / layoutScale;
        var offsetX = (int)Math.Floor(Math.Max(0, left) * ratio);
        var offsetY = (int)Math.Floor(Math.Max(0, top) * ratio);
        return new((float)scale, offsetX, offsetY, Math.Max(1, (int)Math.Floor(width * ratio)), Math.Max(1, (int)Math.Floor(height * ratio)));
    }
}
