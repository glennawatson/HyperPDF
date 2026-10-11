// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
using PdfViewerLite.App.Rendering;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Layout;
using PdfViewerLite.Core.Navigation;
using PdfViewerLite.Core.Rendering;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.ObservableEvents;
using ReactiveUI.Primitives.Signals;

namespace PdfViewerLite.App.Controls;

/// <summary>
/// Draws a document as a continuous strip of pages inside a <see cref="ScrollViewer"/>. Only the tiles intersecting the
/// viewport are drawn or requested; every frame re-requests what it is missing under a fresh render generation so work
/// for regions that scrolled away is dropped by the scheduler before it reaches the renderer.
/// </summary>
[DebuggerDisplay("PageCanvas: {Tab}")]
public sealed partial class PageCanvas : Control
{
    /// <summary>Defines the <see cref="Tab"/> property.</summary>
    public static readonly StyledProperty<DocumentTabViewModel?> TabProperty = AvaloniaProperty.Register<PageCanvas, DocumentTabViewModel?>(nameof(Tab));

    /// <summary>Defines the <see cref="HitBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> HitBrushProperty = AvaloniaProperty.Register<PageCanvas, IBrush?>(nameof(HitBrush));

    /// <summary>Defines the <see cref="CurrentHitOutline"/> property.</summary>
    public static readonly StyledProperty<IBrush?> CurrentHitOutlineProperty = AvaloniaProperty.Register<PageCanvas, IBrush?>(nameof(CurrentHitOutline));

    /// <summary>Defines the <see cref="SelectionBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> SelectionBrushProperty = AvaloniaProperty.Register<PageCanvas, IBrush?>(nameof(SelectionBrush));

    /// <summary>Defines the <see cref="DimBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> DimBrushProperty = AvaloniaProperty.Register<PageCanvas, IBrush?>(nameof(DimBrush));

    /// <summary>Defines the <see cref="LabelBackground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> LabelBackgroundProperty = AvaloniaProperty.Register<PageCanvas, IBrush?>(nameof(LabelBackground));

    /// <summary>Defines the <see cref="LabelForeground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> LabelForegroundProperty = AvaloniaProperty.Register<PageCanvas, IBrush?>(nameof(LabelForeground));

    /// <summary>Defines the <see cref="SpokenBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> SpokenBrushProperty = AvaloniaProperty.Register<PageCanvas, IBrush?>(nameof(SpokenBrush));

    /// <summary>The gap between pages.</summary>
    private const double PageSpacing = 12;

    /// <summary>The thickness of the line under the word being read.</summary>
    private const double WordUnderline = 1.5;

    /// <summary>How far the focus band reaches above and below the sentence being read.</summary>
    private const double FocusBandMargin = 6;

    /// <summary>The margin around the pages.</summary>
    private const double ContentMargin = 16;

    /// <summary>How far beyond the viewport, in viewport heights, pages are prefetched.</summary>
    private const double PrefetchViewports = 0.75;

    /// <summary>Zoom change per wheel notch.</summary>
    private const double WheelZoomStep = 1.1;

    /// <summary>The hit tolerance for text, in points.</summary>
    private const float TextTolerance = 4F;

    /// <summary>The distance kept to the left of a navigation target.</summary>
    private const double TargetPadding = 24;

    /// <summary>Where in the viewport a navigation target is placed, as a fraction of its height from the top.</summary>
    private const double TargetViewportFraction = 0.3;

    /// <summary>The width of the outline marking the current search hit, so it differs by shape as well as colour.</summary>
    private const double CurrentHitOutlineWidth = 2;

    /// <summary>The page shadow offset.</summary>
    private const double ShadowOffset = 2;

    /// <summary>Half, for finding the middle of the viewport.</summary>
    private const double Half = 0.5;

    /// <summary>The pointer travel that turns a click into a drag.</summary>
    private const double DragThreshold = 3;

    /// <summary>The page shadow brush.</summary>
    private static readonly IBrush ShadowBrush = new SolidColorBrush(Color.FromArgb(0x40, 0, 0, 0));

    /// <summary>
    /// How page images are sampled, with aliased edges so tiles meet without seams. Only stand-ins are ever scaled: the
    /// preview and the last scale's tiles while a zoom settles. Bilinear with mipmaps draws those about ten times
    /// faster than cubic (BitmapDrawBenchmarks), which keeps the zoom ease smooth; sharp tiles are drawn 1:1.
    /// </summary>
    private static readonly RenderOptions PageRenderOptions = new() { BitmapInterpolationMode = BitmapInterpolationMode.MediumQuality, EdgeMode = EdgeMode.Aliased };

