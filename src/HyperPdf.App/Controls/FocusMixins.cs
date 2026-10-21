// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Input;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Controls;

/// <summary>The end of a chain that moves the keyboard focus, so views never hold a method that only moves it.</summary>
internal static class FocusMixins
{
    /// <summary>Adds the focus moves for elements.</summary>
    /// <param name="elements">The elements to focus, one per value.</param>
    extension(IObservable<InputElement> elements)
    {
        /// <summary>Gives each element the keyboard focus.</summary>
        /// <returns>The subscription.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IDisposable TakeFocus() => elements.SubscribeSafe(static element => _ = element.Focus(), ErrorTrace.Write);
    }

    /// <summary>Adds the focus moves for text boxes.</summary>
    /// <param name="boxes">The text boxes to focus, one per value.</param>
    extension(IObservable<TextBox> boxes)
    {
        /// <summary>Gives each text box that is shown the keyboard focus, with the caret after its text, so typing carries on.</summary>
        /// <returns>The subscription.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IDisposable FocusAtEnd() => boxes
            .Where(static box => box.IsVisible)
            .SubscribeSafe(
                static box =>
                {
                    _ = box.Focus();
                    box.CaretIndex = box.Text?.Length ?? 0;
                },
                ErrorTrace.Write);

        /// <summary>Gives each text box that is shown the keyboard focus with all its text selected, so typing replaces it.</summary>
        /// <returns>The subscription.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IDisposable FocusSelectingAll() => boxes
            .Where(static box => box.IsVisible)
            .SubscribeSafe(
                static box =>
                {
                    _ = box.Focus();
                    box.SelectAll();
                },
                ErrorTrace.Write);
    }
}
