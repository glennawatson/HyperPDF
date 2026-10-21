// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Navigation;
using HyperPdf.Presentation.Input;

namespace HyperPdf.Presentation.Canvas;

/// <summary>Lays the pages out, scrolls to places, steps page by page and reports where the reader is, for any toolkit's canvas.</summary>
public static class CanvasViewportMixins
{
    /// <summary>Half, for finding the middle of the viewport.</summary>
    private const double Half = 0.5;

    /// <summary>How far the scroll extent may differ from the layout, in canvas units, and still count as matching.</summary>
    private const double ExtentTolerance = 1;

    /// <summary>Adds the layout, scrolling and reporting helpers to a canvas state.</summary>
    /// <param name="state">The canvas state.</param>
    extension(PageCanvasState state)
    {
        /// <summary>Gets the height of the part of the canvas in view, or nothing before there is a scrolling area.</summary>
        public double? ViewportHeight => state.Host.HasScroller ? state.Host.ViewportSize.Height : null;

        /// <summary>Recomputes the layout from the tab's settings and the viewport.</summary>
        public void RebuildLayout()
        {
            var host = state.Host;
            var scaleBefore = state.PageScale;
            var tab = state.Tab;
            state.UseSizes(tab?.Source.PageSizes ?? []);
            if (tab is null || state.Sizes.Length == 0 || !host.HasScroller)
            {
                state.UseLayout(DocumentLayout.Empty, null);
                host.InvalidateMeasure();
                return;
            }

            var viewport = host.ScrollerSize;
            var zoom = tab.Zoom;
            if (tab.ZoomMode != ZoomMode.Free)
            {
                zoom = ZoomCalculator.GetFitZoom(
                    state.FitExtents.Get(state.Sizes, tab.Rotation, tab.LayoutMode),
                    new(tab.Rotation, tab.LayoutMode, tab.ZoomMode, viewport.Width, viewport.Height, CanvasMetrics.PageSpacing, CanvasMetrics.ContentMargin));
                tab.ReportZoom(zoom);
            }

            var options = new LayoutOptions(tab.Rotation, tab.LayoutMode, zoom * ZoomCalculator.PixelsPerPoint, CanvasMetrics.PageSpacing, CanvasMetrics.ContentMargin, viewport.Width)
            {
                PageByPage = tab.IsPageByPage,
                ViewportHeight = viewport.Height,
                RightToLeft = tab.RightToLeft,
                Scroll = tab.ScrollMode,
            };

            // A resize that changes nothing the layout depends on (a height change outside page by page) keeps the layout as it is.
            if (ReferenceEquals(state.LayoutSizes, state.Sizes) && state.Layout.Matches(options))
            {
                host.InvalidateVisual();
                return;
            }

            state.UseLayout(DocumentLayout.Create(state.Sizes, options, state.Layout), state.Sizes);
            host.InvalidateMeasure();
            host.InvalidateVisual();
            host.PageScaleChanged(scaleBefore, state.PageScale);
        }

        /// <summary>Scrolls to a remembered position once laid out.</summary>
        /// <param name="position">The position.</param>
        public void ScrollToPosition(DocumentPosition position) =>
            state.PendingScroll = () =>
            {
                if (!state.Host.HasScroller || position.PageIndex < 0 || position.PageIndex >= state.Layout.PageCount)
                {
                    return;
                }

                state.Host.ScrollOffset = state.GetPageScrollOffset(position.PageIndex, position.OffsetFraction, state.Host.ScrollOffset);
            };

        /// <summary>Applies a pending scroll once the scrolling area's extent matches the layout.</summary>
        public void ApplyPendingScroll()
        {
            var host = state.Host;
            if (state.PendingScroll is null || !host.HasScroller || Math.Abs(host.ExtentSize.Height - state.Layout.ExtentHeight) > ExtentTolerance
                || (state.Layout.Options.Scroll == ScrollMode.Horizontal && Math.Abs(host.ExtentSize.Width - state.Layout.ExtentWidth) > ExtentTolerance))
            {
                return;
            }

            var scroll = state.PendingScroll;
            state.PendingScroll = null;
            scroll();
            state.ReportPosition();
        }

        /// <summary>Lays the pages out again for a new viewport size, keeping the reader's place.</summary>
        public void RelayoutInPlace()
        {
            if (state.Tab is not { } tab)
            {
                return;
            }

            var position = tab.Position;
            state.RebuildLayout();
            state.ScrollToPosition(position);
        }

        /// <summary>Lays the pages out again for a new zoom, arrangement or rotation, keeping the reader's place unless a zoom holds its own anchor.</summary>
        public void RelayoutForSettings()
        {
            if (state.Tab is not { } tab)
            {
                return;
            }

            var position = tab.Position;
            var hasAnchor = state.Anchoring || state.PendingScroll is not null;
            state.RebuildLayout();
            if (!hasAnchor)
            {
                state.ScrollToPosition(position);
            }
        }