    /// <summary>The hand cursor for links.</summary>
    private static readonly Cursor HandCursor = new(StandardCursorType.Hand);

    /// <summary>The text cursor.</summary>
    private static readonly Cursor TextCursor = new(StandardCursorType.Ibeam);

    /// <summary>The pointer while measuring.</summary>
    private static readonly Cursor CrossCursor = new(StandardCursorType.Cross);

    /// <summary>Links by page, loaded lazily.</summary>
    private readonly Dictionary<int, IReadOnlyList<PageLink>> _links = [];

    /// <summary>Selection rectangles by page, computed lazily.</summary>
    private readonly Dictionary<int, List<PageRect>> _selectionRects = [];

    /// <summary>The current layout.</summary>
    private DocumentLayout _layout = DocumentLayout.Empty;

    /// <summary>The page sizes the layout was built from.</summary>
    private PageSize[] _sizes = [];

    /// <summary>The hosting scroll viewer.</summary>
    private ScrollViewer? _scroller;

    /// <summary>The tab currently wired up.</summary>
    private DocumentTabViewModel? _wiredTab;

    /// <summary>Subscriptions to the wired tab.</summary>
    private MultipleDisposable? _tabSubscriptions;

    /// <summary>Subscriptions to the hosting scroll viewer.</summary>
    private MultipleDisposable? _scrollerSubscriptions;

    /// <summary>Property and input subscriptions while attached.</summary>
    private MultipleDisposable? _controlSubscriptions;

    /// <summary>A position to restore once the layout has been measured.</summary>
    private Action? _pendingScroll;

    /// <summary>The pointer position where a press started.</summary>
    private Point _pressPoint;

    /// <summary>The link under the pointer when it was pressed.</summary>
    private PageLink? _pressedLink;

    /// <summary>Whether a text selection drag is active.</summary>
    private bool _selecting;

    /// <summary>The selection anchor (page, character).</summary>
    private (int Page, int Char) _selectionAnchor = (-1, -1);

    /// <summary>The selection focus (page, character).</summary>
    private (int Page, int Char) _selectionFocus = (-1, -1);

    /// <summary>The tile scale of the last frame whose visible pages were all sharp, drawn while newer tiles render.</summary>
    private float _fallbackScale;

    /// <summary>Whether every visible page in the frame being drawn had its sharp tiles.</summary>
    private bool _frameComplete;

    /// <summary>The pen outlining the current search hit, rebuilt when <see cref="CurrentHitOutline"/> changes.</summary>
    private IPen? _currentHitPen;

    /// <summary>Initializes static members of the <see cref="PageCanvas"/> class.</summary>
    static PageCanvas()
    {
        AffectsRender<PageCanvas>(TabProperty, HitBrushProperty, CurrentHitOutlineProperty, SelectionBrushProperty, SpokenBrushProperty, DimBrushProperty);
        AffectsRender<PageCanvas>(LabelBackgroundProperty, LabelForegroundProperty);
        FocusableProperty.OverrideDefaultValue<PageCanvas>(true);
    }

    /// <summary>Initializes a new instance of the <see cref="PageCanvas"/> class.</summary>
    public PageCanvas()
    {
        // These live as long as the control and only reference it, so they need no owner.
        _ = this.Events().AttachedToVisualTree.SubscribeSafe(_ => Attach(), OnError);
        _ = this.Events().DetachedFromVisualTree.SubscribeSafe(_ => Detach(), OnError);

        // Two fingers on a touch screen zoom the pages (PageCanvas.Pinch.cs).
        GestureRecognizers.Add(new PinchGestureRecognizer());
    }

    /// <summary>Gets or sets the tab to display.</summary>
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

    /// <summary>Gets or sets the outline of the current search hit.</summary>
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

    /// <summary>Gets or sets the translucent paper colour laid over text away from what is being read, for the focus band.</summary>
    public IBrush? DimBrush
    {
        get => GetValue(DimBrushProperty);
        set => SetValue(DimBrushProperty, value);
    }

    /// <summary>Gets or sets the soft fill marking the sentence being read aloud.</summary>
    public IBrush? SpokenBrush
    {
        get => GetValue(SpokenBrushProperty);
        set => SetValue(SpokenBrushProperty, value);
    }

