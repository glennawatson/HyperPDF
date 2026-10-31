// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Presentation.Canvas;

/// <summary>
/// Decides what a key does on the page canvas, for any toolkit: form fields first, then navigation, copying, placing a mark,
/// measuring, annotations, the caret and page-by-page scrolling. The toolkit passes the key in and does what
/// <see cref="InputEffects"/> then asks of it.
/// </summary>
public static class CanvasKeyMixins
{
    /// <summary>Adds the key handling to a canvas state.</summary>
    /// <param name="state">The canvas state.</param>
    extension(PageCanvasState state)
    {
        /// <summary>Handles a key press.</summary>
        /// <param name="key">The key.</param>
        /// <param name="modifiers">The modifiers held with it.</param>
        /// <returns>What the toolkit must now do; one instance the canvas reuses for every event.</returns>
        public InputEffects PressKey(ViewerKey key, ViewerModifiers modifiers)
        {
            var fx = state.Effects;
            fx.Reset();
            fx.Handled = state.HandleDocumentKey(key, modifiers, fx);
            return fx;
        }

        /// <summary>Handles letting go of a key: Space ends a hold, and page by page turns the page unless the pages were moved while it was held.</summary>
        /// <param name="key">The key.</param>
        /// <param name="modifiers">The modifiers held with it.</param>
        /// <returns>What the toolkit must now do; one instance the canvas reuses for every event.</returns>
        public InputEffects ReleaseKey(ViewerKey key, ViewerModifiers modifiers)
        {
            var fx = state.Effects;
            fx.Reset();
            if (key != ViewerKey.Space || !state.Tools.SpaceHeld)
            {
                return fx;
            }

            if (state.EndSpaceHold())
            {
                _ = state.HandlePageByPageKey(key, modifiers);
            }

            if (!state.Tools.Panning)
            {
                fx.Cursor = state.GetToolCursor();
            }

            fx.Handled = true;
            return fx;
        }

        /// <summary>Handles document keyboard actions.</summary>
        /// <param name="key">The key.</param>
        /// <param name="modifiers">The modifiers held with it.</param>
        /// <param name="fx">What the toolkit must do.</param>
        /// <returns><see langword="true"/> when the key was handled.</returns>
        private bool HandleDocumentKey(ViewerKey key, ViewerModifiers modifiers, InputEffects fx)
        {
            // A focused form field takes Space and Tab before they scroll or move between pages.
            if (state.HandleFormKey(key, modifiers, fx) || state.HandleEditKey(key, modifiers) || state.HandleNavigationKey(key, modifiers, fx))
            {
                return true;
            }

            if (key == ViewerKey.C && (modifiers & ViewerModifiers.Control) != ViewerModifiers.None)
            {
                fx.CopyText = state.GetSelectedText();
                return true;
            }

            return state.HandlePlacementKey(key, modifiers)
                || state.HandleMeasureKey(key)
                || state.HandleAnnotationKey(key, modifiers)
                || state.HandleCaretKey(key, modifiers)
                || state.HandlePageByPageKey(key, modifiers);
        }

        /// <summary>Moves between form fields with Tab and Shift+Tab, ticks the focused box with Space, opens a focused list with Enter, and drops focus with Escape.</summary>
        /// <param name="key">The key.</param>
        /// <param name="modifiers">The modifiers held with it.</param>
        /// <param name="fx">What the toolkit must do.</param>
        /// <returns><see langword="true"/> when a field took the key.</returns>
        private bool HandleFormKey(ViewerKey key, ViewerModifiers modifiers, InputEffects fx)
        {
            if (state.Tab is not { } tab)
            {
                return false;
            }

            var result = DocumentKeys.HandleForm(tab, key, modifiers);
            switch (result.Outcome)
            {
                case FormKeyOutcome.Redraw:
                {
                    state.Host.InvalidateVisual();
                    return true;
                }

                case FormKeyOutcome.OpenChoices:
                {
                    fx.Choices = result.Field;
                    return true;
                }

                default:
                {
                    return false;
                }
            }
        }

        /// <summary>Handles the moving and grabbing keys before the other page keys.</summary>
        /// <param name="key">The key.</param>
        /// <param name="modifiers">The modifiers held with it.</param>
        /// <param name="fx">What the toolkit must do.</param>
        /// <returns><see langword="true"/> when the key was used.</returns>
        private bool HandleNavigationKey(ViewerKey key, ViewerModifiers modifiers, InputEffects fx)
        {
            if (state.Tab is not { } tab)
            {
                return false;
            }

            if (DocumentKeys.HandleAutoScroll(tab, key, modifiers))
            {
                return true;
            }

            if (key == ViewerKey.A && modifiers == ViewerModifiers.Control)
            {
                state.SelectAllText(tab);
                return true;
            }

            return (modifiers == ViewerModifiers.None || key == ViewerKey.Space) && state.HandlePlainNavigationKey(tab, key, modifiers, fx);
        }

