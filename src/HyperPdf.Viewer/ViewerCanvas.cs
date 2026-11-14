// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Layout;
using HyperPdf.Presentation.Canvas;
using HyperPdf.Presentation.Input;
using HyperPdf.ViewModels.Services;
using HyperPdf.ViewModels.Viewing;
using HyperPdf.Viewer.Chrome;
using HyperPdf.Windowing.Windows;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;

namespace HyperPdf.Viewer;

/// <summary>
/// The page canvas of a native window. It owns the scroll offset, hands the window's input to the shared canvas state and draws each
/// frame through the window's render backend. Everything here runs on the thread that pumps the window.
/// </summary>
[DebuggerDisplay("ViewerCanvas: offset ({_offset.X}, {_offset.Y})")]
public sealed class ViewerCanvas : ICanvasHost, IDisposable
{
    /// <summary>The window.</summary>
    private readonly IPlatformWindow _window;

    /// <summary>The painter, made on the first frame and reused for every frame after it.</summary>
    private ViewerFramePainter? _painter;

    /// <summary>The click counter that tells a double click from two clicks.</summary>
    private ClickCounter _clicks;

    /// <summary>The streams of the tab shown, replaced when another tab is shown.</summary>
    private MultipleDisposable _tabStreams = [];

    /// <summary>The scroll offset in canvas units.</summary>
    private CanvasPoint _offset;

    /// <summary>The tab shown.</summary>
    private DocumentTabViewModel? _tab;

    /// <summary>The part of the window the pages are shown in, in device pixels.</summary>
    private LayoutRect _bounds;

    /// <summary>The command the right-click menu copies selected text with, made on first use.</summary>
    private CopyTextCommand? _copy;

    /// <summary>Whether <see cref="_bounds"/> has been read from the window.</summary>
    private bool _boundsRead;

    /// <summary>Whether the layout changed since the last frame, so a waiting scroll can be applied.</summary>
    private bool _layoutChanged;

    /// <summary>Whether a text selection is being dragged past the edge, which scrolls a little every frame.</summary>
    private bool _selectionDrag;

    /// <summary>Initializes a new instance of the <see cref="ViewerCanvas"/> class.</summary>
    /// <param name="window">The window the canvas fills.</param>
    public ViewerCanvas(IPlatformWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        _window = window;
    }

    /// <summary>Gets the shared canvas state, made on first use.</summary>
    public PageCanvasState State => field ??= new(this);

    /// <summary>Gets or sets the colours frames are drawn with.</summary>
    public ViewerColors Colors { get; set; }

    /// <summary>Gets the scroll offset in canvas units.</summary>
    public CanvasPoint Offset => _offset;

    /// <inheritdoc/>
    public bool HasScroller => true;

    /// <inheritdoc/>
    public bool IsInWindow => true;

    /// <inheritdoc/>
    public CanvasPoint ScrollOffset
    {
        get => _offset;
        set => ScrollTo(value);
    }

    /// <summary>Gets or sets the window's controls, which are drawn over the pages and leave them the middle of the window.</summary>
    public ViewerChrome? Chrome { get; set; }

    /// <summary>Gets the part of the window the pages are shown in, in device pixels.</summary>
    public LayoutRect Bounds
    {
        get
        {
            if (!_boundsRead)
            {
                _ = ReadBounds();
            }

            return _bounds;
        }
    }

    /// <summary>Gets where the top left corner of the page area is inside the window, in canvas units.</summary>
    public CanvasPoint Origin
    {
        get
        {
            var scale = _window.Scale;
            return new(_bounds.X / scale, _bounds.Y / scale);
        }
    }

    /// <summary>Gets the canvas point drawn at the window's top left corner: the scroll offset moved back by the page area's origin.</summary>
    public CanvasPoint DrawOffset
    {
        get
        {
            var origin = Origin;
            return new(_offset.X - origin.X, _offset.Y - origin.Y);
        }
    }

    /// <inheritdoc/>
    public CanvasSize ViewportSize
    {
        get
        {
            if (!_boundsRead)
            {
                _ = ReadBounds();
            }

            var scale = _window.Scale;
            return new(_bounds.Width / scale, _bounds.Height / scale);
        }
    }

    /// <inheritdoc/>
    public CanvasSize ExtentSize => new(State.Layout.ExtentWidth, State.Layout.ExtentHeight);

    /// <inheritdoc/>
    public CanvasSize ScrollerSize => ViewportSize;

    /// <summary>Gets or sets where the pointer last was inside the window, in canvas units. Wheel zoom stays put around it.</summary>
    internal CanvasPoint PointerInWindow { get; set; }

