// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Rendering;

namespace HyperPdf.Presentation.Canvas;

/// <summary>
/// Plans a frame of the page canvas for any toolkit: which pages are drawn, which cached tiles go down, and which tiles and
/// previews are asked of the renderer, so a head only has to paint what it is told to.
/// </summary>
public static class CanvasRenderMixins
{
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

    /// <summary>Adds the frame planning to a canvas state.</summary>
    /// <param name="state">The canvas state.</param>
    extension(PageCanvasState state)
    {
        /// <summary>Reuses the generation when a tile arrival repaints the same view.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="document">The document.</param>
        /// <param name="viewport">The viewport.</param>
        /// <param name="scaling">The display scale.</param>
        /// <returns>The generation for requests from this view.</returns>
        public int GetRenderGeneration(DocumentTabViewModel tab, IDocument document, in LayoutRect viewport, double scaling)
        {
            ArgumentNullException.ThrowIfNull(tab);
            ArgumentNullException.ThrowIfNull(document);
            var plan = state.Render;
            if (ReferenceEquals(plan.RenderTab, tab) && ReferenceEquals(plan.RenderDocument, document)
                && plan.RenderViewport.Equals(viewport) && Math.Abs(plan.RenderScaling - scaling) < RenderScaleTolerance
                && plan.RenderLayout == state.Layout.Options && plan.RenderRotation == tab.Rotation && plan.RenderToneId == tab.PageTone.Id)
            {
                return tab.CanvasClient.Generation;
            }

            plan.RenderTab = tab;
            plan.RenderDocument = document;
            plan.RenderViewport = viewport;
            plan.RenderScaling = scaling;
            plan.RenderLayout = state.Layout.Options;
            plan.RenderRotation = tab.Rotation;
            plan.RenderToneId = tab.PageTone.Id;
            return tab.CanvasClient.Advance();
        }

        /// <summary>Draws the pages around the viewport, and remembers the scale once every visible page is sharp.</summary>
        /// <param name="painter">The toolkit's painter.</param>
        /// <param name="frame">The frame.</param>
        public void DrawPages(ITilePainter painter, in FrameContext frame)
        {
            ArgumentNullException.ThrowIfNull(painter);
            var plan = state.Render;
            var viewport = frame.Viewport;
            var prefetch = viewport.Height * PrefetchViewports;
            var sidePrefetch = viewport.Width * PrefetchViewports;
            state.Layout.GetVisiblePages(viewport.X - sidePrefetch, viewport.Right + sidePrefetch, viewport.Y - prefetch, viewport.Bottom + prefetch, out var first, out var last);
            plan.StartFrame();
            state.UpdateScrollDirection(viewport.Y);
            var cache = frame.Hub.Cache;
            var cancelled = frame.Tab.SelectedWorkToken.IsCancellationRequested;
            var stamp = new PrefetchStamp(frame.Generation, first, last, plan.ScrollingDown, state.IsZoomEasing, cancelled, cache.Count, cache.CurrentBytes, cache.PreviewBytes);

            // A frame that asked for nothing, repeated with the same view and the same cache, would ask for nothing again. A request
            // that was made last frame is asked for again, so one the scheduler dropped is not lost. The TileCache has no eviction
            // counter, so its tile count and byte totals stand in: an eviction that is exactly offset by an arrival in the same
            // frame is the one change they cannot see.
            plan.SkipPrefetch = plan.LastPrefetchSettled && stamp == plan.LastPrefetch;

            painter.BeginPages();
            for (var page = first; page <= last && page >= 0; page++)
            {
                state.DrawPage(painter, frame, page);
            }

            painter.EndPages();
            if (!plan.SkipPrefetch)
            {
                state.RequestPreviewReach(frame, first, last);
            }

            plan.LastPrefetch = stamp;
            plan.LastPrefetchSettled = plan.RequestsThisFrame == 0;
            state.NeighbourScale = state.GetTileScale(frame.RenderScaling);
            if (plan.FrameComplete && !state.IsZoomEasing)
            {
                plan.FallbackScale = state.GetTileScale(frame.RenderScaling);
            }
        }

        /// <summary>Gets the device pixels per point that tiles are rendered at.</summary>
        /// <param name="scaling">The device pixels per canvas unit.</param>
        /// <returns>The tile scale.</returns>
        public float GetTileScale(double scaling) => (float)Math.Min(state.Layout.Options.Scale * scaling, TileGrid.MaxScale);

