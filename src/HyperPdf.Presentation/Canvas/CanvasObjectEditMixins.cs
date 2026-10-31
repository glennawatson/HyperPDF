// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;
using HyperPdf.Presentation.Input;

namespace HyperPdf.Presentation.Canvas;

/// <summary>Keys and outlines for Edit mode, where the text, pictures and drawings of a page are picked, moved, retyped and removed.</summary>
public static class CanvasObjectEditMixins
{
    /// <summary>Outlines the object under the pointer in Edit mode and shows a hand over it.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="hover">The page and point under the pointer.</param>
    /// <param name="fx">What the toolkit must do.</param>
    /// <returns><see langword="true"/> in Edit mode.</returns>
    public static bool HoverEditObject(DocumentTabViewModel tab, PointerHover hover, InputEffects fx)
    {
        if (!tab.ContentEdit.IsEditing)
        {
            return false;
        }

        tab.ContentEdit.HoverAt(hover.Page, hover.Point);
        fx.Cursor = tab.ContentEdit.Hovered is null ? ViewerCursor.Arrow : ViewerCursor.Hand;
        return true;
    }

    /// <summary>Adds the Edit mode helpers to a canvas state.</summary>
    /// <param name="state">The canvas state.</param>
    extension(PageCanvasState state)
    {
        /// <summary>Handles a key in Edit mode: Escape lets go, Delete removes, Enter and F2 retype, and the arrows move the picked object.</summary>
        /// <param name="key">The key.</param>
        /// <param name="modifiers">The modifiers held with it.</param>
        /// <returns><see langword="true"/> when the key was used.</returns>
        public bool HandleEditKey(ViewerKey key, ViewerModifiers modifiers)
        {
            if (state.Tab is not { } tab || !tab.ContentEdit.IsEditing)
            {
                return false;
            }

            var edit = tab.ContentEdit;
            return key switch
            {
                ViewerKey.Escape => edit.Escape(),
                ViewerKey.Delete => edit.DeleteSelected(),
                ViewerKey.Enter or ViewerKey.F2 when modifiers == ViewerModifiers.None => edit.BeginTextEdit(),
                _ => state.NudgeEditObject(key, modifiers),
            };
        }

        /// <summary>Carries the outline of a dragged object with the pointer.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="position">The pointer on the canvas.</param>
        /// <param name="fx">What the toolkit must do.</param>
        /// <returns><see langword="true"/> while an object is being dragged.</returns>
        public bool DragEditObject(DocumentTabViewModel tab, CanvasPoint position, InputEffects fx)
        {
            if (tab.ContentEdit.DragPage < 0)
            {
                return false;
            }

            tab.ContentEdit.DragTo(state.ToPage(tab, tab.ContentEdit.DragPage, position));
            fx.Cursor = ViewerCursor.Move;
            return true;
        }

        /// <summary>Moves the picked object a few screen pixels with the arrow keys, more with Shift.</summary>
        /// <param name="key">The key.</param>
        /// <param name="modifiers">The modifiers held with it.</param>
        /// <returns><see langword="true"/> when an object was moved.</returns>
        private bool NudgeEditObject(ViewerKey key, ViewerModifiers modifiers)
        {
            if (state.Tab is not { ContentEdit.Selected: { } picked } tab || (uint)picked.Page >= (uint)state.Sizes.Length)
            {
                return false;
            }

            var step = ScreenStep.FromArrow(key, modifiers);
            if (step.IsEmpty)
            {
                return false;
            }

            // A screen step is turned into a page step through the page's own transform, so it follows zoom and rotation.
            var transform = new PageTransform(state.Layout.GetPageBounds(picked.Page), state.Sizes[picked.Page], tab.Rotation, state.Layout.Options.Scale);
            var start = transform.MapToPage(0, 0);
            var end = transform.MapToPage(step.X * transform.Scale, step.Y * transform.Scale);
            return tab.ContentEdit.MoveSelected(end.X - start.X, end.Y - start.Y);
        }
    }
}
