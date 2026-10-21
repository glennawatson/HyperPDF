// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Navigation;
using HyperPdf.Core.Presentation;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.ObservableEvents;

namespace HyperPdf.App.Controls;

/// <summary>
/// Draws a document as a continuous strip of pages inside a <see cref="ScrollViewer"/>. Only the tiles intersecting the
/// viewport are drawn or requested. A changed view advances the render generation so work for regions that scrolled
/// away is dropped by the scheduler before it reaches the renderer.
/// </summary>
[DebuggerDisplay("PageCanvas: {Tab}")]
public sealed class PageCanvas : Control, IActivatableView, ICanvasHost
{
    /// <summary>Defines the <see cref="Tab"/> property.</summary>
    public static readonly StyledProperty<DocumentTabViewModel?> TabProperty = AvaloniaProperty.Register<PageCanvas, DocumentTabViewModel?>(nameof(Tab));

    /// <summary>Defines the <see cref="PageScale"/> property.</summary>
    public static readonly DirectProperty<PageCanvas, double> PageScaleProperty = AvaloniaProperty.RegisterDirect<PageCanvas, double>(nameof(PageScale), static canvas => canvas.PageScale);

    /// <summary>Defines the <see cref="CurrentHitOutline"/> property.</summary>
    public static readonly StyledProperty<IBrush?> CurrentHitOutlineProperty = AvaloniaProperty.Register<PageCanvas, IBrush?>(nameof(CurrentHitOutline));

    /// <summary>Defines the <see cref="SelectionBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> SelectionBrushProperty = AvaloniaProperty.Register<PageCanvas, IBrush?>(nameof(SelectionBrush));

    /// <summary>Defines the <see cref="LabelBackground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> LabelBackgroundProperty = AvaloniaProperty.Register<PageCanvas, IBrush?>(nameof(LabelBackground));

    /// <summary>Defines the <see cref="LabelForeground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> LabelForegroundProperty = AvaloniaProperty.Register<PageCanvas, IBrush?>(nameof(LabelForeground));

    /// <summary>The gap between pages.</summary>
    internal const double PageSpacing = 12;

    /// <summary>The margin around the pages.</summary>
    internal const double ContentMargin = 16;

    /// <summary>Half, for finding the middle of the viewport.</summary>
    internal const double Half = 0.5;

    /// <summary>The pointer travel that turns a click into a drag.</summary>
    internal const double DragThreshold = 3;

    /// <summary>The hand cursor for links and interactive triggers.</summary>
    internal static readonly Cursor HandCursor = new(StandardCursorType.Hand);

    /// <summary>The text cursor.</summary>
    internal static readonly Cursor TextCursor = new(StandardCursorType.Ibeam);

    /// <summary>The pointer while measuring or drawing.</summary>
    internal static readonly Cursor CrossCursor = new(StandardCursorType.Cross);

    /// <summary>The pointer while the pages are being moved.</summary>
    internal static readonly Cursor MoveCursor = new(StandardCursorType.SizeAll);

    /// <summary>The distance kept to the left of a navigation target.</summary>
    private const double TargetPadding = 24;

    /// <summary>Where in the viewport a navigation target is placed, as a fraction of its height from the top.</summary>
    private const double TargetViewportFraction = 0.3;

    /// <summary>The width of the outline marking the current search hit, so it differs by shape as well as colour.</summary>
    private const double CurrentHitOutlineWidth = 2;

    /// <summary>Defines the <see cref="WheelZoom"/> property.</summary>
    private static readonly DirectProperty<PageCanvas, WheelZoomEase> WheelZoomProperty =
        AvaloniaProperty.RegisterDirect<PageCanvas, WheelZoomEase>(nameof(WheelZoom), static canvas => canvas.WheelZoom);

    /// <summary>Defines the <see cref="Transition"/> property.</summary>
    private static readonly DirectProperty<PageCanvas, TransitionRun> TransitionProperty =
        AvaloniaProperty.RegisterDirect<PageCanvas, TransitionRun>(nameof(Transition), static canvas => canvas.Transition);