    /// <summary>Shows a tab, or no tab.</summary>
    /// <param name="tab">The tab, or <see langword="null"/> for the empty window.</param>
    public void ShowTab(DocumentTabViewModel? tab)
    {
        if (_tab is { } old && !ReferenceEquals(old, tab))
        {
            _ = old.CanvasClient.Advance();
            old.PageTools.IsAutoScrolling = false;
        }

        _tabStreams.Dispose();
        _tabStreams = [];
        State.Render.ForgetRequest();
        _tab = tab;
        State.Tab = tab;
        State.Links.Clear();
        State.ClearSelection();
        if (tab is null)
        {
            State.UseLayout(DocumentLayout.Empty, null);
            _window.RequestFrame();
            return;
        }

        FollowTab(tab);
        var position = tab.Position;
        State.RebuildLayout();
        State.ScrollToPosition(position);
        _window.RequestFrame();
    }

    /// <summary>Handles one window event.</summary>
    /// <param name="windowEvent">The event.</param>
    public void Handle(in WindowEvent windowEvent)
    {
        switch (windowEvent.Kind)
        {
            case WindowEventKind.Resized:
                {
                    _ = ReadBounds();
                    State.RelayoutInPlace();
                    _window.RequestFrame();
                    break;
                }

            case WindowEventKind.Exposed:
                {
                    DrawFrame();
                    break;
                }

            case WindowEventKind.Scrolled:
                {
                    ViewerCanvasInput.Scroll(this, windowEvent);
                    break;
                }

            default:
                {
                    Apply(ViewerCanvasInput.Route(this, ref _clicks, windowEvent));
                    break;
                }
        }
    }

    /// <summary>Draws one frame: steps what is moving, then draws the pages and what lies over them.</summary>
    public void DrawFrame()
    {
        if (ReadBounds())
        {
            // The page area changed size without a resize event, as when the controls are first laid out.
            State.RelayoutInPlace();
        }

        var moving = StepMotion();
        using (var pass = _window.Backend.BeginFrame())
        {
            pass.Clear(Colors.Canvas);
            if (_tab is { } tab && tab.TryGetDocument() is { } document && State.Layout.PageCount > 0)
            {
                var view = ViewportSize;
                var area = new LayoutRect(_offset.X, _offset.Y, view.Width, view.Height);
                var scale = _window.Scale;
                var frame = new FrameContext(tab, document, tab.RenderHub, area, scale, State.GetRenderGeneration(tab, document, area, scale));
                var painter = _painter ??= new(State);
                painter.Start(pass, Colors, DrawOffset, scale, Chrome?.Text);
                State.DrawPages(painter, frame);
                State.DrawMarks(painter, tab, area);
                State.DrawAnnotationOverlay(painter, tab, area);
                State.DrawRegions(painter, tab, area);
                State.DrawMeasurement(painter, tab);
                State.DrawArea(painter);
                State.DrawCaret(painter, tab);
                painter.Finish();
            }

            // The controls are opaque and drawn last, so pages that reach under the bars are covered.
            Chrome?.Draw(pass);
        }

        if (moving)
        {
            _window.RequestFrame();
        }
    }

