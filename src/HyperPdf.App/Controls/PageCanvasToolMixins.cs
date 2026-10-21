// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Measuring;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.ObservableEvents;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Controls;

/// <summary>
/// Reactive streams and drawing helpers for the page tools on <see cref="PageCanvas"/>: auto-scroll, moving the pages
/// with the Hand tool, middle mouse button or Space, dragging a Zoom to Area or Snapshot rectangle, and the Measure tool.
/// </summary>
internal static class PageCanvasToolMixins
{
    /// <summary>A double click.</summary>
    internal const int DoubleClick = 2;

    /// <summary>The radius of a measured point's dot.</summary>
    private const double MeasurePointRadius = 3;

    /// <summary>The size of the measurement label's text.</summary>
    private const double MeasureLabelSize = 13;

    /// <summary>How far the label sits from the last point.</summary>
    private const double MeasureLabelOffset = 10;

    /// <summary>The padding around the label's text.</summary>
    private const double MeasureLabelPadding = 4;

    /// <summary>Adds the page tool, auto-scroll, pan, area and measure streams and helpers to a page canvas.</summary>
    /// <param name="canvas">The page canvas.</param>
    extension(PageCanvas canvas)
    {
        /// <summary>Gets the Space key releases while Space was held on the canvas.</summary>
        internal IObservable<KeyEventArgs> SpaceKeyUps => canvas.Events().KeyUp
            .WhereWith(canvas, static (c, e) => e.Key == Key.Space && c.SpaceHeld);

        /// <summary>Gets the animation frames while auto-scroll is active on the current tab.</summary>
        /// <param name="tabs">The tab stream.</param>
        /// <returns>The frame timestamps while auto-scrolling.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<TimeSpan> AutoScrollFrames(IObservable<DocumentTabViewModel?> tabs) =>
            tabs.Follow(static tab => tab.AutoScrolling)
                .DistinctUntilChanged()
                .SelectWith(canvas, static (c, on) => on ? Signal.Defer(c.StartGlide) : Signal.Never<TimeSpan>())
                .SwitchMap(static frames => frames);

        /// <summary>
        /// Gets the pointer presses handled by auto-scroll, the Hand tool, middle/Space panning, the Area tools, or the
        /// Measure tool, and prepares left-button presses by focusing the canvas and recording the press origin.
        /// </summary>
        /// <param name="tabs">The tab stream.</param>
        /// <returns>The matching page tool presses.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<PageToolPress> PageToolPresses(IObservable<DocumentTabViewModel?> tabs) =>
            canvas.PointerPresses
                .Where(static e => !e.Handled)
                .Inject(tabs)
                .Pick(canvas.TryMatchPageToolPress);

        /// <summary>
        /// Starts a glide: the keyboard comes to the pages so the arrows change the speed and Escape stops, and the window's
        /// frames move the pages until the subscription ends.
        /// </summary>
        /// <returns>The frames, or none when the canvas is not in a window.</returns>
        internal IObservable<TimeSpan> StartGlide()
        {
            if (TopLevel.GetTopLevel(canvas) is not { } top)
            {
                return Signal.Never<TimeSpan>();
            }

            _ = canvas.Focus();
            canvas.AutoScroller.Reset();
            canvas.AutoScrollLast = Stopwatch.GetTimestamp();
            _ = canvas.SetAutoScrollRunning(1);

            // Finally has no state-passing form, so the end of the glide reaches the flag through this closure.
            return top.AnimationFrames.Finally(() => canvas.SetAutoScrollRunning(0));
        }

        /// <summary>Moves the pages on by the time since the last frame, and ends auto-scroll at the end of the document.</summary>
        internal void StepAutoScroll()
        {
            if (canvas.Scroller is null || canvas.Tab is not { PageTools.IsAutoScrolling: true } tab)
            {
                return;
            }

            var now = Stopwatch.GetTimestamp();
            var seconds = Stopwatch.GetElapsedTime(canvas.AutoScrollLast, now).TotalSeconds;
            canvas.AutoScrollLast = now;
            var distance = canvas.AutoScroller.Advance(seconds, tab.PageTools.AutoScrollSpeed, tab.ReduceMotion);
            if (distance > 0 && !canvas.AutoScrollBy(distance))
            {
                // Turning auto-scroll off ends the glide's frames.
                tab.PageTools.IsAutoScrolling = false;
            }
        }

