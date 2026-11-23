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
                DrawEditOutlines(painter, tab, page, transform);
            }
        }
    }

    /// <summary>Outlines the object under the pointer and the object picked, in Edit mode.</summary>
    /// <param name="painter">The toolkit's painter.</param>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The page.</param>
    /// <param name="transform">How the page maps onto the canvas.</param>
    private static void DrawEditOutlines(IMarksPainter painter, DocumentTabViewModel tab, int page, in PageTransform transform)
    {
        // Nothing is being edited in a tab whose editing has not been used, and reading it here would make it for every tab drawn.
        if (tab.ContentEditIfMade is not { IsEditing: true } edit)
        {
            return;
        }

        if (edit.Hovered is { } hovered && hovered.Page == page && !SameObject(hovered, edit.Selected))
        {
            painter.DrawEditOutline(transform.MapToCanvas(hovered.Object.Bounds), false);
        }

        DrawPickedOutline(painter, edit, page, transform);
    }

    /// <summary>Outlines the picked object, shifted by how far it has been dragged.</summary>
    /// <param name="painter">The toolkit's painter.</param>
    /// <param name="edit">Edit mode.</param>
    /// <param name="page">The page.</param>
    /// <param name="transform">How the page maps onto the canvas.</param>
    private static void DrawPickedOutline(IMarksPainter painter, PageContentEditViewModel edit, int page, in PageTransform transform)
    {
        if (edit.Selected is not { } picked || picked.Page != page)
        {
            return;
        }

        var group = edit.Group;
        if (group.Length == 0)
        {
            painter.DrawEditOutline(transform.MapToCanvas(edit.IsResizing ? edit.ResizeBounds : Dragged(edit, page, picked.Object.Bounds)), true);
            return;
        }

        // Several objects are outlined one by one, and one box with handles stands around them all.
        if (!edit.IsResizing)
        {
            painter.DrawEditOutline(transform.MapToCanvas(Dragged(edit, page, picked.Object.Bounds)), false);
            for (var i = 0; i < group.Length; i++)
            {
                painter.DrawEditOutline(transform.MapToCanvas(Dragged(edit, page, group[i].Object.Bounds)), false);
            }
        }

        painter.DrawEditOutline(transform.MapToCanvas(edit.IsResizing ? edit.ResizeBounds : Dragged(edit, page, edit.GroupBounds())), true);
    }

    /// <summary>Gets a picked object's box shifted by how far the pick has been dragged.</summary>
    /// <param name="edit">Edit mode.</param>
    /// <param name="page">The page.</param>
    /// <param name="bounds">The box where the object is on the page.</param>
    /// <returns>The box to draw.</returns>
    private static PageRect Dragged(PageContentEditViewModel edit, int page, PageRect bounds) =>
        edit.DragPage == page ? bounds with { Left = bounds.Left + edit.DragDelta.X, Top = bounds.Top + edit.DragDelta.Y } : bounds;

    /// <summary>Tells whether two picks are the same object on the same page.</summary>
    /// <param name="first">The first pick.</param>
    /// <param name="second">The second pick, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when both name the same object.</returns>
    private static bool SameObject(PageObjectPick first, PageObjectPick? second) =>
        second is not null && first.Page == second.Page && first.Object.Index == second.Object.Index;

    /// <summary>Draws the search hits on a page, outlining the current one.</summary>
    /// <param name="painter">The toolkit's painter.</param>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The page.</param>
    /// <param name="transform">How the page maps onto the canvas.</param>
    /// <param name="viewport">The visible part of the canvas; hits outside it are not drawn.</param>
    private static void DrawHits(IMarksPainter painter, DocumentTabViewModel tab, int page, in PageTransform transform, in LayoutRect viewport)
    {
        // A tab whose search has not been used has no hits, and reading the search here would make it for every tab drawn.
        if (tab.SearchIfMade is not { } search || search.GetHits(page) is not { } hits)
        {
            return;
        }

        var current = search.CurrentHit;
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
        // Nothing is spoken in a tab whose Read Aloud has not been used, and reading it here would make it for every tab drawn.
        if (tab.ReadAloudIfMade is not { } reader || !painter.CanDraw(false) || reader.SpokenPage != page)
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
        // The dimming is a setting of reading aloud. It once needed Focus Mode to have been opened first, which hid it from anyone who had not.
        if (tab.ReadAloudIfMade is not { DimRest: true } reader || !painter.CanDraw(true) || reader.SpokenPage < 0 || reader.SpokenBounds.Length == 0)
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
