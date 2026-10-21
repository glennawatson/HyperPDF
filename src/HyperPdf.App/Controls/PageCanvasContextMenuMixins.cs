// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using HyperPdf.Core.Forms;

namespace HyperPdf.App.Controls;

/// <summary>Opens the right-click menu and the menu of a choice field's options on <see cref="PageCanvas"/>; what they offer is worked out by the presentation layer and the view models.</summary>
internal static class PageCanvasContextMenuMixins
{
    /// <summary>Adds the menu openers to a page canvas.</summary>
    /// <param name="canvas">The page canvas.</param>
    extension(PageCanvas canvas)
    {
        /// <summary>Opens the right-click menu for whatever is under the pointer.</summary>
        /// <param name="position">The canvas point.</param>
        internal void ShowContextMenu(Point position)
        {
            if (canvas.State.GetContextMenu(new(position.X, position.Y), canvas.CopyCommand) is not { } items)
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
    }
}
