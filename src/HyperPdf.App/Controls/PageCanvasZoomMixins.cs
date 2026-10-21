// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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

        /// <summary>Gets the wheel turns that move a horizontal layout sideways.</summary>
        internal IObservable<PointerWheelEventArgs> HorizontalWheels => canvas.PointerWheels
            .Where(static e => (e.KeyModifiers & KeyModifiers.Control) == 0 && e.Delta.Y != 0)
            .WhereWith(canvas, static (c, e) => c.StepSideways(-e.Delta.Y * LineStep));

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ZoomByWheel(DocumentTabViewModel tab, double factor, Point anchor) => canvas.State.ZoomByWheel(tab, factor, new(anchor.X, anchor.Y));

        /// <summary>Moves a wheel zoom one frame on, and settles it at its target once its time is up.</summary>
        /// <param name="ease">The ease.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void StepZoom(WheelZoomEase ease) => canvas.State.StepZoom(ease);

        /// <summary>Zooms keeping the point under the pointer fixed, with the new layout and offset in the same frame.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="zoom">The new zoom.</param>
        /// <param name="anchor">The canvas point to keep fixed.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ZoomAround(DocumentTabViewModel tab, double zoom, Point anchor) => canvas.State.ZoomAround(tab, zoom, new(anchor.X, anchor.Y));

        /// <summary>Zooms until an area fills the view, with the area's middle in the middle of the view.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="area">The area, in canvas coordinates.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ZoomToArea(DocumentTabViewModel tab, in Rect area) => canvas.State.ZoomToArea(tab, new(area.X, area.Y, area.Width, area.Height));

        /// <summary>Gets the vertical offset that shows a position on a page, kept inside the page's slot when page by page.</summary>
        /// <param name="pageIndex">The page.</param>
        /// <param name="offsetFraction">How far down the page, from 0 to 1.</param>
        /// <returns>The offset.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal double GetPageScrollTop(int pageIndex, double offsetFraction) => canvas.State.GetPageScrollTop(pageIndex, offsetFraction);

        /// <summary>Keeps an offset inside a page's slot when page by page.</summary>
        /// <param name="pageIndex">The page.</param>
        /// <param name="offset">The offset.</param>
        /// <returns>The offset, clamped when page by page.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal double ClampToSlot(int pageIndex, double offset) => canvas.State.ClampToSlot(pageIndex, offset);

        /// <summary>Scrolls page by page: within the current slot, or to the neighbouring slot from its edge.</summary>
        /// <param name="delta">How far to move; positive is down.</param>
        /// <returns><see langword="true"/> when page by page handled the movement.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool StepPageByPage(double delta) => canvas.State.StepPageByPage(delta);

        /// <summary>Handles the scrolling keys when page by page.</summary>
        /// <param name="e">The key event.</param>
        /// <returns><see langword="true"/> when handled.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool HandlePageByPageKey(KeyEventArgs e) => canvas.State.HandlePageByPageKey(e.ViewerKey, e.ViewerModifiers);

        /// <summary>Gets the pages to report: the requested or current page wherever its row is the row found.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="top">The position of the top left of the content in view.</param>
        /// <param name="middle">The position of the middle of the viewport.</param>
        /// <param name="topPage">The page at the top of the viewport.</param>
        /// <param name="middlePage">The page in the middle of the viewport, shown as the current page.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void GetCurrentPages(DocumentTabViewModel tab, Point top, Point middle, out int topPage, out int middlePage) =>
            canvas.State.GetCurrentPages(tab, new(top.X, top.Y), new(middle.X, middle.Y), out topPage, out middlePage);

        /// <summary>Gets the horizontal offset that shows a page: its left side in a horizontal layout, and otherwise the offset the reader has.</summary>
        /// <param name="pageIndex">The page.</param>
        /// <param name="current">The current horizontal offset.</param>
        /// <returns>The offset.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal double GetPageScrollLeft(int pageIndex, double current) => canvas.State.GetPageScrollLeft(pageIndex, current);

        /// <summary>Gets the offset that shows a position on a page.</summary>
        /// <param name="pageIndex">The page.</param>
        /// <param name="offsetFraction">How far down the page, from 0 to 1.</param>
        /// <param name="current">The current offset.</param>
        /// <returns>The offset.</returns>
        internal Vector GetPageScrollOffset(int pageIndex, double offsetFraction, Vector current)
        {
            var offset = canvas.State.GetPageScrollOffset(pageIndex, offsetFraction, new(current.X, current.Y));
            return new(offset.X, offset.Y);
        }

        /// <summary>Moves a horizontal layout sideways.</summary>
        /// <param name="delta">How far to move; positive is to the right.</param>
        /// <returns><see langword="true"/> when the layout is horizontal and moved.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool StepSideways(double delta) => canvas.State.StepSideways(delta);
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
