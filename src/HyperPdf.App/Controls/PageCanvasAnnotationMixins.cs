// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using HyperPdf.App.Views;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Forms.Detection;
using HyperPdf.Core.Geometry;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Controls;

/// <summary>
/// Reactive streams, keyboard handlers and overlay drawing for annotations, shapes, polygons, ink strokes, signature
/// placement, form fields and printed-form region suggestions on <see cref="PageCanvas"/>.
/// </summary>
internal static class PageCanvasAnnotationMixins
{
    /// <summary>How far the selected annotation outline sits outside the annotation.</summary>
    internal const double OutlineInset = 2;

    /// <summary>The colour of the live stroke while drawing: the annotation ink colour.</summary>
    internal static readonly ImmutablePen StrokePen = new(new ImmutableSolidColorBrush(Color.FromUInt32(0xFF000000U | AnnotationColors.Ink)), OverlayPenWidth);

    /// <summary>The width of the live stroke and the selected annotation outline, in device independent pixels.</summary>
    private const double OverlayPenWidth = 1.5;

    /// <summary>The corner radius of on-page comment indicator badges.</summary>
    private const double BadgeRadius = 3;

    /// <summary>The left inset of the ruled lines inside a comment indicator badge, as a fraction of its width.</summary>
    private const double BadgeLineLeft = 0.25;

    /// <summary>The right inset of the ruled lines inside a comment indicator badge, as a fraction of its width.</summary>
    private const double BadgeLineRight = 0.75;

    /// <summary>The vertical position of the first ruled line inside a comment indicator badge, as a fraction of its height.</summary>
    private const double BadgeFirstLineY = 0.38;

    /// <summary>The vertical position of the second ruled line inside a comment indicator badge, as a fraction of its height.</summary>
    private const double BadgeSecondLineY = 0.64;

    /// <summary>Points kept per drawing before a new stroke is needed.</summary>
    private const int MaxStrokePoints = 4096;

    /// <summary>The shortest drag, in points, that draws a shape; shorter drags are taken as stray clicks.</summary>
    private const float MinShapeDrag = 4;

    /// <summary>The radius of a placed corner's dot.</summary>
    private const double CornerRadius = 3;

    /// <summary>The width of a drawn signature's ink on the page, in points.</summary>
    private const double MarkInkWidth = 1.5;

    /// <summary>How far the placement outline sits outside the mark.</summary>
    private const double PlacementInset = 4;

    /// <summary>The alpha of the highlighter pen's preview, about 40% like the highlight it saves.</summary>
    private const uint HighlighterAlpha = 0x66000000U;

    /// <summary>The smallest zoom change that makes the ink pen again.</summary>
    private const double PenTolerance = 0.001;

    /// <summary>The width of the field focus ring.</summary>
    private const double FocusRingWidth = 2;

    /// <summary>How far outside the field the focus ring sits.</summary>
    private const double FocusRingGap = 3;

    /// <summary>The width of a suggestion's outline.</summary>
    private const double SuggestionWidth = 1;

    /// <summary>The width of the outline of the suggestion under the pointer.</summary>
    private const double HoveredSuggestionWidth = 2;

    /// <summary>The border and line pen for on-page comment indicator badges.</summary>
    private static readonly ImmutablePen BadgeBorderPen = new(new ImmutableSolidColorBrush(Color.FromArgb(0xB0, 0x20, 0x20, 0x20)), 1);

    /// <summary>The ink brush for typed marks.</summary>
    private static readonly IImmutableSolidColorBrush MarkInk = new ImmutableSolidColorBrush(Color.FromUInt32(0xFF000000U | AnnotationColors.Ink));

    /// <summary>The dashes of a suggestion's outline.</summary>
    private static readonly ImmutableDashStyle SuggestionDashes = new([3, 3], 0);

    /// <summary>Adds the annotation tool, form field, placement and overlay helpers to a page canvas.</summary>
    /// <param name="canvas">The page canvas.</param>
    extension(PageCanvas canvas)
    {
        /// <summary>
        /// Gets the left pointer presses claimed by signature placement, shape dragging, polygon corner placement,
        /// freehand ink drawing, form field activation, or click-based annotation tools (Note, Text, Stamp).
        /// </summary>
        /// <param name="tabs">The tab stream.</param>
        /// <returns>The matched drawing or field presses.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<DrawingToolPress> DrawingAndFieldPresses(IObservable<DocumentTabViewModel?> tabs) =>
            canvas.PointerPresses
                .Where(static e => !e.Handled)
                .Inject(tabs)
                .Pick(canvas.TryMatchDrawingOrFieldPress);

