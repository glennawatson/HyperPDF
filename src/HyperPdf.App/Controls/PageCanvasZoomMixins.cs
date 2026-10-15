// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Layout;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.ObservableEvents;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Controls;

/// <summary>Reactive zoom, pinch, wheel and page-by-page streams and layout offset helpers for <see cref="PageCanvas"/> and its hosting <see cref="ScrollViewer"/>.</summary>
internal static class PageCanvasZoomMixins
{
    /// <summary>How far one wheel notch or arrow key scrolls within a slot.</summary>
    internal const double LineStep = 48;

    /// <summary>How close to a slot edge counts as at the edge.</summary>
    internal const double EdgeTolerance = 0.5;

    /// <summary>Zoom change per wheel notch.</summary>
    private const double WheelZoomFactor = 1.1;

    /// <summary>The power of the ease out curve.</summary>
    private const double ZoomEasePower = 3;

    /// <summary>How long one wheel zoom takes to ease to its target.</summary>
    private static readonly TimeSpan ZoomDuration = TimeSpan.FromMilliseconds(140);

    /// <summary>Adds the zoom, pinch and page-by-page streams and helpers to a page canvas.</summary>
    /// <param name="canvas">The page canvas.</param>
    extension(PageCanvas canvas)
    {
        /// <summary>Gets the frames of the wheel zoom easing now, each carrying the ease; none once it settles.</summary>
        internal IObservable<WheelZoomEase> WheelZoomFrames => canvas.WhenChanged(static c => c.WheelZoom)
            .SelectWith(canvas, static (c, ease) => ZoomFrames(c, ease))
            .SwitchMap(static frames => frames);

        /// <summary>Gets the non-Ctrl wheel turns that step page by page.</summary>
        internal IObservable<PointerWheelEventArgs> PageByPageWheels => canvas.PointerWheels
            .Where(static e => (e.KeyModifiers & KeyModifiers.Control) == 0)
            .WhereWith(canvas, static (c, e) => c.StepPageByPage(-e.Delta.Y * LineStep));

        /// <summary>Gets the moments a touch pinch ends.</summary>
        internal IObservable<PinchEndedEventArgs> PinchEnds => canvas.Events().PinchEnded;

        /// <summary>Gets the touch pinch steps, paired with the active tab.</summary>
        /// <param name="tabs">The tab stream.</param>
        /// <returns>The pinch steps.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<PinchZoomStep> PinchZooms(IObservable<DocumentTabViewModel?> tabs) =>
            canvas.Events().Pinch
                .Where(static e => e.Scale > 0)
                .Inject(tabs)
                .SelectWith(canvas, static (c, item) => new PinchZoomStep(c, item.State, item.Value));

        /// <summary>Gets the touchpad magnify steps, marked handled and paired with the active tab.</summary>
        /// <param name="tabs">The tab stream.</param>
        /// <returns>The wheel zoom steps.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<WheelZoomStep> MagnifyZooms(IObservable<DocumentTabViewModel?> tabs) =>
            canvas.Events().PointerTouchPadGestureMagnify
                .Where(static e => 1 + e.Delta.X > 0)
                .MarkHandled()
                .Inject(tabs)
                .SelectWith(canvas, static (c, item) => new WheelZoomStep(c, item.State, 1 + item.Value.Delta.X, item.Value.GetPosition(c)));

        /// <summary>Ends a touch pinch and redraws.</summary>
        internal void EndPinch()
        {
            canvas.PinchStartZoom = 0;
            canvas.InvalidateVisual();
        }

        /// <summary>Starts or extends a wheel zoom towards a new factor.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="factor">The zoom multiplier.</param>
        /// <param name="anchor">The canvas point under the pointer.</param>
        internal void ZoomByWheel(DocumentTabViewModel tab, double factor, Point anchor)
        {
            if (canvas.Scroller is not { } scroller)
            {
                return;
            }

            var target = ZoomCalculator.Clamp((canvas.IsZoomEasing ? canvas.WheelZoom.To : tab.Zoom) * factor);
            if (tab.ReduceMotion || TopLevel.GetTopLevel(canvas) is null)
            {
                canvas.ZoomAround(tab, target, anchor);
                return;
            }

            // The ease starts from the zoom shown now, so a notch while easing carries on smoothly towards the new target.
            canvas.WheelZoom = new(true, tab.Zoom, target, Stopwatch.GetTimestamp(), anchor - scroller.Offset);
        }

