// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Forms;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Controls;

/// <summary>
/// Reactive right-click context menu stream and menu builders for <see cref="PageCanvas"/>: selected text, picked
/// annotations, blank pages and choice form fields.
/// </summary>
internal static class PageCanvasContextMenuMixins
{
    /// <summary>Adds the right-click context menu stream and menu builders to a page canvas.</summary>
    /// <param name="canvas">The page canvas.</param>
    extension(PageCanvas canvas)
    {
        /// <summary>Gets the unhandled right-button pointer presses on the canvas, paired with the active tab.</summary>
        /// <param name="tabs">The tab stream.</param>
        /// <returns>The right-click menu requests.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<ContextMenuClick> RightClicks(IObservable<DocumentTabViewModel?> tabs) =>
            canvas.PointerPresses
                .Where(static e => !e.Handled)
                .Inject(tabs)
                .Pick(canvas.TryMatchRightClick);

        /// <summary>Opens the right-click menu for whatever is under the pointer.</summary>
        /// <param name="tab">The active tab.</param>
        /// <param name="position">The canvas point.</param>
        internal void ShowContextMenu(DocumentTabViewModel tab, Point position)
        {
            var page = canvas.Layout.HitTest(position.X, position.Y);
            var annotations = tab.Annotations;
            var hit = page >= 0 ? annotations.HitTest(page, canvas.ToPage(tab, page, position)) : null;
            IReadOnlyList<MenuEntry> items;
            if (canvas.TryGetSelection(out var start, out _))
            {
                items = ContextMenus.ForSelection(tab, canvas.CopyCommand, canvas.GetSelectedText(), canvas.GetSelectionLines(), start);
            }
            else if (hit is not null)
            {
                annotations.Select(hit);
                items = ContextMenus.ForAnnotation(annotations, hit);
            }
            else if (page >= 0)
            {
                items = ContextMenus.ForPage(tab, new(page, canvas.ToPage(tab, page, position)));
            }
            else
            {
                return;
            }

            var menu = new ContextMenu { ItemsSource = items.ToControls(), Placement = PlacementMode.Pointer };
            menu.Open(canvas);
            canvas.InvalidateVisual();
        }

        /// <summary>Opens a menu of a choice field's options, with the current one ticked.</summary>
        /// <param name="forms">The form state.</param>
        /// <param name="field">The field.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ShowChoices(FormsViewModel forms, FormField field) =>
            new ContextMenu { ItemsSource = ContextMenus.ForChoices(forms, field).ToControls(), Placement = PlacementMode.Pointer }.Open(canvas);

        /// <summary>Matches a right-button press on the canvas.</summary>
        /// <param name="item">The pointer press and tab.</param>
        /// <returns>The right-click request, or <see langword="null"/> when another button was pressed.</returns>
        private ContextMenuClick? TryMatchRightClick(Injected<PointerPressedEventArgs, DocumentTabViewModel> item)
        {
            var (e, tab) = item;
            var point = e.GetCurrentPoint(canvas);
            return PointerRouting.IsContextPress(point.ViewerButtons) ? new(canvas, tab, e, point.Position) : null;
        }

    }

    /// <summary>A right-button click on the page canvas that opens the context menu.</summary>
    /// <param name="Canvas">The page canvas.</param>
    /// <param name="Tab">The active tab.</param>
    /// <param name="Args">The pointer press arguments.</param>
    /// <param name="Position">The point in canvas coordinates.</param>
    internal readonly record struct ContextMenuClick(PageCanvas Canvas, DocumentTabViewModel Tab, PointerPressedEventArgs Args, Point Position)
    {
        /// <summary>Opens the context menu and marks the press handled.</summary>
        internal void OpenMenu()
        {
            Canvas.ShowContextMenu(Tab, Position);
            Args.Handled = true;
        }
    }
}