        /// <summary>Fills the field under a click, if there is one.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="page">The page.</param>
        /// <param name="position">The canvas point.</param>
        /// <returns><see langword="true"/> when a field took the click.</returns>
        internal bool TryActivateField(DocumentTabViewModel tab, int page, Point position)
        {
            if (tab.Forms.HitTest(page, canvas.ToPage(tab, page, position)) is not { } field)
            {
                return false;
            }

            if (!tab.Forms.Activate(field) && field.Kind is FormFieldKind.ComboBox or FormFieldKind.ListBox)
            {
                canvas.ShowChoices(tab.Forms, field);
            }

            canvas.InvalidateVisual();
            return true;
        }

        /// <summary>Determines whether a canvas point is over a fillable field.</summary>
        /// <param name="hover">The page and page point under the pointer.</param>
        /// <returns><see langword="true"/> when over a field.</returns>
        internal bool IsOverField(PointerHover hover) =>
            hover.IsOverPage && canvas.Tab is { } tab && tab.Forms.HitTest(hover.Page, hover.Point) is not null;

        /// <summary>Moves between form fields with Tab and Shift+Tab, ticks the focused box with Space, opens a focused list with Enter, and drops focus with Escape.</summary>
        /// <param name="e">The key press.</param>
        /// <returns><see langword="true"/> when a field took the key.</returns>
        internal bool HandleFormKey(KeyEventArgs e)
        {
            if (canvas.Tab is not { } tab)
            {
                return false;
            }

            var result = DocumentKeys.HandleForm(tab, e.ViewerKey, e.ViewerModifiers);
            switch (result.Outcome)
            {
                case FormKeyOutcome.Redraw:
                {
                    canvas.InvalidateVisual();
                    return true;
                }

                case FormKeyOutcome.OpenChoices:
                {
                    canvas.ShowChoices(tab.Forms, result.Field!);
                    return true;
                }

                default:
                {
                    return false;
                }
            }
        }

        /// <summary>Handles the placing keys while a signature or initials mark is being placed.</summary>
        /// <param name="e">The key press.</param>
        /// <returns><see langword="true"/> when the key was used.</returns>
        internal bool HandlePlacementKey(KeyEventArgs e)
        {
            if (canvas.Tab is not { } tab)
            {
                return false;
            }

            var used = DocumentKeys.HandlePlacement(
                tab.FillAndSign,
                e.ViewerKey,
                e.ViewerModifiers,
                new PlacementMove(canvas, tab),
                static (move, dx, dy) => move.Canvas.MovePlacement(move.Tab, dx, dy));
            if (used)
            {
                canvas.InvalidateVisual();
            }

            return used;
        }

        /// <summary>Handles the polygon tools' keys, Escape, Delete and the arrow keys.</summary>
        /// <param name="e">The key press.</param>
        /// <returns><see langword="true"/> when the key was used.</returns>
        internal bool HandleAnnotationKey(KeyEventArgs e)
        {
            var key = e.ViewerKey;
            if (canvas.HandlePolygonKey(key) || canvas.NudgeSelected(e))
            {
                return true;
            }

            if (key == ViewerKey.Escape)
            {
                canvas.ClearSelection();
                canvas.Tab?.Annotations.CloseBubble();
                canvas.Tab?.Annotations.Select(null);
                return false;
            }

            return canvas.Tab?.Annotations is { } annotations
                && (DocumentKeys.EditPickedText(annotations, key, e.ViewerModifiers) || DocumentKeys.DeleteSelected(annotations, key));
        }

        /// <summary>Adds a point to the stroke or shape being drawn.</summary>
        /// <param name="position">The canvas point.</param>
        /// <returns><see langword="true"/> when a shape or stroke is being drawn.</returns>
        internal bool ContinueStroke(Point position)
        {
            canvas.HoverPolygon(position);
            if (canvas.ContinueShape(position) || canvas.ActiveEdit is not null)
            {
                return true;
            }

            if (canvas.StrokePage < 0 || canvas.Tab is not { } tab)
            {
                return false;
            }

            if (canvas.Stroke.Count < MaxStrokePoints)
            {
                canvas.Stroke.Add(canvas.ToPage(tab, canvas.StrokePage, position));
                canvas.InvalidateVisual();
            }

            return true;
        }