        /// <summary>Moves a wheel zoom one frame on, and settles it at its target once its time is up.</summary>
        /// <param name="ease">The ease.</param>
        internal void StepZoom(WheelZoomEase ease)
        {
            if (canvas.Scroller is not { } scroller || canvas.Tab is not { } tab)
            {
                canvas.WheelZoom = ease with { IsRunning = false };
                return;
            }

            var progress = Math.Min(1, Stopwatch.GetElapsedTime(ease.Started) / ZoomDuration);

            // Cubic ease out, applied to the zoom ratio so each frame scales by the same proportion.
            var eased = 1 - Math.Pow(1 - progress, ZoomEasePower);
            var zoom = ease.From * Math.Pow(ease.To / ease.From, eased);
            var done = progress >= 1;
            if (done)
            {
                // Settled before the last step, so the tiles of the final zoom are requested.
                canvas.WheelZoom = ease with { IsRunning = false };
            }

            canvas.ZoomAround(tab, done ? ease.To : zoom, ease.Anchor + scroller.Offset);
            if (done)
            {
                canvas.InvalidateVisual();
            }
        }

        /// <summary>Zooms keeping the point under the pointer fixed, with the new layout and offset in the same frame.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="zoom">The new zoom.</param>
        /// <param name="anchor">The canvas point to keep fixed.</param>
        internal void ZoomAround(DocumentTabViewModel tab, double zoom, Point anchor)
        {
            if (canvas.Scroller is not { } scroller)
            {
                return;
            }

            var page = canvas.Layout.HitTest(anchor.X, anchor.Y);
            if (page < 0)
            {
                page = canvas.Layout.GetPageNearest(anchor.Y);
            }

            if (page < 0)
            {
                tab.SetZoom(zoom);
                return;
            }

            var pagePoint = new PageTransform(canvas.Layout.GetPageBounds(page), canvas.Sizes[page], tab.Rotation, canvas.Layout.Options.Scale).ToPage(anchor);
            var screenOffset = anchor - scroller.Offset;
            canvas.Anchoring = true;
            try
            {
                tab.SetZoom(zoom);

                // The layout was rebuilt by the zoom change; measuring now lets the offset follow before the next frame,
                // so no frame shows the new size at the old offset.
                scroller.UpdateLayout();
                var target = new PageTransform(canvas.Layout.GetPageBounds(page), canvas.Sizes[page], tab.Rotation, canvas.Layout.Options.Scale).ToCanvas(pagePoint);
                scroller.Offset = new(target.X - screenOffset.X, target.Y - screenOffset.Y);
            }
            finally
            {
                canvas.Anchoring = false;
            }

            canvas.ReportPosition();
        }

        /// <summary>Zooms until an area fills the view, with the area's middle in the middle of the view.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="area">The area, in canvas coordinates.</param>
        internal void ZoomToArea(DocumentTabViewModel tab, in Rect area)
        {
            if (canvas.Scroller is not { } scroller)
            {
                return;
            }

            var viewport = scroller.Viewport;
            var centre = area.Center;
            var zoom = AreaZoom.GetZoom(tab.Zoom, area.Width, area.Height, viewport.Width, viewport.Height);
            var page = canvas.Layout.HitTest(centre.X, centre.Y);
            if (page < 0)
            {
                page = canvas.Layout.GetPageNearest(centre.Y);
            }

            if (page < 0)
            {
                tab.SetZoom(zoom);
                return;
            }

            var pagePoint = canvas.ToPage(tab, page, centre);
            canvas.Anchoring = true;
            try
            {
                tab.SetZoom(zoom);

                // Measured now so the offset follows the new size in the same frame.
                scroller.UpdateLayout();
                var target = new PageTransform(canvas.Layout.GetPageBounds(page), canvas.Sizes[page], tab.Rotation, canvas.Layout.Options.Scale).ToCanvas(pagePoint);
                viewport = scroller.Viewport;
                var extent = scroller.Extent;
                var y = canvas.ClampToSlot(page, AreaZoom.GetCentredOffset(target.Y, viewport.Height, extent.Height));
                scroller.Offset = new(AreaZoom.GetCentredOffset(target.X, viewport.Width, extent.Width), y);
            }
            finally
            {
                canvas.Anchoring = false;
            }

            canvas.ReportPosition();
        }

