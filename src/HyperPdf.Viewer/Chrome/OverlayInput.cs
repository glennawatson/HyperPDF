// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using HyperPdf.Presentation.Input;

namespace HyperPdf.Viewer.Chrome;

/// <summary>One thing the user did to the open menu, dialog or search bar. The window sends these to it as a stream.</summary>
/// <param name="Kind">What the user did.</param>
/// <param name="Item">The control pressed, for a press.</param>
/// <param name="Key">The key, for a key.</param>
/// <param name="Modifiers">The modifier keys held, for a key.</param>
/// <param name="Value">
/// The Unicode scalar value for a typed character, the rows to move down for a scroll, negative to move up, the presses counted close
/// together for a press, or the x of the pointer on a drawing pad.
/// </param>
/// <param name="Y">The y of the pointer on a drawing pad, in device pixels.</param>
[DebuggerDisplay("OverlayInput {Kind} {Key} {Value}")]
internal readonly record struct OverlayInput(OverlayInputKind Kind, ChromeItem? Item, ViewerKey Key, ViewerModifiers Modifiers, int Value, int Y)
{
    /// <summary>Gets a value indicating whether this is a typed character that can be shown. Control characters are not.</summary>
    internal bool IsTypedCharacter => Kind == OverlayInputKind.Text && Rune.IsValid(Value) && !Rune.IsControl(new(Value));

    /// <summary>Gets the typed character as text, which is empty unless <see cref="IsTypedCharacter"/>.</summary>
    internal string TypedText => IsTypedCharacter ? char.ConvertFromUtf32(Value) : string.Empty;

    /// <summary>Makes the press of a control.</summary>
    /// <param name="item">The control.</param>
    /// <returns>The input.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static OverlayInput Press(ChromeItem item) => Press(item, 1);

    /// <summary>Makes the press of a control that was pressed again close behind the one before, as a double click is.</summary>
    /// <param name="item">The control.</param>
    /// <param name="clicks">The presses counted close together, this one included.</param>
    /// <returns>The input, which carries the count in <see cref="Value"/>.</returns>
    internal static OverlayInput Press(ChromeItem item, int clicks) => new(OverlayInputKind.Press, item, default, default, clicks, 0);

    /// <summary>Makes a key press.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifier keys held.</param>
    /// <returns>The input.</returns>
    internal static OverlayInput KeyDown(ViewerKey key, ViewerModifiers modifiers) => new(OverlayInputKind.Key, null, key, modifiers, 0, 0);

    /// <summary>Makes a typed character.</summary>
    /// <param name="scalar">The Unicode scalar value.</param>
    /// <returns>The input.</returns>
    internal static OverlayInput Text(int scalar) => new(OverlayInputKind.Text, null, default, default, scalar, 0);

    /// <summary>Makes a scroll.</summary>
    /// <param name="rows">The rows to move down, negative to move up.</param>
    /// <returns>The input.</returns>
    internal static OverlayInput Scroll(int rows) => new(OverlayInputKind.Scroll, null, default, default, rows, 0);

    /// <summary>Makes the sending away of the overlay without an answer.</summary>
    /// <returns>The input.</returns>
    internal static OverlayInput Dismiss() => new(OverlayInputKind.Dismiss, null, default, default, 0, 0);

    /// <summary>Makes a stroke of the pointer on a drawing pad.</summary>
    /// <param name="kind">Whether the pointer went down, moved or was let go.</param>
    /// <param name="x">The x of the pointer in device pixels.</param>
    /// <param name="y">The y of the pointer in device pixels.</param>
    /// <returns>The input.</returns>
    internal static OverlayInput Pad(OverlayInputKind kind, int x, int y) => new(kind, null, default, default, x, y);
}
