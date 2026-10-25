// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Platform.Windows.Accessibility.Uia;

/// <summary>The UI Automation control types the bridge gives elements.</summary>
internal enum UiaControlType
{
    /// <summary>A push button.</summary>
    Button = 50_000,

    /// <summary>A check box.</summary>
    CheckBox = 50_002,

    /// <summary>A combination of a text entry and a list.</summary>
    ComboBox = 50_003,

    /// <summary>A text entry.</summary>
    Edit = 50_004,

    /// <summary>A link.</summary>
    Hyperlink = 50_005,

    /// <summary>A picture.</summary>
    Image = 50_006,

    /// <summary>One item of a list.</summary>
    ListItem = 50_007,

    /// <summary>A list.</summary>
    List = 50_008,

    /// <summary>A menu.</summary>
    Menu = 50_009,

    /// <summary>A bar of menus.</summary>
    MenuBar = 50_010,

    /// <summary>One entry of a menu.</summary>
    MenuItem = 50_011,

    /// <summary>A progress bar.</summary>
    ProgressBar = 50_012,

    /// <summary>A radio button.</summary>
    RadioButton = 50_013,

    /// <summary>A scroll bar.</summary>
    ScrollBar = 50_014,

    /// <summary>A slider.</summary>
    Slider = 50_015,

    /// <summary>A number box with up and down buttons.</summary>
    Spinner = 50_016,

    /// <summary>A status bar.</summary>
    StatusBar = 50_017,

    /// <summary>A set of tab pages.</summary>
    Tab = 50_018,

    /// <summary>One tab.</summary>
    TabItem = 50_019,

    /// <summary>Text that is only shown.</summary>
    Text = 50_020,

    /// <summary>A tool bar.</summary>
    ToolBar = 50_021,

    /// <summary>A pop-up that explains a control.</summary>
    ToolTip = 50_022,

    /// <summary>A tree.</summary>
    Tree = 50_023,

    /// <summary>One node of a tree.</summary>
    TreeItem = 50_024,

    /// <summary>A control with no standard type.</summary>
    Custom = 50_025,

    /// <summary>A container that groups related controls.</summary>
    Group = 50_026,

    /// <summary>One item of data, such as a table cell.</summary>
    DataItem = 50_029,

    /// <summary>A document.</summary>
    Document = 50_030,

    /// <summary>A top level window.</summary>
    Window = 50_032,

    /// <summary>A container with no meaning of its own.</summary>
    Pane = 50_033,

    /// <summary>The header of a column.</summary>
    HeaderItem = 50_035,

    /// <summary>A table.</summary>
    Table = 50_036,

    /// <summary>A separator line.</summary>
    Separator = 50_038,
}