        /// <summary>Scrolls down, page by page within the page shown and then to the next page.</summary>
        /// <param name="distance">How far.</param>
        /// <returns><see langword="true"/> when the pages moved; <see langword="false"/> at the end.</returns>
        internal bool AutoScrollBy(double distance)
        {
            if (canvas.Scroller is not { } scroller)
            {
                return false;
            }

            var before = scroller.Offset.Y;
            if (canvas.StepPageByPage(distance))
            {
                return scroller.Offset.Y > before;
            }

            var end = Math.Max(0, scroller.Extent.Height - scroller.Viewport.Height);
            if (before >= end - PageCanvasZoomMixins.EdgeTolerance)
            {
                return false;
            }

            scroller.Offset = new(scroller.Offset.X, Math.Min(end, before + distance));
            canvas.ReportPosition();
            return true;
        }

        /// <summary>Moves the pages with the pointer while panning.</summary>
        /// <param name="e">The pointer movement.</param>
        /// <returns><see langword="true"/> while the pages are being moved.</returns>
        internal bool ContinuePan(PointerEventArgs e)
        {
            if (!canvas.Panning || canvas.Scroller is not { } scroller)
            {
                return false;
            }

            var moved = e.GetPosition(scroller) - canvas.PanStart;
            var y = canvas.PanOffset.Y - moved.Y;
            if (canvas.Layout.Options.PageByPage && canvas.Layout.PageCount > 0)
            {
                // Page by page, a drag moves within the page shown and never shows half of the next one.
                y = canvas.ClampToSlot(canvas.Layout.GetPageNearest(canvas.PanOffset.Y + PageCanvasZoomMixins.EdgeTolerance), y);
            }

            scroller.Offset = new(canvas.PanOffset.X - moved.X, y);
            return true;
        }

        /// <summary>Ends moving the pages, following a link when the Hand tool clicked one without moving.</summary>
        /// <param name="position">The canvas point.</param>
        /// <returns><see langword="true"/> when a drag was moving the pages.</returns>
        internal bool EndPan(Point position)
        {
            if (!canvas.Panning)
            {
                return false;
            }

            canvas.Panning = false;
            canvas.Cursor = canvas.ToolCursor();
            canvas.ReportPosition();
            var link = canvas.PressedLink;
            canvas.PressedLink = null;
            var travel = position - canvas.PressPoint;
            if (link is { } pressed && Math.Abs(travel.X) < PageCanvas.DragThreshold && Math.Abs(travel.Y) < PageCanvas.DragThreshold)
            {
                canvas.Tab?.Navigate(pressed.Target);
            }

            return true;
        }

        /// <summary>Notes that Space is held, so a drag moves the pages.</summary>
        /// <param name="e">The key press.</param>
        /// <returns><see langword="true"/> when the key was Space.</returns>
        internal bool PressSpace(KeyEventArgs e)
        {
            if (e.Key != Key.Space || (e.KeyModifiers & ~KeyModifiers.Shift) != 0)
            {
                return false;
            }

            // Held keys repeat; only the first press starts a hold.
            if (!canvas.SpaceHeld)
            {
                canvas.SpaceHeld = true;
                canvas.SpacePanned = false;
                canvas.Cursor = PageCanvas.HandCursor;
            }

            return true;
        }

        /// <summary>Handles letting go of Space: page by page turns the page unless the pages were moved while it was held.</summary>
        /// <param name="e">The key release.</param>
        internal void CompleteSpaceHold(KeyEventArgs e)
        {
            canvas.SpaceHeld = false;
            if (!canvas.SpacePanned && !canvas.Panning)
            {
                _ = canvas.HandlePageByPageKey(e);
            }

            if (!canvas.Panning)
            {
                canvas.Cursor = canvas.ToolCursor();
            }

            e.Handled = true;
        }

        /// <summary>Forgets a held Space when the pages lose the keyboard, as its release will not arrive.</summary>
        internal void ReleaseSpace()
        {
            canvas.SpaceHeld = false;
            canvas.SpacePanned = false;
        }

        /// <summary>Gets the pointer for the chosen tool when it is not over anything special.</summary>
        /// <returns>The cursor, or <see langword="null"/> for select text, whose pointer follows what is under it.</returns>
        internal Cursor? ToolCursor() =>
            canvas.SpaceHeld ? PageCanvas.HandCursor : canvas.Tab?.PageTools.PageTool switch
            {
                PageTool.Hand => PageCanvas.HandCursor,
                PageTool.ZoomArea or PageTool.Snapshot => PageCanvas.CrossCursor,
                _ => null,
            };

