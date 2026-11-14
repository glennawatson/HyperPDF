// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Windowing.Accessibility;

namespace HyperPdf.Head.Windows.Accessibility;

/// <summary>The UI Automation property, event and control type ids the bridge uses.</summary>
internal static class UiaIds
{
    /// <summary>The BoundingRectangle property.</summary>
    internal const int BoundingRectangleProperty = 30_001;

    /// <summary>The ControlType property.</summary>
    internal const int ControlTypeProperty = 30_003;

    /// <summary>The Name property.</summary>
    internal const int NameProperty = 30_005;

    /// <summary>The HasKeyboardFocus property.</summary>
    internal const int HasKeyboardFocusProperty = 30_008;

    /// <summary>The IsKeyboardFocusable property.</summary>
    internal const int IsKeyboardFocusableProperty = 30_009;

    /// <summary>The IsEnabled property.</summary>
    internal const int IsEnabledProperty = 30_010;

    /// <summary>The AutomationId property.</summary>
    internal const int AutomationIdProperty = 30_011;

    /// <summary>The ClassName property.</summary>
    internal const int ClassNameProperty = 30_012;

    /// <summary>The IsControlElement property.</summary>
    internal const int IsControlElementProperty = 30_016;

    /// <summary>The IsContentElement property.</summary>
    internal const int IsContentElementProperty = 30_017;

    /// <summary>The FrameworkId property.</summary>
    internal const int FrameworkIdProperty = 30_024;

    /// <summary>The AutomationFocusChanged event.</summary>
    internal const int FocusChangedEvent = 20_005;

    /// <summary>The lParam of WM_GETOBJECT that asks for the UI Automation root, UiaRootObjectId.</summary>
    internal const int RootObjectId = -25;

    /// <summary>The structure change type that says the children were replaced.</summary>
    internal const int ChildrenInvalidated = 2;

    /// <summary>The runtime id prefix that tells UI Automation to add the window's own id.</summary>
    internal const int AppendRuntimeId = 3;

    /// <summary>The Button control type.</summary>
    private const int ButtonControl = 50_000;

    /// <summary>The CheckBox control type.</summary>
    private const int CheckBoxControl = 50_002;

    /// <summary>The Edit control type.</summary>
    private const int EditControl = 50_004;

    /// <summary>The Hyperlink control type.</summary>
    private const int HyperlinkControl = 50_005;

    /// <summary>The ListItem control type.</summary>
    private const int ListItemControl = 50_007;

    /// <summary>The MenuItem control type.</summary>
    private const int MenuItemControl = 50_011;

    /// <summary>The TabItem control type.</summary>
    private const int TabItemControl = 50_019;

    /// <summary>The Text control type.</summary>
    private const int TextControl = 50_020;

    /// <summary>The Group control type.</summary>
    private const int GroupControl = 50_026;

    /// <summary>The Document control type, which a page is.</summary>
    private const int DocumentControl = 50_030;

    /// <summary>The Window control type.</summary>
    private const int WindowControl = 50_032;

    /// <summary>Gets the UI Automation control type id for a role.</summary>
    /// <param name="role">The role.</param>
    /// <returns>The control type id.</returns>
    internal static int ControlType(AccessibleRole role) => role switch
    {
        AccessibleRole.Button => ButtonControl,
        AccessibleRole.CheckBox => CheckBoxControl,
        AccessibleRole.TextBox => EditControl,
        AccessibleRole.Link => HyperlinkControl,
        AccessibleRole.ListItem => ListItemControl,
        AccessibleRole.MenuItem => MenuItemControl,
        AccessibleRole.Tab => TabItemControl,
        AccessibleRole.Text => TextControl,
        AccessibleRole.Group => GroupControl,
        AccessibleRole.Page => DocumentControl,
        _ => WindowControl,
    };
}
