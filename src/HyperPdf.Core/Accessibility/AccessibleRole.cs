// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Accessibility;

/// <summary>What an element is, in the words every desktop screen reader has: a button, a heading, a list item and so on.</summary>
public enum AccessibleRole
{
    /// <summary>The kind is not known.</summary>
    Unknown = 0,

    /// <summary>The whole application, the root of every tree.</summary>
    Application = 1,

    /// <summary>A top level window.</summary>
    Window = 2,

    /// <summary>A dialog window.</summary>
    Dialog = 3,

    /// <summary>A plain container with no meaning of its own.</summary>
    Panel = 4,

    /// <summary>A container that groups related controls and has a name.</summary>
    Group = 5,

    /// <summary>A push button.</summary>
    Button = 6,

    /// <summary>A check box.</summary>
    CheckBox = 7,

    /// <summary>A radio button.</summary>
    RadioButton = 8,

    /// <summary>A control that can be on, off or in between and shows it as a toggle.</summary>
    ToggleButton = 9,

    /// <summary>A single or multi line text entry.</summary>
    Edit = 10,

    /// <summary>Text that is only shown.</summary>
    Label = 11,

    /// <summary>A list of items.</summary>
    List = 12,

    /// <summary>One item of a list.</summary>
    ListItem = 13,

    /// <summary>A pop-up or drop-down menu.</summary>
    Menu = 14,

    /// <summary>A bar of menus.</summary>
    MenuBar = 15,

    /// <summary>One entry of a menu.</summary>
    MenuItem = 16,

    /// <summary>A scroll bar.</summary>
    ScrollBar = 17,

    /// <summary>A slider.</summary>
    Slider = 18,

    /// <summary>A number box with up and down buttons.</summary>
    SpinButton = 19,

    /// <summary>A set of tab pages.</summary>
    TabList = 20,

    /// <summary>One tab of a tab set.</summary>
    TabItem = 21,

    /// <summary>A tree.</summary>
    Tree = 22,

    /// <summary>One node of a tree.</summary>
    TreeItem = 23,

    /// <summary>A table.</summary>
    Table = 24,

    /// <summary>A row of a table.</summary>
    TableRow = 25,

    /// <summary>A cell of a table.</summary>
    TableCell = 26,

    /// <summary>A column header of a table.</summary>
    ColumnHeader = 27,

    /// <summary>A heading; its level is on the node.</summary>
    Heading = 28,

    /// <summary>A paragraph of text.</summary>
    Paragraph = 29,

    /// <summary>A document, such as the pages.</summary>
    Document = 30,

    /// <summary>A picture.</summary>
    Image = 31,

    /// <summary>A link.</summary>
    Link = 32,

    /// <summary>A separator line.</summary>
    Separator = 33,

    /// <summary>A tool bar.</summary>
    ToolBar = 34,

    /// <summary>A status bar.</summary>
    StatusBar = 35,

    /// <summary>A progress bar.</summary>
    ProgressBar = 36,

    /// <summary>A combination of a text entry and a list.</summary>
    ComboBox = 37,

    /// <summary>A landmark region such as the main area, navigation or search; its kind is on the node.</summary>
    Landmark = 38,

    /// <summary>A pop-up that explains a control.</summary>
    ToolTip = 39,

    /// <summary>A scrolling area.</summary>
    ScrollPane = 40,

    /// <summary>A section of a document.</summary>
    Section = 41,
}