        /// <summary>Lays the pages out again after the document loads, reloads or its pages change, keeping the reader's place.</summary>
        public void RelayoutAfterPagesChange()
        {
            var position = state.Tab?.Position ?? default;
            state.UseLayout(state.Layout, null);
            state.FitExtents.Invalidate();
            state.RebuildLayout();
            state.ScrollToPosition(position);
        }

        /// <summary>Scrolls to a place the reader asked to go.</summary>
        /// <param name="request">The place.</param>
        public void ScrollTo(NavigationRequest request)
        {
            var host = state.Host;
            if (!host.HasScroller || state.Tab is not { } tab || request.PageIndex < 0 || request.PageIndex >= state.Layout.PageCount)
            {
                return;
            }

            var bounds = state.Layout.GetPageBounds(request.PageIndex);
            state.RequestedPage = request.PageIndex;
            var offset = host.ScrollOffset;
            if (request.Target is { } target)
            {
                var transform = new PageTransform(bounds, state.Sizes[request.PageIndex], tab.Rotation, state.Layout.Options.Scale);
                var area = transform.MapToCanvas(target);
                var x = area.Width > 0 && (area.X < offset.X || area.X + area.Width > offset.X + host.ViewportSize.Width)
                    ? area.X - CanvasMetrics.TargetPadding
                    : offset.X;
                host.ScrollOffset = new(x, state.ClampToSlot(request.PageIndex, area.Y - (host.ViewportSize.Height * CanvasMetrics.TargetViewportFraction)));
            }
            else
            {
                host.ScrollOffset = state.GetPageScrollOffset(request.PageIndex, request.OffsetFraction, offset);
            }

            state.ReportPosition();
        }

        /// <summary>Reports the current position to the tab.</summary>
        public void ReportPosition()
        {
            var host = state.Host;
            if (!host.HasScroller || state.Tab is not { } tab || state.Layout.PageCount == 0)
            {
                return;
            }

            // The wheel-zoom ease reports once from the offset change and again when the anchor is set; the same view gives the same report.
            var offset = host.ScrollOffset;
            var viewport = host.ViewportSize;
            var layout = state.Layout;
            var view = new ReportedView(tab, new(offset.X, offset.Y, viewport.Width, viewport.Height), layout.Options, layout.PageCount, layout.ExtentWidth, layout.ExtentHeight);
            if (view == state.Reported)
            {
                return;
            }

            state.Reported = view;
            var top = offset.Y + CanvasMetrics.ContentMargin;
            state.GetCurrentPages(
                tab,
                new(offset.X + CanvasMetrics.ContentMargin, top),
                new(
                    offset.X + (layout.Options.Scroll == ScrollMode.Horizontal ? CanvasMetrics.ContentMargin : viewport.Width * Half),
                    offset.Y + (viewport.Height * Half)),
                out var topPage,
                out var middlePage);
            var bounds = layout.GetPageBounds(topPage);
            var fraction = bounds.Height > 0 ? Math.Clamp((top - bounds.Y) / bounds.Height, 0, 1) : 0;
            tab.ReportPosition(new(topPage, fraction), middlePage);

            // Visit does nothing for a page it already has, so the preparer's lock is only taken on a change.
            if (tab.TryGetDocument() is not { } document || (ReferenceEquals(document, state.VisitedDocument) && middlePage == state.VisitedPage))
            {
                return;
            }

            state.VisitedDocument = document;
            state.VisitedPage = middlePage;
            tab.RenderHub.Preparer.Visit(document, middlePage, state.NeighbourScale, tab.Rotation, RenderFlags.Annotations);
        }

        /// <summary>Gets the pages to report: the requested or current page wherever its row is the row found.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="top">The position of the top left of the content in view.</param>
        /// <param name="middle">The position of the middle of the viewport.</param>
        /// <param name="topPage">The page at the top of the viewport.</param>
        /// <param name="middlePage">The page in the middle of the viewport, shown as the current page.</param>
        public void GetCurrentPages(DocumentTabViewModel tab, CanvasPoint top, CanvasPoint middle, out int topPage, out int middlePage)
        {
            var preferred = state.RequestedPage >= 0 ? state.RequestedPage : tab.CurrentPageIndex;
            state.RequestedPage = -1;
            topPage = state.Layout.GetCurrentPage(top.X, top.Y, preferred);
            middlePage = state.Layout.GetCurrentPage(middle.X, middle.Y, preferred);
        }

        /// <summary>Gets the vertical offset that shows a position on a page, kept inside the page's slot when page by page.</summary>
        /// <param name="pageIndex">The page.</param>
        /// <param name="offsetFraction">How far down the page, from 0 to 1.</param>
        /// <returns>The offset.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double GetPageScrollTop(int pageIndex, double offsetFraction) =>
            PageScrolling.GetTop(state.Layout, pageIndex, offsetFraction, CanvasMetrics.ContentMargin, state.ViewportHeight);

