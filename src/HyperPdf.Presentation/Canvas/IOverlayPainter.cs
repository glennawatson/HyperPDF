// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;
using HyperPdf.Core.Layout;

namespace HyperPdf.Presentation.Canvas;

/// <summary>What the page canvas needs from a toolkit to draw the marks over the pages: lines, outlines, dots, badges and labels.</summary>
public interface IOverlayPainter
{
    /// <summary>Tells whether the toolkit has an ink to draw with, so the canvas skips marks it could not draw.</summary>
    /// <param name="ink">The ink.</param>
    /// <returns><see langword="true"/> when marks in that ink can be drawn.</returns>
    bool CanDraw(OverlayInk ink);

    /// <summary>Draws a line.</summary>
    /// <param name="ink">The ink.</param>
    /// <param name="from">Where it starts.</param>
    /// <param name="to">Where it ends.</param>
    void DrawLine(OverlayInk ink, CanvasPoint from, CanvasPoint to);

    /// <summary>Draws a line of a freehand stroke being drawn, as thin ink or as a wide see-through highlighter.</summary>
    /// <param name="highlighter">Whether it is the highlighter.</param>
    /// <param name="color">The colour as 0xRRGGBB, for the highlighter.</param>
    /// <param name="width">The width in canvas units, for the highlighter.</param>
    /// <param name="from">Where it starts.</param>
    /// <param name="to">Where it ends.</param>
    void DrawStrokeLine(bool highlighter, uint color, double width, CanvasPoint from, CanvasPoint to);

    /// <summary>Draws the outline of a rectangle.</summary>
    /// <param name="ink">The ink.</param>
    /// <param name="rect">The rectangle.</param>
    void DrawRectangle(OverlayInk ink, in LayoutRect rect);

    /// <summary>Draws the outline of an ellipse that fits in a rectangle.</summary>
    /// <param name="ink">The ink.</param>
    /// <param name="box">The rectangle the ellipse fits in.</param>
    void DrawEllipse(OverlayInk ink, in LayoutRect box);

    /// <summary>Fills a rectangle.</summary>
    /// <param name="ink">The ink.</param>
    /// <param name="rect">The rectangle.</param>
    void FillRectangle(OverlayInk ink, in LayoutRect rect);

    /// <summary>Fills a dot.</summary>
    /// <param name="ink">The ink.</param>
    /// <param name="centre">The middle of the dot.</param>
    /// <param name="radius">The radius.</param>
    void FillDot(OverlayInk ink, CanvasPoint centre, double radius);

    /// <summary>Draws the small badge that marks an annotation with a comment.</summary>
    /// <param name="rect">The badge on the canvas.</param>
    /// <param name="color">The badge colour as 0xRRGGBB.</param>
    void DrawBadge(in LayoutRect rect, uint color);

    /// <summary>Draws the signature or initials mark being placed.</summary>
    /// <param name="mark">The mark.</param>
    /// <param name="area">Where it goes on the canvas.</param>
    /// <param name="inkWidth">The width of the mark's strokes in canvas units.</param>
    void DrawMark(SignatureMark mark, in LayoutRect area, double inkWidth);

    /// <summary>Draws a measurement in a framed label beside a point.</summary>
    /// <param name="ink">The ink that frames the label.</param>
    /// <param name="text">The measurement.</param>
    /// <param name="beside">The point the label sits beside.</param>
    void DrawLabel(OverlayInk ink, string text, CanvasPoint beside);
}
