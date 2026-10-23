// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Accessibility;

namespace HyperPdf.Platform.Linux.Accessibility.AtSpi;

/// <summary>Turns the app's roles into AT-SPI role numbers and the role names screen readers speak.</summary>
internal static class AtSpiRoles
{
    /// <summary>The AT-SPI role number of an unknown element.</summary>
    internal const uint Unknown = 67;

    /// <summary>The AT-SPI role number of a landmark.</summary>
    internal const uint Landmark = 110;

    /// <summary>The AT-SPI role numbers, indexed by the app's role value.</summary>
    private static readonly uint[] Numbers =
    [
        Unknown,
        75, // Application
        23, // Window (a frame)
        16, // Dialog
        39, // Panel
        99, // Group (grouping)
        43, // Button (push button)
        7, // CheckBox
        44, // RadioButton
        62, // ToggleButton
        79, // Edit (entry)
        29, // Label
        31, // List
        32, // ListItem
        33, // Menu
        34, // MenuBar
        35, // MenuItem
        48, // ScrollBar
        51, // Slider
        52, // SpinButton
        38, // TabList (page tab list)
        37, // TabItem (page tab)
        65, // Tree
        91, // TreeItem
        55, // Table
        90, // TableRow
        56, // TableCell
        57, // ColumnHeader (table column header)
        83, // Heading
        73, // Paragraph
        82, // Document (document frame)
        27, // Image
        88, // Link
        50, // Separator
        63, // ToolBar
        54, // StatusBar
        42, // ProgressBar
        11, // ComboBox
        Landmark,
        64, // ToolTip
        49, // ScrollPane
        85, // Section
    ];

    /// <summary>The names screen readers speak for each role, indexed by the app's role value.</summary>
    private static readonly string[] Names =
    [
        "unknown",
        "application",
        "frame",
        "dialog",
        "panel",
        "grouping",
        "push button",
        "check box",
        "radio button",
        "toggle button",
        "entry",
        "label",
        "list",
        "list item",
        "menu",
        "menu bar",
        "menu item",
        "scroll bar",
        "slider",
        "spin button",
        "page tab list",
        "page tab",
        "tree",
        "tree item",
        "table",
        "table row",
        "table cell",
        "column header",
        "heading",
        "paragraph",
        "document frame",
        "image",
        "link",
        "separator",
        "tool bar",
        "status bar",
        "progress bar",
        "combo box",
        "landmark",
        "tool tip",
        "scroll pane",
        "section",
    ];

    /// <summary>Gets the AT-SPI role number of a role.</summary>
    /// <param name="role">The role.</param>
    /// <returns>The number.</returns>
    internal static uint GetNumber(AccessibleRole role) => (uint)role < Numbers.Length ? Numbers[(int)role] : Unknown;

    /// <summary>Gets the name screen readers speak for a role.</summary>
    /// <param name="role">The role.</param>
    /// <returns>The name.</returns>
    internal static string GetName(AccessibleRole role) => (uint)role < Names.Length ? Names[(int)role] : Names[0];
}
