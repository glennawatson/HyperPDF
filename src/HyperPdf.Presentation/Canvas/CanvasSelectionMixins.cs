// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Layout;

namespace HyperPdf.Presentation.Canvas;

/// <summary>Working with the text the reader has selected on the page canvas: clearing, extending, copying, marking and the right-click menu, for any toolkit.</summary>
public static class CanvasSelectionMixins
{
    /// <summary>Adds the selection helpers to a canvas state.</summary>
    /// <param name="state">The canvas state.</param>
    extension(PageCanvasState state)
    {
        /// <summary>Clears the text selection.</summary>
        public void ClearSelection()
        {
            state.Selection.Clear();
            state.Host.InvalidateVisual();
        }

        /// <summary>Gets the selected text.</summary>
        /// <returns>The text, or an empty string.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string GetSelectedText() => state.Selection.GetText(state.Tab?.TryGetDocument());

        /// <summary>Gets the line rectangles of the selection on each page it covers.</summary>
        /// <returns>The rectangles by page; empty when nothing is selected.</returns>
        public Dictionary<int, List<PageRect>> GetSelectionLines()
        {
            var lines = new Dictionary<int, List<PageRect>>();
            if (!state.Selection.TryGet(out var start, out var end))
            {
                return lines;
            }

            var document = state.Tab?.TryGetDocument();
            for (var page = start.Page; page <= end.Page; page++)
            {
                lines[page] = [.. state.Selection.GetRects(document, page)];
            }

            return lines;
        }

        /// <summary>Moves the selection focus to the character under a canvas point and redraws only when the focus changed.</summary>
        /// <param name="position">The canvas point.</param>
        public void ExtendSelectionTo(CanvasPoint position)
        {
            if (state.TryHitTestCharacter(position, out var page, out var character) && state.Selection.MoveFocus(new(page, character)))
            {
                state.Host.InvalidateVisual();
            }
        }

        /// <summary>Marks the selected text when a markup tool is active, then clears the selection.</summary>
        public void ApplyMarkupTool()
        {
            if (state.Tab is not { } tab || AnnotationsViewModel.GetMarkupKind(tab.Annotations.Tool) is not { } kind)
            {
                return;
            }

            state.MarkSelection(tab, kind, tab.Annotations.Color);
        }

        /// <summary>Marks the selected text and clears the selection.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="kind">The markup kind.</param>
        /// <param name="color">The colour.</param>
        public void MarkSelection(DocumentTabViewModel tab, AnnotationKind kind, uint color)
        {
            ArgumentNullException.ThrowIfNull(tab);
            if (state.Selection.TryGet(out _, out _) && tab.Annotations.MarkText(kind, state.GetSelectionLines(), color))
            {
                state.ClearSelection();
            }
        }

        /// <summary>Works out the right-click menu for whatever is under the pointer.</summary>
        /// <param name="position">The canvas point.</param>
        /// <param name="copy">The command that copies the selected text.</param>
        /// <returns>The menu, or <see langword="null"/> when there is nothing to offer.</returns>
        public IReadOnlyList<MenuEntry>? GetContextMenu(CanvasPoint position, ICommand copy)
        {
            if (state.Tab is not { } tab)
            {
                return null;
            }

            var page = state.Layout.HitTest(position.X, position.Y);
            var annotations = tab.Annotations;
            var hit = page >= 0 ? annotations.HitTest(page, state.ToPage(tab, page, position)) : null;
            if (state.Selection.TryGet(out var start, out _))
            {
                return ContextMenus.ForSelection(tab, copy, state.GetSelectedText(), state.GetSelectionLines(), start);
            }

            if (hit is not null)
            {
                annotations.Select(hit);
                return ContextMenus.ForAnnotation(annotations, hit);
            }

            return page >= 0 ? ContextMenus.ForPage(tab, new(page, state.ToPage(tab, page, position))) : null;
        }
    }
}
