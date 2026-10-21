// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using HyperPdf.App.Rendering;
using HyperPdf.App.Views;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Rendering;
using HyperPdf.Presentation.Canvas;

namespace HyperPdf.App.Controls;

/// <summary>Puts what the canvas plans on an Avalonia drawing context: paper, cached tiles, and every mark over the pages.</summary>
internal sealed class AvaloniaCanvasPainter : ITilePainter, IOverlayPainter, IDisposable
{
    /// <summary>The page shadow offset.</summary>
    private const double ShadowOffset = 2;

    /// <summary>The width of the live stroke and the selected annotation outline, in device independent pixels.</summary>
    private const double OverlayPenWidth = 1.5;

    /// <summary>The corner radius of on-page comment indicator badges.</summary>
    private const double BadgeRadius = 3;

    /// <summary>The left inset of the ruled lines inside a comment indicator badge, as a fraction of its width.</summary>
    private const double BadgeLineLeft = 0.25;

    /// <summary>The right inset of the ruled lines inside a comment indicator badge, as a fraction of its width.</summary>
    private const double BadgeLineRight = 0.75;

    /// <summary>The vertical position of the first ruled line inside a comment indicator badge, as a fraction of its height.</summary>
    private const double BadgeFirstLineY = 0.38;

    /// <summary>The vertical position of the second ruled line inside a comment indicator badge, as a fraction of its height.</summary>
    private const double BadgeSecondLineY = 0.64;

    /// <summary>The alpha of the highlighter pen's preview, about 40% like the highlight it saves.</summary>
    private const uint HighlighterAlpha = 0x66000000U;

    /// <summary>The smallest zoom change that makes the ink pen again.</summary>
    private const double PenTolerance = 0.001;

    /// <summary>The width of the field focus ring.</summary>
    private const double FocusRingWidth = 2;

    /// <summary>The width of a suggestion's outline.</summary>
    private const double SuggestionWidth = 1;

    /// <summary>The width of the outline of the suggestion under the pointer.</summary>
    private const double HoveredSuggestionWidth = 2;

    /// <summary>The size of the measurement label's text.</summary>
    private const double MeasureLabelSize = 13;

    /// <summary>How far the label sits from the last point.</summary>
    private const double MeasureLabelOffset = 10;

    /// <summary>The padding around the label's text.</summary>
    private const double MeasureLabelPadding = 4;

    /// <summary>The colour of the live stroke while drawing: the annotation ink colour.</summary>
    private static readonly ImmutablePen StrokePen = new(new ImmutableSolidColorBrush(Color.FromUInt32(0xFF000000U | AnnotationColors.Ink)), OverlayPenWidth);

    /// <summary>The border and line pen for on-page comment indicator badges.</summary>
    private static readonly ImmutablePen BadgeBorderPen = new(new ImmutableSolidColorBrush(Color.FromArgb(0xB0, 0x20, 0x20, 0x20)), 1);

    /// <summary>The ink brush for typed marks.</summary>
    private static readonly IImmutableSolidColorBrush MarkInk = new ImmutableSolidColorBrush(Color.FromUInt32(0xFF000000U | AnnotationColors.Ink));

    /// <summary>The dashes of a suggestion's outline.</summary>
    private static readonly ImmutableDashStyle SuggestionDashes = new([3, 3], 0);

    /// <summary>The page shadow brush.</summary>
    private static readonly IBrush ShadowBrush = new SolidColorBrush(Color.FromArgb(0x40, 0, 0, 0));

    /// <summary>
    /// How page images are sampled, with aliased edges so tiles meet without seams. Only stand-ins are ever scaled: the
    /// preview and the last scale's tiles while a zoom settles. Bilinear with mipmaps draws those about ten times
    /// faster than cubic (BitmapDrawBenchmarks), which keeps the zoom ease smooth; sharp tiles are drawn 1:1.
    /// </summary>
    private static readonly RenderOptions PageRenderOptions = new() { BitmapInterpolationMode = BitmapInterpolationMode.MediumQuality, EdgeMode = EdgeMode.Aliased };

    /// <summary>The canvas the pages belong to.</summary>
    private readonly PageCanvas _canvas;

    /// <summary>Queues each page's GPU tiles so they reach the scene as one operation.</summary>
    private readonly TileDrawBatch _tiles = new();

    /// <summary>The context the frame is drawn into.</summary>
    private DrawingContext? _context;

