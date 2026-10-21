// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Layout;

namespace HyperPdf.Presentation.Canvas;

/// <summary>Drawing ink, shapes and polygons, click tools and placing marks, for any toolkit's canvas.</summary>
public static class CanvasDrawingMixins
{
    /// <summary>The most points one ink stroke keeps.</summary>
    private const int MaxStrokePoints = 4096;

    /// <summary>The least a shape is dragged, in points, for it to be added rather than treated as a click.</summary>
    private const float MinShapeDrag = 4;

    /// <summary>Adds the annotation drawing helpers to a canvas state.</summary>
    /// <param name="state">The canvas state.</param>
    extension(PageCanvasState state)
    {
        /// <summary>Moves the next polygon side's free end with the pointer.</summary>
        /// <param name="position">The canvas point.</param>
        public void HoverPolygon(CanvasPoint position)
        {
            var polygon = state.Drawing.Polygon;
            if (polygon.IsActive && state.Tab is { } tab && polygon.MoveHover(state.ToPage(tab, polygon.Page, position)))
            {
                state.Host.InvalidateVisual();
            }
        }

        /// <summary>Forgets the placed polygon corners.</summary>
        public void CancelPolygon()
        {
            if (state.Drawing.Polygon.Cancel())
            {
                state.Host.InvalidateVisual();
            }
        }

        /// <summary>Adds a corner when a polygon tool is active, or finishes the polygon on a double-click.</summary>
        /// <param name="tab">The active tab.</param>
        /// <param name="page">The page index, or -1.</param>
        /// <param name="position">The canvas position.</param>
        /// <param name="finish">Whether the press was a double-click, which finishes the polygon.</param>
        public void PressPolygonCorner(DocumentTabViewModel tab, int page, CanvasPoint position, bool finish)
        {
            ArgumentNullException.ThrowIfNull(tab);
            if (page >= 0 && state.Drawing.Polygon.Press(tab.Annotations, page, state.ToPage(tab, page, position), finish))
            {
                state.Host.InvalidateVisual();
            }
        }

        /// <summary>Handles Enter (finish), Backspace (take back a corner) and Escape (start again) while placing corners.</summary>
        /// <param name="key">The key.</param>
        /// <returns><see langword="true"/> when the key was used.</returns>
        public bool HandlePolygonKey(ViewerKey key)
        {
            if (state.Tab is not { } tab || !DocumentKeys.HandlePolygon(state.Drawing.Polygon, tab.Annotations, key))
            {
                return false;
            }

            state.Host.InvalidateVisual();
            return true;
        }

        /// <summary>Starts dragging a shape or callout annotation.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="page">The page.</param>
        /// <param name="position">The canvas point.</param>
        /// <returns><see langword="true"/> when the chosen tool draws a shape.</returns>
        public bool BeginShape(DocumentTabViewModel tab, int page, CanvasPoint position)
        {
            ArgumentNullException.ThrowIfNull(tab);
            if (AnnotationsViewModel.GetShapeKind(tab.Annotations.Tool) is not { } shapeKind)
            {
                return false;
            }

            var start = state.ToPage(tab, page, position);
            state.Drawing.ActiveShape = new(shapeKind, page, start, start);
            return true;
        }

        /// <summary>Starts a freehand ink stroke.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="page">The page.</param>
        /// <param name="position">The canvas point.</param>
        public void BeginInk(DocumentTabViewModel tab, int page, CanvasPoint position)
        {
            ArgumentNullException.ThrowIfNull(tab);
            var drawing = state.Drawing;
            drawing.Stroke.Clear();
            drawing.StrokePage = page;
            drawing.Stroke.Add(state.ToPage(tab, page, position));
        }

        /// <summary>Notes the page a click tool was pressed on, until the release adds the annotation.</summary>
        /// <param name="page">The page.</param>
        /// <param name="snaps">Whether the click snaps to a detected form region.</param>
        public void BeginClick(int page, bool snaps)
        {
            state.Drawing.ClickPage = page;
            state.Drawing.ClickSnaps = snaps;
        }

        /// <summary>Adds a point to the stroke or shape being drawn, or follows the polygon's free end.</summary>
        /// <param name="position">The canvas point.</param>
        /// <returns><see langword="true"/> when a shape, stroke or edit is under way.</returns>
        public bool ContinueDrawing(CanvasPoint position)
        {
            state.HoverPolygon(position);
            if (state.ContinueShape(position) || state.Tools.ActiveEdit is not null)
            {
                return true;
            }

            var drawing = state.Drawing;
            if (drawing.StrokePage < 0 || state.Tab is not { } tab)
            {
                return false;
            }

            if (drawing.Stroke.Count < MaxStrokePoints)
            {
                drawing.Stroke.Add(state.ToPage(tab, drawing.StrokePage, position));
                state.Host.InvalidateVisual();
            }

            return true;
        }