    /// <summary>Gets the selected text.</summary>
    /// <returns>The text, or an empty string.</returns>
    public string GetSelectedText()
    {
        var document = Tab?.TryGetDocument();
        if (document is null || !TryGetSelection(out var start, out var end))
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
        _selectionAnchor = (-1, -1);
        _selectionFocus = (-1, -1);
        _selectionRects.Clear();
        InvalidateVisual();
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var tab = Tab;
        var document = tab?.TryGetDocument();
        if (tab is null || document is null || _scroller is null || _layout.PageCount == 0)
        {
            return;
        }

        var viewport = new Rect(_scroller.Offset.X, _scroller.Offset.Y, _scroller.Viewport.Width, _scroller.Viewport.Height);
        DrawPages(context, new(tab, document, tab.RenderHub, viewport, TopLevel.GetTopLevel(this)?.RenderScaling ?? 1, tab.CanvasClient.Advance()));
        DrawAnnotationOverlay(context, tab);
        DrawRegions(context, tab, viewport);
        DrawMeasurement(context, tab);
        DrawArea(context);
        DrawCaret(context, tab);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize) => new(_layout.ExtentWidth, _layout.ExtentHeight);

    /// <summary>Reports a failure in a subscription.</summary>
    /// <param name="error">The error.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OnError(Exception error) => Trace.TraceError(error.ToString());

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
        var key = TileKey.Preview(tab.Source.Id, page, tab.Rotation, tab.PageTone.Id);
        if (frame.Hub.Cache.TryGet(key, out var surface))
        {
            if (draw)
            {
                TilePresentation.Draw(context, surface, rect, smooth: true);
            }

            return;
        }

