// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Layout;

namespace HyperPdf.Presentation.Canvas;

/// <summary>
/// Decides what a pointer press, move or release does on the page canvas, for any toolkit: page tools and the right-click menu
/// first, then select-tool editing, then drawing and form fields, then text and links. The toolkit passes the event in and
/// does what <see cref="InputEffects"/> then asks of it.
/// </summary>
public static class CanvasPointerMixins
{
    /// <summary>How many clicks in a row make a double-click.</summary>
    private const int DoubleClick = 2;

    /// <summary>Adds the pointer handling to a canvas state.</summary>
    /// <param name="state">The canvas state.</param>
    extension(PageCanvasState state)
    {
        /// <summary>Handles a pointer press.</summary>
        /// <param name="press">The press.</param>
        /// <returns>What the toolkit must now do; one instance the canvas reuses for every event.</returns>
        public InputEffects Press(in ViewerPress press)
        {
            var fx = state.Effects;
            fx.Reset();
            fx.Handled = press.Handled;
            if (state.Tab is not { } tab)
            {
                return fx;
            }

            if (!fx.Handled)
            {
                state.PressPageTool(tab, press, fx);
            }

            if (!fx.Handled && PointerRouting.IsContextPress(press.Buttons))
            {
                fx.ShowContextMenu = true;
                fx.Handled = true;
            }

            if (!fx.Handled)
            {
                state.PressSelectTool(tab, press, fx);
            }

            if (!fx.Handled)
            {
                state.PressEditObject(tab, press, fx);
            }

            if (!fx.Handled)
            {
                state.PressDrawingTool(tab, press, fx);
            }

            if (!fx.Handled)
            {
                state.PressText(press, fx);
            }

            return fx;
        }

        /// <summary>Handles a pointer move.</summary>
        /// <param name="move">The move.</param>
        /// <returns>What the toolkit must now do; one instance the canvas reuses for every event.</returns>
        public InputEffects Move(in ViewerMove move)
        {
            var fx = state.Effects;
            fx.Reset();
            if (state.Tab is not { } tab)
            {
                return fx;
            }

            if (state.DragEditObject(tab, move.Position, fx))
            {
                return fx;
            }

            if (state.Tools.ActiveEdit is not null && state.MoveEdit(tab, move.Position))
            {
                state.Host.InvalidateVisual();
            }

            var hover = state.GetHover(move.Position);
            state.HoverRegion(hover.IsOverPage, hover.Page, hover.Point);

            if (state.ContinuePageTool(tab, move, fx) || state.ContinueMeasure(tab, move.Position, fx) || state.ContinueDrawing(move.Position))
            {
                return fx;
            }

            if (CanvasObjectEditMixins.HoverEditObject(tab, hover, fx))
            {
                return fx;
            }

            if (state.Tools.Selecting)
            {
                state.DragSelectionTo(move.Position, move.InScroller);
                return fx;
            }

            fx.Cursor = state.GetHoverCursor(hover);
            return fx;
        }

        /// <summary>Handles a pointer release.</summary>
        /// <param name="position">Where the pointer is on the canvas.</param>
        /// <param name="handled">Whether something else has already used the release.</param>
        /// <returns>What the toolkit must now do; one instance the canvas reuses for every event.</returns>
        public InputEffects Release(CanvasPoint position, bool handled)
        {
            var fx = state.Effects;
            fx.Reset();
            if (state.Tab is not { } tab)
            {
                return fx;
            }

            if (tab.ContentEdit.EndDrag())
            {
                fx.ReleaseCapture = true;
                fx.Handled = true;
                return fx;
            }

            if (state.Tools.ActiveEdit is not null)
            {
                fx.ReleaseCapture = true;
                fx.Handled = true;
                _ = state.EndEdit(tab, position);
                return fx;
            }

            fx.Handled = handled;
            if (handled)
            {
                return fx;
            }

            fx.ReleaseCapture = true;
            if (!state.EndPan(position, fx) && !state.EndArea(tab, position, fx) && !state.EndDrawing(position))
            {
                state.EndText(tab, position);
            }

            return fx;
        }