        /// <summary>Finishes a shape, ink stroke or click tool on pointer release.</summary>
        /// <param name="position">The canvas point.</param>
        /// <returns><see langword="true"/> when the release was used.</returns>
        public bool EndDrawing(CanvasPoint position)
        {
            if (state.Tab is not { } tab)
            {
                return false;
            }

            if (state.EndShape(tab, position))
            {
                return true;
            }

            var drawing = state.Drawing;
            if (drawing.StrokePage >= 0)
            {
                state.EndStroke(tab);
                return true;
            }

            if (drawing.ClickPage < 0)
            {
                return false;
            }

            var clicked = drawing.ClickPage;
            drawing.ClickPage = -1;
            ClickTools.Run(tab.Annotations, clicked, state.ToPage(tab, clicked, position), drawing.ClickSnaps);
            return true;
        }

        /// <summary>Follows the pointer over the places to write, redrawing when it moves onto or off one.</summary>
        /// <param name="isOverPage">Whether the pointer is over a page.</param>
        /// <param name="page">The page under the pointer.</param>
        /// <param name="point">The point on that page.</param>
        public void HoverRegion(bool isOverPage, int page, PagePoint point)
        {
            HoveredFormRegion? hovered = null;
            if (isOverPage && state.Tab is { Annotations: { Tool: AnnotationTool.Text, IsEditingText: false } annotations }
                && annotations.RegionAt(page, point) is { } region)
            {
                hovered = new(page, region);
            }

            if (hovered == state.Drawing.HoveredRegion)
            {
                return;
            }

            state.Drawing.HoveredRegion = hovered;
            state.Host.InvalidateVisual();
        }

        /// <summary>Moves the mark being placed by a distance on screen, so arrows follow the screen on a rotated page.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="dx">The move right on screen, in points.</param>
        /// <param name="dy">The move down on screen, in points.</param>
        /// <returns><see langword="true"/> when a mark is being placed.</returns>
        public bool MovePlacement(DocumentTabViewModel tab, float dx, float dy)
        {
            ArgumentNullException.ThrowIfNull(tab);
            if (tab.FillAndSign.Placement is not { } placement || (uint)placement.Page >= (uint)state.Sizes.Length)
            {
                return false;
            }

            var transform = new PageTransform(state.Layout.GetPageBounds(placement.Page), state.Sizes[placement.Page], tab.Rotation, state.Layout.Options.Scale);
            var origin = transform.MapToCanvas(new PagePoint(placement.Bounds.Left, placement.Bounds.Top));
            var start = transform.MapToPage(origin.X, origin.Y);
            var end = transform.MapToPage(origin.X + (dx * transform.Scale), origin.Y + (dy * transform.Scale));
            return tab.FillAndSign.Move(end.X - start.X, end.Y - start.Y);
        }

        /// <summary>Follows the pointer with the shape's free end.</summary>
        /// <param name="position">The canvas point.</param>
        /// <returns><see langword="true"/> while a shape is dragged.</returns>
        private bool ContinueShape(CanvasPoint position)
        {
            if (state.Drawing.ActiveShape is not { } shape || state.Tab is not { } tab)
            {
                return false;
            }

            state.Drawing.ActiveShape = shape with { End = state.ToPage(tab, shape.Page, position) };
            state.Host.InvalidateVisual();
            return true;
        }

        /// <summary>Adds the dragged shape on release.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="position">The canvas point.</param>
        /// <returns><see langword="true"/> when a shape was being dragged.</returns>
        private bool EndShape(DocumentTabViewModel tab, CanvasPoint position)
        {
            if (state.Drawing.ActiveShape is not { } shape)
            {
                return false;
            }

            state.Drawing.ActiveShape = null;
            var end = state.ToPage(tab, shape.Page, position);
            if (Math.Abs(end.X - shape.Start.X) + Math.Abs(end.Y - shape.Start.Y) < MinShapeDrag)
            {
                state.Host.InvalidateVisual();
                return true;
            }

            if (shape.Kind == AnnotationKind.Callout)
            {
                _ = tab.Annotations.AddCalloutAsync(shape.Page, shape.Start, end);
            }
            else
            {
                _ = tab.Annotations.AddShape(shape.Page, shape.Kind, shape.Start, end);
            }

            state.Host.InvalidateVisual();
            return true;
        }

        /// <summary>Adds the ink stroke that was being drawn.</summary>
        /// <param name="tab">The tab.</param>
        private void EndStroke(DocumentTabViewModel tab)
        {
            var drawing = state.Drawing;
            var page = drawing.StrokePage;
            drawing.StrokePage = -1;
            if (drawing.Stroke.Count > 1)
            {
                ReadOnlySpan<int> lengths = [drawing.Stroke.Count];
                var kind = tab.Annotations.Tool == AnnotationTool.FreehandHighlight ? AnnotationKind.FreehandHighlight : AnnotationKind.Ink;
                _ = tab.Annotations.AddInk(page, CollectionsMarshal.AsSpan(drawing.Stroke), lengths, kind);
            }

            drawing.Stroke.Clear();
            state.Host.InvalidateVisual();
        }
    }
}