        var size = tab.Source.PageSizes[page];
        var scale = TileGrid.GetPreviewScale(size, tab.Rotation);
        TileGrid.GetPagePixelSize(size, tab.Rotation, scale, out var width, out var height);
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
        var key = new TileKey(tab.Source.Id, grid.Page, grid.ScaleKey, tab.Rotation, tab.PageTone.Id, (short)column, (short)row);
        TileGrid.GetTileSize(grid.PixelWidth, grid.PixelHeight, column, row, out var tileWidth, out var tileHeight);
        if (!frame.Hub.Cache.TryGet(key, out var surface))
        {
            if (request)
            {
                var info = new PageRenderInfo(grid.Page, grid.Scale, tab.Rotation, column * tileSize, row * tileSize, RenderFlags.Annotations);
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

        TilePresentation.Draw(context, surface, destination, smooth: false);
    }

    /// <summary>Finds the hosting scroll viewer and subscribes to it, to the tab and to input.</summary>
    private void Attach()
    {
        _scroller = this.FindAncestorOfType<ScrollViewer>();
        if (_scroller is { } scroller)
        {
            _scrollerSubscriptions =
            [
                scroller.WhenChanged(static x => x.Offset).Skip(1).SubscribeSafe(_ => OnScrolled(), OnError),
                scroller.WhenChanged(static x => x.Bounds).Select(static bounds => bounds.Size).DistinctUntilChanged().Skip(1).SubscribeSafe(_ => OnViewportResized(), OnError),

                // The extent changes after a layout pass; apply any pending scroll once the dispatcher is idle again.
                scroller.WhenChanged(static x => x.Extent).ObserveOn(RxSchedulers.MainThreadScheduler).SubscribeSafe(_ => ApplyPendingScroll(), OnError),
            ];
        }

        _controlSubscriptions =
        [
            this.WhenChanged(static x => x.Tab).SubscribeSafe(Wire, OnError),
            this.WhenChanged(static x => x.CurrentHitOutline)
                .SubscribeSafe(outline => _currentHitPen = outline is not null ? new ImmutablePen(outline.ToImmutable(), CurrentHitOutlineWidth) : null, OnError),

            // One failing gesture must not end the subscription, or the page would stop answering input.
            this.Events().PointerWheelChanged.SubscribeSafe(e => Guard(HandlePointerWheel, e), OnError),
            this.Events().PointerPressed.SubscribeSafe(e => Guard(HandlePointerPressed, e), OnError),
            this.Events().PointerMoved.SubscribeSafe(e => Guard(HandlePointerMoved, e), OnError),
            this.ObserveRouted(PointerReleasedEvent, handledEventsToo: true).SubscribeSafe(e => Guard(HandlePointerReleased, e), OnError),
            this.Events().KeyDown.SubscribeSafe(e => Guard(HandleKeyDown, e), OnError),

            // Moving around: pinch zoom, and Space held to move the pages (PageCanvas.Pinch.cs, PageCanvas.Hand.cs).
            this.Events().Pinch.SubscribeSafe(e => Guard(HandlePinch, e), OnError),
            this.Events().PinchEnded.SubscribeSafe(e => Guard(HandlePinchEnded, e), OnError),
            this.Events().PointerTouchPadGestureMagnify.SubscribeSafe(e => Guard(HandleMagnify, e), OnError),
            this.Events().KeyUp.SubscribeSafe(e => Guard(HandleKeyUp, e), OnError),
            this.Events().LostFocus.SubscribeSafe(_ => ReleaseSpace(), OnError),
        ];
    }

    /// <summary>Runs an input handler, reporting a failure instead of letting it end the input subscription.</summary>
    /// <typeparam name="T">The routed event's argument type.</typeparam>
    /// <param name="handler">The handler.</param>
    /// <param name="args">The routed event's data.</param>
    private void Guard<T>(Action<T> handler, T args)
        where T : RoutedEventArgs
    {
        try
        {
            handler(args);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // The gesture is abandoned: release any capture so the next press starts cleanly.
            OnError(error);
            (args as PointerEventArgs)?.Pointer.Capture(null);
            _selecting = false;
            _panning = false;
            _areaDragging = false;
            StopAutoScrollFrames();
            InvalidateVisual();
        }
    }

    /// <summary>Releases the scroll viewer, tab and input subscriptions.</summary>
    private void Detach()
    {
        _controlSubscriptions?.Dispose();
        _controlSubscriptions = null;
        StopAutoScrollFrames();
        _markPainter?.Dispose();
        _markPainter = null;
        _scrollerSubscriptions?.Dispose();
        _scrollerSubscriptions = null;
        _scroller = null;
        Wire(null);
    }

    /// <summary>Handles page stepping and pointer anchored zoom.</summary>
    /// <param name="e">The routed input.</param>
    private void HandlePointerWheel(PointerWheelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if ((e.KeyModifiers & KeyModifiers.Control) == 0 && StepPageByPage(-e.Delta.Y * LineStep))
        {
            e.Handled = true;
            return;
        }

        if ((e.KeyModifiers & KeyModifiers.Control) == 0 || Tab is not { } tab)
        {
            return;
        }

        ZoomByWheel(tab, Math.Pow(WheelZoomStep, e.Delta.Y), e.GetPosition(this));
        e.Handled = true;
    }

    /// <summary>Begins a selection, measurement or annotation.</summary>
    /// <param name="e">The routed input.</param>
    private void HandlePointerPressed(PointerPressedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        var point = e.GetCurrentPoint(this);

        // Auto-scroll, the hand and area tools, and moving the pages with Space or the middle button (PageCanvas.AutoScroll.cs).
        if (BeginPageToolPress(e, point))
        {
            return;
        }

        if (point.Properties.IsRightButtonPressed)
        {
            ShowContextMenu(point.Position);
            e.Handled = true;
            return;
        }

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        _ = Focus();
        _pressPoint = point.Position;
        if (BeginMeasurePress(point.Position, e) || BeginAnnotationPress(point.Position, e))
        {
            return;
        }

        _pressedLink = HitTestLink(point.Position, out _);
        ClearSelection();
        if (_pressedLink is not null || !TryHitTestCharacter(point.Position, out var page, out var character))
        {
            return;
        }

        _selectionAnchor = (page, character);
        _selectionFocus = (page, character);
        _caret = (page, character);
        _selecting = true;
        e.Pointer.Capture(this);
    }

    /// <summary>Updates pointer interaction and the hover cursor.</summary>
    /// <param name="e">The routed input.</param>
    private void HandlePointerMoved(PointerEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        var position = e.GetPosition(this);
        HoverRegion(position);
        if (ContinuePageTool(e, position) || ContinueMeasure(position) || ContinueStroke(position))
        {
            return;
        }

        if (_selecting)
        {
            if (TryHitTestCharacter(position, out var page, out var character))
            {
                _selectionFocus = (page, character);
                _selectionRects.Clear();
                InvalidateVisual();
            }

            return;
        }

        var cursor = TryHitTestCharacter(position, out _, out _) ? TextCursor : Cursor.Default;
        if (HitTestLink(position, out _) is not null || IsOverField(position))
        {
            cursor = HandCursor;
        }

        Cursor = cursor;
    }

    /// <summary>Completes a pointer interaction.</summary>
    /// <param name="e">The routed input.</param>
    private void HandlePointerReleased(PointerReleasedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        e.Pointer.Capture(null);
        if (EndPan(e.GetPosition(this)) || EndArea(e.GetPosition(this)) || EndAnnotationPress(e.GetPosition(this)))
        {
            return;
        }

        if (_selecting)
        {
            _selecting = false;
            ApplyMarkupTool();
        }

        var link = _pressedLink;
        _pressedLink = null;
        var travel = e.GetPosition(this) - _pressPoint;
        if (link is { } pressed && Math.Abs(travel.X) < DragThreshold && Math.Abs(travel.Y) < DragThreshold)
        {
            Tab?.Navigate(pressed.Target);
        }
    }

    /// <summary>Handles document keyboard actions.</summary>
    /// <param name="e">The routed input.</param>
    private void HandleKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        // A focused form field takes Space and Tab before they scroll or move between pages.
        if (HandleFormKey(e) || HandleNavigationKey(e))
        {
            e.Handled = true;
            return;
        }

        if (e.Key == Key.C && (e.KeyModifiers & KeyModifiers.Control) != 0)
        {
            CopyToClipboard(GetSelectedText());
            e.Handled = true;
            return;
        }

        if (!HandlePlacementKey(e) && !HandleMeasureKey(e.Key) && !HandleAnnotationKey(e) && !HandleCaretKey(e) && !HandlePageByPageKey(e))
        {
            return;
        }

        e.Handled = true;
    }