        /// <summary>Finishes a shape, ink stroke or click tool on pointer release.</summary>
        /// <param name="position">The canvas point.</param>
        /// <returns><see langword="true"/> when the release was handled.</returns>
        internal bool EndAnnotationPress(Point position)
        {
            if (canvas.Tab is not { } tab)
            {
                return false;
            }

            if (canvas.EndShape(tab, position))
            {
                return true;
            }

            if (canvas.StrokePage >= 0)
            {
                var page = canvas.StrokePage;
                canvas.StrokePage = -1;
                if (canvas.Stroke.Count > 1)
                {
                    ReadOnlySpan<int> lengths = [canvas.Stroke.Count];
                    var kind = tab.Annotations.Tool == AnnotationTool.FreehandHighlight ? AnnotationKind.FreehandHighlight : AnnotationKind.Ink;
                    _ = tab.Annotations.AddInk(page, CollectionsMarshal.AsSpan(canvas.Stroke), lengths, kind);
                }

                canvas.Stroke.Clear();
                canvas.InvalidateVisual();
                return true;
            }

            if (canvas.ClickPage < 0)
            {
                return false;
            }

            var clicked = canvas.ClickPage;
            canvas.ClickPage = -1;
            ClickTools.Run(tab.Annotations, clicked, canvas.ToPage(tab, clicked, position), canvas.ClickSnaps);
            return true;
        }

        /// <summary>Marks the selected text when a markup tool is active, then clears the selection.</summary>
        internal void ApplyMarkupTool()
        {
            if (canvas.Tab is not { } tab || AnnotationsViewModel.GetMarkupKind(tab.Annotations.Tool) is not { } kind)
            {
                return;
            }

            canvas.MarkSelection(tab, kind, tab.Annotations.Color);
        }

        /// <summary>Gets the line rectangles of the selection on each page it covers.</summary>
        /// <returns>The rectangles by page; empty when nothing is selected.</returns>
        internal Dictionary<int, List<PageRect>> GetSelectionLines()
        {
            var lines = new Dictionary<int, List<PageRect>>();
            if (!canvas.TryGetSelection(out var start, out var end))
            {
                return lines;
            }

            for (var page = start.Page; page <= end.Page; page++)
            {
                lines[page] = [.. canvas.GetSelectionRects(page)];
            }

            return lines;
        }

        /// <summary>Follows the pointer over the places to write, redrawing when it moves onto or off one.</summary>
        /// <param name="hover">The page and page point under the pointer.</param>
        internal void HoverRegion(PointerHover hover)
        {
            HoveredFormRegion? hovered = null;
            if (hover.IsOverPage && canvas.Tab is { Annotations: { Tool: AnnotationTool.Text, IsEditingText: false } annotations }
                && annotations.RegionAt(hover.Page, hover.Point) is { } region)
            {
                hovered = new(hover.Page, region);
            }

            if (hovered == canvas.HoveredRegion)
            {
                return;
            }

            canvas.HoveredRegion = hovered;
            canvas.InvalidateVisual();
        }

        /// <summary>Moves the next polygon side's free end with the pointer.</summary>
        /// <param name="position">The canvas point.</param>
        internal void HoverPolygon(Point position)
        {
            if (canvas.Polygon.IsActive && canvas.Tab is { } tab && canvas.Polygon.MoveHover(canvas.ToPage(tab, canvas.Polygon.Page, position)))
            {
                canvas.InvalidateVisual();
            }
        }

        /// <summary>Forgets the placed polygon corners.</summary>
        internal void CancelPolygon()
        {
            if (canvas.Polygon.Cancel())
            {
                canvas.InvalidateVisual();
            }
        }

