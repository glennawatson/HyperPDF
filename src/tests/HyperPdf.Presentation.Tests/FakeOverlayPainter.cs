// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;
using HyperPdf.Core.Layout;

namespace HyperPdf.Presentation.Tests;

/// <summary>An overlay painter with no toolkit: it counts what it is told to draw.</summary>
internal sealed class FakeOverlayPainter : IOverlayPainter
{
    /// <summary>Gets or sets a value indicating whether the toolkit has a colour for the current search hit.</summary>
    public bool HasHit { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the toolkit has an ink for the caret.</summary>
    public bool HasCaretInk { get; set; } = true;

    /// <summary>Gets how many lines were drawn.</summary>
    public int Lines { get; private set; }

    /// <summary>Gets how many freehand stroke lines were drawn.</summary>
    public int StrokeLines { get; private set; }

    /// <summary>Gets a value indicating whether a freehand stroke line was the highlighter.</summary>
    public bool SawHighlighter { get; private set; }

    /// <summary>Gets how many rectangle outlines were drawn.</summary>
    public int Rectangles { get; private set; }

    /// <summary>Gets how many ellipse outlines were drawn.</summary>
    public int Ellipses { get; private set; }

    /// <summary>Gets how many rectangles were filled.</summary>
    public int Fills { get; private set; }

    /// <summary>Gets how many dots were filled.</summary>
    public int Dots { get; private set; }

    /// <summary>Gets how many badges were drawn.</summary>
    public int Badges { get; private set; }

    /// <summary>Gets how many marks were drawn.</summary>
    public int Marks { get; private set; }

    /// <summary>Gets how many labels were drawn.</summary>
    public int Labels { get; private set; }

    /// <inheritdoc/>
    public bool CanDraw(OverlayInk ink) => ink switch
    {
        OverlayInk.Hit => HasHit,
        OverlayInk.Caret => HasCaretInk,
        _ => true,
    };

    /// <inheritdoc/>
    public void DrawLine(OverlayInk ink, CanvasPoint from, CanvasPoint to) => Lines++;

    /// <inheritdoc/>
    public void DrawStrokeLine(bool highlighter, uint color, double width, CanvasPoint from, CanvasPoint to)
    {
        StrokeLines++;
        SawHighlighter |= highlighter;
    }

    /// <inheritdoc/>
    public void DrawRectangle(OverlayInk ink, in LayoutRect rect) => Rectangles++;

    /// <inheritdoc/>
    public void DrawEllipse(OverlayInk ink, in LayoutRect box) => Ellipses++;

    /// <inheritdoc/>
    public void FillRectangle(OverlayInk ink, in LayoutRect rect) => Fills++;

    /// <inheritdoc/>
    public void FillDot(OverlayInk ink, CanvasPoint centre, double radius) => Dots++;

    /// <inheritdoc/>
    public void DrawBadge(in LayoutRect rect, uint color) => Badges++;

    /// <inheritdoc/>
    public void DrawMark(SignatureMark mark, in LayoutRect area, double inkWidth) => Marks++;

    /// <inheritdoc/>
    public void DrawLabel(OverlayInk ink, string text, CanvasPoint beside) => Labels++;
}
