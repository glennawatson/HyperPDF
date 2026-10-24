// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Accessibility;

/// <summary>What is true of an element right now.</summary>
[Flags]
public enum AccessibleStates
{
    /// <summary>Nothing is set.</summary>
    None = 0,

    /// <summary>The element can be used.</summary>
    Enabled = 1 << 0,

    /// <summary>The element is on screen.</summary>
    Showing = 1 << 1,

    /// <summary>The element can take the keyboard focus.</summary>
    Focusable = 1 << 2,

    /// <summary>The element has the keyboard focus.</summary>
    Focused = 1 << 3,

    /// <summary>The element can be selected.</summary>
    Selectable = 1 << 4,

    /// <summary>The element is selected.</summary>
    Selected = 1 << 5,

    /// <summary>The element is checked or switched on.</summary>
    Checked = 1 << 6,

    /// <summary>The element is neither checked nor unchecked.</summary>
    Indeterminate = 1 << 7,

    /// <summary>The element can open and close.</summary>
    Expandable = 1 << 8,

    /// <summary>The element is open.</summary>
    Expanded = 1 << 9,

    /// <summary>The element's text can be changed.</summary>
    Editable = 1 << 10,

    /// <summary>The element holds more than one line.</summary>
    MultiLine = 1 << 11,

    /// <summary>The element is the active window.</summary>
    Active = 1 << 12,

    /// <summary>The element blocks the rest of the application.</summary>
    Modal = 1 << 13,

    /// <summary>The element is working and its content is not ready.</summary>
    Busy = 1 << 14,

    /// <summary>The element must be filled in.</summary>
    Required = 1 << 15,

    /// <summary>The element holds a value that is not accepted.</summary>
    Invalid = 1 << 16,

    /// <summary>The element is a button held down.</summary>
    Pressed = 1 << 17,

    /// <summary>The element lies horizontally.</summary>
    Horizontal = 1 << 18,

    /// <summary>The element lies vertically.</summary>
    Vertical = 1 << 19,

    /// <summary>The element cannot be changed but can be read and copied.</summary>
    ReadOnly = 1 << 20,
}
