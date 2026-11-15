// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using ReactiveUI.Primitives.Extensions;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Viewer.Chrome;

/// <summary>Holds what a menu, a dialog or the search bar shows as a record that each thing the user does turns into the next one.</summary>
internal static class OverlayStates
{
    /// <summary>Follows what the user does to an overlay: each input makes the next state from the one before it.</summary>
    /// <typeparam name="TState">The record that holds what the overlay shows.</typeparam>
    /// <param name="input">What the user does.</param>
    /// <param name="initial">The state when the overlay opens.</param>
    /// <param name="reduce">Makes the state that follows an input.</param>
    /// <returns>The state, which says its latest value and each change.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlyState<TState> Track<TState>(IObservable<OverlayInput> input, TState initial, Func<TState, OverlayInput, TState> reduce) =>
        input.ScanWithInitial(initial, reduce).ToReadOnlyState(initial, static state => state);
}