    /// <summary>The render options in force while the pages are drawn.</summary>
    private DrawingContext.PushedState _options;

    /// <summary>Draws the signature mark being placed.</summary>
    private SignatureMarkPainter? _markPainter;

    /// <summary>The pen the signature mark is drawn with, made again only when the zoom changes.</summary>
    private Pen? _markPen;

    /// <summary>The pen of the highlighter preview, kept until the colour or zoom changes.</summary>
    private Pen? _highlighterPen;

    /// <summary>The dashed outline of the places to write.</summary>
    private Pen? _suggestionPen;

    /// <summary>The outline of the place to write under the pointer.</summary>
    private Pen? _hoveredSuggestionPen;

    /// <summary>The ring around the field with the focus.</summary>
    private Pen? _focusPen;

    /// <summary>Initializes a new instance of the <see cref="AvaloniaCanvasPainter"/> class.</summary>
    /// <param name="canvas">The canvas the pages belong to.</param>
    internal AvaloniaCanvasPainter(PageCanvas canvas) => _canvas = canvas;

    /// <summary>Gets the context the frame is drawn into.</summary>
    private DrawingContext Context => _context ?? throw new InvalidOperationException("No frame is being drawn.");

    /// <inheritdoc/>
    public void BeginPages() => _options = Context.PushRenderOptions(PageRenderOptions);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EndPages() => _options.Dispose();