        /// <summary>Handles a press claimed by a page tool: stopping auto-scroll, moving the pages, dragging an area, or measuring.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="press">The press.</param>
        /// <param name="fx">What the toolkit must do.</param>
        private void PressPageTool(DocumentTabViewModel tab, in ViewerPress press, InputEffects fx)
        {
            switch (PointerRouting.GetPageToolPress(tab, press.Buttons, state.Host.HasScroller, state.Tools.SpaceHeld))
            {
                case PageToolPressKind.StopAutoScroll:
                {
                    tab.PageTools.IsAutoScrolling = false;
                    fx.Focus = true;
                    fx.Handled = true;
                    break;
                }

                case PageToolPressKind.Pan:
                {
                    state.BeginPanPress(press, fx);
                    break;
                }

                case PageToolPressKind.Area:
                {
                    state.BeginAreaPress(press, fx);
                    break;
                }

                case PageToolPressKind.Measure:
                {
                    fx.Focus = true;
                    state.Tools.PressPoint = press.Position;
                    state.AddMeasurePoint(tab, press);
                    fx.Handled = true;
                    break;
                }

                case PageToolPressKind.Focus:
                {
                    fx.Focus = true;
                    state.Tools.PressPoint = press.Position;
                    break;
                }

                default:
                {
                    break;
                }
            }
        }

        /// <summary>Starts moving the pages.</summary>
        /// <param name="press">The press.</param>
        /// <param name="fx">What the toolkit must do.</param>
        private void BeginPanPress(in ViewerPress press, InputEffects fx)
        {
            var tools = state.Tools;
            fx.Focus = true;
            tools.PressPoint = press.Position;
            tools.PressedLink = (press.Buttons & ViewerButtons.Left) != ViewerButtons.None && !tools.SpaceHeld ? state.HitTestLink(press.Position) : null;
            state.BeginPan(press.InScroller);
            fx.Cursor = ViewerCursor.Move;
            fx.Capture = true;
            fx.Handled = true;
        }

        /// <summary>Starts dragging a zoom or snapshot area.</summary>
        /// <param name="press">The press.</param>
        /// <param name="fx">What the toolkit must do.</param>
        private void BeginAreaPress(in ViewerPress press, InputEffects fx)
        {
            var tools = state.Tools;
            fx.Focus = true;
            tools.AreaStart = press.Position;
            tools.AreaEnd = press.Position;
            tools.AreaDragging = true;
            fx.Capture = true;
            fx.Handled = true;
            state.Host.InvalidateVisual();
        }

        /// <summary>Adds a measurement point or finishes the measurement on a double-click.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="press">The press.</param>
        private void AddMeasurePoint(DocumentTabViewModel tab, in ViewerPress press)
        {
            var page = state.Layout.HitTest(press.Position.X, press.Position.Y);
            if (page < 0)
            {
                return;
            }

            if (press.ClickCount >= DoubleClick)
            {
                tab.Measure.Finish();
            }
            else
            {
                tab.Measure.AddPoint(page, state.ToPage(tab, page, press.Position));
            }

            state.Host.InvalidateVisual();
        }

        /// <summary>Handles a press with the Select tool: editing text boxes, resizing, comment badges and bubbles, and moving annotations.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="press">The press.</param>
        /// <param name="fx">What the toolkit must do.</param>
        private void PressSelectTool(DocumentTabViewModel tab, in ViewerPress press, InputEffects fx)
        {
            var page = state.Layout.HitTest(press.Position.X, press.Position.Y);
            if (!PointerRouting.IsSelectToolPress(tab, press.Buttons) || page < 0)
            {
                return;
            }

            var pagePoint = state.ToPage(tab, page, press.Position);
            var doubleClick = press.ClickCount >= DoubleClick;
            if (state.TryPressTextBoxOrHandle(tab, page, pagePoint, press, fx) || state.TryPressComment(tab.Annotations, page, pagePoint, doubleClick, fx))
            {
                return;
            }

            state.PressAnnotation(tab, page, pagePoint, press, fx);
        }

