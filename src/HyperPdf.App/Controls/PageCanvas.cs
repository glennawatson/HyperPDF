// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
using HyperPdf.App.Rendering;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Navigation;
using HyperPdf.Core.Rendering;
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

    /// <summary>How far beyond the viewport, in viewport heights, pages are prefetched.</summary>
    private const double PrefetchViewports = 0.75;

    /// <summary>The smallest change in the viewport's top, in canvas units, that counts as scrolling in a direction.</summary>
    private const double ScrollDirectionTolerance = 0.5;

    /// <summary>How many pages past the drawn ones get a low resolution preview on the side the reader is scrolling towards.</summary>
    private const int PreviewReachAhead = 24;

    /// <summary>How many pages past the drawn ones get a low resolution preview on the side the reader came from.</summary>
    private const int PreviewReachBehind = 8;

    /// <summary>The smallest display scale change that needs a new tile generation.</summary>
    private const double RenderScaleTolerance = 1e-9;

    /// <summary>The distance kept to the left of a navigation target.</summary>
    private const double TargetPadding = 24;

    /// <summary>Where in the viewport a navigation target is placed, as a fraction of its height from the top.</summary>
    private const double TargetViewportFraction = 0.3;

    /// <summary>The width of the outline marking the current search hit, so it differs by shape as well as colour.</summary>
    private const double CurrentHitOutlineWidth = 2;

    /// <summary>The page shadow offset.</summary>
    private const double ShadowOffset = 2;

    /// <summary>Defines the <see cref="WheelZoom"/> property.</summary>
    private static readonly DirectProperty<PageCanvas, WheelZoomEase> WheelZoomProperty =
        AvaloniaProperty.RegisterDirect<PageCanvas, WheelZoomEase>(nameof(WheelZoom), static canvas => canvas.WheelZoom);

    /// <summary>Defines the <see cref="Transition"/> property.</summary>
    private static readonly DirectProperty<PageCanvas, PageCanvasTransitionMixins.TransitionRun> TransitionProperty =
        AvaloniaProperty.RegisterDirect<PageCanvas, PageCanvasTransitionMixins.TransitionRun>(nameof(Transition), static canvas => canvas.Transition);

    /// <summary>The page shadow brush.</summary>
    private static readonly IBrush ShadowBrush = new SolidColorBrush(Color.FromArgb(0x40, 0, 0, 0));

    /// <summary>
    /// How page images are sampled, with aliased edges so tiles meet without seams. Only stand-ins are ever scaled: the
    /// preview and the last scale's tiles while a zoom settles. Bilinear with mipmaps draws those about ten times
    /// faster than cubic (BitmapDrawBenchmarks), which keeps the zoom ease smooth; sharp tiles are drawn 1:1.
    /// </summary>
    private static readonly RenderOptions PageRenderOptions = new() { BitmapInterpolationMode = BitmapInterpolationMode.MediumQuality, EdgeMode = EdgeMode.Aliased };

    /// <summary>Queues each page's GPU tiles so they reach the scene as one operation.</summary>
    private readonly TileDrawBatch _tiles = new();

    /// <summary>The hosting scroll viewer.</summary>
    private ScrollViewer? _scroller;

    /// <summary>The tab shown, which lets go of its render requests and auto-scroll when another tab replaces it.</summary>
    private DocumentTabViewModel? _shownTab;

    /// <summary>The tile scale of the last frame whose visible pages were all sharp, drawn while newer tiles render.</summary>
    private float _fallbackScale;

    /// <summary>Whether every visible page in the frame being drawn had its sharp tiles.</summary>
    private bool _frameComplete;

    /// <summary>The request state used to decide when queued tiles have become stale.</summary>
    private DocumentTabViewModel? _renderTab;

    /// <summary>The document used by the last tile request.</summary>
    private IDocument? _renderDocument;

    /// <summary>The viewport used by the last tile request.</summary>
    private Rect _renderViewport;

    /// <summary>The display scale used by the last tile request.</summary>
    private double _renderScaling;

    /// <summary>The layout used by the last tile request.</summary>
    private LayoutOptions _renderLayout;

    /// <summary>The rotation used by the last tile request.</summary>
    private PageRotation _renderRotation;

    /// <summary>The tone used by the last tile request.</summary>
    private int _renderToneId;

    /// <summary>The top of the viewport at the last frame, to tell which way the reader is scrolling.</summary>
    private double _previousViewportTop;

    /// <summary>What the last frame's prefetch passes depended on.</summary>
    private PrefetchStamp _lastPrefetch;

    /// <summary>Whether the last frame found every tile and preview it looked for already cached, so it asked for nothing.</summary>
    private bool _lastPrefetchSettled;

    /// <summary>Whether this frame skips the prefetch passes because they would find what the last frame found.</summary>
    private bool _skipPrefetch;

    /// <summary>The render requests made so far in this frame.</summary>
    private int _requestsThisFrame;

    /// <summary>Whether the reader last scrolled towards the end of the document, so previews are made further ahead on that side.</summary>
    private bool _scrollingDown = true;

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

            // Input routing: page tools and context menus first, then select-tool editing, then drawing/fields, then text/links.
            var selectPresses = this.SelectToolPresses(tabs).Publish().RefCount();
            disposables.Add(this.PageToolPresses(tabs).SubscribeSafe(static step => step.Begin(), ErrorTrace.Write));
            disposables.Add(this.RightClicks(tabs).SubscribeSafe(static step => step.OpenMenu(), ErrorTrace.Write));
            disposables.Add(selectPresses.TextBoxDoubleClicks.SubscribeSafe(static step => step.EditText(), ErrorTrace.Write));
            disposables.Add(selectPresses.ResizeHandleStarts.SubscribeSafe(static step => step.Begin(), ErrorTrace.Write));
            disposables.Add(selectPresses.CommentBadgeClicks.SubscribeSafe(static step => step.OpenBubble(), ErrorTrace.Write));
            disposables.Add(selectPresses.BubbleDoubleClicks.SubscribeSafe(static step => step.SelectAndOpenBubble(), ErrorTrace.Write));
            disposables.Add(selectPresses.AnnotationMoveStarts.SubscribeSafe(static step => step.Begin(), ErrorTrace.Write));
            disposables.Add(this.DrawingAndFieldPresses(tabs).SubscribeSafe(static step => step.Begin(), ErrorTrace.Write));
            disposables.Add(this.SelectionAndLinkPresses(tabs).SubscribeSafe(static step => step.Begin(), ErrorTrace.Write));
            disposables.Add(this.EditMoves(tabs).SubscribeSafe(static step => step.UpdatePreview(), ErrorTrace.Write));
            disposables.Add(this.PointerMoveSteps(tabs).SubscribeSafe(static step => step.Apply(), ErrorTrace.Write));
            disposables.Add(this.EditReleases(tabs).SubscribeSafe(static step => step.CommitOrOpenBubble(), ErrorTrace.Write));
            disposables.Add(this.PointerReleaseSteps(tabs).SubscribeSafe(static step => step.Apply(), ErrorTrace.Write));
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

    /// <summary>Gets the links by page, loaded lazily.</summary>
    internal Dictionary<int, IReadOnlyList<PageLink>> Links { get; } = [];

    /// <summary>Gets the selection rectangles by page, computed lazily.</summary>
    internal Dictionary<int, List<PageRect>> SelectionRects { get; } = [];

    /// <summary>Gets the emptied selection rectangle lists kept for reuse by the next page that needs one.</summary>
    internal Stack<List<PageRect>> SpareSelectionRects { get; } = [];

    /// <summary>Gets the page points of the ink stroke being drawn.</summary>
    internal List<PagePoint> Stroke { get; } = [];

    /// <summary>Gets the corners placed so far in a polygon, cloud or polyline.</summary>
    internal PolygonDraft Polygon { get; } = new();

    /// <summary>Gets the annotations list reused while drawing comment indicator badges.</summary>
    internal List<PageAnnotation> BadgeAnnotations { get; } = [];

    /// <summary>Gets the character bounds list reused for caret lookups.</summary>
    internal List<PageRect> CaretBounds { get; } = [];

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

    /// <summary>Gets or sets the pointer position where a press started.</summary>
    internal Point PressPoint { get; set; }

    /// <summary>Gets or sets the link under the pointer when it was pressed.</summary>
    internal PageLink? PressedLink { get; set; }

    /// <summary>Gets or sets a value indicating whether a text selection drag is active.</summary>
    internal bool Selecting { get; set; }

    /// <summary>Gets or sets the selection anchor.</summary>
    internal CharacterPosition SelectionAnchor { get; set; } = new(-1, -1);

    /// <summary>Gets or sets the selection focus.</summary>
    internal CharacterPosition SelectionFocus { get; set; } = new(-1, -1);

    /// <summary>Gets or sets the caret position.</summary>
    internal CharacterPosition Caret { get; set; } = new(-1, -1);

    /// <summary>Gets or sets the wheel zoom easing now, or the last one once it has settled.</summary>
    internal WheelZoomEase WheelZoom
    {
        get => State.WheelZoom;
        set => State.WheelZoom = value;
    }

    /// <summary>Gets or sets the page transition playing now, or the last one once it has ended.</summary>
    internal PageCanvasTransitionMixins.TransitionRun Transition
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
    internal PageCanvasEditMixins.AnnotationEditSession? ActiveEdit { get; set; }

    /// <summary>Gets or sets the page an ink stroke is being drawn on, or -1 when not drawing.</summary>
    internal int StrokePage { get; set; } = -1;

    /// <summary>Gets or sets the shape currently being dragged on a page, or <see langword="null"/>.</summary>
    internal PageCanvasAnnotationMixins.ShapePreview? ActiveShape { get; set; }

    /// <summary>Gets or sets the page clicked by a click-based annotation tool, or -1 when none.</summary>
    internal int ClickPage { get; set; } = -1;

    /// <summary>Gets or sets a value indicating whether a click-based text tool snaps to a detected form region.</summary>
    internal bool ClickSnaps { get; set; }

    /// <summary>Gets or sets the cached signature mark preview.</summary>
    internal SignatureMarkPainter? MarkPainter { get; set; }

    /// <summary>Gets or sets the cached pen used to draw a signature mark preview.</summary>
    internal Pen? MarkPen { get; set; }

    /// <summary>Gets or sets the cached pen the highlighter's live stroke is drawn with.</summary>
    internal Pen? HighlighterPen { get; set; }

    /// <summary>Gets or sets the printed-form region currently hovered by the pointer.</summary>
    internal PageCanvasAnnotationMixins.HoveredFormRegion? HoveredRegion { get; set; }

    /// <summary>Gets or sets the dashed pen used to draw printed-form region suggestions.</summary>
    internal Pen? SuggestionPen { get; set; }

    /// <summary>Gets or sets the solid pen used to draw the hovered printed-form region suggestion.</summary>
    internal Pen? HoveredSuggestionPen { get; set; }

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
    public string GetSelectedText()
    {
        var document = Tab?.TryGetDocument();
        if (document is null || !this.TryGetSelection(out var start, out var end))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (var page = start.Page; page <= end.Page; page++)
        {
            var first = page == start.Page ? start.Char : 0;
            var last = page == end.Page ? end.Char : document.GetCharacterCount(page) - 1;
            if (last >= first)
            {
                _ = builder.Append(document.GetText(page, first, last - first + 1));
            }

            if (page != end.Page)
            {
                _ = builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    /// <summary>Clears the text selection.</summary>
    public void ClearSelection()
    {
        SelectionAnchor = new(-1, -1);
        SelectionFocus = new(-1, -1);
        SelectionRects.Clear();
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
        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var generation = RenderGeneration(tab, document, viewport, scaling);
        var frame = new FrameContext(tab, document, tab.RenderHub, viewport, scaling, generation);
        var slide = this.GetSlideOffset(viewport);
        if (slide != default)
        {
            using (context.PushTransform(Matrix.CreateTranslation(slide.X, slide.Y)))
            {
                DrawPages(context, frame);
            }
        }
        else
        {
            DrawPages(context, frame);
        }

        this.DrawCurtain(context, viewport);
        this.DrawAnnotationOverlay(context, tab);
        this.DrawRegions(context, tab, viewport);
        this.DrawMeasurement(context, tab);
        this.DrawArea(context);
        this.DrawCaret(context, tab);
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

    /// <summary>Reuses the generation when a tile arrival repaints the same view.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="document">The document.</param>
    /// <param name="viewport">The viewport.</param>
    /// <param name="scaling">The display scale.</param>
    /// <returns>The generation for requests from this view.</returns>
    private int RenderGeneration(DocumentTabViewModel tab, IDocument document, in Rect viewport, double scaling)
    {
        if (ReferenceEquals(_renderTab, tab) && ReferenceEquals(_renderDocument, document)
            && _renderViewport.Equals(viewport) && Math.Abs(_renderScaling - scaling) < RenderScaleTolerance
            && _renderLayout == State.Layout.Options && _renderRotation == tab.Rotation && _renderToneId == tab.PageTone.Id)
        {
            return tab.CanvasClient.Generation;
        }

        _renderTab = tab;
        _renderDocument = document;
        _renderViewport = viewport;
        _renderScaling = scaling;
        _renderLayout = State.Layout.Options;
        _renderRotation = tab.Rotation;
        _renderToneId = tab.PageTone.Id;
        return tab.CanvasClient.Advance();
    }

    /// <summary>Draws the low resolution preview of a page, requesting it when missing.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="page">The page.</param>
    /// <param name="rect">The page rectangle.</param>
    /// <param name="visible">Whether the page is visible.</param>
    /// <param name="draw">Whether sharp tiles leave part of the page for the preview to fill.</param>
    private void DrawPreview(DrawingContext context, in FrameContext frame, int page, in Rect rect, bool visible, bool draw)
    {
        var tab = frame.Tab;
        var key = TileKey.Preview(tab.TileId, page, tab.Rotation, tab.PageTone.Id);
        if (frame.Hub.Cache.TryGet(key, out var surface))
        {
            if (draw)
            {
                _tiles.Draw(context, surface, rect, smooth: true);
            }

            return;
        }

        var size = tab.Source.PageSizes[page];
        var scale = TileGrid.GetPreviewScale(size, tab.Rotation);
        TileGrid.GetPagePixelSize(size, tab.Rotation, scale, out var width, out var height);
        _requestsThisFrame++;
        frame.Request(key, new(page, scale, tab.Rotation, 0, 0, RenderFlags.Annotations), width, height, visible ? RenderPriority.VisiblePreview : RenderPriority.Prefetch);
    }

    /// <summary>Draws one tile, or requests it when it is not cached.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="grid">The page's tile grid.</param>
    /// <param name="column">The tile column.</param>
    /// <param name="row">The tile row.</param>
    /// <param name="visible">Whether to draw (true) or only request (false).</param>
    /// <param name="request">Whether a missing tile is requested.</param>
    private void DrawTile(DrawingContext context, in FrameContext frame, in TileRange grid, int column, int row, bool visible, bool request)
    {
        var tab = frame.Tab;
        var tileSize = TileGrid.TileSize;
        var key = new TileKey(tab.TileId, grid.Page, grid.ScaleKey, tab.Rotation, tab.PageTone.Id, (short)column, (short)row);
        TileGrid.GetTileSize(grid.PixelWidth, grid.PixelHeight, column, row, out var tileWidth, out var tileHeight);
        if (!frame.Hub.Cache.TryGet(key, out var surface))
        {
            if (request)
            {
                var info = new PageRenderInfo(grid.Page, grid.Scale, tab.Rotation, column * tileSize, row * tileSize, RenderFlags.Annotations);
                _requestsThisFrame++;
                frame.Request(key, info, tileWidth, tileHeight, visible ? RenderPriority.Visible : RenderPriority.Prefetch);
            }

            return;
        }

        var scaling = frame.RenderScaling;
        var destination = new Rect((grid.OriginX + (column * tileSize)) / scaling, (grid.OriginY + (row * tileSize)) / scaling, tileWidth / scaling, tileHeight / scaling);
        if (!visible || !destination.Intersects(frame.Viewport))
        {
            return;
        }

        _tiles.Draw(context, surface, destination, smooth: false);
    }

    /// <summary>Draws the pages around the viewport, and remembers the scale once every visible page is sharp.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="frame">The frame.</param>
    private void DrawPages(DrawingContext context, in FrameContext frame)
    {
        var viewport = frame.Viewport;
        var prefetch = viewport.Height * PrefetchViewports;
        var sidePrefetch = viewport.Width * PrefetchViewports;
        State.Layout.GetVisiblePages(viewport.Left - sidePrefetch, viewport.Right + sidePrefetch, viewport.Top - prefetch, viewport.Bottom + prefetch, out var first, out var last);
        _frameComplete = true;
        _requestsThisFrame = 0;
        UpdateScrollDirection(viewport.Top);
        var cache = frame.Hub.Cache;
        var cancelled = frame.Tab.SelectedWorkToken.IsCancellationRequested;
        var stamp = new PrefetchStamp(frame.Generation, first, last, _scrollingDown, IsZoomEasing, cancelled, cache.Count, cache.CurrentBytes, cache.PreviewBytes);

        // A frame that asked for nothing, repeated with the same view and the same cache, would ask for nothing again. A request
        // that was made last frame is asked for again, so one the scheduler dropped is not lost. The TileCache has no eviction
        // counter, so its tile count and byte totals stand in: an eviction that is exactly offset by an arrival in the same
        // frame is the one change they cannot see.
        _skipPrefetch = _lastPrefetchSettled && stamp == _lastPrefetch;

        // Aliased edges keep neighbouring tiles from showing seams where they meet at fractional positions.
        using (context.PushRenderOptions(PageRenderOptions))
        {
            for (var page = first; page <= last && page >= 0; page++)
            {
                DrawPage(context, frame, page);
            }
        }

        if (!_skipPrefetch)
        {
            RequestPreviewReach(frame, first, last);
        }

        _lastPrefetch = stamp;
        _lastPrefetchSettled = _requestsThisFrame == 0;
        State.NeighbourScale = GetTileScale(frame.RenderScaling);
        if (_frameComplete && !IsZoomEasing)
        {
            _fallbackScale = GetTileScale(frame.RenderScaling);
        }
    }

    /// <summary>
    /// Keeps a low resolution preview of the pages around the drawn ones, nearest first and further ahead in the direction of
    /// scrolling, so a quick scroll up or down shows each page's shape and text before its sharp tiles are made.
    /// </summary>
    /// <param name="frame">The frame.</param>
    /// <param name="first">The first drawn page, or -1 when none.</param>
    /// <param name="last">The last drawn page.</param>
    private void RequestPreviewReach(in FrameContext frame, int first, int last)
    {
        if (first < 0)
        {
            return;
        }

        var after = _scrollingDown ? PreviewReachAhead : PreviewReachBehind;
        var before = _scrollingDown ? PreviewReachBehind : PreviewReachAhead;
        for (var distance = 1; distance <= Math.Max(after, before); distance++)
        {
            if (distance <= after)
            {
                RequestDistantPreview(frame, last + distance);
            }

            if (distance <= before)
            {
                RequestDistantPreview(frame, first - distance);
            }
        }
    }

    /// <summary>Works out which way the reader is scrolling from the top of the viewport.</summary>
    /// <param name="top">The top of the viewport.</param>
    private void UpdateScrollDirection(double top)
    {
        if (Math.Abs(top - _previousViewportTop) <= ScrollDirectionTolerance)
        {
            return;
        }

        _scrollingDown = top > _previousViewportTop;
        _previousViewportTop = top;
    }

    /// <summary>Tells whether prefetching waits: a zoom is easing, or the last frame found nothing left to ask for.</summary>
    /// <returns><see langword="true"/> when prefetching is held back.</returns>
    private bool IsPrefetchHeld() => IsZoomEasing || _skipPrefetch;

    /// <summary>Marks a page's preview as recently used, or requests it at the lowest page priority when it is missing.</summary>
    /// <param name="frame">The frame.</param>
    /// <param name="page">The page, which may be outside the document.</param>
    private void RequestDistantPreview(in FrameContext frame, int page)
    {
        var tab = frame.Tab;
        if ((uint)page >= (uint)State.Layout.PageCount)
        {
            return;
        }

        var key = TileKey.Preview(tab.TileId, page, tab.Rotation, tab.PageTone.Id);
        if (frame.Hub.Cache.TryGet(key, out _))
        {
            return;
        }

        var size = tab.Source.PageSizes[page];
        var scale = TileGrid.GetPreviewScale(size, tab.Rotation);
        TileGrid.GetPagePixelSize(size, tab.Rotation, scale, out var width, out var height);
        _requestsThisFrame++;
        frame.Request(key, new(page, scale, tab.Rotation, 0, 0, RenderFlags.Annotations), width, height, RenderPriority.PrefetchPreview);
    }

    /// <summary>Draws one page: shadow, background, preview, tiles and highlights.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="page">The page.</param>
    private void DrawPage(DrawingContext context, in FrameContext frame, int page)
    {
        var bounds = State.Layout.GetPageBounds(page);
        var rect = new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        var visible = rect.Intersects(frame.Viewport);
        if (visible)
        {
            context.FillRectangle(ShadowBrush, rect.Translate(new(ShadowOffset, ShadowOffset)));
            context.FillRectangle(PaperBrush.Get(frame.Tab.PageTone), rect);
        }

        var tiled = TryGetTileWindow(frame, page, rect, visible, out var grid, out var window);
        var tab = frame.Tab;
        var covered = tiled && visible && frame.Hub.Cache.Covers(new(tab.TileId, page, grid.ScaleKey, tab.Rotation, tab.PageTone.Id, 0, 0), window);
        DrawPreview(context, frame, page, rect, visible, visible && !covered);
        if (visible && tiled && !covered)
        {
            // Until the sharp tiles arrive, the last complete scale stands in, so the page does not flash blurry.
            _frameComplete = false;
            DrawFallbackTiles(context, frame, page, rect, grid.ScaleKey);
        }

        if (tiled)
        {
            DrawTiles(context, frame, grid, window, visible);
            PrefetchAdjacentTiles(context, frame, page, rect, window, visible);
        }

        _tiles.Flush(context);

        if (!visible)
        {
            return;
        }

        DrawSelection(context, frame.Tab, page, bounds);
    }

    /// <summary>Gets the device pixels per point that tiles are rendered at.</summary>
    /// <param name="scaling">The device pixels per canvas unit.</param>
    /// <returns>The tile scale.</returns>
    private float GetTileScale(double scaling) => (float)Math.Min(State.Layout.Options.Scale * scaling, TileGrid.MaxScale);

    /// <summary>Finds the full resolution tiles of a page that intersect the viewport, or its prefetch area when off screen.</summary>
    /// <param name="frame">The frame.</param>
    /// <param name="page">The page.</param>
    /// <param name="rect">The page rectangle.</param>
    /// <param name="visible">Whether the page is on screen.</param>
    /// <param name="grid">The page's tile grid.</param>
    /// <param name="window">The tiles in the area.</param>
    /// <returns><see langword="true"/> when the page needs tiles in the area; small pages use only the preview.</returns>
    private bool TryGetTileWindow(in FrameContext frame, int page, in Rect rect, bool visible, out TileRange grid, out TileWindow window)
    {
        var scaling = frame.RenderScaling;
        var scale = GetTileScale(scaling);
        TileGrid.GetPagePixelSize(State.Sizes[page], frame.Tab.Rotation, scale, out var pixelWidth, out var pixelHeight);
        var area = visible ? frame.Viewport : frame.Viewport.Inflate(new Thickness(0, frame.Viewport.Height * PrefetchViewports));
        var region = area.Intersect(rect);

        // Snap the page origin to device pixels so tiles meet without seams.
        var originX = Math.Round(rect.X * scaling);
        var originY = Math.Round(rect.Y * scaling);
        grid = new(page, TileGrid.ToScaleKey(scale), scale, pixelWidth, pixelHeight, originX, originY);
        if (pixelWidth <= TileGrid.PreviewWidth || region.Width <= 0 || region.Height <= 0)
        {
            window = default;
            return false;
        }

        window = TileGrid.GetTileWindow(
            pixelWidth,
            pixelHeight,
            (region.X * scaling) - originX,
            (region.Y * scaling) - originY,
            (region.Right * scaling) - originX,
            (region.Bottom * scaling) - originY);
        return true;
    }

    /// <summary>Draws the cached tiles in a window and requests missing ones, unless a zoom is still easing.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="grid">The page's tile grid.</param>
    /// <param name="window">The tiles to draw.</param>
    /// <param name="visible">Whether the page is on screen; off screen pages are only prefetched.</param>
    private void DrawTiles(DrawingContext context, in FrameContext frame, in TileRange grid, in TileWindow window, bool visible)
    {
        var request = !IsZoomEasing;
        for (var row = window.FirstRow; row <= window.LastRow; row++)
        {
            for (var column = window.FirstColumn; column <= window.LastColumn; column++)
            {
                DrawTile(context, frame, grid, column, row, visible, request);
            }
        }
    }

    /// <summary>Requests nearby rows of a visible page before scrolling exposes them.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="page">The page.</param>
    /// <param name="rect">The page bounds.</param>
    /// <param name="visibleWindow">The tile range already requested for drawing.</param>
    /// <param name="visible">Whether the page is visible.</param>
    private void PrefetchAdjacentTiles(DrawingContext context, in FrameContext frame, int page, in Rect rect, in TileWindow visibleWindow, bool visible)
    {
        if (!visible || IsPrefetchHeld() || !TryGetTileWindow(frame, page, rect, false, out var grid, out var window))
        {
            return;
        }

        for (var row = window.FirstRow; row <= window.LastRow; row++)
        {
            for (var column = window.FirstColumn; column <= window.LastColumn; column++)
            {
                if (row >= visibleWindow.FirstRow && row <= visibleWindow.LastRow && column >= visibleWindow.FirstColumn && column <= visibleWindow.LastColumn)
                {
                    continue;
                }

                DrawTile(context, frame, grid, column, row, false, true);
            }
        }
    }

    /// <summary>Stretches the cached tiles of the last complete scale over the visible part of a page.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="page">The page.</param>
    /// <param name="rect">The page rectangle.</param>
    /// <param name="scaleKey">The quantised scale being drawn, which needs no stand in.</param>
    private void DrawFallbackTiles(DrawingContext context, in FrameContext frame, int page, in Rect rect, int scaleKey)
    {
        var scale = _fallbackScale;
        var fallbackKey = TileGrid.ToScaleKey(scale);
        if (scale <= 0 || fallbackKey == scaleKey)
        {
            return;
        }

        var tab = frame.Tab;
        TileGrid.GetPagePixelSize(State.Sizes[page], tab.Rotation, scale, out var pixelWidth, out var pixelHeight);
        var region = frame.Viewport.Intersect(rect);
        if (pixelWidth <= TileGrid.PreviewWidth || region.Width <= 0 || region.Height <= 0)
        {
            return;
        }

        var tileSize = TileGrid.TileSize;
        var unitX = rect.Width / pixelWidth;
        var unitY = rect.Height / pixelHeight;
        TileGrid.GetTileCounts(pixelWidth, pixelHeight, out var columns, out var rows);
        var firstColumn = Math.Clamp((int)((region.X - rect.X) / unitX / tileSize), 0, columns - 1);
        var lastColumn = Math.Clamp((int)((region.Right - rect.X) / unitX / tileSize), 0, columns - 1);
        var firstRow = Math.Clamp((int)((region.Y - rect.Y) / unitY / tileSize), 0, rows - 1);
        var lastRow = Math.Clamp((int)((region.Bottom - rect.Y) / unitY / tileSize), 0, rows - 1);
        for (var row = firstRow; row <= lastRow; row++)
        {
            for (var column = firstColumn; column <= lastColumn; column++)
            {
                var key = new TileKey(tab.TileId, page, fallbackKey, tab.Rotation, tab.PageTone.Id, (short)column, (short)row);
                if (!frame.Hub.Cache.TryGet(key, out var surface))
                {
                    continue;
                }

                var destination = new Rect(rect.X + (column * tileSize * unitX), rect.Y + (row * tileSize * unitY), surface.Width * unitX, surface.Height * unitY);
                _tiles.Draw(context, surface, destination, smooth: true);
            }
        }
    }

    /// <summary>Draws the text selection on a page; the search hits and reading marks are drawn by <see cref="PageMarksLayer"/>.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The page.</param>
    /// <param name="bounds">The page bounds.</param>
    private void DrawSelection(DrawingContext context, DocumentTabViewModel tab, int page, in LayoutRect bounds)
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

    /// <summary>Lets go of what the canvas holds once it is put away: the signature mark preview, the snapshotter, the scroll viewer and the tab.</summary>
    private void PutAway()
    {
        MarkPainter?.Dispose();
        MarkPainter = null;
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

        _renderTab = null;
        _renderDocument = null;
        _shownTab = tab;
        Links.Clear();
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
        Links.Clear();
        ClearSelection();
        State.RelayoutAfterPagesChange();
    }
}
