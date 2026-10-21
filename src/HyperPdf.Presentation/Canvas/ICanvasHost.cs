// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;

namespace HyperPdf.Presentation.Canvas;

/// <summary>What the page canvas needs from the UI toolkit that shows it: a scrolling area to move, and ways to ask for a redraw.</summary>
public interface ICanvasHost
{
    /// <summary>Gets a value indicating whether the canvas sits in a scrolling area.</summary>
    bool HasScroller { get; }

    /// <summary>Gets a value indicating whether the canvas is in a window, so animation frames can run.</summary>
    bool IsInWindow { get; }

    /// <summary>Gets or sets how far the scrolling area is scrolled.</summary>
    CanvasPoint ScrollOffset { get; set; }

    /// <summary>Gets the size of the part of the canvas in view.</summary>
    CanvasSize ViewportSize { get; }

    /// <summary>Gets the size of everything the scrolling area can scroll over.</summary>
    CanvasSize ExtentSize { get; }

    /// <summary>Gets the size of the scrolling area itself, which the pages are laid out to fit.</summary>
    CanvasSize ScrollerSize { get; }

    /// <summary>Asks the toolkit to measure the canvas again, because the laid out size changed.</summary>
    void InvalidateMeasure();

    /// <summary>Asks the toolkit to draw the canvas again.</summary>
    void InvalidateVisual();

    /// <summary>Makes the toolkit finish measuring now, so the scroll extent matches the layout.</summary>
    void UpdateLayout();

    /// <summary>Tells the toolkit the canvas units per point changed.</summary>
    /// <param name="before">The scale before.</param>
    /// <param name="after">The scale now.</param>
    void PageScaleChanged(double before, double after);

    /// <summary>Tells the toolkit the wheel zoom ease changed, so it can follow its frames.</summary>
    /// <param name="before">The ease before.</param>
    /// <param name="after">The ease now.</param>
    void WheelZoomChanged(WheelZoomEase before, WheelZoomEase after);
}
