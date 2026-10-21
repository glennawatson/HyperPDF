// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;

namespace HyperPdf.Presentation.Canvas;

/// <summary>The fixed distances and steps of the page canvas, in canvas units.</summary>
public static class CanvasMetrics
{
    /// <summary>The gap between pages.</summary>
    public static readonly double PageSpacing = 12;

    /// <summary>The margin around the pages.</summary>
    public static readonly double ContentMargin = 16;

    /// <summary>How far one wheel notch or arrow key scrolls within a slot.</summary>
    public static readonly double LineStep = 48;

    /// <summary>How close to a slot edge counts as at the edge.</summary>
    public static readonly double EdgeTolerance = 0.5;

    /// <summary>The pointer travel that turns a click into a drag.</summary>
    public static readonly double DragThreshold = 3;

    /// <summary>The distance kept to the left of a navigation target.</summary>
    public static readonly double TargetPadding = 24;

    /// <summary>Where in the viewport a navigation target is placed, as a fraction of its height from the top.</summary>
    public static readonly double TargetViewportFraction = 0.3;

    /// <summary>Gets whether a press and release stayed close enough to count as a click rather than a drag.</summary>
    /// <param name="pressed">Where the pointer went down.</param>
    /// <param name="released">Where the pointer came up.</param>
    /// <returns><see langword="true"/> when it was a click.</returns>
    public static bool IsClick(CanvasPoint pressed, CanvasPoint released) =>
        Math.Abs(released.X - pressed.X) < DragThreshold && Math.Abs(released.Y - pressed.Y) < DragThreshold;
}