        /// <summary>Keeps an offset inside a page's slot when page by page.</summary>
        /// <param name="pageIndex">The page.</param>
        /// <param name="offset">The offset.</param>
        /// <returns>The offset, clamped when page by page.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double ClampToSlot(int pageIndex, double offset) =>
            PageScrolling.ClampToSlot(state.Layout, pageIndex, offset, state.ViewportHeight);

        /// <summary>Gets the horizontal offset that shows a page: its left side in a horizontal layout, and otherwise the offset the reader has.</summary>
        /// <param name="pageIndex">The page.</param>
        /// <param name="current">The current horizontal offset.</param>
        /// <returns>The offset.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double GetPageScrollLeft(int pageIndex, double current) =>
            PageScrolling.GetLeft(state.Layout, pageIndex, current, CanvasMetrics.ContentMargin);

        /// <summary>Gets the offset that shows a position on a page.</summary>
        /// <param name="pageIndex">The page.</param>
        /// <param name="offsetFraction">How far down the page, from 0 to 1.</param>
        /// <param name="current">The current offset.</param>
        /// <returns>The offset.</returns>
        public CanvasPoint GetPageScrollOffset(int pageIndex, double offsetFraction, CanvasPoint current) =>
            new(
                state.GetPageScrollLeft(pageIndex, current.X),
                PageScrolling.GetScrollTop(state.Layout, pageIndex, offsetFraction, current.Y, CanvasMetrics.ContentMargin, state.ViewportHeight));

        /// <summary>Scrolls page by page: within the current slot, or to the neighbouring slot from its edge.</summary>
        /// <param name="delta">How far to move; positive is down.</param>
        /// <returns><see langword="true"/> when page by page handled the movement.</returns>
        public bool StepPageByPage(double delta)
        {
            var host = state.Host;
            var layout = state.Layout;
            if (!host.HasScroller || !layout.Options.PageByPage || layout.PageCount == 0)
            {
                return false;
            }

            var offset = host.ScrollOffset;
            var y = offset.Y;
            var viewport = host.ViewportSize.Height;
            var tolerance = CanvasMetrics.EdgeTolerance;
            layout.GetRowExtent(layout.GetPageNearest(y + tolerance), out var top, out var bottom);
            var last = Math.Max(top, bottom - viewport);
            double target;
            if (delta > 0)
            {
                target = y < last - tolerance ? Math.Min(y + delta, last) : bottom;
                if (target >= layout.ExtentHeight - tolerance)
                {
                    return true;
                }
            }
            else if (y > top + tolerance)
            {
                target = Math.Max(y + delta, top);
            }
            else if (top > 0)
            {
                // Show the end of the previous slot, which for a page that fits is the whole page.
                layout.GetRowExtent(layout.GetPageNearest(top - tolerance), out var previousTop, out var previousBottom);
                target = Math.Max(previousTop, previousBottom - viewport);
            }
            else
            {
                return true;
            }

            host.ScrollOffset = new(offset.X, target);
            state.ReportPosition();
            return true;
        }

        /// <summary>Handles the scrolling keys when page by page.</summary>
        /// <param name="key">The key.</param>
        /// <param name="modifiers">The modifiers held.</param>
        /// <returns><see langword="true"/> when handled.</returns>
        public bool HandlePageByPageKey(ViewerKey key, ViewerModifiers modifiers)
        {
            var host = state.Host;
            if (!state.Layout.Options.PageByPage || !host.HasScroller || (modifiers & ~ViewerModifiers.Shift) != 0)
            {
                return false;
            }

            var page = host.ViewportSize.Height;
            var shift = (modifiers & ViewerModifiers.Shift) != 0;
            return key switch
            {
                ViewerKey.Down => state.StepPageByPage(CanvasMetrics.LineStep),
                ViewerKey.Up => state.StepPageByPage(-CanvasMetrics.LineStep),
                ViewerKey.PageDown or ViewerKey.Right => state.StepPageByPage(page),
                ViewerKey.PageUp or ViewerKey.Left => state.StepPageByPage(-page),
                ViewerKey.Space => state.StepPageByPage(shift ? -page : page),
                _ => false,
            };
        }

        /// <summary>Moves a horizontal layout sideways.</summary>
        /// <param name="delta">How far to move; positive is to the right.</param>
        /// <returns><see langword="true"/> when the layout is horizontal and moved.</returns>
        public bool StepSideways(double delta)
        {
            var host = state.Host;
            if (!host.HasScroller || state.Layout.Options.Scroll != ScrollMode.Horizontal)
            {
                return false;
            }

            var limit = Math.Max(0, host.ExtentSize.Width - host.ViewportSize.Width);
            var offset = host.ScrollOffset;
            host.ScrollOffset = new(Math.Clamp(offset.X + delta, 0, limit), offset.Y);
            state.ReportPosition();
            return true;
        }

    }
}