        /// <summary>
        /// Gets the pen the live stroke is drawn with: thin ink for drawing, or a wide see-through pen in the chosen colour for the
        /// highlighter. The highlighter pen is kept until the colour or zoom changes.
        /// </summary>
        /// <param name="tab">The tab.</param>
        /// <returns>The pen.</returns>
        internal IPen GetStrokePen(DocumentTabViewModel tab)
        {
            if (tab.Annotations.Tool != AnnotationTool.FreehandHighlight)
            {
                return StrokePen;
            }

            var width = AnnotationsViewModel.HighlighterWidth * canvas.Layout.Options.Scale;
            var color = Color.FromUInt32(HighlighterAlpha | tab.Annotations.Color);
            if (canvas.HighlighterPen is { Brush: ISolidColorBrush brush } cached && brush.Color == color && Math.Abs(cached.Thickness - width) <= PenTolerance)
            {
                return cached;
            }

            var made = new Pen(new ImmutableSolidColorBrush(color), width, lineCap: PenLineCap.Round);
            canvas.HighlighterPen = made;
            return made;
        }

        /// <summary>Draws the stroke being drawn, comment badges and the outline of the picked annotation.</summary>
        /// <param name="context">The drawing context.</param>
        /// <param name="tab">The tab.</param>
        internal void DrawAnnotationOverlay(DrawingContext context, DocumentTabViewModel tab)
        {
            canvas.DrawCommentBadges(context, tab);
            canvas.DrawShapePreview(context, tab);
            canvas.DrawPolygonPreview(context, tab);
            canvas.DrawPlacement(context, tab);
            canvas.DrawEditing(context, tab);
            canvas.DrawFieldFocus(context, tab);
            if (canvas.StrokePage >= 0 && canvas.Stroke.Count > 1)
            {
                var transform = new PageTransform(canvas.Layout.GetPageBounds(canvas.StrokePage), canvas.Sizes[canvas.StrokePage], tab.Rotation, canvas.Layout.Options.Scale);
                var previous = transform.ToCanvas(canvas.Stroke[0]);
                var strokePen = canvas.GetStrokePen(tab);
                for (var i = 1; i < canvas.Stroke.Count; i++)
                {
                    var next = transform.ToCanvas(canvas.Stroke[i]);
                    context.DrawLine(strokePen, previous, next);
                    previous = next;
                }
            }

            if (tab.Annotations.Selected is { } selected && selected.PageIndex < canvas.Sizes.Length && canvas.CurrentHitPen is { } pen)
            {
                var transform = new PageTransform(canvas.Layout.GetPageBounds(selected.PageIndex), canvas.Sizes[selected.PageIndex], tab.Rotation, canvas.Layout.Options.Scale);
                context.DrawRectangle(null, pen, transform.ToCanvas(selected.Bounds).Inflate(OutlineInset));
            }
        }

        /// <summary>Outlines the places to write on the pages in view while the Text tool is on.</summary>
        /// <param name="context">The drawing context.</param>
        /// <param name="tab">The tab.</param>
        /// <param name="viewport">The viewport.</param>
        internal void DrawRegions(DrawingContext context, DocumentTabViewModel tab, in Rect viewport)
        {
            var annotations = tab.Annotations;
            if (annotations.Tool != AnnotationTool.Text || annotations.IsEditingText)
            {
                return;
            }

            canvas.Layout.GetVisiblePages(viewport.Left, viewport.Right, viewport.Top, viewport.Bottom, out var first, out var last);
            canvas.UpdateSuggestionPens((canvas.CurrentHitPen ?? StrokePen).Brush);
            for (var page = first; page <= last && page >= 0; page++)
            {
                foreach (var region in annotations.RegionsOn(page))
                {
                    var hovered = canvas.HoveredRegion is { } under && under.Page == page && under.Region == region;
                    context.DrawRectangle(null, hovered ? canvas.HoveredSuggestionPen : canvas.SuggestionPen, canvas.GetCanvasRect(page, region.Bounds));
                }
            }
        }

        /// <summary>Adds a corner when a polygon tool is active, or finishes the polygon on a double-click.</summary>
        /// <param name="tab">The active tab.</param>
        /// <param name="page">The page index, or -1.</param>
        /// <param name="position">The canvas position.</param>
        /// <param name="clickCount">The pointer click count.</param>
        internal void PressPolygonCorner(DocumentTabViewModel tab, int page, Point position, int clickCount)
        {
            if (page >= 0 && canvas.Polygon.Press(tab.Annotations, page, canvas.ToPage(tab, page, position), clickCount >= PageCanvasToolMixins.DoubleClick))
            {
                canvas.InvalidateVisual();
            }
        }

