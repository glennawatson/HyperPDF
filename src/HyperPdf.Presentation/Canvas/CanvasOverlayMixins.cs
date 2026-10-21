// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Measuring;

namespace HyperPdf.Presentation.Canvas;

/// <summary>Plans the marks drawn over the pages for any toolkit: comment badges, shape and polygon previews, handles, outlines, the caret and measurements.</summary>
public static class CanvasOverlayMixins
{
    /// <summary>The radius of a placed polygon corner's dot.</summary>
    private const double CornerRadius = 3;

    /// <summary>The radius of a measured point's dot.</summary>
    private const double MeasurePointRadius = 3;

    /// <summary>The width of the signature mark's ink in points.</summary>
    private const double MarkInkWidth = 1.5;

    /// <summary>How far the outline of a placed mark sits outside it.</summary>
    private const double PlacementInset = 4;

    /// <summary>How far the focus ring sits outside the field.</summary>
    private const double FocusRingGap = 3;

    /// <summary>Half, for the reach of a handle from its centre.</summary>
    private const double Half = 0.5;

    /// <summary>The ends of an edge, for growing a rectangle on both sides.</summary>
    private const double Ends = 2;

    /// <summary>Adds the overlay planning to a canvas state.</summary>
    /// <param name="state">The canvas state.</param>
    extension(PageCanvasState state)
    {
        /// <summary>Draws the comment badges and everything an annotation tool shows while it is used: shape, polygon, mark, handles, focus ring, stroke and outline.</summary>
        /// <param name="painter">The toolkit's painter.</param>
        /// <param name="tab">The tab.</param>
        /// <param name="viewport">The part of the canvas in view.</param>
        public void DrawAnnotationOverlay(IOverlayPainter painter, DocumentTabViewModel tab, in LayoutRect viewport)
        {
            ArgumentNullException.ThrowIfNull(painter);
            ArgumentNullException.ThrowIfNull(tab);
            state.DrawCommentBadges(painter, tab, viewport);
            state.DrawShapePreview(painter, tab);
            state.DrawPolygonPreview(painter, tab);
            state.DrawPlacement(painter, tab);
            state.DrawEditing(painter, tab);
            state.DrawFieldFocus(painter, tab);
            state.DrawStroke(painter, tab);
            state.DrawPickedOutline(painter, tab);
        }

        /// <summary>Outlines the places to write on the pages in view while the Text tool is on.</summary>
        /// <param name="painter">The toolkit's painter.</param>
        /// <param name="tab">The tab.</param>
        /// <param name="viewport">The part of the canvas in view.</param>
        public void DrawRegions(IOverlayPainter painter, DocumentTabViewModel tab, in LayoutRect viewport)
        {
            ArgumentNullException.ThrowIfNull(painter);
            ArgumentNullException.ThrowIfNull(tab);
            var annotations = tab.Annotations;
            if (annotations.Tool != AnnotationTool.Text || annotations.IsEditingText)
            {
                return;
            }

            state.Layout.GetVisiblePages(viewport.X, viewport.Right, viewport.Y, viewport.Bottom, out var first, out var last);
            for (var page = first; page <= last && page >= 0; page++)
            {
                foreach (var region in annotations.RegionsOn(page))
                {
                    var hovered = state.Drawing.HoveredRegion is { } under && under.Page == page && under.Region == region;
                    painter.DrawRectangle(hovered ? OverlayInk.HoveredSuggestion : OverlayInk.Suggestion, state.GetCanvasRect(page, region.Bounds));
                }
            }
        }

        /// <summary>Draws the measurement: its lines, its points and the measurement beside the last point.</summary>
        /// <param name="painter">The toolkit's painter.</param>
        /// <param name="tab">The tab.</param>
        public void DrawMeasurement(IOverlayPainter painter, DocumentTabViewModel tab)
        {
            ArgumentNullException.ThrowIfNull(painter);
            ArgumentNullException.ThrowIfNull(tab);
            var measure = tab.Measure;
            var points = measure.Points;
            if (!measure.IsOn || measure.Page < 0 || measure.Page >= state.Sizes.Length || points.Count == 0 || !painter.CanDraw(OverlayInk.Hit))
            {
                return;
            }

            var transform = new PageTransform(state.Layout.GetPageBounds(measure.Page), state.Sizes[measure.Page], tab.Rotation, state.Layout.Options.Scale);
            var last = DrawMeasureLines(painter, transform, points, measure.Mode == MeasureMode.Area);
            if (measure.Result.Length > 0)
            {
                painter.DrawLabel(OverlayInk.Hit, measure.Result, last);
            }
        }

