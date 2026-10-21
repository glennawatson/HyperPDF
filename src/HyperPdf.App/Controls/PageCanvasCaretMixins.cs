// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Controls;

/// <summary>
/// Reactive streams and helpers for caret navigation (F7), text selection, link clicking, keyboard shortcuts and pointer
/// movement/release routing on <see cref="PageCanvas"/>.
/// </summary>
internal static class PageCanvasCaretMixins
{
    /// <summary>The hit tolerance for text, in points.</summary>
    private const float TextTolerance = 4F;

    /// <summary>Adds the caret, text selection, link click, pointer move/release and document key streams and helpers to a page canvas.</summary>
    /// <param name="canvas">The page canvas.</param>
    extension(PageCanvas canvas)
    {
        /// <summary>Gets the document keyboard presses handled by the canvas.</summary>
        internal IObservable<KeyEventArgs> DocumentKeys => canvas.KeyDowns
            .WhereWith(canvas, static (c, e) => c.TryHandleDocumentKey(e));

        /// <summary>Gets the unhandled left pointer presses that click a link or start a text selection drag.</summary>
        /// <param name="tabs">The tab stream.</param>
        /// <returns>The link or selection presses.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<SelectionOrLinkPress> SelectionAndLinkPresses(IObservable<DocumentTabViewModel?> tabs) =>
            canvas.PointerPresses
                .Where(static e => !e.Handled)
                .Inject(tabs)
                .Pick(canvas.TryCreateSelectionOrLinkPress);

        /// <summary>Gets the pointer movements that update active tools, text selection or hover cursors.</summary>
        /// <param name="tabs">The tab stream.</param>
        /// <returns>The pointer move steps.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<PointerMoveStep> PointerMoveSteps(IObservable<DocumentTabViewModel?> tabs) =>
            canvas.PointerMoves
                .Inject(tabs)
                .SelectWith(canvas, static (c, item) => new PointerMoveStep(c, item.Value));

        /// <summary>Gets the pointer releases that complete panning, area drags, annotation tools, text selection or link clicks.</summary>
        /// <param name="tabs">The tab stream.</param>
        /// <returns>The pointer release steps.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<PointerReleaseStep> PointerReleaseSteps(IObservable<DocumentTabViewModel?> tabs) =>
            canvas.PointerReleases
                .Where(static e => !e.Handled)
                .Inject(tabs)
                .SelectWith(canvas, static (c, item) => new PointerReleaseStep(c, item.State, item.Value));

        /// <summary>Selects all the text on the page shown, as Ctrl+A does; the caret moves to its end.</summary>
        internal void SelectAllText()
        {
            if (canvas.Tab is { } tab)
            {
                canvas.SelectAllOnPage(tab);
            }
        }

        /// <summary>Puts the caret at the first character of the current page or later, when caret mode starts.</summary>
        /// <param name="tab">The tab.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void StartCaret(DocumentTabViewModel tab) => canvas.State.StartCaret(tab);

        /// <summary>Handles the moving and grabbing keys before the other page keys.</summary>
        /// <param name="e">The key press.</param>
        /// <returns><see langword="true"/> when the key was used.</returns>
        internal bool HandleNavigationKey(KeyEventArgs e)
        {
            if (canvas.Tab is not { } tab)
            {
                return false;
            }

            if (DocumentKeys.HandleAutoScroll(tab, e.ViewerKey, e.ViewerModifiers))
            {
                return true;
            }

            if (e.Key == Key.A && e.KeyModifiers == KeyModifiers.Control)
            {
                canvas.SelectAllOnPage(tab);
                return true;
            }

            return (e.KeyModifiers == KeyModifiers.None || e.Key == Key.Space) && canvas.HandlePlainNavigationKey(tab, e);
        }

        /// <summary>Handles the arrow keys in caret mode.</summary>
        /// <param name="e">The key event.</param>
        /// <returns><see langword="true"/> when handled.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool HandleCaretKey(KeyEventArgs e) => canvas.State.HandleCaretKey(e.ViewerKey, e.ViewerModifiers);

        /// <summary>Gets the normalised selection.</summary>
        /// <param name="start">The start.</param>
        /// <param name="end">The end.</param>
        /// <returns><see langword="true"/> when there is a selection.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryGetSelection(out CharacterPosition start, out CharacterPosition end) => canvas.Selection.TryGet(out start, out end);

        /// <summary>Gets the selection rectangles on a page, computing them on first use.</summary>
        /// <param name="page">The page.</param>
        /// <returns>The rectangles.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal List<PageRect> GetSelectionRects(int page) => canvas.Selection.GetRects(canvas.Tab?.TryGetDocument(), page);