        /// <summary>Moves the pages or the area with the pointer, or shows the chosen tool's pointer while hovering.</summary>
        /// <param name="e">The pointer movement.</param>
        /// <param name="position">The canvas point.</param>
        /// <returns><see langword="true"/> when the movement was used.</returns>
        internal bool ContinuePageTool(PointerEventArgs e, Point position)
        {
            if (canvas.ContinuePan(e) || canvas.ContinueArea(position))
            {
                return true;
            }

            // Only a hover shows the tool's pointer; a drag that is selecting, drawing or measuring keeps its own.
            if (canvas.Selecting || canvas.Tab is null or { Measure.IsOn: true } || e.GetCurrentPoint(canvas).Properties.IsLeftButtonPressed || canvas.ToolCursor() is not { } cursor)
            {
                return false;
            }

            canvas.Cursor = cursor;
            return true;
        }

        /// <summary>Moves the far corner of the area with the pointer.</summary>
        /// <param name="position">The canvas point.</param>
        /// <returns><see langword="true"/> while an area is being dragged.</returns>
        internal bool ContinueArea(Point position)
        {
            if (!canvas.AreaDragging)
            {
                return false;
            }

            canvas.AreaEnd = position;
            canvas.InvalidateVisual();
            return true;
        }

        /// <summary>Finishes the area: zooms to it or copies a picture of it.</summary>
        /// <param name="position">The canvas point.</param>
        /// <returns><see langword="true"/> when an area was being dragged.</returns>
        internal bool EndArea(Point position)
        {
            if (!canvas.AreaDragging)
            {
                return false;
            }

            canvas.AreaDragging = false;
            canvas.InvalidateVisual();
            if (canvas.Tab is not { } tab)
            {
                return true;
            }

            var area = Between(canvas.AreaStart, position);
            if (tab.PageTools.PageTool == PageTool.ZoomArea)
            {
                canvas.ZoomToArea(tab, area);
            }
            else
            {
                _ = canvas.Snapshotter.SnapshotAsync(canvas, tab, area);
            }

            return true;
        }

        /// <summary>Handles Escape for the moving and area tools: cancels a drag, or goes back to selecting text.</summary>
        /// <param name="tab">The tab.</param>
        /// <returns><see langword="true"/> when Escape was used.</returns>
        internal bool EscapePageTool(DocumentTabViewModel tab)
        {
            if (canvas.AreaDragging)
            {
                canvas.AreaDragging = false;
                canvas.InvalidateVisual();
                return true;
            }

            if (tab.PageTools.PageTool == PageTool.SelectText)
            {
                return false;
            }

            tab.PageTools.PageTool = PageTool.SelectText;
            canvas.Cursor = null;
            return true;
        }

        /// <summary>Draws the outline of the area being dragged.</summary>
        /// <param name="context">The drawing context.</param>
        internal void DrawArea(DrawingContext context)
        {
            if (!canvas.AreaDragging || canvas.CurrentHitPen is not { } pen)
            {
                return;
            }

            context.DrawRectangle(null, pen, Between(canvas.AreaStart, canvas.AreaEnd));
        }

        /// <summary>Moves the measurement's last point with the pointer.</summary>
        /// <param name="position">The canvas point.</param>
        /// <returns><see langword="true"/> while measuring.</returns>
        internal bool ContinueMeasure(Point position)
        {
            if (canvas.Tab is not { Measure: { IsOn: true } measure } tab)
            {
                return false;
            }

            canvas.Cursor = PageCanvas.CrossCursor;
            var page = canvas.Layout.HitTest(position.X, position.Y);
            if (page >= 0 && measure.MovePoint(page, canvas.ToPage(tab, page, position)))
            {
                canvas.InvalidateVisual();
            }

            return true;
        }

        /// <summary>Handles Enter (finish) and Escape (clear) while measuring.</summary>
        /// <param name="e">The key press.</param>
        /// <returns><see langword="true"/> when the key was used.</returns>
        internal bool HandleMeasureKey(KeyEventArgs e)
        {
            if (canvas.Tab is not { } tab || !DocumentKeys.HandleMeasure(tab.Measure, e.ViewerKey))
            {
                return false;
            }

            canvas.InvalidateVisual();
            return true;
        }

