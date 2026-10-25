// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Automation.Peers;
using HyperPdf.Core.Accessibility;

namespace HyperPdf.App.Accessibility;

/// <summary>Maps Avalonia's control types to the roles screen readers know, and landmark types to their names.</summary>
internal static class AvaloniaRoleMap
{
    /// <summary>The role for each control type; a text element with a heading level is handled apart.</summary>
    private static readonly Dictionary<AutomationControlType, AccessibleRole> Roles = new()
    {
        [AutomationControlType.Button] = AccessibleRole.Button,
        [AutomationControlType.SplitButton] = AccessibleRole.Button,
        [AutomationControlType.Thumb] = AccessibleRole.Panel,
        [AutomationControlType.CheckBox] = AccessibleRole.CheckBox,
        [AutomationControlType.RadioButton] = AccessibleRole.RadioButton,
        [AutomationControlType.ComboBox] = AccessibleRole.ComboBox,
        [AutomationControlType.ComboBoxItem] = AccessibleRole.ListItem,
        [AutomationControlType.ListItem] = AccessibleRole.ListItem,
        [AutomationControlType.DataItem] = AccessibleRole.ListItem,
        [AutomationControlType.Edit] = AccessibleRole.Edit,
        [AutomationControlType.Hyperlink] = AccessibleRole.Link,
        [AutomationControlType.Image] = AccessibleRole.Image,
        [AutomationControlType.List] = AccessibleRole.List,
        [AutomationControlType.Menu] = AccessibleRole.Menu,
        [AutomationControlType.MenuBar] = AccessibleRole.MenuBar,
        [AutomationControlType.MenuItem] = AccessibleRole.MenuItem,
        [AutomationControlType.ProgressBar] = AccessibleRole.ProgressBar,
        [AutomationControlType.ScrollBar] = AccessibleRole.ScrollBar,
        [AutomationControlType.Slider] = AccessibleRole.Slider,
        [AutomationControlType.Spinner] = AccessibleRole.SpinButton,
        [AutomationControlType.StatusBar] = AccessibleRole.StatusBar,
        [AutomationControlType.Tab] = AccessibleRole.TabList,
        [AutomationControlType.TabItem] = AccessibleRole.TabItem,
        [AutomationControlType.Text] = AccessibleRole.Label,
        [AutomationControlType.ToolBar] = AccessibleRole.ToolBar,
        [AutomationControlType.ToolTip] = AccessibleRole.ToolTip,
        [AutomationControlType.Tree] = AccessibleRole.Tree,
        [AutomationControlType.TreeItem] = AccessibleRole.TreeItem,
        [AutomationControlType.Group] = AccessibleRole.Group,
        [AutomationControlType.Expander] = AccessibleRole.Group,
        [AutomationControlType.DataGrid] = AccessibleRole.Table,
        [AutomationControlType.Table] = AccessibleRole.Table,
        [AutomationControlType.Document] = AccessibleRole.Document,
        [AutomationControlType.Window] = AccessibleRole.Window,
        [AutomationControlType.Header] = AccessibleRole.ColumnHeader,
        [AutomationControlType.HeaderItem] = AccessibleRole.ColumnHeader,
        [AutomationControlType.Separator] = AccessibleRole.Separator,
        [AutomationControlType.ScrollViewer] = AccessibleRole.ScrollPane,
        [AutomationControlType.Pane] = AccessibleRole.Panel,
        [AutomationControlType.Custom] = AccessibleRole.Panel,
        [AutomationControlType.Calendar] = AccessibleRole.Panel,
        [AutomationControlType.TitleBar] = AccessibleRole.Panel,
    };

    /// <summary>The name of each landmark type, as a web author would write it.</summary>
    private static readonly Dictionary<AutomationLandmarkType, string> Landmarks = new()
    {
        [AutomationLandmarkType.Banner] = "banner",
        [AutomationLandmarkType.Complementary] = "complementary",
        [AutomationLandmarkType.ContentInfo] = "contentinfo",
        [AutomationLandmarkType.Region] = "region",
        [AutomationLandmarkType.Form] = "form",
        [AutomationLandmarkType.Main] = "main",
        [AutomationLandmarkType.Navigation] = "navigation",
        [AutomationLandmarkType.Search] = "search",
    };

    /// <summary>Gets the role for a control type.</summary>
    /// <param name="type">Avalonia's control type.</param>
    /// <param name="headingLevel">The element's heading level, which makes a text element a heading.</param>
    /// <returns>The role.</returns>
    internal static AccessibleRole ToRole(AutomationControlType type, int headingLevel) =>
        type == AutomationControlType.Text && headingLevel > 0 ? AccessibleRole.Heading : Roles.GetValueOrDefault(type, AccessibleRole.Unknown);

    /// <summary>Gets the name of a landmark type as a web author would write it.</summary>
    /// <param name="landmark">The landmark type, or <see langword="null"/>.</param>
    /// <returns>The name, or an empty string when the element is not a landmark.</returns>
    internal static string ToLandmarkName(AutomationLandmarkType? landmark) =>
        landmark is { } kind && Landmarks.TryGetValue(kind, out var name) ? name : string.Empty;
}