        /// <summary>Works out which way the reader is scrolling from the top of the viewport.</summary>
        /// <param name="top">The top of the viewport.</param>
        private void UpdateScrollDirection(double top)
        {
            var plan = state.Render;
            if (Math.Abs(top - plan.PreviousViewportTop) <= ScrollDirectionTolerance)
            {
                return;
            }

            plan.ScrollingDown = top > plan.PreviousViewportTop;
            plan.PreviousViewportTop = top;
        }

        /// <summary>Draws one page: shadow, background, preview, tiles and highlights.</summary>
        /// <param name="painter">The toolkit's painter.</param>
        /// <param name="frame">The frame.</param>
        /// <param name="page">The page.</param>
        private void DrawPage(ITilePainter painter, in FrameContext frame, int page)
        {
            var bounds = state.Layout.GetPageBounds(page);
            var visible = bounds.Intersects(frame.Viewport);
            if (visible)
            {
                painter.DrawPaper(bounds, frame.Tab.PageTone);
            }

            var tiled = state.TryGetTileWindow(frame, page, bounds, visible, out var grid, out var window);
            var tab = frame.Tab;
            var covered = tiled && visible && frame.Hub.Cache.Covers(new(tab.TileId, page, grid.ScaleKey, tab.Rotation, tab.PageTone.Id, 0, 0), window);
            state.DrawPreview(painter, frame, page, bounds, visible, visible && !covered);
            if (visible && tiled && !covered)
            {
                // Until the sharp tiles arrive, the last complete scale stands in, so the page does not flash blurry.
                state.Render.FrameComplete = false;
                state.DrawFallbackTiles(painter, frame, page, bounds, grid.ScaleKey);
            }

            if (tiled)
            {
                state.DrawTiles(painter, frame, grid, window, visible);
                state.PrefetchAdjacentTiles(painter, frame, page, bounds, window, visible);
            }

            painter.FlushSurfaces();
            if (visible)
            {
                painter.DrawPageOverlay(page, bounds);
            }
        }

        /// <summary>Draws the low resolution preview of a page, requesting it when missing.</summary>
        /// <param name="painter">The toolkit's painter.</param>
        /// <param name="frame">The frame.</param>
        /// <param name="page">The page.</param>
        /// <param name="rect">The page rectangle.</param>
        /// <param name="visible">Whether the page is visible.</param>
        /// <param name="draw">Whether sharp tiles leave part of the page for the preview to fill.</param>
        private void DrawPreview(ITilePainter painter, in FrameContext frame, int page, in LayoutRect rect, bool visible, bool draw)
        {
            var tab = frame.Tab;
            var key = TileKey.Preview(tab.TileId, page, tab.Rotation, tab.PageTone.Id);
            if (frame.Hub.Cache.TryGet(key, out var surface))
            {
                if (draw)
                {
                    painter.DrawSurface(surface, rect, true);
                }

                return;
            }

            state.RequestPreview(frame, page, key, visible ? RenderPriority.VisiblePreview : RenderPriority.Prefetch);
        }

        /// <summary>Asks the renderer for the low resolution preview of a page.</summary>
        /// <param name="frame">The frame.</param>
        /// <param name="page">The page.</param>
        /// <param name="key">The preview's key.</param>
        /// <param name="priority">The priority.</param>
        private void RequestPreview(in FrameContext frame, int page, in TileKey key, RenderPriority priority)
        {
            var tab = frame.Tab;
            var size = tab.Source.PageSizes[page];
            var scale = TileGrid.GetPreviewScale(size, tab.Rotation);
            TileGrid.GetPagePixelSize(size, tab.Rotation, scale, out var width, out var height);
            state.Render.NoteRequest();
            frame.Request(key, new(page, scale, tab.Rotation, 0, 0, RenderFlags.Annotations), width, height, priority);
        }

        /// <summary>Draws one tile, or requests it when it is not cached.</summary>
        /// <param name="painter">The toolkit's painter.</param>
        /// <param name="frame">The frame.</param>
        /// <param name="grid">The page's tile grid.</param>
        /// <param name="column">The tile column.</param>
        /// <param name="row">The tile row.</param>
        /// <param name="visible">Whether to draw (true) or only request (false).</param>
        /// <param name="request">Whether a missing tile is requested.</param>
        private void DrawTile(ITilePainter painter, in FrameContext frame, in TileRange grid, int column, int row, bool visible, bool request)
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
                    state.Render.NoteRequest();
                    frame.Request(key, info, tileWidth, tileHeight, visible ? RenderPriority.Visible : RenderPriority.Prefetch);
                }