        /// <summary>Draws the measurement: its lines, its points and the measurement beside the last point.</summary>
        /// <param name="context">The drawing context.</param>
        /// <param name="tab">The tab.</param>
        internal void DrawMeasurement(DrawingContext context, DocumentTabViewModel tab)
        {
            var measure = tab.Measure;
            var points = measure.Points;
            if (!measure.IsOn || measure.Page < 0 || measure.Page >= canvas.Sizes.Length || points.Count == 0 || canvas.CurrentHitPen is not { } pen)
            {
                return;
            }

            var transform = new PageTransform(canvas.Layout.GetPageBounds(measure.Page), canvas.Sizes[measure.Page], tab.Rotation, canvas.Layout.Options.Scale);
            var last = DrawMeasureLines(context, pen, transform, points, measure.Mode == MeasureMode.Area);
            if (measure.Result.Length > 0)
            {
                DrawMeasureLabel(canvas, context, pen, measure.Result, last);
            }
        }

        /// <summary>Matches a pointer press to an active page tool or measure action.</summary>
        /// <param name="item">The pointer press and tab.</param>
        /// <returns>The matched press action, or <see langword="null"/> when the press belongs to another handler.</returns>
        private PageToolPress? TryMatchPageToolPress(Injected<PointerPressedEventArgs, DocumentTabViewModel> item)
        {
            var (e, tab) = item;
            var point = e.GetCurrentPoint(canvas);
            if (tab.PageTools.IsAutoScrolling)
            {
                return new(canvas, tab, e, point, PageToolPress.StopAutoScroll);
            }

            if (canvas.IsPanPress(point, tab))
            {
                return new(canvas, tab, e, point, PageToolPress.Pan);
            }

            if (!point.Properties.IsLeftButtonPressed || point.Properties.IsRightButtonPressed)
            {
                return null;
            }

            if (tab.PageTools.PageTool is PageTool.ZoomArea or PageTool.Snapshot)
            {
                return new(canvas, tab, e, point, PageToolPress.Area);
            }

            _ = canvas.Focus();
            canvas.PressPoint = point.Position;
            return tab.Measure.IsOn ? new(canvas, tab, e, point, PageToolPress.Measure) : null;
        }

        /// <summary>Determines whether a pointer press starts panning the pages.</summary>
        /// <param name="point">The pointer point.</param>
        /// <param name="tab">The active tab.</param>
        /// <returns><see langword="true"/> when the press starts panning.</returns>
        private bool IsPanPress(PointerPoint point, DocumentTabViewModel tab) =>
            canvas.Scroller is not null
            && (point.Properties.IsMiddleButtonPressed || (point.Properties.IsLeftButtonPressed && (canvas.SpaceHeld || tab.PageTools.PageTool == PageTool.Hand)));
    }

    /// <summary>Gets the rectangle between two corners, whichever way it was dragged.</summary>
    /// <param name="first">One corner.</param>
    /// <param name="second">The opposite corner.</param>
    /// <returns>The rectangle.</returns>
    private static Rect Between(Point first, Point second) =>
        new(Math.Min(first.X, second.X), Math.Min(first.Y, second.Y), Math.Abs(second.X - first.X), Math.Abs(second.Y - first.Y));

    /// <summary>Draws a measurement's lines and points.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="pen">The pen.</param>
    /// <param name="transform">The page transform.</param>
    /// <param name="points">The points, in page space.</param>
    /// <param name="closed">Whether the shape closes back to its first point.</param>
    /// <returns>The last point, in canvas space.</returns>
    private static Point DrawMeasureLines(DrawingContext context, IPen pen, in PageTransform transform, IReadOnlyList<PagePoint> points, bool closed)
    {
        var previous = transform.ToCanvas(points[0]);
        var first = previous;
        for (var i = 1; i < points.Count; i++)
        {
            var next = transform.ToCanvas(points[i]);
            context.DrawLine(pen, previous, next);
            previous = next;
        }

        if (closed && points.Count > 2)
        {
            context.DrawLine(pen, previous, first);
        }

        foreach (var point in points)
        {
            context.DrawEllipse(pen.Brush, null, transform.ToCanvas(point), MeasurePointRadius, MeasurePointRadius);
        }

        return previous;
    }

