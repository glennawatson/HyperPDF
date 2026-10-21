// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Input;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Geometry;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Controls;

/// <summary>
/// Reactive streams, keyboard handlers and overlay drawing for annotations, shapes, polygons, ink strokes, signature
/// placement, form fields and printed-form region suggestions on <see cref="PageCanvas"/>.
/// </summary>
internal static class PageCanvasAnnotationMixins
{
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool ContinueStroke(Point position) => canvas.State.ContinueDrawing(new(position.X, position.Y));

        /// <summary>Finishes a shape, ink stroke or click tool on pointer release.</summary>
        /// <param name="position">The canvas point.</param>
        /// <returns><see langword="true"/> when the release was handled.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool EndAnnotationPress(Point position) => canvas.State.EndDrawing(new(position.X, position.Y));

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void HoverRegion(PointerHover hover) => canvas.State.HoverRegion(hover.IsOverPage, hover.Page, hover.Point);

        /// <summary>Moves the next polygon side's free end with the pointer.</summary>
        /// <param name="position">The canvas point.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void HoverPolygon(Point position) => canvas.State.HoverPolygon(new(position.X, position.Y));

        /// <summary>Forgets the placed polygon corners.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void CancelPolygon() => canvas.State.CancelPolygon();

        /// <summary>Adds a corner when a polygon tool is active, or finishes the polygon on a double-click.</summary>
        /// <param name="tab">The active tab.</param>
        /// <param name="page">The page index, or -1.</param>
        /// <param name="position">The canvas position.</param>
        /// <param name="clickCount">The pointer click count.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void PressPolygonCorner(DocumentTabViewModel tab, int page, Point position, int clickCount) =>
            canvas.State.PressPolygonCorner(tab, page, new(position.X, position.Y), clickCount >= PageCanvasToolMixins.DoubleClick);

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

        /// <summary>Handles Enter (finish), Backspace (take back a corner) and Escape (start again) while placing corners.</summary>
        /// <param name="key">The key.</param>
        /// <returns><see langword="true"/> when the key was used.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HandlePolygonKey(ViewerKey key) => canvas.State.HandlePolygonKey(key);

        /// <summary>Moves the mark being placed by a distance on screen, so arrows follow the screen on a rotated page.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="dx">The move right on screen, in points.</param>
        /// <param name="dy">The move down on screen, in points.</param>
        /// <returns><see langword="true"/> when a mark is being placed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool MovePlacement(DocumentTabViewModel tab, float dx, float dy) => canvas.State.MovePlacement(tab, dx, dy);

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

    }

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
                    Canvas.State.BeginClick(Page, (Args.KeyModifiers & KeyModifiers.Alt) == 0);
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
            if (!Canvas.State.BeginShape(Tab, Page, new(Position.X, Position.Y)))
            {
                return;
            }

            Args.Pointer.Capture(Canvas);
            Args.Handled = true;
        }

        /// <summary>Captures the pointer and begins a freehand ink stroke.</summary>
        private void BeginInk()
        {
            Canvas.State.BeginInk(Tab, Page, new(Position.X, Position.Y));
            Args.Pointer.Capture(Canvas);
            Args.Handled = true;
        }
    }

    /// <summary>The canvas and tab a keyboard move of the mark being placed is made on.</summary>
    /// <param name="Canvas">The page canvas.</param>
    /// <param name="Tab">The tab.</param>
    private readonly record struct PlacementMove(PageCanvas Canvas, DocumentTabViewModel Tab);
}