        /// <summary>Draws the outline of the area being dragged.</summary>
        /// <param name="painter">The toolkit's painter.</param>
        public void DrawArea(IOverlayPainter painter)
        {
            ArgumentNullException.ThrowIfNull(painter);
            if (state.Tools.AreaDragging && painter.CanDraw(OverlayInk.Hit))
            {
                painter.DrawRectangle(OverlayInk.Hit, state.GetArea());
            }
        }

        /// <summary>Draws the steady caret when caret mode is on.</summary>
        /// <param name="painter">The toolkit's painter.</param>
        /// <param name="tab">The tab.</param>
        public void DrawCaret(IOverlayPainter painter, DocumentTabViewModel tab)
        {
            ArgumentNullException.ThrowIfNull(painter);
            ArgumentNullException.ThrowIfNull(tab);
            if (tab.IsCaretMode && painter.CanDraw(OverlayInk.Caret) && state.TryGetCaretArea(out var area))
            {
                painter.FillRectangle(OverlayInk.Caret, area);
            }
        }

        /// <summary>Draws the speech-bubble badges on commented annotations on visible pages, and the icon preview of a new sticky note.</summary>
        /// <param name="painter">The toolkit's painter.</param>
        /// <param name="tab">The tab.</param>
        /// <param name="viewport">The part of the canvas in view.</param>
        private void DrawCommentBadges(IOverlayPainter painter, DocumentTabViewModel tab, in LayoutRect viewport)
        {
            // Without an annotation editor no page has a badge, so the per-page list copies are skipped.
            if (tab.Annotations.CanAnnotate)
            {
                state.GetPagesBetween(viewport.X, viewport.Right, viewport.Y, viewport.Bottom, out var first, out var last);
                for (var page = first; page <= last && page >= 0 && page < state.Sizes.Length; page++)
                {
                    state.DrawPageCommentBadges(painter, tab, page);
                }
            }

            if (tab.Annotations.ActiveBubble is not { NewNoteLocation: not null } bubble || (uint)bubble.PageIndex >= (uint)state.Sizes.Length)
            {
                return;
            }

            var transform = new PageTransform(state.Layout.GetPageBounds(bubble.PageIndex), state.Sizes[bubble.PageIndex], tab.Rotation, state.Layout.Options.Scale);
            painter.DrawBadge(transform.MapToCanvas(bubble.Bounds), bubble.Color);
        }

        /// <summary>Draws the speech-bubble badges on one page.</summary>
        /// <param name="painter">The toolkit's painter.</param>
        /// <param name="tab">The tab.</param>
        /// <param name="page">The page index.</param>
        private void DrawPageCommentBadges(IOverlayPainter painter, DocumentTabViewModel tab, int page)
        {
            tab.Annotations.GetPageAnnotations(page, state.BadgeAnnotations);
            if (state.BadgeAnnotations.Count == 0)
            {
                return;
            }

            var transform = new PageTransform(state.Layout.GetPageBounds(page), state.Sizes[page], tab.Rotation, state.Layout.Options.Scale);
            foreach (var annotation in state.BadgeAnnotations)
            {
                if (AnnotationsViewModel.HasCommentBadge(annotation))
                {
                    painter.DrawBadge(transform.MapToCanvas(AnnotationsViewModel.CommentBadgeBounds(annotation)), annotation.Color);
                }
            }
        }