    /// <summary>Draws the measurement beside the last point, in the window's colours, framed by the measuring line's colour.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="context">The drawing context.</param>
    /// <param name="outline">The measurement's pen, which frames the label.</param>
    /// <param name="result">The measurement.</param>
    /// <param name="last">The last point, in canvas space.</param>
    private static void DrawMeasureLabel(PageCanvas canvas, DrawingContext context, IPen outline, string result, Point last)
    {
        var text = new FormattedText(result, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, MeasureLabelSize, canvas.LabelForeground ?? outline.Brush);
        var origin = last + new Vector(MeasureLabelOffset, MeasureLabelOffset);
        var box = new Rect(origin, new Size(text.Width, text.Height)).Inflate(MeasureLabelPadding);
        context.DrawRectangle(canvas.LabelBackground, outline, box, MeasureLabelPadding, MeasureLabelPadding);
        context.DrawText(text, origin);
    }

    /// <summary>A pointer press claimed by auto-scroll, panning, area dragging or measuring.</summary>
    /// <param name="Canvas">The page canvas.</param>
    /// <param name="Tab">The active tab.</param>
    /// <param name="Args">The pointer event arguments.</param>
    /// <param name="Point">The pointer point on the canvas.</param>
    /// <param name="Kind">The matched tool action.</param>
    internal readonly record struct PageToolPress(PageCanvas Canvas, DocumentTabViewModel Tab, PointerPressedEventArgs Args, PointerPoint Point, int Kind)
    {
        /// <summary>Stops an active auto-scroll glide.</summary>
        internal const int StopAutoScroll = 0;

        /// <summary>Starts panning the pages.</summary>
        internal const int Pan = 1;

        /// <summary>Starts dragging a zoom or snapshot area.</summary>
        internal const int Area = 2;

        /// <summary>Adds or finishes a measurement point.</summary>
        internal const int Measure = 3;

        /// <summary>The pointer while the pages are being moved.</summary>
        private static readonly Cursor MoveCursor = new(StandardCursorType.SizeAll);

        /// <summary>Runs the matched page tool press and marks the event handled.</summary>
        internal void Begin()
        {
            switch (Kind)
            {
                case StopAutoScroll:
                {
                    Tab.PageTools.IsAutoScrolling = false;
                    _ = Canvas.Focus();
                    Args.Handled = true;
                    break;
                }

                case Pan:
                {
                    BeginPan();
                    break;
                }

                case Area:
                {
                    BeginArea();
                    break;
                }

                case Measure:
                {
                    BeginMeasure();
                    break;
                }

                default:
                {
                    break;
                }
            }
        }

        /// <summary>Captures the pointer and begins panning the pages.</summary>
        private void BeginPan()
        {
            if (Canvas.Scroller is not { } scroller)
            {
                return;
            }

            _ = Canvas.Focus();
            Canvas.PressPoint = Point.Position;
            Canvas.PressedLink = Point.Properties.IsLeftButtonPressed && !Canvas.SpaceHeld ? Canvas.HitTestLink(Point.Position, out _) : null;
            Canvas.Panning = true;
            Canvas.SpacePanned |= Canvas.SpaceHeld;
            Canvas.PanStart = Args.GetPosition(scroller);
            Canvas.PanOffset = scroller.Offset;
            Canvas.Cursor = MoveCursor;
            Args.Pointer.Capture(Canvas);
            Args.Handled = true;
        }

        /// <summary>Captures the pointer and begins dragging a zoom or snapshot area.</summary>
        private void BeginArea()
        {
            _ = Canvas.Focus();
            Canvas.AreaStart = Point.Position;
            Canvas.AreaEnd = Point.Position;
            Canvas.AreaDragging = true;
            Args.Pointer.Capture(Canvas);
            Args.Handled = true;
            Canvas.InvalidateVisual();
        }

        /// <summary>Adds a measurement point or finishes the measurement on a double-click.</summary>
        private void BeginMeasure()
        {
            var page = Canvas.Layout.HitTest(Point.Position.X, Point.Position.Y);
            if (page >= 0)
            {
                if (Args.ClickCount >= DoubleClick)
                {
                    Tab.Measure.Finish();
                }
                else
                {
                    Tab.Measure.AddPoint(page, Canvas.ToPage(Tab, page, Point.Position));
                }

                Canvas.InvalidateVisual();
            }

            Args.Handled = true;
        }
    }
}