        /// <summary>Starts editing a text box that was double-clicked, or resizing an annotation whose handle was pressed.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="page">The page the press landed on.</param>
        /// <param name="pagePoint">The press in page space.</param>
        /// <param name="press">The press.</param>
        /// <param name="fx">What the toolkit must do.</param>
        /// <returns><see langword="true"/> when the press was used.</returns>
        private bool TryPressTextBoxOrHandle(DocumentTabViewModel tab, int page, PagePoint pagePoint, in ViewerPress press, InputEffects fx)
        {
            var annotations = tab.Annotations;
            if (press.ClickCount >= DoubleClick && annotations.HitTest(page, pagePoint) is { Kind: AnnotationKind.TextBox } box && annotations.EditText(box))
            {
                fx.Handled = true;
                return true;
            }

            if (!state.TryStartResize(tab, page, press.Position, out var session))
            {
                return false;
            }

            state.Tools.ActiveEdit = session;
            fx.Capture = true;
            fx.Handled = true;
            return true;
        }

        /// <summary>Opens the comment bubble of a badge that was clicked, or of an annotation that was double-clicked.</summary>
        /// <param name="annotations">The annotations.</param>
        /// <param name="page">The page the press landed on.</param>
        /// <param name="pagePoint">The press in page space.</param>
        /// <param name="doubleClick">Whether the press was a double-click.</param>
        /// <param name="fx">What the toolkit must do.</param>
        /// <returns><see langword="true"/> when the press was used.</returns>
        private bool TryPressComment(AnnotationsViewModel annotations, int page, PagePoint pagePoint, bool doubleClick, InputEffects fx)
        {
            if (annotations.HitTestCommentBadge(page, pagePoint) is { } badge)
            {
                _ = annotations.OpenBubble(badge);
            }
            else if (doubleClick && annotations.HitTest(page, pagePoint) is { } hit && AnnotationsViewModel.CanShowBubble(hit))
            {
                annotations.Select(hit);
                _ = annotations.OpenBubble(hit);
            }
            else
            {
                return false;
            }

            state.Host.InvalidateVisual();
            fx.Handled = true;
            return true;
        }

        /// <summary>Selects the annotation under a press (closing the bubble on blank page) and starts a move drag when it is movable.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="page">The page the press landed on.</param>
        /// <param name="pagePoint">The press in page space.</param>
        /// <param name="press">The press.</param>
        /// <param name="fx">What the toolkit must do.</param>
        private void PressAnnotation(DocumentTabViewModel tab, int page, PagePoint pagePoint, in ViewerPress press, InputEffects fx)
        {
            var outcome = state.SelectAndTryStartMove(tab, page, pagePoint, press.Position);
            fx.Handled |= outcome.Handled;
            if (outcome.Session is not { } session)
            {
                return;
            }

            state.Tools.ActiveEdit = session;
            fx.Capture = true;
            fx.Handled = true;
        }

        /// <summary>Picks the text, picture or drawing under a press in Edit mode.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="press">The press.</param>
        /// <param name="fx">What the toolkit must do.</param>
        private void PressEditObject(DocumentTabViewModel tab, in ViewerPress press, InputEffects fx)
        {
            var page = state.Layout.HitTest(press.Position.X, press.Position.Y);
            if (!PointerRouting.IsEditPress(tab, press.Buttons, page))
            {
                return;
            }

            var edit = tab.ContentEdit;
            var point = state.ToPage(tab, page, press.Position);

            // A press on the object already picked starts a drag; any other press picks what is under it.
            if (press.ClickCount < DoubleClick && edit.BeginDrag(page, point))
            {
                fx.Capture = true;
                fx.Handled = true;
                return;
            }

            _ = edit.PressAsync(page, point, press.ClickCount >= DoubleClick);
            fx.Handled = true;
        }