                return;
            }

            var scaling = frame.RenderScaling;
            var destination = new LayoutRect((grid.OriginX + (column * tileSize)) / scaling, (grid.OriginY + (row * tileSize)) / scaling, tileWidth / scaling, tileHeight / scaling);
            if (visible && destination.Intersects(frame.Viewport))
            {
                painter.DrawSurface(surface, destination, false);
            }
        }

        /// <summary>Finds the full resolution tiles of a page that intersect the viewport, or its prefetch area when off screen.</summary>
        /// <param name="frame">The frame.</param>
        /// <param name="page">The page.</param>
        /// <param name="rect">The page rectangle.</param>
        /// <param name="visible">Whether the page is on screen.</param>
        /// <param name="grid">The page's tile grid.</param>
        /// <param name="window">The tiles in the area.</param>
        /// <returns><see langword="true"/> when the page needs tiles in the area; small pages use only the preview.</returns>
        private bool TryGetTileWindow(in FrameContext frame, int page, in LayoutRect rect, bool visible, out TileRange grid, out TileWindow window)
        {
            var scaling = frame.RenderScaling;
            var scale = state.GetTileScale(scaling);
            TileGrid.GetPagePixelSize(state.Sizes[page], frame.Tab.Rotation, scale, out var pixelWidth, out var pixelHeight);
            var area = visible ? frame.Viewport : frame.Viewport.Inflate(0, frame.Viewport.Height * PrefetchViewports);
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
        /// <param name="painter">The toolkit's painter.</param>
        /// <param name="frame">The frame.</param>
        /// <param name="grid">The page's tile grid.</param>
        /// <param name="window">The tiles to draw.</param>
        /// <param name="visible">Whether the page is on screen; off screen pages are only prefetched.</param>
        private void DrawTiles(ITilePainter painter, in FrameContext frame, in TileRange grid, in TileWindow window, bool visible)
        {
            var request = !state.IsZoomEasing;
            for (var row = window.FirstRow; row <= window.LastRow; row++)
            {
                for (var column = window.FirstColumn; column <= window.LastColumn; column++)
                {
                    state.DrawTile(painter, frame, grid, column, row, visible, request);
                }
            }
        }

        /// <summary>Requests nearby rows of a visible page before scrolling exposes them.</summary>
        /// <param name="painter">The toolkit's painter.</param>
        /// <param name="frame">The frame.</param>
        /// <param name="page">The page.</param>
        /// <param name="rect">The page bounds.</param>
        /// <param name="visibleWindow">The tile range already requested for drawing.</param>
        /// <param name="visible">Whether the page is visible.</param>
        private void PrefetchAdjacentTiles(ITilePainter painter, in FrameContext frame, int page, in LayoutRect rect, in TileWindow visibleWindow, bool visible)
        {
            if (!visible || state.IsPrefetchHeld() || !state.TryGetTileWindow(frame, page, rect, false, out var grid, out var window))
            {
                return;
            }

            for (var row = window.FirstRow; row <= window.LastRow; row++)
            {
                for (var column = window.FirstColumn; column <= window.LastColumn; column++)
                {
                    if (!Contains(visibleWindow, column, row))
                    {
                        state.DrawTile(painter, frame, grid, column, row, false, true);
                    }
                }
            }
        }

        /// <summary>Tells whether prefetching waits: a zoom is easing, or the last frame found nothing left to ask for.</summary>
        /// <returns><see langword="true"/> when prefetching is held back.</returns>
        private bool IsPrefetchHeld() => state.IsZoomEasing || state.Render.SkipPrefetch;

        /// <summary>Stretches the cached tiles of the last complete scale over the visible part of a page.</summary>
        /// <param name="painter">The toolkit's painter.</param>
        /// <param name="frame">The frame.</param>
        /// <param name="page">The page.</param>
        /// <param name="rect">The page rectangle.</param>
        /// <param name="scaleKey">The quantised scale being drawn, which needs no stand in.</param>
        private void DrawFallbackTiles(ITilePainter painter, in FrameContext frame, int page, in LayoutRect rect, int scaleKey)
        {
            var scale = state.Render.FallbackScale;
            var fallbackKey = TileGrid.ToScaleKey(scale);
            if (scale <= 0 || fallbackKey == scaleKey)
            {
                return;
            }

            var tab = frame.Tab;
            TileGrid.GetPagePixelSize(state.Sizes[page], tab.Rotation, scale, out var pixelWidth, out var pixelHeight);
            var region = frame.Viewport.Intersect(rect);
            if (pixelWidth <= TileGrid.PreviewWidth || region.Width <= 0 || region.Height <= 0)
            {
                return;
            }

            var tileSize = TileGrid.TileSize;
            var unitX = rect.Width / pixelWidth;
            var unitY = rect.Height / pixelHeight;
            TileGrid.GetTileCounts(pixelWidth, pixelHeight, out var columns, out var rows);
            var window = new TileWindow(
                Math.Clamp((int)((region.X - rect.X) / unitX / tileSize), 0, columns - 1),
                Math.Clamp((int)((region.Right - rect.X) / unitX / tileSize), 0, columns - 1),
                Math.Clamp((int)((region.Y - rect.Y) / unitY / tileSize), 0, rows - 1),
                Math.Clamp((int)((region.Bottom - rect.Y) / unitY / tileSize), 0, rows - 1));
            var origin = new CanvasPoint(rect.X, rect.Y);
            var unit = new CanvasPoint(unitX, unitY);
            for (var row = window.FirstRow; row <= window.LastRow; row++)
            {
                for (var column = window.FirstColumn; column <= window.LastColumn; column++)
                {
                    DrawFallbackTile(painter, frame, new(tab.TileId, page, fallbackKey, tab.Rotation, tab.PageTone.Id, (short)column, (short)row), origin, unit);
                }
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

            var down = state.Render.ScrollingDown;
            var after = down ? PreviewReachAhead : PreviewReachBehind;
            var before = down ? PreviewReachBehind : PreviewReachAhead;
            for (var distance = 1; distance <= Math.Max(after, before); distance++)
            {
                if (distance <= after)
                {
                    state.RequestDistantPreview(frame, last + distance);
                }

                if (distance <= before)
                {
                    state.RequestDistantPreview(frame, first - distance);
                }
            }
        }

        /// <summary>Marks a page's preview as recently used, or requests it at the lowest page priority when it is missing.</summary>
        /// <param name="frame">The frame.</param>
        /// <param name="page">The page, which may be outside the document.</param>
        private void RequestDistantPreview(in FrameContext frame, int page)
        {
            if ((uint)page >= (uint)state.Layout.PageCount)
            {
                return;
            }

            var tab = frame.Tab;
            var key = TileKey.Preview(tab.TileId, page, tab.Rotation, tab.PageTone.Id);
            if (!frame.Hub.Cache.TryGet(key, out _))
            {
                state.RequestPreview(frame, page, key, RenderPriority.PrefetchPreview);
            }
        }
    }

    /// <summary>Tells whether a tile is inside a window of tiles.</summary>
    /// <param name="window">The window.</param>
    /// <param name="column">The tile column.</param>
    /// <param name="row">The tile row.</param>
    /// <returns><see langword="true"/> when the tile is in the window.</returns>
    private static bool Contains(in TileWindow window, int column, int row) =>
        row >= window.FirstRow && row <= window.LastRow && column >= window.FirstColumn && column <= window.LastColumn;

    /// <summary>Stretches one cached tile of the last complete scale to where the page's tile of the current scale goes.</summary>
    /// <param name="painter">The toolkit's painter.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="key">The stand in tile's key.</param>
    /// <param name="origin">The top left of the page on the canvas.</param>
    /// <param name="unit">The canvas size of one device pixel of the stand in scale, across and down.</param>
    private static void DrawFallbackTile(ITilePainter painter, in FrameContext frame, in TileKey key, CanvasPoint origin, CanvasPoint unit)
    {
        if (!frame.Hub.Cache.TryGet(key, out var surface))
        {
            return;
        }

        var tileSize = TileGrid.TileSize;
        var destination = new LayoutRect(origin.X + (key.Column * tileSize * unit.X), origin.Y + (key.Row * tileSize * unit.Y), surface.Width * unit.X, surface.Height * unit.Y);
        painter.DrawSurface(surface, destination, true);
    }
}