        /// <summary>Draws the shape being dragged.</summary>
        /// <param name="painter">The toolkit's painter.</param>
        /// <param name="tab">The tab.</param>
        private void DrawShapePreview(IOverlayPainter painter, DocumentTabViewModel tab)
        {
            if (state.Drawing.ActiveShape is not { } shape || shape.Page < 0 || shape.Page >= state.Sizes.Length)
            {
                return;
            }

            var transform = new PageTransform(state.Layout.GetPageBounds(shape.Page), state.Sizes[shape.Page], tab.Rotation, state.Layout.Options.Scale);
            var start = transform.MapToCanvas(shape.Start);
            var end = transform.MapToCanvas(shape.End);
            var box = new LayoutRect(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));
            switch (shape.Kind)
            {
                case AnnotationKind.Rectangle:
                {
                    painter.DrawRectangle(OverlayInk.Stroke, box);
                    break;
                }

                case AnnotationKind.Ellipse:
                {
                    painter.DrawEllipse(OverlayInk.Stroke, box);
                    break;
                }

                default:
                {
                    painter.DrawLine(OverlayInk.Stroke, start, end);
                    break;
                }
            }
        }

        /// <summary>Draws the placed polygon corners, the sides between them and the side that follows the pointer.</summary>
        /// <param name="painter">The toolkit's painter.</param>
        /// <param name="tab">The tab.</param>
        private void DrawPolygonPreview(IOverlayPainter painter, DocumentTabViewModel tab)
        {
            var polygon = state.Drawing.Polygon;
            if (!polygon.IsActive || polygon.Page >= state.Sizes.Length || polygon.Corners.Count == 0)
            {
                return;
            }

            var transform = new PageTransform(state.Layout.GetPageBounds(polygon.Page), state.Sizes[polygon.Page], tab.Rotation, state.Layout.Options.Scale);
            var previous = transform.MapToCanvas(polygon.Corners[0]);
            foreach (var corner in polygon.Corners)
            {
                var next = transform.MapToCanvas(corner);
                painter.DrawLine(OverlayInk.Stroke, previous, next);
                painter.FillDot(OverlayInk.Stroke, next, CornerRadius);
                previous = next;
            }

            painter.DrawLine(OverlayInk.Stroke, previous, transform.MapToCanvas(polygon.Hover));
        }

        /// <summary>Draws the signature mark being placed and its outline.</summary>
        /// <param name="painter">The toolkit's painter.</param>
        /// <param name="tab">The tab.</param>
        private void DrawPlacement(IOverlayPainter painter, DocumentTabViewModel tab)
        {
            if (tab.FillAndSign.Placement is not { } placement || (uint)placement.Page >= (uint)state.Sizes.Length)
            {
                return;
            }

            var transform = new PageTransform(state.Layout.GetPageBounds(placement.Page), state.Sizes[placement.Page], tab.Rotation, state.Layout.Options.Scale);
            var area = transform.MapToCanvas(placement.Bounds);
            painter.DrawMark(placement.Mark, area, MarkInkWidth * transform.Scale);
            if (painter.CanDraw(OverlayInk.Hit))
            {
                painter.DrawRectangle(OverlayInk.Hit, Inflate(area, PlacementInset));
            }
        }

        /// <summary>Draws the outline of an annotation being dragged, and the resize handles of the picked one.</summary>
        /// <param name="painter">The toolkit's painter.</param>
        /// <param name="tab">The tab.</param>
        private void DrawEditing(IOverlayPainter painter, DocumentTabViewModel tab)
        {
            if (state.Tools.ActiveEdit is { } edit && (uint)edit.Annotation.PageIndex < (uint)state.Sizes.Length)
            {
                painter.DrawRectangle(OverlayInk.HitOrStroke, state.GetOutline(tab, edit.Annotation.PageIndex, edit.Bounds));
                return;
            }

            if (tab.Annotations.Selected is not { } picked || !AnnotationRules.CanResize(picked) || (uint)picked.PageIndex >= (uint)state.Sizes.Length)
            {
                return;
            }

            var outline = state.GetOutline(tab, picked.PageIndex, picked.Bounds);
            var side = CanvasMetrics.HandleSize;
            var reach = side * Half;
            painter.FillRectangle(OverlayInk.HitOrStroke, new(outline.X - reach, outline.Y - reach, side, side));
            painter.FillRectangle(OverlayInk.HitOrStroke, new(outline.Right - reach, outline.Y - reach, side, side));
            painter.FillRectangle(OverlayInk.HitOrStroke, new(outline.Right - reach, outline.Bottom - reach, side, side));
            painter.FillRectangle(OverlayInk.HitOrStroke, new(outline.X - reach, outline.Bottom - reach, side, side));
        }

