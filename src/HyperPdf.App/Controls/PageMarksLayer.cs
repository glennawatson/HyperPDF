// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
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

    /// <summary>The width of the outline marking the current search hit, so it differs by shape as well as colour.</summary>
    private const double CurrentHitOutlineWidth = 2;

    /// <summary>The scroll viewer the pages sit in, while the layer is shown.</summary>
    private ScrollViewer? _scroller;

    /// <summary>The pen outlining the current search hit, rebuilt when <see cref="CurrentHitOutline"/> changes.</summary>
    private IPen? _currentHitPen;

    /// <summary>Puts the planned marks on a drawing context, made when the first frame is drawn.</summary>
    private AvaloniaMarksPainter? _painter;

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

    /// <summary>Gets the pen outlining the current search hit, or <see langword="null"/> when the outline has no brush.</summary>
    internal IPen? CurrentHitPen => _currentHitPen;

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

        var painter = _painter ??= new(this);
        painter.Start(context);
        pages.State.DrawMarks(painter, tab, new(scroller.Offset.X, scroller.Offset.Y, scroller.Viewport.Width, scroller.Viewport.Height));
    }
}