    /// <summary>Draws the pages around the viewport, and remembers the scale once every visible page is sharp.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="frame">The frame.</param>
    private void DrawPages(DrawingContext context, in FrameContext frame)
    {
        var viewport = frame.Viewport;
        var prefetch = viewport.Height * PrefetchViewports;
        _layout.GetVisiblePages(viewport.Top - prefetch, viewport.Bottom + prefetch, out var first, out var last);
        _frameComplete = true;

        // Aliased edges keep neighbouring tiles from showing seams where they meet at fractional positions.
        using (context.PushRenderOptions(PageRenderOptions))
        {
            for (var page = first; page <= last && page >= 0; page++)
            {
                DrawPage(context, frame, page);
            }
        }

        if (_frameComplete && !IsZoomEasing)
        {
            _fallbackScale = GetTileScale(frame.RenderScaling);
        }
    }

    /// <summary>Draws one page: shadow, background, preview, tiles and highlights.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="page">The page.</param>
    private void DrawPage(DrawingContext context, in FrameContext frame, int page)
    {
        var bounds = _layout.GetPageBounds(page);
        var rect = new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        var visible = rect.Intersects(frame.Viewport);
        if (visible)
        {
            context.FillRectangle(ShadowBrush, rect.Translate(new(ShadowOffset, ShadowOffset)));
            context.FillRectangle(PaperBrush.Get(frame.Tab.PageTone), rect);
        }

        var tiled = TryGetTileWindow(frame, page, rect, visible, out var grid, out var window);
        var tab = frame.Tab;
        var covered = tiled && visible && frame.Hub.Cache.Covers(new(tab.Source.Id, page, grid.ScaleKey, tab.Rotation, tab.PageTone.Id, 0, 0), window);
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
        }

        if (!visible)
        {
            return;
        }

