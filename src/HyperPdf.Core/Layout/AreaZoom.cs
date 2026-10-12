// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Layout;

/// <summary>
/// Zoom to area: a rectangle dragged on the pages is zoomed until it fills the view and is then centred. A press with
/// almost no drag zooms in one step around the point instead.
/// </summary>
public static class AreaZoom
{
    /// <summary>The drag, in device independent pixels, below which a press counts as a click.</summary>
    public static readonly double MinDragSize = 8;

    /// <summary>Half, for the middle of the viewport.</summary>
    private const double Half = 0.5;

    /// <summary>Determines whether a dragged area is too small to zoom to, so it counts as a click.</summary>
    /// <param name="width">The area's width on screen.</param>
    /// <param name="height">The area's height on screen.</param>
    /// <returns><see langword="true"/> when the press was a click.</returns>
    public static bool IsClick(double width, double height) => width < MinDragSize && height < MinDragSize;

    /// <summary>Gets the zoom at which an area fills the view.</summary>
    /// <param name="zoom">The current zoom.</param>
    /// <param name="width">The area's width on screen at the current zoom.</param>
    /// <param name="height">The area's height on screen at the current zoom.</param>
    /// <param name="viewportWidth">The view's width.</param>
    /// <param name="viewportHeight">The view's height.</param>
    /// <returns>The new zoom, within the zoom limits; one step in for a click.</returns>
    public static double GetZoom(double zoom, double width, double height, double viewportWidth, double viewportHeight)
    {
        if (IsClick(width, height))
        {
            return ZoomCalculator.ZoomIn(zoom);
        }

        var factor = Math.Min(viewportWidth / Math.Max(width, 1), viewportHeight / Math.Max(height, 1));
        return ZoomCalculator.Clamp(zoom * factor);
    }

    /// <summary>Gets the scroll offset along one axis that puts a point in the middle of the view, kept within the content.</summary>
    /// <param name="centre">The point, in content coordinates.</param>
    /// <param name="viewport">The view's length along the axis.</param>
    /// <param name="extent">The content's length along the axis.</param>
    /// <returns>The offset.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double GetCentredOffset(double centre, double viewport, double extent) =>
        Math.Clamp(centre - (viewport * Half), 0, Math.Max(0, extent - viewport));
}