        /// <summary>Moves the selection focus.</summary>
        /// <param name="next">The new focus.</param>
        /// <returns><see langword="true"/> when the focus moved.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool MoveSelectionFocus(CharacterPosition next) => canvas.Selection.MoveFocus(next);

        /// <summary>Moves the selection focus to the character under a canvas point and redraws only when the focus changed.</summary>
        /// <param name="position">The canvas point.</param>
        internal void ExtendSelectionTo(Point position)
        {
            if (canvas.TryHitTestCharacter(position, out var page, out var character) && canvas.MoveSelectionFocus(new(page, character)))
            {
                canvas.InvalidateVisual();
            }
        }

        /// <summary>Finds the character under a canvas point.</summary>
        /// <param name="position">The canvas point.</param>
        /// <param name="page">The page.</param>
        /// <param name="character">The character index.</param>
        /// <returns><see langword="true"/> when over text.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryHitTestCharacter(Point position, out int page, out int character) => canvas.State.TryHitTestCharacter(new(position.X, position.Y), out page, out character);

        /// <summary>Determines whether the pointer is over text.</summary>
        /// <param name="hover">The page and page point under the pointer.</param>
        /// <returns><see langword="true"/> when over text.</returns>
        internal bool IsOverText(PointerHover hover) =>
            hover.IsOverPage && canvas.Tab?.TryGetDocument() is { } document && document.GetCharacterIndexAt(hover.Page, hover.Point, TextTolerance) >= 0;

        /// <summary>Finds the link under the pointer.</summary>
        /// <param name="hover">The page and page point under the pointer.</param>
        /// <returns>The link, if any.</returns>
        internal PageLink? HitTestLink(PointerHover hover)
        {
            if (!hover.IsOverPage || canvas.Tab is not { } tab)
            {
                return null;
            }

            ref var links = ref CollectionsMarshal.GetValueRefOrAddDefault(canvas.Links, hover.Page, out _);
            links ??= tab.TryGetDocument()?.GetLinks(hover.Page) ?? [];
            foreach (var link in links)
            {
                if (link.Bounds.Contains(hover.Point))
                {
                    return link;
                }
            }

            return null;
        }

        /// <summary>Finds the link under a canvas point.</summary>
        /// <param name="position">The canvas point.</param>
        /// <param name="page">The page.</param>
        /// <returns>The link, if any.</returns>
        internal PageLink? HitTestLink(Point position, out int page)
        {
            page = canvas.Layout.HitTest(position.X, position.Y);
            var tab = canvas.Tab;
            if (page < 0 || tab is null)
            {
                return null;
            }

            ref var links = ref CollectionsMarshal.GetValueRefOrAddDefault(canvas.Links, page, out _);
            links ??= tab.TryGetDocument()?.GetLinks(page) ?? [];

            var point = new PageTransform(canvas.Layout.GetPageBounds(page), canvas.Sizes[page], tab.Rotation, canvas.Layout.Options.Scale).ToPage(position);
            foreach (var link in links)
            {
                if (link.Bounds.Contains(point))
                {
                    return link;
                }
            }

            return null;
        }

        /// <summary>Puts text on the clipboard.</summary>
        /// <param name="text">The text.</param>
        internal void CopyToClipboard(string text)
        {
            if (text.Length > 0 && TopLevel.GetTopLevel(canvas)?.Clipboard is { } clipboard)
            {
                _ = clipboard.SetTextAsync(text);
            }
        }

        /// <summary>Handles document keyboard actions.</summary>
        /// <param name="e">The routed input.</param>
        /// <returns><see langword="true"/> when the key was handled.</returns>
        internal bool TryHandleDocumentKey(KeyEventArgs e)
        {
            // A focused form field takes Space and Tab before they scroll or move between pages.
            if (canvas.HandleFormKey(e) || canvas.HandleNavigationKey(e))
            {
                return true;
            }

            if (e.Key == Key.C && (e.KeyModifiers & KeyModifiers.Control) != 0)
            {
                canvas.CopyToClipboard(canvas.GetSelectedText());
                return true;
            }

            return canvas.HandlePlacementKey(e)
                || canvas.HandleMeasureKey(e)
                || canvas.HandleAnnotationKey(e)
                || canvas.HandleCaretKey(e)
                || canvas.HandlePageByPageKey(e);
        }

        /// <summary>Creates a <see cref="SelectionOrLinkPress"/> for an unhandled left press on the canvas.</summary>
        /// <param name="item">The pointer press and tab.</param>
        /// <returns>The press step, or <see langword="null"/> when not an unhandled left press.</returns>
        private SelectionOrLinkPress? TryCreateSelectionOrLinkPress(Injected<PointerPressedEventArgs, DocumentTabViewModel> item)
        {
            var e = item.Value;
            if (e.Handled)
            {
                return null;
            }

            var point = e.GetCurrentPoint(canvas);
            return PointerRouting.IsPlainLeft(point.ViewerButtons)
                ? new(canvas, e, point.Position)
                : null;
        }

