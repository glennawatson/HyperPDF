// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Presentation.Input;

namespace HyperPdf.Windowing.Windows;

/// <summary>
/// One event from a platform window, in a shape every head can fill without allocating. A head translates its own key, button
/// and modifier numbers before it reports an event, so the viewer never sees a platform code.
/// </summary>
/// <param name="Kind">What happened.</param>
/// <param name="X">The pointer x, the new width for a resize, or the sideways scroll amount, in device pixels or scroll units.</param>
/// <param name="Y">The pointer y, the new height for a resize, or the up and down scroll amount, in device pixels or scroll units.</param>
/// <param name="Code">A <see cref="ViewerKey"/> for a key event, the <see cref="ViewerButtons"/> that changed for a button event, or the buttons held for a pointer move.</param>
/// <param name="Modifiers">The <see cref="ViewerModifiers"/> held at the time.</param>
[DebuggerDisplay("WindowEvent {Kind} ({X}, {Y}) code {Code}")]
public readonly record struct WindowEvent(WindowEventKind Kind, int X, int Y, uint Code, uint Modifiers)
{
    /// <summary>The scroll amount of one wheel notch.</summary>
    private const int NotchUnits = 120;

    /// <summary>Gets the scroll amount of one wheel notch. A positive amount scrolls up or to the left, and a touchpad reports parts of a notch.</summary>
    public static int ScrollNotch => NotchUnits;

    /// <summary>Gets the key of a key event.</summary>
    public ViewerKey Key => (ViewerKey)Code;

    /// <summary>Gets the buttons of a pointer event.</summary>
    public ViewerButtons Buttons => (ViewerButtons)Code;

    /// <summary>Gets the modifier keys held at the time of the event.</summary>
    public ViewerModifiers Held => (ViewerModifiers)Modifiers;
}
