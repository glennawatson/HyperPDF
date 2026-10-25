// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Layout;

namespace HyperPdf.Presentation.Canvas;

/// <summary>Working with the text the reader has selected on the page canvas: clearing, extending, copying, marking and the right-click menu, for any toolkit.</summary>
public static class CanvasSelectionMixins
{
    /// <summary>Gets how far to scroll on one axis for a pointer held outside the view, or zero while it is inside.</summary>
    /// <param name="pointer">The pointer along the axis, in the scrolling area.</param>
    /// <param name="size">The size of the view along the axis.</param>
    /// <param name="seconds">The time since the last step.</param>
    /// <returns>The distance, negative towards the start.</returns>
    private static double GetDragScroll(double pointer, double size, double seconds)
    {
        var outside = pointer < 0 ? -pointer : pointer - size;
        if (outside <= 0)
        {
            return 0;
        }

        var rate = Math.Min(CanvasMetrics.SelectionScrollMax, CanvasMetrics.SelectionScrollMin + (outside * CanvasMetrics.SelectionScrollRate));
        return (pointer < 0 ? -rate : rate) * seconds;
    }

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

        /// <summary>Moves the selection focus to the character nearest a canvas point and redraws only when the focus changed.</summary>
        /// <param name="position">The canvas point.</param>
        public void ExtendSelectionTo(CanvasPoint position)
        {
            state.Tools.SelectionPointer = position;
            if (state.TryFindNearestCharacter(position, out var page, out var character) && state.Selection.MoveFocus(new(page, character)))
            {
                state.Host.InvalidateVisual();
            }
        }

        /// <summary>Starts a selection drag at a character, and tells the toolkit so it can follow the pointer outside the view.</summary>
        /// <param name="start">The character the drag starts at.</param>
        /// <param name="position">Where the pointer is on the canvas.</param>
        /// <param name="inScroller">Where the pointer is in the scrolling area.</param>
        public void BeginSelectionDrag(CharacterPosition start, CanvasPoint position, CanvasPoint inScroller)
        {
            var tools = state.Tools;
            state.Selection.Begin(start);
            state.Caret = start;
            tools.SelectionPointer = position;
            tools.SelectionPointerInScroller = inScroller;
            tools.SelectionDragLast = Stopwatch.GetTimestamp();
            tools.Selecting = true;
            state.Host.SelectionDragChanged(true);
        }

        /// <summary>Extends the selection from where it started, or from the caret, to a character; a shift-click.</summary>
        /// <param name="target">The character clicked.</param>
        /// <param name="position">Where the pointer is on the canvas.</param>
        /// <param name="inScroller">Where the pointer is in the scrolling area.</param>
        /// <returns><see langword="true"/> when there was something to extend from.</returns>
        public bool ExtendSelectionFromAnchor(CharacterPosition target, CanvasPoint position, CanvasPoint inScroller)
        {
            var selection = state.Selection;
            var origin = selection.Anchor.Page >= 0 ? selection.Anchor : state.Caret;
            if (origin.Page < 0)
            {
                return false;
            }

            var tools = state.Tools;
            selection.Begin(origin);
            _ = selection.MoveFocus(target);
            state.Caret = target;
            tools.SelectionPointer = position;
            tools.SelectionPointerInScroller = inScroller;
            tools.SelectionDragLast = Stopwatch.GetTimestamp();
            tools.Selecting = true;
            state.Host.SelectionDragChanged(true);
            state.Host.InvalidateVisual();
            return true;
        }

        /// <summary>Ends a selection drag.</summary>
        public void EndSelectionDrag()
        {
            var tools = state.Tools;
            if (!tools.Selecting)
            {
                return;
            }

            tools.Selecting = false;
            state.Host.SelectionDragChanged(false);
        }

        /// <summary>Records where the pointer is during a selection drag, then extends the selection to the text nearest it.</summary>
        /// <param name="position">The canvas point.</param>
        /// <param name="inScroller">Where the pointer is in the scrolling area.</param>
        public void DragSelectionTo(CanvasPoint position, CanvasPoint inScroller)
        {
            state.Tools.SelectionPointerInScroller = inScroller;
            state.ExtendSelectionTo(position);
        }

        /// <summary>
        /// Scrolls the pages while the pointer is held outside the view during a selection drag, faster the further out it is, and
        /// carries the selection on to the text that comes into view under the still pointer.
        /// </summary>
        /// <returns><see langword="true"/> when the pages moved.</returns>
        public bool StepSelectionDrag()
        {
            var tools = state.Tools;
            var host = state.Host;
            var now = Stopwatch.GetTimestamp();
            var seconds = Stopwatch.GetElapsedTime(tools.SelectionDragLast, now).TotalSeconds;
            tools.SelectionDragLast = now;
            if (!tools.Selecting || !host.HasScroller)
            {
                return false;
            }

            var viewport = host.ViewportSize;
            var pointer = tools.SelectionPointerInScroller;
            var deltaY = GetDragScroll(pointer.Y, viewport.Height, seconds);
            var deltaX = GetDragScroll(pointer.X, viewport.Width, seconds);
            if (deltaX == 0 && deltaY == 0)
            {
                return false;
            }

            var before = host.ScrollOffset;
            if (deltaY == 0 || !state.StepPageByPage(deltaY))
            {
                var maxX = Math.Max(0, host.ExtentSize.Width - viewport.Width);
                var maxY = Math.Max(0, host.ExtentSize.Height - viewport.Height);
                host.ScrollOffset = new(Math.Clamp(before.X + deltaX, 0, maxX), Math.Clamp(before.Y + deltaY, 0, maxY));
            }

            var after = host.ScrollOffset;
            if (after == before)
            {
                return false;
            }

            state.ReportPosition();

            // The pointer has not moved, but the pages under it have by exactly the scroll.
            var carried = new CanvasPoint(tools.SelectionPointer.X + (after.X - before.X), tools.SelectionPointer.Y + (after.Y - before.Y));
            state.ExtendSelectionTo(carried);
            return true;
        }

        /// <summary>Finds the character nearest a canvas point: the one under it, or the closest text on the page, or an end of the page's text when the point is above or below it.</summary>
        /// <param name="position">The canvas point.</param>
        /// <param name="page">The page.</param>
        /// <param name="character">The character index.</param>
        /// <returns><see langword="true"/> when a character was found.</returns>
        public bool TryFindNearestCharacter(CanvasPoint position, out int page, out int character)
        {
            character = -1;
            page = -1;
            if (state.Tab is not { } tab || tab.TryGetDocument() is not { } document || state.Layout.PageCount == 0)
            {
                return false;
            }

            page = state.Layout.HitTest(position.X, position.Y);
            if (page < 0)
            {
                page = state.Layout.GetPageNearest(position.X, position.Y);
                var bounds = state.Layout.GetPageBounds(page);
                if (position.Y < bounds.Y || position.Y > bounds.Bottom)
                {
                    var count = document.GetCharacterCount(page);
                    character = position.Y < bounds.Y ? 0 : count - 1;
                    return count > 0;
                }
            }

            var transform = new PageTransform(state.Layout.GetPageBounds(page), state.Sizes[page], tab.Rotation, state.Layout.Options.Scale);
            var point = transform.MapToPage(position.X, position.Y);
            for (var reach = CanvasMetrics.TextTolerance; reach <= CanvasMetrics.NearestTextLimit; reach *= CanvasMetrics.NearestTextGrowth)
            {
                character = document.GetCharacterIndexAt(page, point, reach);
                if (character >= 0)
                {
                    return true;
                }
            }

            return false;
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