        /// <summary>Handles a press with a drawing tool, a signature being placed, or a form field.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="press">The press.</param>
        /// <param name="fx">What the toolkit must do.</param>
        private void PressDrawingTool(DocumentTabViewModel tab, in ViewerPress press, InputEffects fx)
        {
            if (!PointerRouting.IsPlainLeft(press.Buttons))
            {
                return;
            }

            var page = state.Layout.HitTest(press.Position.X, press.Position.Y);
            var kind = PointerRouting.GetDrawingPress(tab, press.Buttons, page, page >= 0 ? state.ToPage(tab, page, press.Position) : default);
            if (kind is not (DrawingPressKind.Placement or DrawingPressKind.Shape or DrawingPressKind.Polygon))
            {
                state.CancelPolygon();
            }

            switch (kind)
            {
                case DrawingPressKind.Placement:
                {
                    if (page >= 0 && tab.FillAndSign.MoveTo(page, state.ToPage(tab, page, press.Position)))
                    {
                        state.Host.InvalidateVisual();
                    }

                    fx.Handled = true;
                    break;
                }

                case DrawingPressKind.Shape:
                {
                    fx.Capture = state.BeginShape(tab, page, press.Position);
                    fx.Handled = fx.Capture;
                    break;
                }

                case DrawingPressKind.Polygon:
                {
                    state.PressPolygonCorner(tab, page, press.Position, press.ClickCount >= DoubleClick);
                    fx.Handled = true;
                    break;
                }

                case DrawingPressKind.Ink:
                {
                    state.BeginInk(tab, page, press.Position);
                    fx.Capture = true;
                    fx.Handled = true;
                    break;
                }

                case DrawingPressKind.Field:
                {
                    state.ActivateField(tab, page, press.Position, fx);
                    fx.Handled = true;
                    break;
                }

                case DrawingPressKind.ClickTool:
                {
                    state.BeginClick(page, (press.Modifiers & ViewerModifiers.Alt) == ViewerModifiers.None);
                    fx.Handled = true;
                    break;
                }

                default:
                {
                    break;
                }
            }
        }

        /// <summary>Fills the field under a click, or asks for its options to be shown.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="page">The page.</param>
        /// <param name="position">The canvas point.</param>
        /// <param name="fx">What the toolkit must do.</param>
        private void ActivateField(DocumentTabViewModel tab, int page, CanvasPoint position, InputEffects fx)
        {
            if (tab.Forms.HitTest(page, state.ToPage(tab, page, position)) is not { } field)
            {
                return;
            }

            if (!tab.Forms.Activate(field) && field.Kind is FormFieldKind.ComboBox or FormFieldKind.ListBox)
            {
                fx.Choices = field;
            }

            state.Host.InvalidateVisual();
        }

        /// <summary>Handles a press on a link or on text, where it starts a selection drag.</summary>
        /// <param name="press">The press.</param>
        /// <param name="fx">What the toolkit must do.</param>
        private void PressText(in ViewerPress press, InputEffects fx)
        {
            if (!PointerRouting.IsPlainLeft(press.Buttons))
            {
                return;
            }

            var tools = state.Tools;
            tools.PressedLink = state.HitTestLink(press.Position);
            var extend = (press.Modifiers & ViewerModifiers.Shift) != ViewerModifiers.None;

            // Shift keeps the selection and moves its end, so a block started pages earlier grows to the click.
            if (!extend)
            {
                state.ClearSelection();
            }

            if (tools.PressedLink is not null || !state.TryHitTestCharacter(press.Position, out var page, out var character))
            {
                return;
            }

            var picked = new CharacterPosition(page, character);
            if (extend && state.ExtendSelectionFromAnchor(picked, press.Position, press.InScroller))
            {
                fx.Capture = true;
                fx.Handled = true;
                return;
            }

            state.BeginSelectionDrag(picked, press.Position, press.InScroller);
            fx.Capture = true;
            fx.Handled = true;
        }

