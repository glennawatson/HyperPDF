// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Rendering;
using HyperPdf.Presentation.Canvas;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Viewer;

/// <summary>
/// Draws what the page canvas asks for onto a frame pass. The canvas speaks in canvas units, so the painter moves everything by the
/// scroll offset and scales it to device pixels. Every shape is built from filled rectangles, which every render backend draws.
/// </summary>
[DebuggerDisplay("ViewerFramePainter: scale {_scale}")]
internal sealed class ViewerFramePainter : ITilePainter, IOverlayPainter, IMarksPainter
{
    /// <summary>How far the page shadow sits from the page, in canvas units.</summary>
    private const double ShadowOffset = 2;

    /// <summary>The width of overlay outlines, in canvas units.</summary>
    private const double OutlineWidth = 1.5;

    /// <summary>The width of the focus ring and the current hit outline, in canvas units.</summary>
    private const double StrongOutlineWidth = 2;

    /// <summary>The edge of the square handles on a picked object, in canvas units.</summary>
    private const double HandleSize = 8;

    /// <summary>Half, for centring.</summary>
    private const double Half = 0.5;

    /// <summary>The alpha of a highlighter stroke.</summary>
    private const uint HighlighterAlpha = 0x66000000;

    /// <summary>A fully opaque alpha.</summary>
    private const uint Opaque = 0xFF000000;

    /// <summary>The straight pieces an ellipse outline is drawn with.</summary>
    private const int EllipseSegments = 48;

    /// <summary>Where the two lines of a note badge start, as a fraction of its width.</summary>
    private const double BadgeLineLeft = 0.25;

    /// <summary>Where the two lines of a note badge end, as a fraction of its width.</summary>
    private const double BadgeLineRight = 0.75;

    /// <summary>The height of the first badge line, as a fraction of its height.</summary>
    private const double BadgeFirstLine = 0.38;

    /// <summary>The height of the second badge line, as a fraction of its height.</summary>
    private const double BadgeSecondLine = 0.64;

    /// <summary>The canvas state the selection is read from.</summary>
    private readonly PageCanvasState _state;

    /// <summary>The frame being drawn.</summary>
    private IFramePass? _pass;

    /// <summary>The colours of this frame.</summary>
    private ViewerColors _colors;

    /// <summary>The scroll offset of this frame, in canvas units.</summary>
    private CanvasPoint _offset;

    /// <summary>Device pixels per canvas unit.</summary>
    private double _scale = 1;

    /// <summary>Initializes a new instance of the <see cref="ViewerFramePainter"/> class.</summary>
    /// <param name="state">The canvas state.</param>
    internal ViewerFramePainter(PageCanvasState state) => _state = state;

    /// <summary>Gets the frame being drawn.</summary>
    private IFramePass Pass => _pass ?? throw new InvalidOperationException("No frame is being drawn.");

    /// <inheritdoc/>
    public void BeginPages()
    {
        // The frame pass has no state to push for pages.
    }

    /// <inheritdoc/>
    public void EndPages()
    {
        // The frame pass has no state to pop for pages.
    }