        DrawHighlights(context, frame.Tab, page, bounds);
        DrawFocusBand(context, frame.Tab, page, bounds, new(bounds, _sizes[page], frame.Tab.Rotation, _layout.Options.Scale));
    }

    /// <summary>Gets the device pixels per point that tiles are rendered at.</summary>
    /// <param name="scaling">The device pixels per canvas unit.</param>
    /// <returns>The tile scale.</returns>
    private float GetTileScale(double scaling) => (float)Math.Min(_layout.Options.Scale * scaling, TileGrid.MaxScale);

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
        TileGrid.GetPagePixelSize(_sizes[page], frame.Tab.Rotation, scale, out var pixelWidth, out var pixelHeight);
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
        TileGrid.GetPagePixelSize(_sizes[page], tab.Rotation, scale, out var pixelWidth, out var pixelHeight);
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
                var key = new TileKey(tab.Source.Id, page, fallbackKey, tab.Rotation, tab.PageTone.Id, (short)column, (short)row);
                if (!frame.Hub.Cache.TryGet(key, out var surface))
                {
                    continue;
                }

                var destination = new Rect(rect.X + (column * tileSize * unitX), rect.Y + (row * tileSize * unitY), surface.Width * unitX, surface.Height * unitY);
                TilePresentation.Draw(context, surface, destination, smooth: true);
            }
        }
    }

    /// <summary>Draws search hits and the text selection on a page.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The page.</param>
    /// <param name="bounds">The page bounds.</param>
    private void DrawHighlights(DrawingContext context, DocumentTabViewModel tab, int page, in LayoutRect bounds)
    {
        var transform = new PageTransform(bounds, _sizes[page], tab.Rotation, _layout.Options.Scale);
        if (tab.Search.GetHits(page) is { } hits)
        {
            var current = tab.Search.CurrentHit;
            var fill = HitBrush;
            foreach (var hit in hits)
            {
                var pen = ReferenceEquals(hit, current) ? _currentHitPen : null;
                foreach (var rect in hit.Bounds)
                {
                    context.DrawRectangle(fill, pen, transform.ToCanvas(rect));
                }
            }
        }

        DrawSpoken(context, tab, page, transform);
        if (SelectionBrush is not { } selection)
        {
            return;
        }

        foreach (var rect in GetSelectionRects(page))
        {
            context.FillRectangle(selection, transform.ToCanvas(rect));
        }
    }

    /// <summary>Softly marks the sentence being read aloud.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The page.</param>
    /// <param name="transform">The page transform.</param>
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
    /// <param name="bounds">The page's bounds on the canvas.</param>
    /// <param name="transform">The page transform.</param>
    private void DrawFocusBand(DrawingContext context, DocumentTabViewModel tab, int page, in LayoutRect bounds, in PageTransform transform)
    {
        var reader = tab.ReadAloud;
        if (DimBrush is not { } dim || !tab.FocusMode.FocusBand || reader.SpokenPage < 0 || reader.SpokenBounds.Count == 0)
        {
            return;
        }

        var pageRect = new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
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

    /// <summary>Gets the selection rectangles on a page, computing them on first use.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The rectangles.</returns>
    private List<PageRect> GetSelectionRects(int page)
    {
        if (_selectionRects.TryGetValue(page, out var cached))
        {
            return cached;
        }

        var rects = new List<PageRect>();
        var document = Tab?.TryGetDocument();
        if (document is not null && TryGetSelection(out var start, out var end) && page >= start.Page && page <= end.Page)
        {
            var first = page == start.Page ? start.Char : 0;
            var last = page == end.Page ? end.Char : document.GetCharacterCount(page) - 1;
            if (last >= first)
            {
                document.GetTextBounds(page, first, last - first + 1, rects);
            }
        }

        _selectionRects[page] = rects;
        return rects;
    }

    /// <summary>Gets the normalised selection.</summary>
    /// <param name="start">The start.</param>
    /// <param name="end">The end.</param>
    /// <returns><see langword="true"/> when there is a selection.</returns>
    private bool TryGetSelection(out (int Page, int Char) start, out (int Page, int Char) end)
    {
        start = _selectionAnchor;
        end = _selectionFocus;
        if (start.Page < 0 || end.Page < 0 || start == end)
        {
            return false;
        }

        if (end.Page < start.Page || (end.Page == start.Page && end.Char < start.Char))
        {
            (start, end) = (end, start);
        }

        return true;
    }

    /// <summary>Finds the character under a canvas point.</summary>
    /// <param name="position">The canvas point.</param>
    /// <param name="page">The page.</param>
    /// <param name="character">The character index.</param>
    /// <returns><see langword="true"/> when over text.</returns>
    private bool TryHitTestCharacter(Point position, out int page, out int character)
    {
        character = -1;
        page = _layout.HitTest(position.X, position.Y);
        var tab = Tab;
        var document = tab?.TryGetDocument();
        if (page < 0 || tab is null || document is null)
        {
            return false;
        }

        var transform = new PageTransform(_layout.GetPageBounds(page), _sizes[page], tab.Rotation, _layout.Options.Scale);
        character = document.GetCharacterIndexAt(page, transform.ToPage(position), TextTolerance);
        return character >= 0;
    }

    /// <summary>Finds the link under a canvas point.</summary>
    /// <param name="position">The canvas point.</param>
    /// <param name="page">The page.</param>
    /// <returns>The link, if any.</returns>
    private PageLink? HitTestLink(Point position, out int page)
    {
        page = _layout.HitTest(position.X, position.Y);
        var tab = Tab;
        if (page < 0 || tab is null)
        {
            return null;
        }

        ref var links = ref CollectionsMarshal.GetValueRefOrAddDefault(_links, page, out _);
        links ??= tab.TryGetDocument()?.GetLinks(page) ?? [];

        var point = new PageTransform(_layout.GetPageBounds(page), _sizes[page], tab.Rotation, _layout.Options.Scale).ToPage(position);
        foreach (var link in links)
        {
            if (link.Bounds.Contains(point))
            {
                return link;
            }
        }

        return null;
    }

    /// <summary>Subscribes to a tab's changes, replacing any previous subscriptions.</summary>
    /// <param name="tab">The tab.</param>
    private void Wire(DocumentTabViewModel? tab)
    {
        _tabSubscriptions?.Dispose();
        _tabSubscriptions = null;
        if (_wiredTab is { } old)
        {
            _ = old.CanvasClient.Advance();
            old.IsAutoScrolling = false;
        }

        _wiredTab = tab;
        _links.Clear();
        ClearSelection();
        if (tab is null)
        {
            _layout = DocumentLayout.Empty;
            return;
        }

        _tabSubscriptions =
        [

            // The zoom only matters to the layout in Free mode, so it is part of the key only then.
            tab.WhenChanged(
                    static x => x.ZoomMode,
                    static x => x.LayoutMode,
                    static x => x.Rotation,
                    static x => x.IsPageByPage,
                    static x => x.Zoom,
                    static (mode, layout, rotation, pageByPage, zoom) => (mode, layout, rotation, pageByPage, Zoom: mode == ZoomMode.Free ? zoom : 0))
                .DistinctUntilChanged()
                .Skip(1)
                .SubscribeSafe(_ => OnLayoutSettingsChanged(), OnError),
            tab.WhenChanged(static x => x.PageTone).Skip(1).SubscribeSafe(_ => InvalidateVisual(), OnError),
            tab.NavigationRequests.SubscribeSafe(OnNavigationRequested, OnError),
            tab.DocumentChanges.SubscribeSafe(_ => OnDocumentChanged(), OnError),
            tab.Search.HighlightChanges.SubscribeSafe(_ => InvalidateVisual(), OnError),
            tab.ReadAloud.WhenChanged(static x => x.SpokenBounds).Skip(1).SubscribeSafe(_ => InvalidateVisual(), OnError),
            tab.ReadAloud.MarksChanged.SubscribeSafe(_ => InvalidateVisual(), OnError),
            tab.FocusMode.WhenChanged(static x => x.FocusBand).Skip(1).SubscribeSafe(_ => InvalidateVisual(), OnError),
            tab.RenderHub.TilesArrived.SubscribeSafe(_ => InvalidateVisual(), OnError),
            tab.PageEdits.SubscribeSafe(_ => InvalidateVisual(), OnError),
            tab.CopyRequests.SubscribeSafe(CopyToClipboard, OnError),

            // The selection belongs to the canvas, so it clears once a menu command has used it.
            tab.Annotations.MarkSelectionCommand.Where(static marked => marked).SubscribeSafe(_ => ClearSelection(), OnError),
            tab.ReadAloud.ReadFromCharacterCommand.SubscribeSafe(_ => ClearSelection(), OnError),
            tab.WhenChanged(static x => x.IsCaretMode).Skip(1).SubscribeSafe(on => OnCaretModeChanged(tab, on), OnError),
            tab.Annotations.WhenChanged(static x => x.Selected).Skip(1).SubscribeSafe(_ => InvalidateVisual(), OnError),
            tab.FillAndSign.WhenChanged(static x => x.Placement).Skip(1).SubscribeSafe(_ => InvalidateVisual(), OnError),

            // Moving around and grabbing content (PageCanvas.Navigation.cs, PageCanvas.AutoScroll.cs, PageCanvas.Area.cs).
            tab.SelectAllRequests.SubscribeSafe(_ => OnSelectAllRequested(), OnError),
            tab.WhenChanged(static x => x.IsAutoScrolling).SubscribeSafe(OnAutoScrollChanged, OnError),
            tab.WhenChanged(static x => x.PageTool).Skip(1).SubscribeSafe(_ => OnPageToolChanged(), OnError),
        ];
        var position = tab.Position;
        RebuildLayout();
        ScrollToPosition(position);
    }

    /// <summary>Recomputes the layout from the tab's settings and the viewport.</summary>
    private void RebuildLayout()
    {
        var tab = Tab;
        _sizes = tab?.Source.PageSizes ?? [];
        if (tab is null || _sizes.Length == 0 || _scroller is null)
        {
            _layout = DocumentLayout.Empty;
            InvalidateMeasure();
            return;
        }

        var viewport = _scroller.Bounds.Size;
        var zoom = tab.Zoom;
        if (tab.ZoomMode != ZoomMode.Free)
        {
            zoom = ZoomCalculator.GetFitZoom(_sizes, new(tab.Rotation, tab.LayoutMode, tab.ZoomMode, viewport.Width, viewport.Height, PageSpacing, ContentMargin));
            tab.ReportZoom(zoom);
        }

        _layout = DocumentLayout.Create(
            _sizes,
            new(tab.Rotation, tab.LayoutMode, zoom * ZoomCalculator.PixelsPerPoint, PageSpacing, ContentMargin, viewport.Width) { PageByPage = tab.IsPageByPage, ViewportHeight = viewport.Height });
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Scrolls to a remembered position once laid out.</summary>
    /// <param name="position">The position.</param>
    private void ScrollToPosition(DocumentPosition position)
    {
        _pendingScroll = () =>
        {
            if (_scroller is null || position.PageIndex < 0 || position.PageIndex >= _layout.PageCount)
            {
                return;
            }

            _scroller.Offset = new(_scroller.Offset.X, GetPageScrollTop(position.PageIndex, position.OffsetFraction));
        };
        SchedulePendingScroll();
    }

    /// <summary>Applies the pending scroll after the next layout pass, covering layouts whose extent did not change.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SchedulePendingScroll() =>
        _ = Signal.Timer(TimeSpan.Zero, RxSchedulers.MainThreadScheduler).SubscribeSafe(_ => ApplyPendingScroll(), OnError);

    /// <summary>Reports the current position to the tab.</summary>
    private void ReportPosition()
    {
        if (_scroller is null || Tab is not { } tab || _layout.PageCount == 0)
        {
            return;
        }

        var top = _scroller.Offset.Y + ContentMargin;
        GetCurrentPages(tab, top, _scroller.Offset.Y + (_scroller.Viewport.Height * Half), out var topPage, out var middlePage);
        var bounds = _layout.GetPageBounds(topPage);
        var fraction = bounds.Height > 0 ? Math.Clamp((top - bounds.Y) / bounds.Height, 0, 1) : 0;
        tab.ReportPosition(new(topPage, fraction), middlePage);
    }

    /// <summary>Applies a pending scroll once the scroll viewer's extent matches the layout.</summary>
    private void ApplyPendingScroll()
    {
        if (_pendingScroll is null || _scroller is null || Math.Abs(_scroller.Extent.Height - _layout.ExtentHeight) > 1)
        {
            return;
        }

        var scroll = _pendingScroll;
        _pendingScroll = null;
        scroll();
        ReportPosition();
    }

    /// <summary>Repaints and reports the position after scrolling.</summary>
    private void OnScrolled()
    {
        InvalidateVisual();
        if (_pendingScroll is null)
        {
            ReportPosition();
        }
    }

    /// <summary>Relayouts when the viewport size changes, keeping the current position.</summary>
    private void OnViewportResized()
    {
        if (Tab is not { } tab)
        {
            return;
        }

        var position = tab.Position;
        RebuildLayout();
        ScrollToPosition(position);
    }

    /// <summary>Relayouts when zoom, arrangement or rotation change, keeping the current position.</summary>
    private void OnLayoutSettingsChanged()
    {
        if (Tab is not { } tab)
        {
            return;
        }

        var position = tab.Position;
        var hasAnchor = _anchoring || _pendingScroll is not null;
        RebuildLayout();
        if (!hasAnchor)
        {
            ScrollToPosition(position);
        }
    }

    /// <summary>Scrolls to a navigation target.</summary>
    /// <param name="request">The request.</param>
    private void OnNavigationRequested(NavigationRequest request)
    {
        if (_scroller is null || Tab is not { } tab || request.PageIndex < 0 || request.PageIndex >= _layout.PageCount)
        {
            return;
        }

        var bounds = _layout.GetPageBounds(request.PageIndex);
        _requestedPage = request.PageIndex;
        if (request.Target is { } target)
        {
            var transform = new PageTransform(bounds, _sizes[request.PageIndex], tab.Rotation, _layout.Options.Scale);
            var area = transform.ToCanvas(target);
            var x = area.Width > 0 && (area.X < _scroller.Offset.X || area.Right > _scroller.Offset.X + _scroller.Viewport.Width)
                ? area.X - TargetPadding
                : _scroller.Offset.X;
            _scroller.Offset = new(x, ClampToSlot(request.PageIndex, area.Y - (_scroller.Viewport.Height * TargetViewportFraction)));
        }
        else
        {
            _scroller.Offset = new(_scroller.Offset.X, GetPageScrollTop(request.PageIndex, request.OffsetFraction));
        }

        ReportPosition();
    }

    /// <summary>Relayouts after the document loads or reloads.</summary>
    private void OnDocumentChanged()
    {
        _links.Clear();
        ClearSelection();
        var position = Tab?.Position ?? default;
        RebuildLayout();
        ScrollToPosition(position);
    }
}