        /// <summary>Handles Home, End, Escape and Space, pressed without Ctrl or Alt.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="key">The key.</param>
        /// <param name="modifiers">The modifiers held with it.</param>
        /// <param name="fx">What the toolkit must do.</param>
        /// <returns><see langword="true"/> when the key was used.</returns>
        private bool HandlePlainNavigationKey(DocumentTabViewModel tab, ViewerKey key, ViewerModifiers modifiers, InputEffects fx)
        {
            // In caret mode Home and End stay with the text cursor.
            if (DocumentKeys.HandleEdgePage(tab, key))
            {
                state.Host.InvalidateVisual();
                return true;
            }

            return key switch
            {
                ViewerKey.Escape => state.EscapePageTool(tab, fx),
                ViewerKey.Space => !tab.IsCaretMode && state.HoldSpace(key, modifiers, fx),
                _ => false,
            };
        }

        /// <summary>Notes that Space is held, so a drag moves the pages.</summary>
        /// <param name="key">The key.</param>
        /// <param name="modifiers">The modifiers held with it.</param>
        /// <param name="fx">What the toolkit must do.</param>
        /// <returns><see langword="true"/> when the key was Space.</returns>
        private bool HoldSpace(ViewerKey key, ViewerModifiers modifiers, InputEffects fx)
        {
            if (!state.PressSpace(key, modifiers, out var started))
            {
                return false;
            }

            if (started)
            {
                fx.Cursor = ViewerCursor.Hand;
            }

            return true;
        }

        /// <summary>Handles Escape for the moving and area tools: cancels a drag, or goes back to selecting text.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="fx">What the toolkit must do.</param>
        /// <returns><see langword="true"/> when Escape was used.</returns>
        private bool EscapePageTool(DocumentTabViewModel tab, InputEffects fx)
        {
            if (state.Tools.AreaDragging)
            {
                state.Tools.AreaDragging = false;
                state.Host.InvalidateVisual();
                return true;
            }

            if (tab.PageTools.PageTool == PageTool.SelectText)
            {
                return false;
            }

            tab.PageTools.PageTool = PageTool.SelectText;
            fx.Cursor = ViewerCursor.Inherit;
            return true;
        }

        /// <summary>Handles the placing keys while a signature or initials mark is being placed.</summary>
        /// <param name="key">The key.</param>
        /// <param name="modifiers">The modifiers held with it.</param>
        /// <returns><see langword="true"/> when the key was used.</returns>
        private bool HandlePlacementKey(ViewerKey key, ViewerModifiers modifiers)
        {
            if (state.Tab is not { } tab)
            {
                return false;
            }

            var used = DocumentKeys.HandlePlacement(tab.FillAndSign, key, modifiers, new PlacementMove(state, tab), static (move, dx, dy) => move.State.MovePlacement(move.Tab, dx, dy));
            if (used)
            {
                state.Host.InvalidateVisual();
            }

            return used;
        }

        /// <summary>Handles Enter (finish) and Escape (clear) while measuring.</summary>
        /// <param name="key">The key.</param>
        /// <returns><see langword="true"/> when the key was used.</returns>
        private bool HandleMeasureKey(ViewerKey key)
        {
            if (state.Tab is not { } tab || !DocumentKeys.HandleMeasure(tab.Measure, key))
            {
                return false;
            }

            state.Host.InvalidateVisual();
            return true;
        }

        /// <summary>Handles the polygon tools' keys, Escape, Delete and the arrow keys.</summary>
        /// <param name="key">The key.</param>
        /// <param name="modifiers">The modifiers held with it.</param>
        /// <returns><see langword="true"/> when the key was used.</returns>
        private bool HandleAnnotationKey(ViewerKey key, ViewerModifiers modifiers)
        {
            if (state.HandlePolygonKey(key) || state.NudgeSelected(key, modifiers))
            {
                return true;
            }

            if (key == ViewerKey.Escape)
            {
                state.ClearSelection();
                state.Tab?.Annotations.CloseBubble();
                state.Tab?.Annotations.Select(null);
                return false;
            }

            return state.Tab?.Annotations is { } annotations
                && (DocumentKeys.EditPickedText(annotations, key, modifiers) || DocumentKeys.DeleteSelected(annotations, key));
        }
    }

    /// <summary>The canvas state and tab a keyboard move of the mark being placed is made on.</summary>
    /// <param name="State">The canvas state.</param>
    /// <param name="Tab">The tab.</param>
    private readonly record struct PlacementMove(PageCanvasState State, DocumentTabViewModel Tab);
}