        /// <summary>Matches a left pointer press to signature placement, shape/polygon/ink drawing, form field activation or click tools.</summary>
        /// <param name="item">The pointer press and tab.</param>
        /// <returns>The matched press action, or <see langword="null"/> when the press belongs to Select or text selection.</returns>
        private DrawingToolPress? TryMatchDrawingOrFieldPress(Injected<PointerPressedEventArgs, DocumentTabViewModel> item)
        {
            var (e, tab) = item;
            var point = e.GetCurrentPoint(canvas);
            var buttons = point.ViewerButtons;
            if (!PointerRouting.IsPlainLeft(buttons))
            {
                return null;
            }

            var page = canvas.Layout.HitTest(point.Position.X, point.Position.Y);
            var kind = PointerRouting.GetDrawingPress(tab, buttons, page, page >= 0 ? canvas.ToPage(tab, page, point.Position) : default);
            if (kind is not (DrawingPressKind.Placement or DrawingPressKind.Shape or DrawingPressKind.Polygon))
            {
                canvas.CancelPolygon();
            }

            return kind switch
            {
                DrawingPressKind.Placement => new(canvas, tab, e, page, point.Position, DrawingToolPress.Placement),
                DrawingPressKind.Shape => new(canvas, tab, e, page, point.Position, DrawingToolPress.Shape),
                DrawingPressKind.Polygon => new(canvas, tab, e, page, point.Position, DrawingToolPress.Polygon),
                DrawingPressKind.Ink => new(canvas, tab, e, page, point.Position, DrawingToolPress.Ink),
                DrawingPressKind.Field => new(canvas, tab, e, page, point.Position, DrawingToolPress.Field),
                DrawingPressKind.ClickTool => new(canvas, tab, e, page, point.Position, DrawingToolPress.ClickTool),
                _ => null,
            };
        }

        /// <summary>Follows the pointer with the shape's free end.</summary>
        /// <param name="position">The canvas point.</param>
        /// <returns><see langword="true"/> while a shape is dragged.</returns>
        private bool ContinueShape(Point position)
        {
            if (canvas.ActiveShape is not { } shape || canvas.Tab is not { } tab)
            {
                return false;
            }

            canvas.ActiveShape = shape with { End = canvas.ToPage(tab, shape.Page, position) };
            canvas.InvalidateVisual();
            return true;
        }

