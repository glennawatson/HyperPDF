// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Layout;

namespace HyperPdf.Presentation.Canvas;

/// <summary>Zooms the pages by wheel, pinch or area while keeping a point fixed, for any toolkit's canvas.</summary>
public static class CanvasZoomMixins
{
    /// <summary>Half, for finding the middle of an area.</summary>
    private const double Half = 0.5;

    /// <summary>Adds the zoom helpers to a canvas state.</summary>
    /// <param name="state">The canvas state.</param>
    extension(PageCanvasState state)
    {
        /// <summary>Starts or extends a wheel zoom towards a new factor.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="factor">The zoom multiplier.</param>
        /// <param name="anchor">The canvas point under the pointer.</param>
        public void ZoomByWheel(DocumentTabViewModel tab, double factor, CanvasPoint anchor)
        {
            ArgumentNullException.ThrowIfNull(tab);
            var host = state.Host;
            if (!host.HasScroller)
            {
                return;
            }

            var target = ZoomCalculator.Clamp((state.IsZoomEasing ? state.WheelZoom.To : tab.Zoom) * factor);
            if (tab.ReduceMotion || !host.IsInWindow)
            {
                state.ZoomAround(tab, target, anchor);
                return;
            }

            // The ease starts from the zoom shown now, so a notch while easing carries on smoothly towards the new target.
            var offset = host.ScrollOffset;
            state.WheelZoom = new(true, tab.Zoom, target, Stopwatch.GetTimestamp(), new(anchor.X - offset.X, anchor.Y - offset.Y));
        }

        /// <summary>Moves a wheel zoom one frame on, and settles it at its target once its time is up.</summary>
        /// <param name="ease">The ease.</param>
        public void StepZoom(WheelZoomEase ease)
        {
            var host = state.Host;
            if (!host.HasScroller || state.Tab is not { } tab)
            {
                state.WheelZoom = ease with { IsRunning = false };
                return;
            }

            var progress = WheelZoomCurve.GetProgress(Stopwatch.GetElapsedTime(ease.Started));
            var zoom = WheelZoomCurve.GetZoom(ease.From, ease.To, progress);
            var done = progress >= 1;
            if (done)
            {
                // Settled before the last step, so the tiles of the final zoom are requested.
                state.WheelZoom = ease with { IsRunning = false };
            }

            var offset = host.ScrollOffset;
            state.ZoomAround(tab, done ? ease.To : zoom, new(ease.Anchor.X + offset.X, ease.Anchor.Y + offset.Y));
            if (done)
            {
                host.InvalidateVisual();
            }
        }

        /// <summary>Zooms keeping the point under the pointer fixed, with the new layout and offset in the same frame.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="zoom">The new zoom.</param>
        /// <param name="anchor">The canvas point to keep fixed.</param>
        public void ZoomAround(DocumentTabViewModel tab, double zoom, CanvasPoint anchor)
        {
            ArgumentNullException.ThrowIfNull(tab);
            var host = state.Host;
            if (!host.HasScroller)
            {
                return;
            }

            var page = PageScrolling.FindZoomAnchorPage(state.Layout, anchor.X, anchor.Y);
            if (page < 0)
            {
                tab.SetZoom(zoom);
                return;
            }

            var pagePoint = state.ToPage(tab, page, anchor);
            var offset = host.ScrollOffset;
            var screenX = anchor.X - offset.X;
            var screenY = anchor.Y - offset.Y;
            state.Anchoring = true;
            try
            {
                tab.SetZoom(zoom);

                // The layout was rebuilt by the zoom change; measuring now lets the offset follow before the next frame,
                // so no frame shows the new size at the old offset.
                host.UpdateLayout();
                var target = new PageTransform(state.Layout.GetPageBounds(page), state.Sizes[page], tab.Rotation, state.Layout.Options.Scale).MapToCanvas(pagePoint);
                host.ScrollOffset = new(target.X - screenX, target.Y - screenY);
            }
            finally
            {
                state.Anchoring = false;
            }

            state.ReportPosition();
        }

        /// <summary>Zooms until an area fills the view, with the area's middle in the middle of the view.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="area">The area, in canvas coordinates.</param>
        public void ZoomToArea(DocumentTabViewModel tab, LayoutRect area)
        {
            ArgumentNullException.ThrowIfNull(tab);
            var host = state.Host;
            if (!host.HasScroller)
            {
                return;
            }

            var viewport = host.ViewportSize;
            var centre = new CanvasPoint(area.X + (area.Width * Half), area.Y + (area.Height * Half));
            var zoom = AreaZoom.GetZoom(tab.Zoom, area.Width, area.Height, viewport.Width, viewport.Height);
            var page = PageScrolling.FindAreaAnchorPage(state.Layout, centre.X, centre.Y);
            if (page < 0)
            {
                tab.SetZoom(zoom);
                return;
            }

            var pagePoint = state.ToPage(tab, page, centre);
            state.Anchoring = true;
            try
            {
                tab.SetZoom(zoom);

                // Measured now so the offset follows the new size in the same frame.
                host.UpdateLayout();
                var target = new PageTransform(state.Layout.GetPageBounds(page), state.Sizes[page], tab.Rotation, state.Layout.Options.Scale).MapToCanvas(pagePoint);
                viewport = host.ViewportSize;
                var extent = host.ExtentSize;
                var y = state.ClampToSlot(page, AreaZoom.GetCentredOffset(target.Y, viewport.Height, extent.Height));
                host.ScrollOffset = new(AreaZoom.GetCentredOffset(target.X, viewport.Width, extent.Width), y);
            }
            finally
            {
                state.Anchoring = false;
            }

            state.ReportPosition();
        }
    }
}
