// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;
using HyperPdf.Core.Layout;

namespace HyperPdf.Presentation.Canvas;

/// <summary>Plans the marks laid over the pages: search hits, the sentence and word being read aloud, and the focus band.</summary>
public static class CanvasMarksMixins
{
    /// <summary>How far the focus band reaches above and below the sentence being read.</summary>
    private const double FocusBandMargin = 6;

    /// <summary>Adds the marks planning to a canvas state.</summary>
    /// <param name="state">The canvas state.</param>
    extension(PageCanvasState state)
    {
        /// <summary>Draws the marks on the pages in view.</summary>
        /// <param name="painter">The toolkit's painter.</param>
        /// <param name="tab">The tab.</param>
        /// <param name="viewport">The part of the canvas in view.</param>
        public void DrawMarks(IMarksPainter painter, DocumentTabViewModel tab, in LayoutRect viewport)
        {
            ArgumentNullException.ThrowIfNull(painter);
            ArgumentNullException.ThrowIfNull(tab);
            state.GetPagesBetween(viewport.X, viewport.Right, viewport.Y, viewport.Bottom, out var first, out var last);
            for (var page = first; page <= last && page >= 0; page++)
            {
                if (!state.TryGetPageTransform(page, out var transform))
                {
                    continue;
                }

                DrawHits(painter, tab, page, transform, viewport);
                DrawSpoken(painter, tab, page, transform);
                DrawFocusBand(painter, tab, page, transform);
            }
        }
    }

    /// <summary>Draws the search hits on a page, outlining the current one.</summary>
    /// <param name="painter">The toolkit's painter.</param>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The page.</param>
    /// <param name="transform">How the page maps onto the canvas.</param>
    /// <param name="viewport">The visible part of the canvas; hits outside it are not drawn.</param>
    private static void DrawHits(IMarksPainter painter, DocumentTabViewModel tab, int page, in PageTransform transform, in LayoutRect viewport)
    {
        if (tab.Search.GetHits(page) is not { } hits)
        {
            return;
        }

        var current = tab.Search.CurrentHit;
        foreach (var hit in hits)
        {
            var isCurrent = ReferenceEquals(hit, current);
            foreach (var rect in hit.Bounds)
            {
                var canvasRect = transform.MapToCanvas(rect);
                if (canvasRect.Intersects(viewport))
                {
                    painter.DrawHit(canvasRect, isCurrent);
                }
            }
        }
    }

    /// <summary>Softly marks the sentence being read aloud, and underlines the word.</summary>
    /// <param name="painter">The toolkit's painter.</param>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The page.</param>
    /// <param name="transform">How the page maps onto the canvas.</param>
    private static void DrawSpoken(IMarksPainter painter, DocumentTabViewModel tab, int page, in PageTransform transform)
    {
        var reader = tab.ReadAloud;
        if (!painter.CanDraw(false) || reader.SpokenPage != page)
        {
            return;
        }

        foreach (var rect in reader.SpokenBounds)
        {
            painter.FillSpoken(transform.MapToCanvas(rect));
        }

        // The word is underlined rather than filled, so the marks stay quiet.
        foreach (var rect in reader.SpokenWordBounds)
        {
            var word = transform.MapToCanvas(rect);
            painter.UnderlineWord(new(word.X, word.Bottom), new(word.Right, word.Bottom));
        }
    }

    /// <summary>
    /// Dims the page away from the sentence being read, when the focus band is on: a band around the sentence stays
    /// clear and the rest of its page, and every other page, is laid over with the translucent paper colour.
    /// </summary>
    /// <param name="painter">The toolkit's painter.</param>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The page.</param>
    /// <param name="transform">How the page maps onto the canvas.</param>
    private static void DrawFocusBand(IMarksPainter painter, DocumentTabViewModel tab, int page, in PageTransform transform)
    {
        var reader = tab.ReadAloud;
        if (!painter.CanDraw(true) || !tab.FocusMode.FocusBand || reader.SpokenPage < 0 || reader.SpokenBounds.Count == 0)
        {
            return;
        }

        var pageRect = transform.Bounds;
        if (reader.SpokenPage != page)
        {
            painter.FillDim(pageRect);
            return;
        }

        var top = double.MaxValue;
        var bottom = double.MinValue;
        foreach (var rect in reader.SpokenBounds)
        {
            var spoken = transform.MapToCanvas(rect);
            top = Math.Min(top, spoken.Y);
            bottom = Math.Max(bottom, spoken.Bottom);
        }

        var clearTop = Math.Max(pageRect.Y, top - FocusBandMargin);
        var clearBottom = Math.Min(pageRect.Bottom, bottom + FocusBandMargin);
        painter.FillDim(new(pageRect.X, pageRect.Y, pageRect.Width, Math.Max(0, clearTop - pageRect.Y)));
        painter.FillDim(new(pageRect.X, clearBottom, pageRect.Width, Math.Max(0, pageRect.Bottom - clearBottom)));
    }
}
