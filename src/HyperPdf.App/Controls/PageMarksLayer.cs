// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
using HyperPdf.App.Services;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Controls;

/// <summary>
/// The marks laid over the pages: search hits, with an outline on the current one, the sentence and word being read
/// aloud, and the focus band that dims the page away from what is being read. It sits over the <see cref="PageCanvas"/>
/// and draws each mark where the canvas has laid its page out.
/// </summary>
[DebuggerDisplay("PageMarksLayer: {Tab}")]
public sealed class PageMarksLayer : Control, IActivatableView
{
    /// <summary>Defines the <see cref="Tab"/> property.</summary>
    public static readonly StyledProperty<DocumentTabViewModel?> TabProperty = AvaloniaProperty.Register<PageMarksLayer, DocumentTabViewModel?>(nameof(Tab));

    /// <summary>Defines the <see cref="HitBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> HitBrushProperty = AvaloniaProperty.Register<PageMarksLayer, IBrush?>(nameof(HitBrush));

    /// <summary>Defines the <see cref="CurrentHitOutline"/> property.</summary>
    public static readonly StyledProperty<IBrush?> CurrentHitOutlineProperty = AvaloniaProperty.Register<PageMarksLayer, IBrush?>(nameof(CurrentHitOutline));

    /// <summary>Defines the <see cref="SpokenBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> SpokenBrushProperty = AvaloniaProperty.Register<PageMarksLayer, IBrush?>(nameof(SpokenBrush));

    /// <summary>Defines the <see cref="DimBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> DimBrushProperty = AvaloniaProperty.Register<PageMarksLayer, IBrush?>(nameof(DimBrush));

    /// <summary>The thickness of the line under the word being read.</summary>
    private const double WordUnderline = 1.5;

    /// <summary>How far the focus band reaches above and below the sentence being read.</summary>
    private const double FocusBandMargin = 6;

    /// <summary>The width of the outline marking the current search hit, so it differs by shape as well as colour.</summary>
    private const double CurrentHitOutlineWidth = 2;

    /// <summary>The scroll viewer the pages sit in, while the layer is shown.</summary>
    private ScrollViewer? _scroller;

    /// <summary>The pen outlining the current search hit, rebuilt when <see cref="CurrentHitOutline"/> changes.</summary>
    private IPen? _currentHitPen;

    /// <summary>Initializes static members of the <see cref="PageMarksLayer"/> class.</summary>
    static PageMarksLayer()
    {
        AffectsRender<PageMarksLayer>(TabProperty, HitBrushProperty, CurrentHitOutlineProperty, SpokenBrushProperty, DimBrushProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<PageMarksLayer>(false);
    }

    /// <summary>Initializes a new instance of the <see cref="PageMarksLayer"/> class.</summary>
    public PageMarksLayer()
    {
        var tabs = this.WhenChanged(static x => x.Tab);
        _ = this.WhenActivated(disposables =>
        {
            _scroller = this.FindAncestorOfType<ScrollViewer>();
            disposables.Add(this.WhenChanged(static x => x.CurrentHitOutline)
                .SubscribeSafe(outline => _currentHitPen = outline is not null ? new ImmutablePen(outline.ToImmutable(), CurrentHitOutlineWidth) : null, ErrorTrace.Write));

            // The marks follow the hits and the reading, and move with the pages when they scroll or are laid out again.
            disposables.Add(tabs.Follow(static tab => tab.MarkRedraws).SubscribeSafe(_ => InvalidateVisual(), ErrorTrace.Write));
            if (Pages is { } pages)
            {
                disposables.Add(pages.WhenChanged(static canvas => canvas.PageScale).SubscribeSafe(_ => InvalidateVisual(), ErrorTrace.Write));
            }

            if (_scroller is { } scroller)
            {
                // With nothing marked there is nothing to move, so scrolling leaves the layer alone; MarkRedraws re-arms it.
                disposables.Add(scroller.WhenChanged(static viewer => viewer.Offset)
                    .WhereWith(this, static (layer, _) => layer.HasMarks)
                    .SubscribeSafe(_ => InvalidateVisual(), ErrorTrace.Write));
            }
        });
    }

    /// <summary>Gets or sets the tab whose marks are drawn.</summary>
    public DocumentTabViewModel? Tab
    {
        get => GetValue(TabProperty);
        set => SetValue(TabProperty, value);
    }

    /// <summary>Gets or sets the fill of search hits.</summary>
    public IBrush? HitBrush
    {
        get => GetValue(HitBrushProperty);
        set => SetValue(HitBrushProperty, value);
    }

    /// <summary>Gets or sets the outline of the current search hit, also used to underline the word being read.</summary>
    public IBrush? CurrentHitOutline
    {
        get => GetValue(CurrentHitOutlineProperty);
        set => SetValue(CurrentHitOutlineProperty, value);
    }

    /// <summary>Gets or sets the soft fill marking the sentence being read aloud.</summary>
    public IBrush? SpokenBrush
    {
        get => GetValue(SpokenBrushProperty);
        set => SetValue(SpokenBrushProperty, value);
    }

    /// <summary>Gets or sets the translucent paper colour laid over text away from what is being read, for the focus band.</summary>
    public IBrush? DimBrush
    {
        get => GetValue(DimBrushProperty);
        set => SetValue(DimBrushProperty, value);
    }

    /// <summary>Gets or sets the pages the marks are drawn over, which say where each page is laid out.</summary>
    internal PageCanvas? Pages { get; set; }

    /// <summary>Gets a value indicating whether the tab has any search hit, spoken sentence or focus band to draw.</summary>
    internal bool HasMarks => Tab is { } tab && (tab.Search.HasHits || tab.ReadAloud.SpokenPage >= 0);

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!HasMarks || Tab is not { } tab || Pages is not { } pages || _scroller is not { } scroller)
        {
            return;
        }