        /// <summary>Gets the vertical offset that shows a position on a page, kept inside the page's slot when page by page.</summary>
        /// <param name="pageIndex">The page.</param>
        /// <param name="offsetFraction">How far down the page, from 0 to 1.</param>
        /// <returns>The offset.</returns>
        internal double GetPageScrollTop(int pageIndex, double offsetFraction)
        {
            var bounds = canvas.Layout.GetPageBounds(pageIndex);
            return canvas.ClampToSlot(pageIndex, bounds.Y + (bounds.Height * offsetFraction) - PageCanvas.ContentMargin);
        }

        /// <summary>Keeps an offset inside a page's slot when page by page.</summary>
        /// <param name="pageIndex">The page.</param>
        /// <param name="offset">The offset.</param>
        /// <returns>The offset, clamped when page by page.</returns>
        internal double ClampToSlot(int pageIndex, double offset)
        {
            if (!canvas.Layout.Options.PageByPage || canvas.Scroller is not { } scroller)
            {
                return offset;
            }

            canvas.Layout.GetRowExtent(pageIndex, out var top, out var bottom);
            return Math.Clamp(offset, top, Math.Max(top, bottom - scroller.Viewport.Height));
        }

        /// <summary>Scrolls page by page: within the current slot, or to the neighbouring slot from its edge.</summary>
        /// <param name="delta">How far to move; positive is down.</param>
        /// <returns><see langword="true"/> when page by page handled the movement.</returns>
        internal bool StepPageByPage(double delta)
        {
            if (canvas.Scroller is not { } scroller || !canvas.Layout.Options.PageByPage || canvas.Layout.PageCount == 0)
            {
                return false;
            }

            var y = scroller.Offset.Y;
            var viewport = scroller.Viewport.Height;
            canvas.Layout.GetRowExtent(canvas.Layout.GetPageNearest(y + EdgeTolerance), out var top, out var bottom);
            var last = Math.Max(top, bottom - viewport);
            double target;
            if (delta > 0)
            {
                target = y < last - EdgeTolerance ? Math.Min(y + delta, last) : bottom;
                if (target >= canvas.Layout.ExtentHeight - EdgeTolerance)
                {
                    return true;
                }
            }
            else if (y > top + EdgeTolerance)
            {
                target = Math.Max(y + delta, top);
            }
            else if (top > 0)
            {
                // Show the end of the previous slot, which for a page that fits is the whole page.
                canvas.Layout.GetRowExtent(canvas.Layout.GetPageNearest(top - EdgeTolerance), out var previousTop, out var previousBottom);
                target = Math.Max(previousTop, previousBottom - viewport);
            }
            else
            {
                return true;
            }

            scroller.Offset = new(scroller.Offset.X, target);
            canvas.ReportPosition();
            return true;
        }

        /// <summary>Handles the scrolling keys when page by page.</summary>
        /// <param name="e">The key event.</param>
        /// <returns><see langword="true"/> when handled.</returns>
        internal bool HandlePageByPageKey(KeyEventArgs e)
        {
            if (!canvas.Layout.Options.PageByPage || canvas.Scroller is not { } scroller || (e.KeyModifiers & ~KeyModifiers.Shift) != 0)
            {
                return false;
            }

            var page = scroller.Viewport.Height;
            var shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
            return e.Key switch
            {
                Key.Down => canvas.StepPageByPage(LineStep),
                Key.Up => canvas.StepPageByPage(-LineStep),
                Key.PageDown or Key.Right => canvas.StepPageByPage(page),
                Key.PageUp or Key.Left => canvas.StepPageByPage(-page),
                Key.Space => canvas.StepPageByPage(shift ? -page : page),
                _ => false,
            };
        }

        /// <summary>Gets the pages to report: the requested or current page wherever its row is the row found.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="top">The vertical offset of the top of the content in view.</param>
        /// <param name="middle">The vertical offset of the middle of the viewport.</param>
        /// <param name="topPage">The page at the top of the viewport.</param>
        /// <param name="middlePage">The page in the middle of the viewport, shown as the current page.</param>
        internal void GetCurrentPages(DocumentTabViewModel tab, double top, double middle, out int topPage, out int middlePage)
        {
            var preferred = canvas.RequestedPage >= 0 ? canvas.RequestedPage : tab.CurrentPageIndex;
            canvas.RequestedPage = -1;
            topPage = canvas.Layout.GetCurrentPage(top, preferred);
            middlePage = canvas.Layout.GetCurrentPage(middle, preferred);
        }
    }