        /// <summary>Adds the dragged shape on release.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="position">The canvas point.</param>
        /// <returns><see langword="true"/> when a shape was being dragged.</returns>
        private bool EndShape(DocumentTabViewModel tab, Point position)
        {
            if (canvas.ActiveShape is not { } shape)
            {
                return false;
            }

            canvas.ActiveShape = null;
            var end = canvas.ToPage(tab, shape.Page, position);
            if (Math.Abs(end.X - shape.Start.X) + Math.Abs(end.Y - shape.Start.Y) < MinShapeDrag)
            {
                canvas.InvalidateVisual();
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

            canvas.InvalidateVisual();
            return true;
        }

        /// <summary>Handles Enter (finish), Backspace (take back a corner) and Escape (start again) while placing corners.</summary>
        /// <param name="key">The key.</param>
        /// <returns><see langword="true"/> when the key was used.</returns>
        private bool HandlePolygonKey(ViewerKey key)
        {
            if (canvas.Tab is not { } tab || !canvas.Polygon.HandleKey(tab.Annotations, key))
            {
                return false;
            }

            canvas.InvalidateVisual();
            return true;
        }

        /// <summary>Moves the mark being placed by a distance on screen, so arrows follow the screen on a rotated page.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="dx">The move right on screen, in points.</param>
        /// <param name="dy">The move down on screen, in points.</param>
        /// <returns><see langword="true"/> when a mark is being placed.</returns>
        private bool MovePlacement(DocumentTabViewModel tab, float dx, float dy)
        {
            if (tab.FillAndSign.Placement is not { } placement || (uint)placement.Page >= (uint)canvas.Sizes.Length)
            {
                return false;
            }

            var transform = new PageTransform(canvas.Layout.GetPageBounds(placement.Page), canvas.Sizes[placement.Page], tab.Rotation, canvas.Layout.Options.Scale);
            var origin = transform.ToCanvas(new PagePoint(placement.Bounds.Left, placement.Bounds.Top));
            var start = transform.ToPage(origin);
            var end = transform.ToPage(origin + new Vector(dx * transform.Scale, dy * transform.Scale));
            return tab.FillAndSign.Move(end.X - start.X, end.Y - start.Y);
        }

        /// <summary>Marks the selected text and clears the selection.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="kind">The markup kind.</param>
        /// <param name="color">The colour.</param>
        private void MarkSelection(DocumentTabViewModel tab, AnnotationKind kind, uint color)
        {
            if (!canvas.TryGetSelection(out _, out _))
            {
                return;
            }

            if (tab.Annotations.MarkText(kind, canvas.GetSelectionLines(), color))
            {
                canvas.ClearSelection();
            }
        }

        /// <summary>Makes the suggestion outlines again when their colour changes, so drawing does not allocate.</summary>
        /// <param name="brush">The outline colour.</param>
        private void UpdateSuggestionPens(IBrush? brush)
        {
            if (canvas.SuggestionPen is not null && ReferenceEquals(canvas.SuggestionPen.Brush, brush))
            {
                return;
            }

            canvas.SuggestionPen = new(brush, SuggestionWidth, SuggestionDashes);
            canvas.HoveredSuggestionPen = new(brush, HoveredSuggestionWidth);
        }

        /// <summary>Draws the speech-bubble badges on commented annotations on visible pages, and the icon preview of a new sticky note.</summary>
        /// <param name="context">The drawing context.</param>
        /// <param name="tab">The tab.</param>
        private void DrawCommentBadges(DrawingContext context, DocumentTabViewModel tab)
        {
            if (canvas.Scroller is not { } scroller)
            {
                return;
            }

            // Without an annotation editor no page has a badge, so the per-page list copies are skipped.
            if (tab.Annotations.CanAnnotate)
            {
                canvas.GetPagesBetween(scroller.Offset.X, scroller.Offset.X + scroller.Viewport.Width, scroller.Offset.Y, scroller.Offset.Y + scroller.Viewport.Height, out var first, out var last);
                for (var page = first; page <= last && page >= 0 && page < canvas.Sizes.Length; page++)
                {
                    canvas.DrawPageCommentBadges(context, tab, page);
                }
            }

            if (tab.Annotations.ActiveBubble is not { NewNoteLocation: not null } bubble || (uint)bubble.PageIndex >= (uint)canvas.Sizes.Length)
            {
                return;
            }

            var transform = new PageTransform(canvas.Layout.GetPageBounds(bubble.PageIndex), canvas.Sizes[bubble.PageIndex], tab.Rotation, canvas.Layout.Options.Scale);
            DrawBadgeIcon(context, transform.ToCanvas(bubble.Bounds), bubble.Color);
        }

        /// <summary>Draws the speech-bubble badges on one page.</summary>
        /// <param name="context">The drawing context.</param>
        /// <param name="tab">The tab.</param>
        /// <param name="page">The page index.</param>
        private void DrawPageCommentBadges(DrawingContext context, DocumentTabViewModel tab, int page)
        {
            tab.Annotations.GetPageAnnotations(page, canvas.BadgeAnnotations);
            if (canvas.BadgeAnnotations.Count == 0)
            {
                return;
            }

            var transform = new PageTransform(canvas.Layout.GetPageBounds(page), canvas.Sizes[page], tab.Rotation, canvas.Layout.Options.Scale);
            foreach (var annotation in canvas.BadgeAnnotations)
            {
                if (AnnotationsViewModel.HasCommentBadge(annotation))
                {
                    DrawBadgeIcon(context, transform.ToCanvas(AnnotationsViewModel.CommentBadgeBounds(annotation)), annotation.Color);
                }
            }
        }

        /// <summary>Draws the shape being dragged.</summary>
        /// <param name="context">The drawing context.</param>
        /// <param name="tab">The tab.</param>
        private void DrawShapePreview(DrawingContext context, DocumentTabViewModel tab)
        {
            if (canvas.ActiveShape is not { } shape || shape.Page < 0 || shape.Page >= canvas.Sizes.Length)
            {
                return;
            }

            var transform = new PageTransform(canvas.Layout.GetPageBounds(shape.Page), canvas.Sizes[shape.Page], tab.Rotation, canvas.Layout.Options.Scale);
            var start = transform.ToCanvas(shape.Start);
            var end = transform.ToCanvas(shape.End);
            var box = new Rect(start, end).Normalize();
            switch (shape.Kind)
            {
                case AnnotationKind.Rectangle:
                {
                    context.DrawRectangle(null, StrokePen, box);
                    break;
                }

                case AnnotationKind.Ellipse:
                {
                    context.DrawEllipse(null, StrokePen, box);
                    break;
                }

                default:
                {
                    context.DrawLine(StrokePen, start, end);
                    break;
                }
            }
        }

        /// <summary>Draws the placed polygon corners, the sides between them and the side that follows the pointer.</summary>
        /// <param name="context">The drawing context.</param>
        /// <param name="tab">The tab.</param>
        private void DrawPolygonPreview(DrawingContext context, DocumentTabViewModel tab)
        {
            var polygon = canvas.Polygon;
            if (!polygon.IsActive || polygon.Page >= canvas.Sizes.Length || polygon.Corners.Count == 0)
            {
                return;
            }

            var transform = new PageTransform(canvas.Layout.GetPageBounds(polygon.Page), canvas.Sizes[polygon.Page], tab.Rotation, canvas.Layout.Options.Scale);
            var previous = transform.ToCanvas(polygon.Corners[0]);
            foreach (var corner in polygon.Corners)
            {
                var next = transform.ToCanvas(corner);
                context.DrawLine(StrokePen, previous, next);
                context.DrawEllipse(StrokePen.Brush, null, next, CornerRadius, CornerRadius);
                previous = next;
            }

            context.DrawLine(StrokePen, previous, transform.ToCanvas(polygon.Hover));
        }

        /// <summary>Draws the signature mark being placed and its outline.</summary>
        /// <param name="context">The drawing context.</param>
        /// <param name="tab">The tab.</param>
        private void DrawPlacement(DrawingContext context, DocumentTabViewModel tab)
        {
            if (tab.FillAndSign.Placement is not { } placement || (uint)placement.Page >= (uint)canvas.Sizes.Length)
            {
                return;
            }

            var transform = new PageTransform(canvas.Layout.GetPageBounds(placement.Page), canvas.Sizes[placement.Page], tab.Rotation, canvas.Layout.Options.Scale);
            var area = transform.ToCanvas(placement.Bounds);
            var inkWidth = MarkInkWidth * transform.Scale;
            if (canvas.MarkPen is null || Math.Abs(canvas.MarkPen.Thickness - inkWidth) > PenTolerance)
            {
                // Made again only when the zoom changes, so redraws while placing do not allocate.
                canvas.MarkPen = new(MarkInk, inkWidth, lineCap: PenLineCap.Round);
            }

            (canvas.MarkPainter ??= new()).Draw(context, placement.Mark, area, MarkInk, canvas.MarkPen);
            if (canvas.CurrentHitPen is { } outline)
            {
                context.DrawRectangle(null, outline, area.Inflate(PlacementInset));
            }
        }

        /// <summary>Draws a ring around the field with the keyboard focus, so it is clear where Space and Tab act.</summary>
        /// <param name="context">The drawing context.</param>
        /// <param name="tab">The tab.</param>
        private void DrawFieldFocus(DrawingContext context, DocumentTabViewModel tab)
        {
            if (tab.Forms.Focused is not { } field || tab.Forms.Editing is not null || (uint)field.PageIndex >= (uint)canvas.Sizes.Length)
            {
                return;
            }

            context.DrawRectangle(null, new Pen((canvas.CurrentHitPen ?? StrokePen).Brush, FocusRingWidth), canvas.GetCanvasRect(field.PageIndex, field.Bounds).Inflate(FocusRingGap));
        }
    }