        /// <summary>Draws a ring around the field with the keyboard focus, so it is clear where Space and Tab act.</summary>
        /// <param name="painter">The toolkit's painter.</param>
        /// <param name="tab">The tab.</param>
        private void DrawFieldFocus(IOverlayPainter painter, DocumentTabViewModel tab)
        {
            if (tab.Forms.Focused is not { } field || tab.Forms.Editing is not null || (uint)field.PageIndex >= (uint)state.Sizes.Length)
            {
                return;
            }

            painter.DrawRectangle(OverlayInk.FocusRing, Inflate(state.GetCanvasRect(field.PageIndex, field.Bounds), FocusRingGap));
        }

        /// <summary>Draws the freehand stroke being drawn.</summary>
        /// <param name="painter">The toolkit's painter.</param>
        /// <param name="tab">The tab.</param>
        private void DrawStroke(IOverlayPainter painter, DocumentTabViewModel tab)
        {
            var drawing = state.Drawing;
            if (drawing.StrokePage < 0 || drawing.Stroke.Count <= 1)
            {
                return;
            }

            var transform = new PageTransform(state.Layout.GetPageBounds(drawing.StrokePage), state.Sizes[drawing.StrokePage], tab.Rotation, state.Layout.Options.Scale);
            var highlighter = tab.Annotations.Tool == AnnotationTool.FreehandHighlight;
            var width = AnnotationsViewModel.HighlighterWidth * state.Layout.Options.Scale;
            var previous = transform.MapToCanvas(drawing.Stroke[0]);
            for (var i = 1; i < drawing.Stroke.Count; i++)
            {
                var next = transform.MapToCanvas(drawing.Stroke[i]);
                painter.DrawStrokeLine(highlighter, tab.Annotations.Color, width, previous, next);
                previous = next;
            }
        }

        /// <summary>Draws the outline of the picked annotation.</summary>
        /// <param name="painter">The toolkit's painter.</param>
        /// <param name="tab">The tab.</param>
        private void DrawPickedOutline(IOverlayPainter painter, DocumentTabViewModel tab)
        {
            if (tab.Annotations.Selected is { } selected && selected.PageIndex < state.Sizes.Length && painter.CanDraw(OverlayInk.Hit))
            {
                painter.DrawRectangle(OverlayInk.Hit, state.GetOutline(tab, selected.PageIndex, selected.Bounds));
            }
        }
    }

    /// <summary>Draws a measurement's lines and points.</summary>
    /// <param name="painter">The toolkit's painter.</param>
    /// <param name="transform">The page transform.</param>
    /// <param name="points">The points, in page space.</param>
    /// <param name="closed">Whether the shape closes back to its first point.</param>
    /// <returns>The last point, in canvas space.</returns>
    private static CanvasPoint DrawMeasureLines(IOverlayPainter painter, in PageTransform transform, IReadOnlyList<PagePoint> points, bool closed)
    {
        var previous = transform.MapToCanvas(points[0]);
        var first = previous;
        for (var i = 1; i < points.Count; i++)
        {
            var next = transform.MapToCanvas(points[i]);
            painter.DrawLine(OverlayInk.Hit, previous, next);
            previous = next;
        }

        if (closed && points.Count > 2)
        {
            painter.DrawLine(OverlayInk.Hit, previous, first);
        }

        foreach (var point in points)
        {
            painter.FillDot(OverlayInk.Hit, transform.MapToCanvas(point), MeasurePointRadius);
        }

        return previous;
    }

    /// <summary>Gets a rectangle grown by a distance on every side.</summary>
    /// <param name="rect">The rectangle.</param>
    /// <param name="distance">How far each edge moves out.</param>
    /// <returns>The grown rectangle.</returns>
    private static LayoutRect Inflate(in LayoutRect rect, double distance) =>
        new(rect.X - distance, rect.Y - distance, rect.Width + (distance * Ends), rect.Height + (distance * Ends));
}