    /// <summary>Adds the viewport and tunnelling Ctrl+wheel streams to a scroll viewer.</summary>
    /// <param name="scroller">The scroll viewer.</param>
    extension(ScrollViewer scroller)
    {
        /// <summary>Gets the distinct viewport size changes after the initial size.</summary>
        internal IObservable<Size> ViewportSizes => scroller.WhenChanged(static x => x.Bounds)
            .Select(static bounds => bounds.Size)
            .DistinctUntilChanged()
            .Skip(1);

        /// <summary>Gets the moments after a layout pass when a pending scroll can be applied.</summary>
        /// <param name="canvas">The page canvas.</param>
        /// <returns>The layout completion signals on the main thread.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<RxVoid> PendingScrollReady(PageCanvas canvas) =>
            Signal.Merge(
                    scroller.WhenChanged(static x => x.Extent).AsVoid,
                    canvas.Events().LayoutUpdated.WhereWith(canvas, static (c, _) => c.PendingScroll is not null).AsVoid)
                .ObserveOn(RxSchedulers.MainThreadScheduler);

        /// <summary>Gets the Ctrl+wheel zoom steps seen on the scroll viewer's tunnelling route, marked handled.</summary>
        /// <param name="canvas">The page canvas.</param>
        /// <param name="tabs">The tab stream.</param>
        /// <returns>The wheel zoom steps.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<WheelZoomStep> CtrlWheelZooms(PageCanvas canvas, IObservable<DocumentTabViewModel?> tabs) =>
            scroller.TunnelWheels
                .Where(static e => (e.KeyModifiers & KeyModifiers.Control) != 0 && !e.Handled)
                .MarkHandled()
                .Inject(tabs)
                .SelectWith(canvas, static (c, item) => new WheelZoomStep(c, item.State, Math.Pow(WheelZoomFactor, item.Value.Delta.Y), item.Value.GetPosition(c)));
    }

    /// <summary>Gets the animation frames for an active wheel zoom ease.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="ease">The ease state.</param>
    /// <returns>The frames, or none when settled.</returns>
    private static IObservable<WheelZoomEase> ZoomFrames(PageCanvas canvas, in WheelZoomEase ease) =>
        ease.IsRunning && TopLevel.GetTopLevel(canvas) is { } top
            ? top.AnimationFrames.SelectWith(ease, static (running, _) => running)
            : Signal.Never<WheelZoomEase>();

    /// <summary>A wheel zoom easing from one zoom to another over the window's frames.</summary>
    /// <param name="IsRunning">Whether it is still easing.</param>
    /// <param name="From">The zoom it started from.</param>
    /// <param name="To">The zoom it ends at.</param>
    /// <param name="Started">When it started, as a <see cref="Stopwatch"/> timestamp.</param>
    /// <param name="Anchor">The point in viewport coordinates that stays still while the zoom eases.</param>
    internal readonly record struct WheelZoomEase(bool IsRunning, double From, double To, long Started, Point Anchor);

    /// <summary>A wheel or touchpad magnify zoom step around a canvas anchor.</summary>
    /// <param name="Canvas">The page canvas.</param>
    /// <param name="Tab">The active tab.</param>
    /// <param name="Factor">The zoom multiplier.</param>
    /// <param name="Anchor">The canvas anchor point.</param>
    internal readonly record struct WheelZoomStep(PageCanvas Canvas, DocumentTabViewModel Tab, double Factor, Point Anchor)
    {
        /// <summary>Applies the wheel zoom step.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Apply() => Canvas.ZoomByWheel(Tab, Factor, Anchor);
    }

    /// <summary>A touch pinch zoom step around the point between the fingers.</summary>
    /// <param name="Canvas">The page canvas.</param>
    /// <param name="Tab">The active tab.</param>
    /// <param name="Args">The pinch event arguments.</param>
    internal readonly record struct PinchZoomStep(PageCanvas Canvas, DocumentTabViewModel Tab, PinchEventArgs Args)
    {
        /// <summary>Applies the touch pinch zoom step and marks the event handled.</summary>
        internal void Apply()
        {
            if (Canvas.PinchStartZoom <= 0)
            {
                Canvas.PinchStartZoom = Tab.Zoom;
            }

            Canvas.ZoomAround(Tab, ZoomCalculator.Clamp(Canvas.PinchStartZoom * Args.Scale), Args.ScaleOrigin);
            Args.Handled = true;
        }
    }
}
