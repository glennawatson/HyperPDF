// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Rendering;
using HyperPdf.Viewer.Chrome.Forms;
using ReactiveUI.Primitives;

namespace HyperPdf.Viewer.Chrome;

/// <summary>
/// Something shown over the window for a while: a menu, a dialog, the search bar. It is nothing but streams: the window sends what
/// the user does into <paramref name="Input"/>, and hears through <paramref name="Changes"/> that it must be laid out again and
/// through <paramref name="Closed"/> that it wants to close. Letting <paramref name="Lifetime"/> go ends all of it.
/// </summary>
/// <param name="Kind">What it is.</param>
/// <param name="Traits">How it behaves towards the rest of the window.</param>
/// <param name="Layout">Lays its controls out.</param>
/// <param name="Input">Takes the presses, keys, typed characters and scrolls the window sends while it is open.</param>
/// <param name="Changes">Signals each change its view model makes by itself, such as a search finding its matches.</param>
/// <param name="Closed">Signals that it wants to close, because the user answered it or sent it away.</param>
/// <param name="Lifetime">What it follows and owns for as long as it is open.</param>
[DebuggerDisplay("Overlay {Kind} {Traits}")]
internal sealed record Overlay(
    OverlayKind Kind,
    OverlayTraits Traits,
    OverlayLayout Layout,
    IObserver<OverlayInput> Input,
    IObservable<RxVoid> Changes,
    IObservable<RxVoid> Closed,
    IDisposable Lifetime)
{
    /// <summary>The index of the control that shows the overlay's own picture. The rows of the pages panel count up from zero.</summary>
    internal const int PictureIndex = -1;

    /// <summary>Gets what gives the picture shown in the control numbered <see cref="PictureIndex"/>, or <see langword="null"/> for an overlay without one.</summary>
    internal Func<IRenderSurface?>? Picture { get; init; }

    /// <summary>Gets what is drawn on the overlay's drawing pad, or <see langword="null"/> for an overlay without one.</summary>
    internal Func<PadInk>? Ink { get; init; }

    /// <summary>
    /// Gets what tells whether the overlay lies somewhere else than when it was laid out, as a box on a page does once the page
    /// scrolls, or <see langword="null"/> for an overlay that stays where it is.
    /// </summary>
    internal Func<bool>? HasMoved { get; init; }

    /// <summary>Gets a value indicating whether the rest of the window is dimmed and cannot be pressed while this is open.</summary>
    internal bool IsModal => (Traits & OverlayTraits.Modal) != 0;

    /// <summary>Gets a value indicating whether every key goes to the overlay. When it does not, only typing, Enter, Escape and Backspace do.</summary>
    internal bool TakesAllKeys => (Traits & OverlayTraits.TakesAllKeys) != 0;

    /// <summary>Gets a value indicating whether a press outside the overlay sends it away.</summary>
    internal bool ClosesOnOutsidePress => (Traits & OverlayTraits.ClosesOnOutsidePress) != 0;

    /// <summary>Gets a value indicating whether the overlay uses Tab itself, so Tab does not move the keyboard between its controls.</summary>
    internal bool UsesTab => (Traits & OverlayTraits.UsesTab) != 0;

    /// <summary>Gets a value indicating whether the overlay stands in for the pages, so a menu or a dialog opens over it and it comes back when that closes.</summary>
    internal bool StaysUnder => (Traits & OverlayTraits.StaysUnder) != 0;

    /// <summary>Gets a value indicating whether it has a drawing pad.</summary>
    internal bool TakesPointer => (Traits & OverlayTraits.TakesPointer) != 0;
}
