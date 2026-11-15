// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Accessibility;

namespace HyperPdf.Head.Windows.Accessibility;

/// <summary>The UI Automation property, event and control type ids the bridge uses.</summary>
internal static class UiaIds
{
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

    /// <summary>The HelpText property.</summary>
    internal const int HelpTextProperty = 30_013;

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

    /// <summary>The ComboBox control type.</summary>
    private const int ComboBoxControl = 50_003;

    /// <summary>The Edit control type.</summary>
    private const int EditControl = 50_004;

    /// <summary>The Hyperlink control type.</summary>
    private const int HyperlinkControl = 50_005;

    /// <summary>The Image control type.</summary>
    private const int ImageControl = 50_006;

    /// <summary>The ListItem control type.</summary>
    private const int ListItemControl = 50_007;

    /// <summary>The List control type.</summary>
    private const int ListControl = 50_008;

    /// <summary>The Menu control type.</summary>
    private const int MenuControl = 50_009;

    /// <summary>The MenuBar control type.</summary>
    private const int MenuBarControl = 50_010;

    /// <summary>The MenuItem control type.</summary>
    private const int MenuItemControl = 50_011;

    /// <summary>The ProgressBar control type.</summary>
    private const int ProgressBarControl = 50_012;

    /// <summary>The RadioButton control type.</summary>
    private const int RadioButtonControl = 50_013;

    /// <summary>The ScrollBar control type.</summary>
    private const int ScrollBarControl = 50_014;

    /// <summary>The Slider control type.</summary>
    private const int SliderControl = 50_015;

    /// <summary>The Spinner control type.</summary>
    private const int SpinnerControl = 50_016;

    /// <summary>The StatusBar control type.</summary>
    private const int StatusBarControl = 50_017;

    /// <summary>The Tab control type.</summary>
    private const int TabControl = 50_018;

    /// <summary>The TabItem control type.</summary>
    private const int TabItemControl = 50_019;

    /// <summary>The Text control type.</summary>
    private const int TextControl = 50_020;

    /// <summary>The ToolBar control type.</summary>
    private const int ToolBarControl = 50_021;

    /// <summary>The ToolTip control type.</summary>
    private const int ToolTipControl = 50_022;

    /// <summary>The Tree control type.</summary>
    private const int TreeControl = 50_023;

    /// <summary>The TreeItem control type.</summary>
    private const int TreeItemControl = 50_024;

    /// <summary>The Group control type.</summary>
    private const int GroupControl = 50_026;

    /// <summary>The DataItem control type, which table rows and cells are.</summary>
    private const int DataItemControl = 50_029;

    /// <summary>The Document control type.</summary>
    private const int DocumentControl = 50_030;

    /// <summary>The Window control type.</summary>
    private const int WindowControl = 50_032;

    /// <summary>The Pane control type.</summary>
    private const int PaneControl = 50_033;

    /// <summary>The HeaderItem control type.</summary>
    private const int HeaderItemControl = 50_035;

    /// <summary>The Table control type.</summary>
    private const int TableControl = 50_036;

    /// <summary>The Separator control type.</summary>
    private const int SeparatorControl = 50_038;

    /// <summary>Gets the UI Automation control type id for a role.</summary>
    /// <param name="role">The role.</param>
    /// <returns>The control type id.</returns>
    internal static int ControlType(AccessibleRole role) => role switch
    {
        AccessibleRole.Window or AccessibleRole.Dialog => WindowControl,
        AccessibleRole.Group or AccessibleRole.Landmark or AccessibleRole.Section => GroupControl,
        AccessibleRole.Button or AccessibleRole.ToggleButton => ButtonControl,
        AccessibleRole.CheckBox => CheckBoxControl,
        AccessibleRole.RadioButton => RadioButtonControl,
        AccessibleRole.Edit => EditControl,
        AccessibleRole.Label or AccessibleRole.Heading or AccessibleRole.Paragraph => TextControl,
        AccessibleRole.List => ListControl,
        AccessibleRole.ListItem => ListItemControl,
        AccessibleRole.Menu => MenuControl,
        AccessibleRole.MenuBar => MenuBarControl,
        AccessibleRole.MenuItem => MenuItemControl,
        _ => ControlTypeBeyondMenus(role),
    };

    /// <summary>Gets the control type for the roles that are not windows, text, lists, menus or buttons.</summary>
    /// <param name="role">The role.</param>
    /// <returns>The control type id.</returns>
    private static int ControlTypeBeyondMenus(AccessibleRole role) => role switch
    {
        AccessibleRole.ScrollBar => ScrollBarControl,
        AccessibleRole.Slider => SliderControl,
        AccessibleRole.SpinButton => SpinnerControl,
        AccessibleRole.TabList => TabControl,
        AccessibleRole.TabItem => TabItemControl,
        AccessibleRole.Tree => TreeControl,
        AccessibleRole.TreeItem => TreeItemControl,
        AccessibleRole.Table => TableControl,
        AccessibleRole.TableRow or AccessibleRole.TableCell => DataItemControl,
        AccessibleRole.ColumnHeader => HeaderItemControl,
        AccessibleRole.Document => DocumentControl,
        AccessibleRole.Image => ImageControl,
        AccessibleRole.Link => HyperlinkControl,
        AccessibleRole.Separator => SeparatorControl,
        AccessibleRole.ToolBar => ToolBarControl,
        AccessibleRole.StatusBar => StatusBarControl,
        AccessibleRole.ProgressBar => ProgressBarControl,
        AccessibleRole.ComboBox => ComboBoxControl,
        AccessibleRole.ToolTip => ToolTipControl,
        _ => PaneControl,
    };
}
