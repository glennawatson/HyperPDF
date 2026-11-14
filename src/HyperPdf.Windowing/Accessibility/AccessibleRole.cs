// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Windowing.Accessibility;

/// <summary>The roles the viewer exposes.</summary>
public enum AccessibleRole
{
    /// <summary>The window.</summary>
    Window = 0,

    /// <summary>A grouping of other nodes.</summary>
    Group = 1,

    /// <summary>A button.</summary>
    Button = 2,

    /// <summary>A text box.</summary>
    TextBox = 3,

    /// <summary>A page of the document.</summary>
    Page = 4,

    /// <summary>A run of document text.</summary>
    Text = 5,

    /// <summary>A tab.</summary>
    Tab = 6,

    /// <summary>A list or tree item.</summary>
    ListItem = 7,

    /// <summary>A menu item.</summary>
    MenuItem = 8,

    /// <summary>A check box.</summary>
    CheckBox = 9,

    /// <summary>A link.</summary>
    Link = 10,
}