    /// <summary>Puts the planned pages on an Avalonia drawing context, made when the first frame is drawn.</summary>
    private AvaloniaCanvasPainter? _painter;

    /// <summary>The hosting scroll viewer.</summary>
    private ScrollViewer? _scroller;

    /// <summary>The tab shown, which lets go of its render requests and auto-scroll when another tab replaces it.</summary>
    private DocumentTabViewModel? _shownTab;

    /// <summary>The pen outlining the current search hit, rebuilt when <see cref="CurrentHitOutline"/> changes.</summary>
    private IPen? _currentHitPen;

    /// <summary>1 while an auto-scroll glide's frames are subscribed.</summary>
    private int _autoScrollRunning;

    /// <summary>Initializes static members of the <see cref="PageCanvas"/> class.</summary>
    static PageCanvas()
    {
        AffectsRender<PageCanvas>(TabProperty, CurrentHitOutlineProperty, SelectionBrushProperty);
        AffectsRender<PageCanvas>(LabelBackgroundProperty, LabelForegroundProperty);
        FocusableProperty.OverrideDefaultValue<PageCanvas>(true);
    }

    /// <summary>Initializes a new instance of the <see cref="PageCanvas"/> class.</summary>
    public PageCanvas()
    {
        CopyCommand = ReactiveCommand.Create<string>(this.CopyToClipboard);
        var tabs = this.WhenChanged(static x => x.Tab);
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(Scope.Create(this, static canvas => canvas.PutAway()));
            if ((_scroller = this.FindAncestorOfType<ScrollViewer>()) is { } scroller)
            {
                WireScroller(scroller, tabs, disposables.Add);
            }

            disposables.Add(tabs.SubscribeSafe(ShowTab, ErrorTrace.Write));
            disposables.Add(tabs.Follow(static tab => tab.CanvasRedraws).SubscribeSafe(_ => InvalidateVisual(), ErrorTrace.Write));
            disposables.Add(tabs.Follow(static tab => tab.LayoutSettingsChanges).SubscribeSafe(_ => State.RelayoutForSettings(), ErrorTrace.Write));
            disposables.Add(tabs.Follow(static tab => tab.Destinations).SubscribeSafe(State.ScrollTo, ErrorTrace.Write));
            disposables.Add(tabs.Follow(static tab => tab.PageReads).SubscribeSafe(_ => ReadPagesAgain(), ErrorTrace.Write));
            disposables.Add(tabs.Follow(static tab => tab.SelectionUses).SubscribeSafe(_ => ClearSelection(), ErrorTrace.Write));
            disposables.Add(tabs.Follow(static tab => tab.CaretModeStarts).Do(this.StartCaret).SelectWith(this, static (canvas, _) => canvas).TakeFocus());
            disposables.Add(tabs.Follow(static tab => tab.PageToolChanges).SubscribeSafe(_ => ShowPageTool(), ErrorTrace.Write));
            disposables.Add(this.WheelZoomFrames.SubscribeSafe(this.StepZoom, ErrorTrace.Write));
            disposables.Add(tabs.Follow(static tab => tab.PagePresentation.WhenChanged(static presentation => presentation.Current)).SubscribeSafe(this.StartTransition, ErrorTrace.Write));
            disposables.Add(this.TransitionFrames.SubscribeSafe(this.StepTransition, ErrorTrace.Write));
            disposables.Add(this.AutoScrollFrames(tabs).SubscribeSafe(_ => this.StepAutoScroll(), ErrorTrace.Write));
            disposables.Add(this.WhenChanged(static x => x.CurrentHitOutline)
                .SubscribeSafe(outline => _currentHitPen = outline is not null ? new ImmutablePen(outline.ToImmutable(), CurrentHitOutlineWidth) : null, ErrorTrace.Write));

            // Input routing: the presentation layer decides, in this order: page tools and context menus, select-tool editing,
            // drawing and fields, then text and links. Each route turns the Avalonia event into the neutral one and does what is asked.
            disposables.Add(this.PointerPressRoutes(tabs).SubscribeSafe(static route => route.Apply(), ErrorTrace.Write));
            disposables.Add(this.PointerMoveRoutes(tabs).SubscribeSafe(static route => route.Apply(), ErrorTrace.Write));
            disposables.Add(this.PointerReleaseRoutes(tabs).SubscribeSafe(static route => route.Apply(), ErrorTrace.Write));
            disposables.Add(this.DocumentKeys.SubscribeSafe(static e => e.Handled = true, ErrorTrace.Write));
            disposables.Add(this.PageByPageWheels.SubscribeSafe(static e => e.Handled = true, ErrorTrace.Write));
            disposables.Add(this.HorizontalWheels.SubscribeSafe(static e => e.Handled = true, ErrorTrace.Write));
            disposables.Add(this.PinchZooms(tabs).SubscribeSafe(static step => step.Apply(), ErrorTrace.Write));
            disposables.Add(this.PinchEnds.SubscribeSafe(_ => this.EndPinch(), ErrorTrace.Write));
            disposables.Add(this.MagnifyZooms(tabs).SubscribeSafe(static step => step.Apply(), ErrorTrace.Write));
            disposables.Add(this.SpaceKeyUps.SubscribeSafe(this.CompleteSpaceHold, ErrorTrace.Write));
            disposables.Add(this.Events().LostFocus.SubscribeSafe(_ => this.ReleaseSpace(), ErrorTrace.Write));
        });

        GestureRecognizers.Add(new PinchGestureRecognizer());
    }

    /// <summary>Gets or sets the tab to display.</summary>
    public DocumentTabViewModel? Tab
    {
        get => GetValue(TabProperty);
        set => SetValue(TabProperty, value);
    }

    /// <summary>Gets or sets the outline of the picked annotation and of the annotation being moved.</summary>
    public IBrush? CurrentHitOutline
    {
        get => GetValue(CurrentHitOutlineProperty);
        set => SetValue(CurrentHitOutlineProperty, value);
    }

    /// <summary>Gets or sets the fill of selected text.</summary>
    public IBrush? SelectionBrush
    {
        get => GetValue(SelectionBrushProperty);
        set => SetValue(SelectionBrushProperty, value);
    }

    /// <summary>Gets or sets the background of labels drawn over the page, such as a measurement.</summary>
    public IBrush? LabelBackground
    {
        get => GetValue(LabelBackgroundProperty);
        set => SetValue(LabelBackgroundProperty, value);
    }

    /// <summary>Gets or sets the text colour of labels drawn over the page.</summary>
    public IBrush? LabelForeground
    {
        get => GetValue(LabelForegroundProperty);
        set => SetValue(LabelForegroundProperty, value);
    }

    /// <summary>Gets the canvas units per point at the current zoom, for sizing an editor placed on a page.</summary>
    public double PageScale => State.PageScale;

    /// <summary>Gets what the canvas knows about the pages it shows, which any toolkit's canvas keeps the same way.</summary>
    internal PageCanvasState State => field ??= new(this);

    /// <summary>Gets the command that copies the selected text from the context menu.</summary>
    internal ICommand CopyCommand { get; }

    /// <summary>Gets the text the reader has selected.</summary>
    internal TextSelection Selection => State.Selection;

    /// <summary>Gets the page points of the ink stroke being drawn.</summary>
    internal List<PagePoint> Stroke => State.Drawing.Stroke;

    /// <summary>Gets the corners placed so far in a polygon, cloud or polyline.</summary>
    internal PolygonDraft Polygon => State.Drawing.Polygon;

    /// <summary>Gets the character bounds list reused for caret lookups.</summary>
    internal List<PageRect> CaretBounds => State.CaretBounds;

    /// <summary>Gets the auto-scroll distance calculator.</summary>
    internal AutoScroller AutoScroller { get; } = new();

    /// <summary>Gets the snapshot renderer.</summary>
    internal PageSnapshotter Snapshotter { get; } = new();

    /// <summary>Gets how many snapshots have been asked for, for tests.</summary>
    internal int SnapshotsRequested => Snapshotter.SnapshotsRequested;

    /// <summary>Gets how many snapshots have taken their turn and started, for tests.</summary>
    internal int SnapshotsStarted => Snapshotter.SnapshotsStarted;

    /// <summary>Gets how many snapshots have finished, for tests.</summary>
    internal int SnapshotsFinished => Snapshotter.SnapshotsFinished;

    /// <summary>Gets the current layout.</summary>
    internal DocumentLayout Layout => State.Layout;

    /// <summary>Gets the page sizes the layout was built from.</summary>
    internal PageSize[] Sizes => State.Sizes;

    /// <summary>Gets the hosting scroll viewer.</summary>
    internal ScrollViewer? Scroller => _scroller;

    /// <summary>Gets the pen outlining the current search hit.</summary>
    internal IPen? CurrentHitPen => _currentHitPen;

    /// <summary>Gets or sets a position to restore once the layout has been measured.</summary>
    internal Action? PendingScroll
    {
        get => State.PendingScroll;
        set => State.PendingScroll = value;
    }

    /// <summary>Gets or sets the selection anchor.</summary>
    internal CharacterPosition SelectionAnchor
    {
        get => Selection.Anchor;
        set => Selection.Anchor = value;
    }

    /// <summary>Gets or sets the selection focus.</summary>
    internal CharacterPosition SelectionFocus
    {
        get => Selection.Focus;
        set => Selection.Focus = value;
    }

    /// <summary>Gets or sets the caret position.</summary>
    internal CharacterPosition Caret
    {
        get => State.Caret;
        set => State.Caret = value;
    }

    /// <summary>Gets or sets the wheel zoom easing now, or the last one once it has settled.</summary>
    internal WheelZoomEase WheelZoom
    {
        get => State.WheelZoom;
        set => State.WheelZoom = value;
    }

    /// <summary>Gets or sets the page transition playing now, or the last one once it has ended.</summary>
    internal TransitionRun Transition
    {
        get;
        set => SetAndRaise(TransitionProperty, ref field, value);
    }

    /// <summary>Gets the brush behind the pages, which a page transition draws back to reveal the page.</summary>
    internal IBrush? CurtainBrush => _scroller?.Background;

    /// <summary>Gets a value indicating whether a wheel zoom is easing, so tiles at passing scales are not requested.</summary>
    internal bool IsZoomEasing => State.IsZoomEasing;

    /// <summary>Gets or sets a value indicating whether the canvas is moving its own scroll offset to hold a zoom anchor.</summary>
    internal bool Anchoring
    {
        get => State.Anchoring;
        set => State.Anchoring = value;
    }

    /// <summary>Gets or sets the zoom when the current pinch started, or 0 when there is no pinch.</summary>
    internal double PinchStartZoom
    {
        get => State.PinchStartZoom;
        set => State.PinchStartZoom = value;
    }

    /// <summary>Gets or sets the page a navigation asked for, until the next position report; -1 when none.</summary>
    internal int RequestedPage
    {
        get => State.RequestedPage;
        set => State.RequestedPage = value;
    }

    /// <summary>Gets or sets when the last auto-scroll frame ran.</summary>
    internal long AutoScrollLast
    {
        get => State.Tools.AutoScrollLast;
        set => State.Tools.AutoScrollLast = value;
    }

    /// <summary>Gets a value indicating whether auto-scroll frames are being requested, for tests.</summary>
    internal bool IsAutoScrollRunning => Volatile.Read(ref _autoScrollRunning) != 0;

    /// <summary>Gets a value indicating whether the Space bar is held for temporary hand panning.</summary>
    internal bool SpaceHeld => State.Tools.SpaceHeld;

    /// <summary>Gets a value indicating whether a hand pan is in progress.</summary>
    internal bool Panning => State.Tools.Panning;

    /// <summary>Gets or sets a value indicating whether a snapshot or marquee-zoom rectangle is being dragged.</summary>
    internal bool AreaDragging
    {
        get => State.Tools.AreaDragging;
        set => State.Tools.AreaDragging = value;
    }

    /// <summary>Gets or sets where the area drag started on the canvas.</summary>
    internal Point AreaStart
    {
        get => new(State.Tools.AreaStart.X, State.Tools.AreaStart.Y);
        set => State.Tools.AreaStart = new(value.X, value.Y);
    }

    /// <summary>Gets or sets where the pointer is now in the area drag.</summary>
    internal Point AreaEnd
    {
        get => new(State.Tools.AreaEnd.X, State.Tools.AreaEnd.Y);
        set => State.Tools.AreaEnd = new(value.X, value.Y);
    }

    /// <summary>Gets or sets the annotation being moved or resized, or <see langword="null"/>.</summary>
    internal AnnotationEditSession? ActiveEdit
    {
        get => State.Tools.ActiveEdit;
        set => State.Tools.ActiveEdit = value;
    }

    /// <summary>Gets or sets the page an ink stroke is being drawn on, or -1 when not drawing.</summary>
    internal int StrokePage
    {
        get => State.Drawing.StrokePage;
        set => State.Drawing.StrokePage = value;
    }

    /// <summary>Gets or sets the shape currently being dragged on a page, or <see langword="null"/>.</summary>
    internal ShapePreview? ActiveShape
    {
        get => State.Drawing.ActiveShape;
        set => State.Drawing.ActiveShape = value;
    }

    /// <summary>Gets or sets the page clicked by a click-based annotation tool, or -1 when none.</summary>
    internal int ClickPage
    {
        get => State.Drawing.ClickPage;
        set => State.Drawing.ClickPage = value;
    }

    /// <summary>Gets or sets a value indicating whether a click-based text tool snaps to a detected form region.</summary>
    internal bool ClickSnaps
    {
        get => State.Drawing.ClickSnaps;
        set => State.Drawing.ClickSnaps = value;
    }

    /// <summary>Gets or sets the printed-form region currently hovered by the pointer.</summary>
    internal HoveredFormRegion? HoveredRegion
    {
        get => State.Drawing.HoveredRegion;
        set => State.Drawing.HoveredRegion = value;
    }

    /// <summary>Gets the canvas rectangle of a rectangle on a page, or an empty rectangle when the page is not laid out.</summary>
    /// <param name="page">The page.</param>
    /// <param name="bounds">The rectangle in page points.</param>
    /// <returns>The rectangle on the canvas.</returns>
    public Rect GetCanvasRect(int page, PageRect bounds)
    {
        var rect = State.GetCanvasRect(page, bounds);
        return new(rect.X, rect.Y, rect.Width, rect.Height);
    }

    /// <summary>Gets the selected text.</summary>
    /// <returns>The text, or an empty string.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetSelectedText() => Selection.GetText(Tab?.TryGetDocument());

    /// <summary>Clears the text selection.</summary>
    public void ClearSelection()
    {
        Selection.Clear();
        InvalidateVisual();
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var tab = Tab;
        var document = tab?.TryGetDocument();
        if (tab is null || document is null || _scroller is null || State.Layout.PageCount == 0)
        {
            return;
        }

        var viewport = new Rect(_scroller.Offset.X, _scroller.Offset.Y, _scroller.Viewport.Width, _scroller.Viewport.Height);
        var area = new LayoutRect(viewport.X, viewport.Y, viewport.Width, viewport.Height);
        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var generation = State.GetRenderGeneration(tab, document, area, scaling);
        var frame = new FrameContext(tab, document, tab.RenderHub, area, scaling, generation);
        var painter = _painter ??= new(this);
        painter.Start(context);
        var slide = this.GetSlideOffset(viewport);
        if (slide != default)
        {
            using (context.PushTransform(Matrix.CreateTranslation(slide.X, slide.Y)))
            {
                State.DrawPages(painter, frame);
            }
        }
        else
        {
            State.DrawPages(painter, frame);
        }

        this.DrawCurtain(context, viewport);
        State.DrawAnnotationOverlay(painter, tab, area);
        State.DrawRegions(painter, tab, area);
        State.DrawMeasurement(painter, tab);
        State.DrawArea(painter);
        State.DrawCaret(painter, tab);
    }

    /// <summary>Draws the text selection on a page; the search hits and reading marks are drawn by <see cref="PageMarksLayer"/>.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The page.</param>
    /// <param name="bounds">The page bounds.</param>
    internal void DrawSelection(DrawingContext context, DocumentTabViewModel tab, int page, in LayoutRect bounds)
    {
        if (SelectionBrush is not { } selection)
        {
            return;
        }

        var transform = new PageTransform(bounds, State.Sizes[page], tab.Rotation, State.Layout.Options.Scale);
        foreach (var rect in this.GetSelectionRects(page))
        {
            context.FillRectangle(selection, transform.ToCanvas(rect));
        }
    }

    /// <summary>Atomically updates the auto-scroll running flag.</summary>
    /// <param name="value">The new flag value.</param>
    /// <returns>The previous value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int SetAutoScrollRunning(int value) => Interlocked.Exchange(ref _autoScrollRunning, value);

    /// <summary>Gets the pages laid out inside an area of the canvas.</summary>
    /// <param name="left">The left side.</param>
    /// <param name="right">The right side.</param>
    /// <param name="top">The upper height.</param>
    /// <param name="bottom">The lower height.</param>
    /// <param name="first">The first page, or -1 when none.</param>
    /// <param name="last">The last page, or -1 when none.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void GetPagesBetween(double left, double right, double top, double bottom, out int first, out int last) =>
        State.GetPagesBetween(left, right, top, bottom, out first, out last);

    /// <summary>Converts a point on a page to canvas coordinates.</summary>
    /// <param name="page">The page.</param>
    /// <param name="point">The point in page space.</param>
    /// <returns>The point on the canvas.</returns>
    internal Point PageToCanvas(int page, PagePoint point)
    {
        var canvas = State.ToCanvas(page, point);
        return new(canvas.X, canvas.Y);
    }

    /// <summary>Converts a canvas point on a known page to page coordinates.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The page.</param>
    /// <param name="position">The canvas position.</param>
    /// <returns>The point in page space.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal PagePoint ToPage(DocumentTabViewModel tab, int page, Point position) => State.ToPage(tab, page, new(position.X, position.Y));

    /// <summary>Gets where a page is laid out on the canvas and how its points map there.</summary>
    /// <param name="page">The page.</param>
    /// <param name="transform">How the page's points map onto the canvas.</param>
    /// <param name="bounds">The page's bounds on the canvas.</param>
    /// <returns><see langword="true"/> when the page is laid out.</returns>
    internal bool TryGetPageTransform(int page, out PageTransform transform, out Rect bounds)
    {
        if (!State.TryGetPageTransform(page, out transform))
        {
            bounds = default;
            return false;
        }

        bounds = new(transform.Bounds.X, transform.Bounds.Y, transform.Bounds.Width, transform.Bounds.Height);
        return true;
    }

    /// <summary>Reports the current position to the tab.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ReportPosition() => State.ReportPosition();

    /// <inheritdoc/>
    bool ICanvasHost.HasScroller => _scroller is not null;

    /// <inheritdoc/>
    bool ICanvasHost.IsInWindow => TopLevel.GetTopLevel(this) is not null;

    /// <inheritdoc/>
    CanvasPoint ICanvasHost.ScrollOffset
    {
        get => _scroller is { } scroller ? new(scroller.Offset.X, scroller.Offset.Y) : default;
        set
        {
            if (_scroller is { } scroller)
            {
                scroller.Offset = new(value.X, value.Y);
            }
        }
    }

    /// <inheritdoc/>
    CanvasSize ICanvasHost.ViewportSize => _scroller is { } scroller ? new(scroller.Viewport.Width, scroller.Viewport.Height) : default;

    /// <inheritdoc/>
    CanvasSize ICanvasHost.ExtentSize => _scroller is { } scroller ? new(scroller.Extent.Width, scroller.Extent.Height) : default;

    /// <inheritdoc/>
    CanvasSize ICanvasHost.ScrollerSize => _scroller is { } scroller ? new(scroller.Bounds.Width, scroller.Bounds.Height) : default;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ICanvasHost.InvalidateMeasure() => InvalidateMeasure();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ICanvasHost.InvalidateVisual() => InvalidateVisual();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ICanvasHost.UpdateLayout() => _scroller?.UpdateLayout();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ICanvasHost.PageScaleChanged(double before, double after) => RaisePropertyChanged(PageScaleProperty, before, after);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ICanvasHost.WheelZoomChanged(WheelZoomEase before, WheelZoomEase after) => RaisePropertyChanged(WheelZoomProperty, before, after);

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TabProperty)
        {
            State.Tab = Tab;
        }
    }

    /// <inheritdoc/>
    protected override AutomationPeer OnCreateAutomationPeer() => new PageCanvasAutomationPeer(this);

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize) => new(State.Layout.ExtentWidth, State.Layout.ExtentHeight);

    /// <summary>Wires the scroll viewer's offset, viewport, extent and Ctrl+wheel streams to the canvas.</summary>
    /// <param name="scroller">The hosting scroll viewer.</param>
    /// <param name="tabs">The tab stream.</param>
    /// <param name="register">Registers each subscription with the activation scope.</param>
    private void WireScroller(ScrollViewer scroller, IObservable<DocumentTabViewModel?> tabs, Action<IDisposable> register)
    {
        var offsets = scroller.WhenChanged(static x => x.Offset).Skip(1);
        register(offsets.SubscribeSafe(_ => InvalidateVisual(), ErrorTrace.Write));
        register(offsets.WhereWith(this, static (canvas, _) => canvas.PendingScroll is null).SubscribeSafe(_ => ReportPosition(), ErrorTrace.Write));
        register(scroller.ViewportSizes.SubscribeSafe(_ => State.RelayoutInPlace(), ErrorTrace.Write));
        register(scroller.PendingScrollReady(this).SubscribeSafe(_ => State.ApplyPendingScroll(), ErrorTrace.Write));
        register(scroller.CtrlWheelZooms(this, tabs).SubscribeSafe(static step => step.Apply(), ErrorTrace.Write));
    }

    /// <summary>Lets go of what the canvas holds once it is put away: the signature mark preview, the snapshotter, the scroll viewer and the tab.</summary>
    private void PutAway()
    {
        _painter?.Dispose();
        Snapshotter.Release();
        _scroller = null;
        ShowTab(null);
    }

    /// <summary>Drops a drag in progress and shows the pointer of the page tool chosen now.</summary>
    private void ShowPageTool()
    {
        AreaDragging = false;
        Cursor = this.ToolCursor();
        InvalidateVisual();
    }

    /// <summary>
    /// Shows a tab: forgets what belonged to the tab shown before, then lays the new tab's pages out where its reader was.
    /// The streams the canvas follows switch to the new tab by themselves.
    /// </summary>
    /// <param name="tab">The tab, or <see langword="null"/> to show nothing.</param>
    private void ShowTab(DocumentTabViewModel? tab)
    {
        if (_shownTab is { } old && !ReferenceEquals(old, tab))
        {
            _ = old.CanvasClient.Advance();
            old.PageTools.IsAutoScrolling = false;
        }

        State.Render.ForgetRequest();
        _shownTab = tab;
        State.Links.Clear();
        ClearSelection();
        if (tab is null)
        {
            State.UseLayout(DocumentLayout.Empty, null);
            return;
        }

        var position = tab.Position;
        State.RebuildLayout();
        State.ScrollToPosition(position);
    }

    /// <summary>Lays the pages out again after the document loads, reloads or its pages change, keeping the reader's place.</summary>
    private void ReadPagesAgain()
    {
        State.Links.Clear();
        ClearSelection();
        State.RelayoutAfterPagesChange();
    }
}