    /// <inheritdoc/>
    public void InvalidateMeasure()
    {
        // The extent is read from the layout, so it is already current. A scroll that waited for it is applied before the next frame.
        _layoutChanged = true;
        _window.RequestFrame();
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void InvalidateVisual() => _window.RequestFrame();

    /// <inheritdoc/>
    public void UpdateLayout()
    {
        // There is no layout pass to force: the extent follows the layout as soon as it changes.
    }

    /// <inheritdoc/>
    public void PageScaleChanged(double before, double after)
    {
        // Nothing follows the page scale in the native heads.
    }

    /// <inheritdoc/>
    public void WheelZoomChanged(WheelZoomEase before, WheelZoomEase after)
    {
        if (after.IsRunning)
        {
            _window.RequestFrame();
        }
    }

    /// <inheritdoc/>
    public void SelectionDragChanged(bool dragging)
    {
        _selectionDrag = dragging;
        if (dragging)
        {
            _window.RequestFrame();
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _tabStreams.Dispose();

    /// <summary>Turns a position in device pixels into canvas units inside the window.</summary>
    /// <param name="x">The x in device pixels.</param>
    /// <param name="y">The y in device pixels.</param>
    /// <returns>The position in the window, in canvas units.</returns>
    internal CanvasPoint ToWindowPoint(int x, int y)
    {
        var scale = _window.Scale;
        return new((x - _bounds.X) / scale, (y - _bounds.Y) / scale);
    }

    /// <summary>Moves the scroll offset by an amount, kept inside the document.</summary>
    /// <param name="x">The sideways amount in canvas units.</param>
    /// <param name="y">The up and down amount in canvas units.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ScrollBy(double x, double y) => ScrollTo(new(_offset.X + x, _offset.Y + y));

    /// <summary>Does what the canvas state asked for after an input event.</summary>
    /// <param name="effects">The effects, or <see langword="null"/> when the event was not routed.</param>
    private void Apply(InputEffects? effects)
    {
        if (effects is null)
        {
            return;
        }

        if (effects.Cursor is { } cursor)
        {
            _window.SetCursor(cursor);
        }

        if (effects.CopyText is { Length: > 0 } text)
        {
            _window.SetClipboardText(text);
        }

        if (effects.ShowContextMenu)
        {
            ShowContextMenu();
        }
    }

    /// <summary>Opens the right-click menu for what is under the pointer.</summary>
    private void ShowContextMenu()
    {
        var pointer = PointerInWindow;
        if (Chrome is not { } chrome || State.GetContextMenu(new(pointer.X + _offset.X, pointer.Y + _offset.Y), _copy ??= new(_window)) is not { } entries)
        {
            return;
        }

        var scale = _window.Scale;
        chrome.ShowMenu(entries, new((int)(_bounds.X + (pointer.X * scale)), (int)(_bounds.Y + (pointer.Y * scale))));
    }

    /// <summary>Reads the part of the window left for the pages.</summary>
    /// <returns><see langword="true"/> when it changed.</returns>
    private bool ReadBounds()
    {
        var size = _window.ClientSize;
        var bounds = Chrome?.CanvasBounds ?? new LayoutRect(0, 0, size.Width, size.Height);
        var changed = _boundsRead && bounds != _bounds;
        _bounds = bounds;
        _boundsRead = true;
        return changed;
    }

    /// <summary>Follows the streams of the tab shown.</summary>
    /// <param name="tab">The tab.</param>
    private void FollowTab(DocumentTabViewModel tab)
    {
        _tabStreams.Add(tab.CanvasRedraws.SubscribeSafe(_ => _window.RequestFrame(), ErrorTrace.Write));
        _tabStreams.Add(tab.MarkRedraws.SubscribeSafe(_ => _window.RequestFrame(), ErrorTrace.Write));
        _tabStreams.Add(tab.LayoutSettingsChanges.SubscribeSafe(_ => State.RelayoutForSettings(), ErrorTrace.Write));
        _tabStreams.Add(tab.Destinations.SubscribeSafe(State.ScrollTo, ErrorTrace.Write));
        _tabStreams.Add(tab.PageReads.SubscribeSafe(_ => ReadPagesAgain(), ErrorTrace.Write));
        _tabStreams.Add(tab.SelectionUses.SubscribeSafe(_ => State.ClearSelection(), ErrorTrace.Write));
        _tabStreams.Add(tab.CaretModeStarts.SubscribeSafe(State.StartCaret, ErrorTrace.Write));
        _tabStreams.Add(tab.PageToolChanges.SubscribeSafe(_ => ShowPageTool(), ErrorTrace.Write));
    }

    /// <summary>Lays the pages out again after the document's pages were read again.</summary>
    private void ReadPagesAgain()
    {
        State.Links.Clear();
        State.ClearSelection();
        State.RelayoutAfterPagesChange();
    }

    /// <summary>Shows the pointer of the page tool that was chosen.</summary>
    private void ShowPageTool()
    {
        State.Tools.AreaDragging = false;
        _window.SetCursor(State.GetToolCursor());
        _window.RequestFrame();
    }

    /// <summary>Steps the zoom ease, a dragged selection and a waiting scroll.</summary>
    /// <returns><see langword="true"/> when something is still moving and needs another frame.</returns>
    private bool StepMotion()
    {
        if (_layoutChanged)
        {
            _layoutChanged = false;
            State.ApplyPendingScroll();
            ScrollTo(_offset);
        }

        var moving = State.IsZoomEasing;
        if (moving)
        {
            State.StepZoom(State.WheelZoom);
        }

        if (_selectionDrag)
        {
            _ = State.StepSelectionDrag();
            moving = true;
        }

        return moving;
    }

    /// <summary>Sets the scroll offset, kept inside the document, and reports the new position when it moved.</summary>
    /// <param name="wanted">The offset asked for, in canvas units.</param>
    private void ScrollTo(CanvasPoint wanted)
    {
        var view = ViewportSize;
        var clamped = new CanvasPoint(
            Math.Clamp(wanted.X, 0, Math.Max(0, State.Layout.ExtentWidth - view.Width)),
            Math.Clamp(wanted.Y, 0, Math.Max(0, State.Layout.ExtentHeight - view.Height)));
        if (clamped == _offset)
        {
            return;
        }

        _offset = clamped;
        _window.RequestFrame();
        if (State.PendingScroll is null)
        {
            State.ReportPosition();
        }
    }
}
