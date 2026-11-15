// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Extensions;

namespace HyperPdf.Viewer.Chrome;

/// <summary>Splits what the user does to a menu, a dialog or the search bar into one stream for each kind of thing done.</summary>
internal static class OverlayInputMixins
{
    /// <summary>Operators over what the user does to an overlay.</summary>
    /// <param name="input">What the user does.</param>
    extension(IObservable<OverlayInput> input)
    {
        /// <summary>Keeps the presses of controls, and gives the control that was pressed.</summary>
        /// <returns>Each control pressed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<ChromeItem> SelectPressedItems() =>
            input.Where(static i => i.Kind == OverlayInputKind.Press && i.Item is not null).Select(static i => i.Item!);

        /// <summary>Keeps the key presses.</summary>
        /// <returns>Each key press, with the modifier keys held.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<OverlayInput> WhereKeyPress() => input.Where(static i => i.Kind == OverlayInputKind.Key);

        /// <summary>Keeps the typed characters that can be shown, as runes. Control characters are dropped.</summary>
        /// <returns>Each character typed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<Rune> SelectTypedRunes() =>
            input.Where(static i => i.Kind == OverlayInputKind.Text && Rune.IsValid(i.Value) && !Rune.IsControl(new(i.Value))).Select(static i => new Rune(i.Value));

        /// <summary>Keeps the scrolls, and gives how far each one moves.</summary>
        /// <returns>The rows each scroll moves down, negative to move up.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<int> SelectScrollRows() => input.Where(static i => i.Kind == OverlayInputKind.Scroll).Select(static i => i.Value);

        /// <summary>Keeps the pointer's strokes on a drawing pad: where it went down, where it moved and where it was let go.</summary>
        /// <returns>Each such input, with the place of the pointer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<OverlayInput> WherePad() =>
            input.Where(static i => i.Kind is OverlayInputKind.PadDown or OverlayInputKind.PadMove or OverlayInputKind.PadUp);

        /// <summary>Keeps each time the overlay was sent away without an answer, by a press outside it or because something took its place.</summary>
        /// <returns>A signal each time.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<RxVoid> SelectDismissals() => input.Where(static i => i.Kind == OverlayInputKind.Dismiss).AsSignal();
    }
}