    /// <inheritdoc/>
    public void DrawPaper(in LayoutRect page, PageTone tone)
    {
        var rect = new Rect(page.X, page.Y, page.Width, page.Height);
        Context.FillRectangle(ShadowBrush, rect.Translate(new(ShadowOffset, ShadowOffset)));
        Context.FillRectangle(PaperBrush.Get(tone), rect);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSurface(IRenderSurface surface, in LayoutRect destination, bool smooth) =>
        _tiles.Draw(Context, surface, new(destination.X, destination.Y, destination.Width, destination.Height), smooth);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FlushSurfaces() => _tiles.Flush(Context);

    /// <inheritdoc/>
    public void DrawPageOverlay(int page, in LayoutRect bounds)
    {
        if (_canvas.Tab is { } tab)
        {
            _canvas.DrawSelection(Context, tab, page, bounds);
        }
    }

    /// <inheritdoc/>
    public bool CanDraw(OverlayInk ink) => ink switch
    {
        OverlayInk.Hit => _canvas.CurrentHitPen is not null,
        OverlayInk.Caret => (_canvas.CurrentHitOutline ?? _canvas.SelectionBrush) is not null,
        _ => true,
    };

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawLine(OverlayInk ink, CanvasPoint from, CanvasPoint to) => Context.DrawLine(GetPen(ink), new(from.X, from.Y), new(to.X, to.Y));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawStrokeLine(bool highlighter, uint color, double width, CanvasPoint from, CanvasPoint to) =>
        Context.DrawLine(highlighter ? GetHighlighterPen(color, width) : StrokePen, new(from.X, from.Y), new(to.X, to.Y));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawRectangle(OverlayInk ink, in LayoutRect rect) => Context.DrawRectangle(null, GetPen(ink), new(rect.X, rect.Y, rect.Width, rect.Height));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawEllipse(OverlayInk ink, in LayoutRect box)
    {
        Rect area = new(box.X, box.Y, box.Width, box.Height);
        Context.DrawEllipse(null, GetPen(ink), area);
    }

    /// <inheritdoc/>
    public void FillRectangle(OverlayInk ink, in LayoutRect rect)
    {
        var brush = ink == OverlayInk.Caret ? _canvas.CurrentHitOutline ?? _canvas.SelectionBrush : GetPen(ink).Brush;
        Context.FillRectangle(brush!, new(rect.X, rect.Y, rect.Width, rect.Height));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FillDot(OverlayInk ink, CanvasPoint centre, double radius) => Context.DrawEllipse(GetPen(ink).Brush, null, new(centre.X, centre.Y), radius, radius);

    /// <inheritdoc/>
    public void DrawBadge(in LayoutRect rect, uint color)
    {
        var area = new Rect(rect.X, rect.Y, rect.Width, rect.Height);
        Context.DrawRectangle(ColourBrushes.Opaque(color), BadgeBorderPen, area, BadgeRadius, BadgeRadius);
        var x1 = area.Left + (area.Width * BadgeLineLeft);
        var x2 = area.Left + (area.Width * BadgeLineRight);
        var y1 = area.Top + (area.Height * BadgeFirstLineY);
        var y2 = area.Top + (area.Height * BadgeSecondLineY);
        Context.DrawLine(BadgeBorderPen, new(x1, y1), new(x2, y1));
        Context.DrawLine(BadgeBorderPen, new(x1, y2), new(x2, y2));
    }

    /// <inheritdoc/>
    public void DrawMark(SignatureMark mark, in LayoutRect area, double inkWidth)
    {
        if (_markPen is null || Math.Abs(_markPen.Thickness - inkWidth) > PenTolerance)
        {
            // Made again only when the zoom changes, so redraws while placing do not allocate.
            _markPen = new(MarkInk, inkWidth, lineCap: PenLineCap.Round);
        }

        (_markPainter ??= new()).Draw(Context, mark, new(area.X, area.Y, area.Width, area.Height), MarkInk, _markPen);
    }

    /// <inheritdoc/>
    public void DrawLabel(OverlayInk ink, string text, CanvasPoint beside)
    {
        var outline = GetPen(ink);
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, MeasureLabelSize, _canvas.LabelForeground ?? outline.Brush);
        var origin = new Point(beside.X + MeasureLabelOffset, beside.Y + MeasureLabelOffset);
        var box = new Rect(origin, new Size(formatted.Width, formatted.Height)).Inflate(MeasureLabelPadding);
        Context.DrawRectangle(_canvas.LabelBackground, outline, box, MeasureLabelPadding, MeasureLabelPadding);
        Context.DrawText(formatted, origin);
    }

    /// <summary>Lets go of the picture of the signature mark.</summary>
    public void Dispose()
    {
        _markPainter?.Dispose();
        _markPainter = null;
    }

    /// <summary>Starts a frame in a drawing context.</summary>
    /// <param name="context">The drawing context.</param>
    internal void Start(DrawingContext context) => _context = context;

    /// <summary>Gets the pen of an ink.</summary>
    /// <param name="ink">The ink.</param>
    /// <returns>The pen.</returns>
    private IPen GetPen(OverlayInk ink) => ink switch
    {
        OverlayInk.Hit or OverlayInk.HitOrStroke => _canvas.CurrentHitPen ?? StrokePen,
        OverlayInk.Suggestion => GetSuggestionPen(false),
        OverlayInk.HoveredSuggestion => GetSuggestionPen(true),
        OverlayInk.FocusRing => GetFocusPen(),
        _ => StrokePen,
    };

    /// <summary>Gets the dashed outline of a place to write, made again when its colour changes so drawing does not allocate.</summary>
    /// <param name="hovered">Whether it is the place under the pointer.</param>
    /// <returns>The pen.</returns>
    private Pen GetSuggestionPen(bool hovered)
    {
        var brush = (_canvas.CurrentHitPen ?? StrokePen).Brush;
        if (_suggestionPen is null || !ReferenceEquals(_suggestionPen.Brush, brush))
        {
            _suggestionPen = new(brush, SuggestionWidth, SuggestionDashes);
            _hoveredSuggestionPen = new(brush, HoveredSuggestionWidth);
        }

        return hovered ? _hoveredSuggestionPen! : _suggestionPen;
    }

    /// <summary>Gets the ring around the field with the focus.</summary>
    /// <returns>The pen.</returns>
    private Pen GetFocusPen()
    {
        var brush = (_canvas.CurrentHitPen ?? StrokePen).Brush;
        return _focusPen is not null && ReferenceEquals(_focusPen.Brush, brush) ? _focusPen : _focusPen = new(brush, FocusRingWidth);
    }

    /// <summary>Gets the wide see-through pen of the highlighter preview, kept until the colour or zoom changes.</summary>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <param name="width">The width in canvas units.</param>
    /// <returns>The pen.</returns>
    private Pen GetHighlighterPen(uint color, double width)
    {
        var tint = Color.FromUInt32(HighlighterAlpha | color);
        return _highlighterPen is { Brush: ISolidColorBrush brush } cached && brush.Color == tint && Math.Abs(cached.Thickness - width) <= PenTolerance
            ? cached
            : _highlighterPen = new(new ImmutableSolidColorBrush(tint), width, lineCap: PenLineCap.Round);
    }
}
