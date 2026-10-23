// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Accessibility;

namespace HyperPdf.Platform.Linux.Accessibility.AtSpi;

/// <summary>Turns the app's element states into the AT-SPI state set: a 64 bit mask sent as two 32 bit numbers.</summary>
internal static class AtSpiStateMap
{
    /// <summary>The number of bits in each half of the sent state set.</summary>
    private const int HalfBits = 32;

    /// <summary>The AT-SPI state numbers the app's states stand for.</summary>
    private static readonly StateBits[] Table =
    [
        new(AccessibleStates.Enabled, Bit(StateId.Enabled) | Bit(StateId.Sensitive)),
        new(AccessibleStates.Showing, Bit(StateId.Showing) | Bit(StateId.Visible) | Bit(StateId.Opaque)),
        new(AccessibleStates.Focusable, Bit(StateId.Focusable)),
        new(AccessibleStates.Focused, Bit(StateId.Focused)),
        new(AccessibleStates.Selectable, Bit(StateId.Selectable)),
        new(AccessibleStates.Selected, Bit(StateId.Selected) | Bit(StateId.Selectable)),
        new(AccessibleStates.Checked, Bit(StateId.Checked) | Bit(StateId.Checkable)),
        new(AccessibleStates.Indeterminate, Bit(StateId.Indeterminate) | Bit(StateId.Checkable)),
        new(AccessibleStates.Expandable, Bit(StateId.Expandable)),
        new(AccessibleStates.Expanded, Bit(StateId.Expanded) | Bit(StateId.Expandable)),
        new(AccessibleStates.Editable, Bit(StateId.Editable)),
        new(AccessibleStates.MultiLine, Bit(StateId.MultiLine)),
        new(AccessibleStates.Active, Bit(StateId.Active)),
        new(AccessibleStates.Modal, Bit(StateId.Modal)),
        new(AccessibleStates.Busy, Bit(StateId.Busy)),
        new(AccessibleStates.Required, Bit(StateId.Required)),
        new(AccessibleStates.Invalid, Bit(StateId.InvalidEntry)),
        new(AccessibleStates.Pressed, Bit(StateId.Pressed)),
        new(AccessibleStates.Horizontal, Bit(StateId.Horizontal)),
        new(AccessibleStates.Vertical, Bit(StateId.Vertical)),
        new(AccessibleStates.ReadOnly, Bit(StateId.ReadOnly)),
    ];

    /// <summary>The AT-SPI state numbers the server uses, from <c>atspi-constants.h</c>.</summary>
    private enum StateId
    {
        /// <summary>The window is active.</summary>
        Active = 1,

        /// <summary>The element is busy.</summary>
        Busy = 3,

        /// <summary>The element is checked.</summary>
        Checked = 4,

        /// <summary>The element is closed.</summary>
        Collapsed = 5,

        /// <summary>The element's text can be changed.</summary>
        Editable = 7,

        /// <summary>The element is enabled.</summary>
        Enabled = 8,

        /// <summary>The element can open.</summary>
        Expandable = 9,

        /// <summary>The element is open.</summary>
        Expanded = 10,

        /// <summary>The element can take focus.</summary>
        Focusable = 11,

        /// <summary>The element has focus.</summary>
        Focused = 12,

        /// <summary>The element lies horizontally.</summary>
        Horizontal = 14,

        /// <summary>The element blocks the application.</summary>
        Modal = 16,

        /// <summary>The element has many lines.</summary>
        MultiLine = 17,

        /// <summary>The element paints every pixel of its area.</summary>
        Opaque = 19,

        /// <summary>The button is held down.</summary>
        Pressed = 20,

        /// <summary>The element can be selected.</summary>
        Selectable = 22,

        /// <summary>The element is selected.</summary>
        Selected = 23,

        /// <summary>The element reacts to input.</summary>
        Sensitive = 24,

        /// <summary>The element is on screen.</summary>
        Showing = 25,

        /// <summary>The element has one line.</summary>
        SingleLine = 26,

        /// <summary>The element lies vertically.</summary>
        Vertical = 29,

        /// <summary>The element is visible.</summary>
        Visible = 30,

        /// <summary>The element is neither on nor off.</summary>
        Indeterminate = 32,

        /// <summary>The element must be filled in.</summary>
        Required = 33,

        /// <summary>The element holds a value that is not accepted.</summary>
        InvalidEntry = 36,

        /// <summary>The element can be checked.</summary>
        Checkable = 41,

        /// <summary>The element cannot be changed.</summary>
        ReadOnly = 43,
    }

    /// <summary>Gets the AT-SPI state set of an element.</summary>
    /// <param name="states">The element's states.</param>
    /// <param name="role">The element's role, which adds the states that follow from it.</param>
    /// <returns>The mask, with the state numbered N in bit N.</returns>
    internal static ulong ToMask(AccessibleStates states, AccessibleRole role)
    {
        var mask = 0UL;
        foreach (var (flag, bits) in Table)
        {
            mask |= (states & flag) != 0 ? bits : 0UL;
        }

        if (role == AccessibleRole.Edit)
        {
            mask |= (states & AccessibleStates.MultiLine) != 0 ? 0UL : Bit(StateId.SingleLine);
        }

        if ((states & AccessibleStates.Expandable) != 0 && (states & AccessibleStates.Expanded) == 0)
        {
            mask |= Bit(StateId.Collapsed);
        }

        return mask;
    }

    /// <summary>Splits a state set into the two 32 bit numbers that go on the wire.</summary>
    /// <param name="mask">The mask.</param>
    /// <returns>The low half and the high half.</returns>
    internal static uint[] Split(ulong mask) => [(uint)(mask & uint.MaxValue), (uint)(mask >> HalfBits)];

    /// <summary>Gets the mask bit of a state number.</summary>
    /// <param name="id">The state.</param>
    /// <returns>The bit.</returns>
    private static ulong Bit(StateId id) => 1UL << (int)id;

    /// <summary>The AT-SPI bits that an app state sets.</summary>
    /// <param name="Flag">The app state.</param>
    /// <param name="Bits">The AT-SPI bits.</param>
    private readonly record struct StateBits(AccessibleStates Flag, ulong Bits);
}