    /// <summary>Draws a small note badge rectangle with two ruled lines inside.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="rect">The badge rectangle on the canvas.</param>
    /// <param name="color">The badge colour as 0xRRGGBB.</param>
    private static void DrawBadgeIcon(DrawingContext context, in Rect rect, uint color)
    {
        context.DrawRectangle(ColourBrushes.Opaque(color), BadgeBorderPen, rect, BadgeRadius, BadgeRadius);
        var x1 = rect.Left + (rect.Width * BadgeLineLeft);
        var x2 = rect.Left + (rect.Width * BadgeLineRight);
        var y1 = rect.Top + (rect.Height * BadgeFirstLineY);
        var y2 = rect.Top + (rect.Height * BadgeSecondLineY);
        context.DrawLine(BadgeBorderPen, new(x1, y1), new(x2, y1));
        context.DrawLine(BadgeBorderPen, new(x1, y2), new(x2, y2));
    }

    /// <summary>The preview state of a shape being dragged on a page.</summary>
    /// <param name="Kind">The shape kind.</param>
    /// <param name="Page">The page index.</param>
    /// <param name="Start">Where the drag started in page coordinates.</param>
    /// <param name="End">Where the pointer is now in page coordinates.</param>
    internal readonly record struct ShapePreview(AnnotationKind Kind, int Page, PagePoint Start, PagePoint End);