        /// <summary>Moves the pages or the area with the pointer, or shows the chosen tool's pointer while hovering.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="move">The move.</param>
        /// <param name="fx">What the toolkit must do.</param>
        /// <returns><see langword="true"/> when the movement was used.</returns>
        private bool ContinuePageTool(DocumentTabViewModel tab, in ViewerMove move, InputEffects fx)
        {
            var tools = state.Tools;
            if (state.ContinuePan(move.InScroller))
            {
                return true;
            }

            if (tools.AreaDragging)
            {
                tools.AreaEnd = move.Position;
                state.Host.InvalidateVisual();
                return true;
            }

            // Only a hover shows the tool's pointer; a drag that is selecting, drawing or measuring keeps its own.
            var cursor = state.GetToolCursor();
            if (tools.Selecting || tab.Measure.IsOn || move.LeftDown || cursor == ViewerCursor.Inherit)
            {
                return false;
            }

            fx.Cursor = cursor;
            return true;
        }

        /// <summary>Moves the measurement's last point with the pointer.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="position">The canvas point.</param>
        /// <param name="fx">What the toolkit must do.</param>
        /// <returns><see langword="true"/> while measuring.</returns>
        private bool ContinueMeasure(DocumentTabViewModel tab, CanvasPoint position, InputEffects fx)
        {
            if (!tab.Measure.IsOn)
            {
                return false;
            }

            fx.Cursor = ViewerCursor.Cross;
            var page = state.Layout.HitTest(position.X, position.Y);
            if (page >= 0 && tab.Measure.MovePoint(page, state.ToPage(tab, page, position)))
            {
                state.Host.InvalidateVisual();
            }

            return true;
        }

        /// <summary>Ends moving the pages, following a link when the Hand tool clicked one without moving.</summary>
        /// <param name="position">The canvas point.</param>
        /// <param name="fx">What the toolkit must do.</param>
        /// <returns><see langword="true"/> when a drag was moving the pages.</returns>
        private bool EndPan(CanvasPoint position, InputEffects fx)
        {
            var tools = state.Tools;
            if (!tools.Panning)
            {
                return false;
            }

            tools.Panning = false;
            fx.Cursor = state.GetToolCursor();
            state.ReportPosition();
            var link = tools.PressedLink;
            tools.PressedLink = null;
            if (link is { } pressed && CanvasMetrics.IsClick(tools.PressPoint, position))
            {
                state.Tab?.Navigate(pressed.Target);
            }

            return true;
        }

        /// <summary>Finishes the area: zooms to it or asks for a picture of it.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="position">The canvas point.</param>
        /// <param name="fx">What the toolkit must do.</param>
        /// <returns><see langword="true"/> when an area was being dragged.</returns>
        private bool EndArea(DocumentTabViewModel tab, CanvasPoint position, InputEffects fx)
        {
            var tools = state.Tools;
            if (!tools.AreaDragging)
            {
                return false;
            }

            tools.AreaDragging = false;
            state.Host.InvalidateVisual();
            tools.AreaEnd = position;
            var area = state.GetArea();
            if (tab.PageTools.PageTool == PageTool.ZoomArea)
            {
                state.ZoomToArea(tab, area);
            }
            else
            {
                fx.Snapshot = area;
            }

            return true;
        }

        /// <summary>Ends a text selection drag, marking the selection when a markup tool is chosen, and follows a link that was clicked.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="position">The canvas point.</param>
        private void EndText(DocumentTabViewModel tab, CanvasPoint position)
        {
            var tools = state.Tools;
            if (tools.Selecting)
            {
                state.EndSelectionDrag();
                state.ApplyMarkupTool();
            }

            var link = tools.PressedLink;
            tools.PressedLink = null;
            if (link is { } pressed && CanvasMetrics.IsClick(tools.PressPoint, position))
            {
                tab.Navigate(pressed.Target);
            }
        }
    }
}
