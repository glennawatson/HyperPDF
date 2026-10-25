// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Accessibility;

namespace HyperPdf.Platform.Windows.Accessibility.Uia;

/// <summary>Maps the app's roles and landmark kinds to what UI Automation calls them.</summary>
internal static class UiaRoles
{
    /// <summary>The landmark number of a landmark with no standard kind.</summary>
    private const int CustomLandmark = 80_000;

    /// <summary>The landmark number of a form.</summary>
    private const int FormLandmark = 80_001;

    /// <summary>The landmark number of the main area.</summary>
    private const int MainLandmark = 80_002;

    /// <summary>The landmark number of a navigation area.</summary>
    private const int NavigationLandmark = 80_003;

    /// <summary>The landmark number of a search area.</summary>
    private const int SearchLandmark = 80_004;

    /// <summary>The heading level number of level 1; the others follow it.</summary>
    private const int FirstHeadingLevel = 80_051;

    /// <summary>The deepest heading level UI Automation has a number for.</summary>
    private const int MaxHeadingLevel = 9;

    /// <summary>The control types, indexed by the app's role value.</summary>
    private static readonly UiaControlType[] ControlTypes =
    [
        UiaControlType.Custom, // Unknown
        UiaControlType.Pane, // Application
        UiaControlType.Window,
        UiaControlType.Window, // Dialog
        UiaControlType.Pane, // Panel
        UiaControlType.Group,
        UiaControlType.Button,
        UiaControlType.CheckBox,
        UiaControlType.RadioButton,
        UiaControlType.Button, // ToggleButton
        UiaControlType.Edit,
        UiaControlType.Text, // Label
        UiaControlType.List,
        UiaControlType.ListItem,
        UiaControlType.Menu,
        UiaControlType.MenuBar,
        UiaControlType.MenuItem,
        UiaControlType.ScrollBar,
        UiaControlType.Slider,
        UiaControlType.Spinner,
        UiaControlType.Tab, // TabList
        UiaControlType.TabItem,
        UiaControlType.Tree,
        UiaControlType.TreeItem,
        UiaControlType.Table,
        UiaControlType.DataItem, // TableRow
        UiaControlType.DataItem, // TableCell
        UiaControlType.HeaderItem, // ColumnHeader
        UiaControlType.Text, // Heading; its level is a property
        UiaControlType.Text, // Paragraph
        UiaControlType.Document,
        UiaControlType.Image,
        UiaControlType.Hyperlink, // Link
        UiaControlType.Separator,
        UiaControlType.ToolBar,
        UiaControlType.StatusBar,
        UiaControlType.ProgressBar,
        UiaControlType.ComboBox,
        UiaControlType.Group, // Landmark; its kind is a property
        UiaControlType.ToolTip,
        UiaControlType.Pane, // ScrollPane
        UiaControlType.Group, // Section
    ];

    /// <summary>Gets the control type of a role.</summary>
    /// <param name="role">The role.</param>
    /// <returns>The control type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static UiaControlType ControlTypeOf(AccessibleRole role) =>
        (uint)role < (uint)ControlTypes.Length ? ControlTypes[(int)role] : UiaControlType.Custom;

    /// <summary>Gets the UI Automation number of a landmark kind.</summary>
    /// <param name="landmark">The kind, such as "main".</param>
    /// <returns>The number.</returns>
    internal static int LandmarkNumber(string landmark) =>
        landmark switch
        {
            "main" => MainLandmark,
            "navigation" => NavigationLandmark,
            "search" => SearchLandmark,
            "form" => FormLandmark,
            _ => CustomLandmark,
        };

    /// <summary>Gets the UI Automation number of a heading level.</summary>
    /// <param name="level">The level from 1.</param>
    /// <returns>The number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int HeadingLevelNumber(int level) => FirstHeadingLevel + Math.Clamp(level, 1, MaxHeadingLevel) - 1;
}