    /// <summary>A printed-form region suggestion currently hovered by the pointer.</summary>
    /// <param name="Page">The page index.</param>
    /// <param name="Region">The form region.</param>
    internal readonly record struct HoveredFormRegion(int Page, FormRegion Region);

    /// <summary>A left pointer press claimed by signature placement, shape/polygon/ink drawing, form field activation or click tools.</summary>
    /// <param name="Canvas">The page canvas.</param>
    /// <param name="Tab">The active tab.</param>
    /// <param name="Args">The pointer press arguments.</param>
    /// <param name="Page">The page index, or -1.</param>
    /// <param name="Position">The point in canvas coordinates.</param>
    /// <param name="Kind">The matched press kind.</param>
    internal readonly record struct DrawingToolPress(
        PageCanvas Canvas,
        DocumentTabViewModel Tab,
        PointerPressedEventArgs Args,
        int Page,
        Point Position,
        int Kind)
    {
        /// <summary>Moves the signature or initials mark being placed.</summary>
        internal const int Placement = 0;

        /// <summary>Starts dragging a shape or callout annotation.</summary>
        internal const int Shape = 1;

        /// <summary>Adds a corner to a polygon, cloud or connected lines annotation.</summary>
        internal const int Polygon = 2;

        /// <summary>Starts a freehand ink stroke.</summary>
        internal const int Ink = 3;

        /// <summary>Activates a fillable form field.</summary>
        internal const int Field = 4;

        /// <summary>Starts a click-based annotation tool (Note, Text, Stamp).</summary>
        internal const int ClickTool = 5;

        /// <summary>Runs the matched drawing or field press and marks the event handled.</summary>
        internal void Begin()
        {
            switch (Kind)
            {
                case Placement:
                {
                    if (Page >= 0 && Tab.FillAndSign.MoveTo(Page, Canvas.ToPage(Tab, Page, Position)))
                    {
                        Canvas.InvalidateVisual();
                    }

                    Args.Handled = true;
                    break;
                }

                case Shape:
                {
                    BeginShape();
                    break;
                }

                case Polygon:
                {
                    Canvas.PressPolygonCorner(Tab, Page, Position, Args.ClickCount);
                    Args.Handled = true;
                    break;
                }

                case Ink:
                {
                    BeginInk();
                    break;
                }

                case Field:
                {
                    _ = Canvas.TryActivateField(Tab, Page, Position);
                    Args.Handled = true;
                    break;
                }

                case ClickTool:
                {
                    Canvas.ClickPage = Page;
                    Canvas.ClickSnaps = (Args.KeyModifiers & KeyModifiers.Alt) == 0;
                    Args.Handled = true;
                    break;
                }

                default:
                {
                    break;
                }
            }
        }

        /// <summary>Captures the pointer and begins dragging a shape or callout annotation.</summary>
        private void BeginShape()
        {
            if (AnnotationsViewModel.GetShapeKind(Tab.Annotations.Tool) is not { } shapeKind)
            {
                return;
            }

            var start = Canvas.ToPage(Tab, Page, Position);
            Canvas.ActiveShape = new(shapeKind, Page, start, start);
            Args.Pointer.Capture(Canvas);
            Args.Handled = true;
        }

        /// <summary>Captures the pointer and begins a freehand ink stroke.</summary>
        private void BeginInk()
        {
            Canvas.Stroke.Clear();
            Canvas.StrokePage = Page;
            Canvas.Stroke.Add(Canvas.ToPage(Tab, Page, Position));
            Args.Pointer.Capture(Canvas);
            Args.Handled = true;
        }
    }

    /// <summary>The canvas and tab a keyboard move of the mark being placed is made on.</summary>
    /// <param name="Canvas">The page canvas.</param>
    /// <param name="Tab">The tab.</param>
    private readonly record struct PlacementMove(PageCanvas Canvas, DocumentTabViewModel Tab);
}
