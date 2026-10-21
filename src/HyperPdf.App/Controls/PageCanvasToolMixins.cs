// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool AutoScrollBy(double distance) => canvas.State.AutoScrollBy(distance);

        /// <summary>Moves the pages with the pointer while panning.</summary>
        /// <param name="e">The pointer movement.</param>
        /// <returns><see langword="true"/> while the pages are being moved.</returns>
        internal bool ContinuePan(PointerEventArgs e)
        {
            if (!canvas.Panning || canvas.Scroller is not { } scroller)
            {
                return false;
            }

            var pointer = e.GetPosition(scroller);
            return canvas.State.ContinuePan(new(pointer.X, pointer.Y));
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

            canvas.State.Tools.Panning = false;
            canvas.Cursor = canvas.ToolCursor();
            canvas.ReportPosition();
            var link = canvas.PressedLink;
            canvas.PressedLink = null;
            if (link is { } pressed && CanvasMetrics.IsClick(new(canvas.PressPoint.X, canvas.PressPoint.Y), new(position.X, position.Y)))
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
            if (!canvas.State.PressSpace(e.ViewerKey, e.ViewerModifiers, out var started))
            {
                return false;
            }

            if (started)
            {
                canvas.Cursor = PageCanvas.HandCursor;
            }

            return true;
        }

        /// <summary>Handles letting go of Space: page by page turns the page unless the pages were moved while it was held.</summary>
        /// <param name="e">The key release.</param>
        internal void CompleteSpaceHold(KeyEventArgs e)
        {
            if (canvas.State.EndSpaceHold())
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ReleaseSpace() => canvas.State.ReleaseSpace();

        /// <summary>Gets the area between where the drag started and where it is now.</summary>
        /// <returns>The area.</returns>
        internal Rect GetArea()
        {
            var area = canvas.State.GetArea();
            return new(area.X, area.Y, area.Width, area.Height);
        }

        /// <summary>Gets the pointer for the chosen tool when it is not over anything special.</summary>
        /// <returns>The cursor, or <see langword="null"/> for select text, whose pointer follows what is under it.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Cursor? ToolCursor() => PointerRouting.GetToolCursor(canvas.SpaceHeld, canvas.Tab?.PageTools.PageTool).ToCursor();

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

            canvas.AreaEnd = position;
            var area = canvas.GetArea();
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

        /// <summary>Matches a pointer press to an active page tool or measure action.</summary>
        /// <param name="item">The pointer press and tab.</param>
        /// <returns>The matched press action, or <see langword="null"/> when the press belongs to another handler.</returns>
        private PageToolPress? TryMatchPageToolPress(Injected<PointerPressedEventArgs, DocumentTabViewModel> item)
        {
            var (e, tab) = item;
            var point = e.GetCurrentPoint(canvas);
            switch (PointerRouting.GetPageToolPress(tab, point.ViewerButtons, canvas.Scroller is not null, canvas.SpaceHeld))
            {
                case PageToolPressKind.StopAutoScroll:
                {
                    return new(canvas, tab, e, point, PageToolPress.StopAutoScroll);
                }

                case PageToolPressKind.Pan:
                {
                    return new(canvas, tab, e, point, PageToolPress.Pan);
                }

                case PageToolPressKind.Area:
                {
                    return new(canvas, tab, e, point, PageToolPress.Area);
                }

                case PageToolPressKind.Measure:
                {
                    _ = canvas.Focus();
                    canvas.PressPoint = point.Position;
                    return new(canvas, tab, e, point, PageToolPress.Measure);
                }

                case PageToolPressKind.Focus:
                {
                    _ = canvas.Focus();
                    canvas.PressPoint = point.Position;
                    return null;
                }

                default:
                {
                    return null;
                }
            }
        }
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
            var start = Args.GetPosition(scroller);
            Canvas.State.BeginPan(new(start.X, start.Y));
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