    /// <inheritdoc/>
    public void DrawPaper(in LayoutRect page, PageTone tone)
    {
        ArgumentNullException.ThrowIfNull(tone);
        Fill(new(page.X + ShadowOffset, page.Y + ShadowOffset, page.Width, page.Height), _colors.Shadow);
        Fill(page, Opaque | tone.Paper);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSurface(IRenderSurface surface, in LayoutRect destination, bool smooth) => Pass.DrawSurface(surface, ToDevice(destination), smooth);

    /// <inheritdoc/>
    public void FlushSurfaces()
    {
        // Tiles are drawn as they arrive, so there is nothing held back to flush.
    }

    /// <inheritdoc/>
    public void DrawPageOverlay(int page, in LayoutRect bounds)
    {
        if (_state.Tab is not { } tab)
        {
            return;
        }

        var transform = new PageTransform(bounds, _state.Sizes[page], tab.Rotation, _state.Layout.Options.Scale);
        foreach (var rect in _state.Selection.GetRects(tab.TryGetDocument(), page))
        {
            Fill(transform.MapToCanvas(rect), _colors.Selection);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CanDraw(OverlayInk ink) => true;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CanDraw(bool dim) => true;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawLine(OverlayInk ink, CanvasPoint from, CanvasPoint to) => Line(from, to, WidthOf(ink), ColorOf(ink));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawStrokeLine(bool highlighter, uint color, double width, CanvasPoint from, CanvasPoint to) =>
        Line(from, to, highlighter ? width : OutlineWidth, highlighter ? HighlighterAlpha | color : _colors.Ink);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawRectangle(OverlayInk ink, in LayoutRect rect) => Outline(rect, WidthOf(ink), ColorOf(ink));

    /// <inheritdoc/>
    public void DrawEllipse(OverlayInk ink, in LayoutRect box)
    {
        var centreX = box.X + (box.Width * Half);
        var centreY = box.Y + (box.Height * Half);
        var previous = new CanvasPoint(box.Right, centreY);
        for (var i = 1; i <= EllipseSegments; i++)
        {
            var angle = Math.Tau * i / EllipseSegments;
            var next = new CanvasPoint(centreX + (box.Width * Half * Math.Cos(angle)), centreY + (box.Height * Half * Math.Sin(angle)));
            DrawLine(ink, previous, next);
            previous = next;
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FillRectangle(OverlayInk ink, in LayoutRect rect) => Fill(rect, ColorOf(ink));

    /// <inheritdoc/>
    public void FillDot(OverlayInk ink, CanvasPoint centre, double radius)
    {
        // A dot is a few canvas units across, so rows one device pixel high make a clean disc.
        var color = ColorOf(ink);
        var step = 1 / _scale;
        for (var y = -radius; y < radius; y += step)
        {
            var half = Math.Sqrt(Math.Max(0, (radius * radius) - (y * y)));
            Fill(new(centre.X - half, centre.Y + y, half + half, step), color);
        }
    }

    /// <inheritdoc/>
    public void DrawBadge(in LayoutRect rect, uint color)
    {
        Fill(rect, Opaque | color);
        Outline(rect, 1, _colors.Ink);
        var left = rect.X + (rect.Width * BadgeLineLeft);
        var right = rect.X + (rect.Width * BadgeLineRight);
        var first = rect.Y + (rect.Height * BadgeFirstLine);
        var second = rect.Y + (rect.Height * BadgeSecondLine);
        Line(new(left, first), new(right, first), 1, _colors.Ink);
        Line(new(left, second), new(right, second), 1, _colors.Ink);
    }

    /// <inheritdoc/>
    public void DrawMark(SignatureMark mark, in LayoutRect area, double inkWidth)
    {
        // The mark's own strokes are drawn by the fill and sign panel of the native heads; until then its place is shown by an outline.
        _ = mark;
        Outline(area, inkWidth, _colors.Ink);
    }

    /// <inheritdoc/>
    public void DrawLabel(OverlayInk ink, string text, CanvasPoint beside)
    {
        // Text labels need the head's text drawing, which the measure tool of the native heads brings; the measured line itself is still drawn.
        _ = ink;
        _ = text;
        _ = beside;
    }

    /// <inheritdoc/>
    public void DrawHit(in LayoutRect rect, bool current)
    {
        Fill(rect, _colors.Hit);
        if (current)
        {
            Outline(rect, StrongOutlineWidth, _colors.Accent);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FillSpoken(in LayoutRect rect) => Fill(rect, _colors.Spoken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UnderlineWord(CanvasPoint from, CanvasPoint to) => Line(from, to, OutlineWidth, _colors.Accent);

    /// <inheritdoc/>
    public void DrawEditOutline(in LayoutRect rect, bool picked)
    {
        Outline(rect, picked ? StrongOutlineWidth : 1, _colors.Accent);
        if (!picked)
        {
            return;
        }

        // Square handles at the corners show the object can be moved.
        Handle(rect.X, rect.Y);
        Handle(rect.Right, rect.Y);
        Handle(rect.X, rect.Bottom);
        Handle(rect.Right, rect.Bottom);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FillDim(in LayoutRect rect) => Fill(rect, _colors.Dim);

    /// <summary>Gets the outline width of an overlay ink.</summary>
    /// <param name="ink">The ink.</param>
    /// <returns>The width in canvas units.</returns>
    internal static double WidthOf(OverlayInk ink) => ink switch
    {
        OverlayInk.FocusRing or OverlayInk.HoveredSuggestion => StrongOutlineWidth,
        OverlayInk.Suggestion => 1,
        _ => OutlineWidth,
    };

    /// <summary>Starts a frame.</summary>
    /// <param name="pass">The frame pass.</param>
    /// <param name="colors">The colours.</param>
    /// <param name="offset">The scroll offset in canvas units.</param>
    /// <param name="scale">Device pixels per canvas unit.</param>
    internal void Start(IFramePass pass, in ViewerColors colors, CanvasPoint offset, double scale)
    {
        _pass = pass;
        _colors = colors;
        _offset = offset;
        _scale = scale;
    }

    /// <summary>Ends the frame, so a late call cannot draw into a finished pass.</summary>
    internal void Finish() => _pass = null;

    /// <summary>Turns a canvas rectangle into device pixels inside the window.</summary>
    /// <param name="rect">The rectangle in canvas units.</param>
    /// <returns>The rectangle in device pixels.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private LayoutRect ToDevice(in LayoutRect rect) => new((rect.X - _offset.X) * _scale, (rect.Y - _offset.Y) * _scale, rect.Width * _scale, rect.Height * _scale);

    /// <summary>Fills a canvas rectangle.</summary>
    /// <param name="rect">The rectangle in canvas units.</param>
    /// <param name="argb">The colour.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Fill(in LayoutRect rect, uint argb) => Pass.FillRect(ToDevice(rect), argb);

    /// <summary>Draws the four edges of a rectangle.</summary>
    /// <param name="rect">The rectangle in canvas units.</param>
    /// <param name="width">The edge width in canvas units.</param>
    /// <param name="argb">The colour.</param>
    private void Outline(in LayoutRect rect, double width, uint argb)
    {
        var half = width * Half;
        Fill(new(rect.X - half, rect.Y - half, rect.Width + width, width), argb);
        Fill(new(rect.X - half, rect.Bottom - half, rect.Width + width, width), argb);
        Fill(new(rect.X - half, rect.Y + half, width, rect.Height - width), argb);
        Fill(new(rect.Right - half, rect.Y + half, width, rect.Height - width), argb);
    }

    /// <summary>Draws a line. A level or upright line is one rectangle; a slanted line is a run of small squares, one for each device pixel of its longer side.</summary>
    /// <param name="from">The start in canvas units.</param>
    /// <param name="to">The end in canvas units.</param>
    /// <param name="width">The width in canvas units.</param>
    /// <param name="argb">The colour.</param>
    private void Line(CanvasPoint from, CanvasPoint to, double width, uint argb)
    {
        var half = width * Half;
        var acrossX = Math.Abs(to.X - from.X);
        var acrossY = Math.Abs(to.Y - from.Y);
        if (acrossX * _scale < 1 || acrossY * _scale < 1)
        {
            Fill(new(Math.Min(from.X, to.X) - half, Math.Min(from.Y, to.Y) - half, acrossX + width, acrossY + width), argb);
            return;
        }

        var steps = (int)Math.Ceiling(Math.Max(acrossX, acrossY) * _scale);
        var stepX = (to.X - from.X) / steps;
        var stepY = (to.Y - from.Y) / steps;
        for (var i = 0; i <= steps; i++)
        {
            Fill(new(from.X + (stepX * i) - half, from.Y + (stepY * i) - half, width, width), argb);
        }
    }

    /// <summary>Fills a square handle centred on a corner.</summary>
    /// <param name="x">The corner x in canvas units.</param>
    /// <param name="y">The corner y in canvas units.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Handle(double x, double y) => Fill(new(x - (HandleSize * Half), y - (HandleSize * Half), HandleSize, HandleSize), _colors.Accent);

    /// <summary>Gets the colour of an overlay ink.</summary>
    /// <param name="ink">The ink.</param>
    /// <returns>The colour.</returns>
    private uint ColorOf(OverlayInk ink) => ink is OverlayInk.Stroke ? _colors.Ink : _colors.Accent;
}