        var viewport = new Rect(scroller.Offset.X, scroller.Offset.Y, scroller.Viewport.Width, scroller.Viewport.Height);
        pages.GetPagesBetween(scroller.Offset.X, scroller.Offset.X + scroller.Viewport.Width, scroller.Offset.Y, scroller.Offset.Y + scroller.Viewport.Height, out var first, out var last);
        for (var page = first; page <= last && page >= 0; page++)
        {
            if (!pages.TryGetPageTransform(page, out var transform, out var bounds))
            {
                continue;
            }

            DrawHits(context, tab, page, transform, viewport);
            DrawSpoken(context, tab, page, transform);
            DrawFocusBand(context, tab, page, bounds, transform);
        }
    }

    /// <summary>Draws the search hits on a page, outlining the current one.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The page.</param>
    /// <param name="transform">How the page maps onto the canvas.</param>
    /// <param name="viewport">The visible part of the canvas; hits outside it are not recorded.</param>
    private void DrawHits(DrawingContext context, DocumentTabViewModel tab, int page, in PageTransform transform, in Rect viewport)
    {
        if (tab.Search.GetHits(page) is not { } hits)
        {
            return;
        }

        var current = tab.Search.CurrentHit;
        var fill = HitBrush;
        foreach (var hit in hits)
        {
            var pen = ReferenceEquals(hit, current) ? _currentHitPen : null;
            foreach (var rect in hit.Bounds)
            {
                var canvasRect = transform.ToCanvas(rect);
                if (canvasRect.Intersects(viewport))
                {
                    context.DrawRectangle(fill, pen, canvasRect);
                }
            }
        }
    }

    /// <summary>Softly marks the sentence being read aloud, and underlines the word.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The page.</param>
    /// <param name="transform">How the page maps onto the canvas.</param>
    private void DrawSpoken(DrawingContext context, DocumentTabViewModel tab, int page, in PageTransform transform)
    {
        var reader = tab.ReadAloud;
        if (SpokenBrush is not { } brush || reader.SpokenPage != page)
        {
            return;
        }

        foreach (var rect in reader.SpokenBounds)
        {
            context.FillRectangle(brush, transform.ToCanvas(rect));
        }

        // The word is underlined rather than filled, so the marks stay quiet.
        var pen = _currentHitPen;
        foreach (var rect in reader.SpokenWordBounds)
        {
            var word = transform.ToCanvas(rect);
            context.DrawLine(pen ?? new Pen(brush, WordUnderline), word.BottomLeft, word.BottomRight);
        }
    }

    /// <summary>
    /// Dims the page away from the sentence being read, when the focus band is on: a band around the sentence stays
    /// clear and the rest of its page, and every other page, is laid over with the translucent paper colour.
    /// </summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The page.</param>
    /// <param name="pageRect">The page's bounds on the canvas.</param>
    /// <param name="transform">How the page maps onto the canvas.</param>
    private void DrawFocusBand(DrawingContext context, DocumentTabViewModel tab, int page, in Rect pageRect, in PageTransform transform)
    {
        var reader = tab.ReadAloud;
        if (DimBrush is not { } dim || !tab.FocusMode.FocusBand || reader.SpokenPage < 0 || reader.SpokenBounds.Count == 0)
        {
            return;
        }

        if (reader.SpokenPage != page)
        {
            context.FillRectangle(dim, pageRect);
            return;
        }

        var band = transform.ToCanvas(reader.SpokenBounds[0]);
        foreach (var rect in reader.SpokenBounds)
        {
            band = band.Union(transform.ToCanvas(rect));
        }

        var top = Math.Max(pageRect.Top, band.Top - FocusBandMargin);
        var bottom = Math.Min(pageRect.Bottom, band.Bottom + FocusBandMargin);
        context.FillRectangle(dim, new(pageRect.Left, pageRect.Top, pageRect.Width, Math.Max(0, top - pageRect.Top)));
        context.FillRectangle(dim, new(pageRect.Left, bottom, pageRect.Width, Math.Max(0, pageRect.Bottom - bottom)));
    }
}