        /// <summary>Selects all the text on the current page; the caret moves to its end, so Shift and the arrows adjust it.</summary>
        /// <param name="tab">The tab.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SelectAllOnPage(DocumentTabViewModel tab) => canvas.State.SelectAllOnPage(tab);

        /// <summary>Handles Home, End, Escape and Space, pressed without Ctrl or Alt.</summary>
        /// <param name="tab">The tab.</param>
        /// <param name="e">The key press.</param>
        /// <returns><see langword="true"/> when the key was used.</returns>
        private bool HandlePlainNavigationKey(DocumentTabViewModel tab, KeyEventArgs e)
        {
            // In caret mode Home and End stay with the text cursor.
            if (DocumentKeys.HandleEdgePage(tab, e.ViewerKey))
            {
                canvas.InvalidateVisual();
                return true;
            }

            return e.Key switch
            {
                Key.Escape => canvas.EscapePageTool(tab),
                Key.Space => !tab.IsCaretMode && canvas.PressSpace(e),
                _ => false,
            };
        }

    }

    /// <summary>An unhandled left pointer press that clicks a link or starts a text selection drag.</summary>
    /// <param name="Canvas">The page canvas.</param>
    /// <param name="Args">The pointer press arguments.</param>
    /// <param name="Position">The point in canvas coordinates.</param>
    internal readonly record struct SelectionOrLinkPress(PageCanvas Canvas, PointerPressedEventArgs Args, Point Position)
    {
        /// <summary>Checks for a link or character under the pointer and starts text selection when over text.</summary>
        internal void Begin()
        {
            Canvas.PressedLink = Canvas.HitTestLink(Position, out _);
            Canvas.ClearSelection();
            if (Canvas.PressedLink is not null || !Canvas.TryHitTestCharacter(Position, out var page, out var character))
            {
                return;
            }

            var picked = new CharacterPosition(page, character);
            Canvas.Selection.Begin(picked);
            Canvas.Caret = picked;
            Canvas.Selecting = true;
            Args.Pointer.Capture(Canvas);
            Args.Handled = true;
        }
    }

    /// <summary>A pointer movement step on the page canvas.</summary>
    /// <param name="Canvas">The page canvas.</param>
    /// <param name="Args">The pointer event arguments.</param>
    internal readonly record struct PointerMoveStep(PageCanvas Canvas, PointerEventArgs Args)
    {
        /// <summary>Updates region hover, active tools, text selection or the hover cursor.</summary>
        internal void Apply()
        {
            var position = Args.GetPosition(Canvas);
            var hover = PointerHover.At(Canvas, position);
            Canvas.HoverRegion(hover);
            if (Canvas.ContinuePageTool(Args, position) || Canvas.ContinueMeasure(position) || Canvas.ContinueStroke(position))
            {
                return;
            }

            if (Canvas.Selecting)
            {
                Canvas.ExtendSelectionTo(position);
                return;
            }

            var clickable = Canvas.HitTestLink(hover) is not null || Canvas.IsOverField(hover) || Canvas.IsOverCommentTrigger(hover);
            Canvas.Cursor = PointerRouting.GetHoverCursor(Canvas.IsOverText(hover), clickable).ToCursor();
        }
    }

    /// <summary>A pointer release step on the page canvas.</summary>
    /// <param name="Canvas">The page canvas.</param>
    /// <param name="Tab">The active tab.</param>
    /// <param name="Args">The pointer release arguments.</param>
    internal readonly record struct PointerReleaseStep(PageCanvas Canvas, DocumentTabViewModel Tab, PointerReleasedEventArgs Args)
    {
        /// <summary>Completes panning, area drags, annotation tools, text selection or link clicks.</summary>
        internal void Apply()
        {
            Args.Pointer.Capture(null);
            var position = Args.GetPosition(Canvas);
            if (Canvas.EndPan(position) || Canvas.EndArea(position) || Canvas.EndAnnotationPress(position))
            {
                return;
            }

            if (Canvas.Selecting)
            {
                Canvas.Selecting = false;
                Canvas.ApplyMarkupTool();
            }

            var link = Canvas.PressedLink;
            Canvas.PressedLink = null;
            var travel = position - Canvas.PressPoint;
            if (link is { } pressed && Math.Abs(travel.X) < PageCanvas.DragThreshold && Math.Abs(travel.Y) < PageCanvas.DragThreshold)
            {
                Tab.Navigate(pressed.Target);
            }
        }
    }
}
