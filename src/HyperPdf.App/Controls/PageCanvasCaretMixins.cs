// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using HyperPdf.Core.Geometry;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Controls;

/// <summary>Reactive streams and helpers for caret navigation (F7), text selection, the clipboard and the document keyboard shortcuts on <see cref="PageCanvas"/>.</summary>
internal static class PageCanvasCaretMixins
{
    /// <summary>Adds the caret, text selection, clipboard and document key streams and helpers to a page canvas.</summary>
    /// <param name="canvas">The page canvas.</param>
    extension(PageCanvas canvas)
    {
        /// <summary>Gets the document keyboard presses handled by the canvas.</summary>
        internal IObservable<KeyEventArgs> DocumentKeys => canvas.KeyDowns
            .WhereWith(canvas, static (c, e) => c.TryHandleDocumentKey(e));

        /// <summary>Selects all the text of the document, as Ctrl+A does; the caret moves to its end.</summary>
        internal void SelectAllText()
        {
            if (canvas.Tab is { } tab)
            {
                canvas.State.SelectAllText(tab);
            }
        }

        /// <summary>Puts the caret at the first character of the current page or later, when caret mode starts.</summary>
        /// <param name="tab">The tab.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void StartCaret(DocumentTabViewModel tab) => canvas.State.StartCaret(tab);

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

        /// <summary>Puts text on the clipboard.</summary>
        /// <param name="text">The text.</param>
        internal void CopyToClipboard(string text)
        {
            if (text.Length > 0 && TopLevel.GetTopLevel(canvas)?.Clipboard is { } clipboard)
            {
                _ = clipboard.SetTextAsync(text);
            }
        }

        /// <summary>Handles document keyboard actions: the presentation layer decides, and the canvas does what it asks.</summary>
        /// <param name="e">The routed input.</param>
        /// <returns><see langword="true"/> when the key was handled.</returns>
        internal bool TryHandleDocumentKey(KeyEventArgs e)
        {
            var effects = canvas.State.PressKey(e.ViewerKey, e.ViewerModifiers);
            canvas.ApplyKeyEffects(effects);
            return effects.Handled;
        }

        /// <summary>Does what the presentation layer asked for after a key.</summary>
        /// <param name="effects">What was asked for.</param>
        internal void ApplyKeyEffects(InputEffects effects)
        {
            if (effects.Cursor is { } cursor)
            {
                canvas.Cursor = cursor.ToCursor();
            }

            if (effects.Choices is { } field && canvas.Tab is { } tab)
            {
                canvas.ShowChoices(tab.Forms, field);
            }

            if (effects.CopyText is { Length: > 0 } text)
            {
                canvas.CopyToClipboard(text);
            }
        }
    }
}
