// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;

namespace HyperPdf.Presentation.Canvas;

/// <summary>The text caret and character hit testing of a page canvas, for any toolkit.</summary>
public static class CanvasCaretMixins
{
    /// <summary>The caret line width in canvas units.</summary>
    private const double CaretWidth = 2;

    /// <summary>How far the caret reaches above and below the glyph, as a fraction of its height, to span the line.</summary>
    private const double CaretOverhang = 0.3;

    /// <summary>Half, for centring the caret line on its character.</summary>
    private const double Half = 0.5;

    /// <summary>Adds the caret helpers to a canvas state.</summary>
    /// <param name="state">The canvas state.</param>
    extension(PageCanvasState state)
    {
        /// <summary>Puts the caret at the first character of the current page or later, when caret mode starts.</summary>
        /// <param name="tab">The tab.</param>
        public void StartCaret(DocumentTabViewModel tab)
        {
            ArgumentNullException.ThrowIfNull(tab);
            if (state.Caret.Page >= 0 || tab.TryGetDocument() is not { } document)
            {
                return;
            }

            for (var page = Math.Max(0, tab.CurrentPageIndex); page < document.PageCount; page++)
            {
                if (document.GetCharacterCount(page) <= 0)
                {
                    continue;
                }

                state.Caret = new(page, 0);
                state.Host.InvalidateVisual();
                return;
            }
        }

        /// <summary>Handles the arrow keys in caret mode.</summary>
        /// <param name="key">The key.</param>
        /// <param name="modifiers">The modifiers held with it.</param>
        /// <returns><see langword="true"/> when handled.</returns>
        public bool HandleCaretKey(ViewerKey key, ViewerModifiers modifiers)
        {
            if (state.Tab is not { IsCaretMode: true } tab || tab.TryGetDocument() is not { } document || (modifiers & ~ViewerModifiers.Shift) != ViewerModifiers.None)
            {
                return false;
            }

            state.StartCaret(tab);
            if (state.Caret.Page < 0)
            {
                return false;
            }

            var next = CaretKeys.GetNext(document, state.Caret, key, state.CaretBounds);
            if (next.Page < 0)
            {
                return CaretKeys.IsCaretKey(key);
            }

            state.MoveCaret(next, (modifiers & ViewerModifiers.Shift) != ViewerModifiers.None);
            return true;
        }

        /// <summary>Moves the caret, extending the selection when asked, and keeps it in view.</summary>
        /// <param name="next">The new position.</param>
        /// <param name="extend">Whether to extend the selection.</param>
        public void MoveCaret(CharacterPosition next, bool extend)
        {
            state.Selection.MoveCaret(state.Caret, next, extend);
            state.Caret = next;
            state.ScrollCaretIntoView();
            state.Host.InvalidateVisual();
        }

        /// <summary>Selects all the text on the current page; the caret moves to its end, so Shift and the arrows adjust it.</summary>
        /// <param name="tab">The tab.</param>
        public void SelectAllOnPage(DocumentTabViewModel tab)
        {
            ArgumentNullException.ThrowIfNull(tab);
            if (tab.TryGetDocument() is not { PageCount: > 0 } document)
            {
                return;
            }

            var page = Math.Clamp(tab.CurrentPageIndex, 0, document.PageCount - 1);
            var count = document.GetCharacterCount(page);
            state.Selection.Clear();
            state.Host.InvalidateVisual();
            if (count <= 0)
            {
                return;
            }

            state.Selection.SelectPage(page, count);
            state.Caret = new(page, count - 1);
        }

        /// <summary>Scrolls just enough to show the caret.</summary>
        public void ScrollCaretIntoView()
        {
            var host = state.Host;
            if (!host.HasScroller || !state.TryGetCaretArea(out var caret))
            {
                return;
            }

            var offset = host.ScrollOffset;
            var viewport = host.ViewportSize;
            host.ScrollOffset = new(
                PageScrolling.GetOffsetToShow(offset.X, viewport.Width, caret.X, caret.Right, CanvasMetrics.ContentMargin),
                PageScrolling.GetOffsetToShow(offset.Y, viewport.Height, caret.Y, caret.Bottom, CanvasMetrics.ContentMargin));
        }

        /// <summary>Gets the caret's line on the canvas.</summary>
        /// <param name="area">The line, when there is a caret in view.</param>
        /// <returns><see langword="true"/> when there is a caret to show.</returns>
        public bool TryGetCaretArea(out LayoutRect area)
        {
            area = default;
            var caret = state.Caret;
            if (state.Tab is not { } tab || caret.Page < 0 || caret.Page >= state.Layout.PageCount || tab.TryGetDocument() is not { } document)
            {
                return false;
            }

            state.CaretBounds.Clear();
            document.GetTextBounds(caret.Page, caret.Char, 1, state.CaretBounds);
            if (state.CaretBounds.Count == 0)
            {
                return false;
            }

            var glyph = new PageTransform(state.Layout.GetPageBounds(caret.Page), state.Sizes[caret.Page], tab.Rotation, state.Layout.Options.Scale).MapToCanvas(state.CaretBounds[0]);
            var overhang = glyph.Height * CaretOverhang;
            area = new(glyph.X - (CaretWidth * Half), glyph.Y - overhang, CaretWidth, glyph.Height + overhang + overhang);
            return true;
        }

        /// <summary>Finds the character under a canvas point.</summary>
        /// <param name="position">The canvas point.</param>
        /// <param name="page">The page.</param>
        /// <param name="character">The character index.</param>
        /// <returns><see langword="true"/> when over text.</returns>
        public bool TryHitTestCharacter(CanvasPoint position, out int page, out int character)
        {
            character = -1;
            page = state.Layout.HitTest(position.X, position.Y);
            var tab = state.Tab;
            var document = tab?.TryGetDocument();
            if (page < 0 || tab is null || document is null)
            {
                return false;
            }

            var transform = new PageTransform(state.Layout.GetPageBounds(page), state.Sizes[page], tab.Rotation, state.Layout.Options.Scale);
            character = document.GetCharacterIndexAt(page, transform.MapToPage(position.X, position.Y), CanvasMetrics.TextTolerance);
            return character >= 0;
        }
    }
}
