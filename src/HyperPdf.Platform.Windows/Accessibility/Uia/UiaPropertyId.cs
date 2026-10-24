// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Platform.Windows.Accessibility.Uia;

/// <summary>The UI Automation properties the bridge answers or reports a change of, with the numbers from <c>UIAutomationClient.h</c>.</summary>
internal enum UiaPropertyId
{
    /// <summary>The runtime identifier.</summary>
    RuntimeId = 30_000,

    /// <summary>The bounding rectangle on the screen.</summary>
    BoundingRectangle = 30_001,

    /// <summary>The identifier of the process that owns the element.</summary>
    ProcessId = 30_002,

    /// <summary>The control type.</summary>
    ControlType = 30_003,

    /// <summary>The name of the control type in the user's language.</summary>
    LocalizedControlType = 30_004,

    /// <summary>The element's name.</summary>
    Name = 30_005,

    /// <summary>Whether the element has the keyboard focus.</summary>
    HasKeyboardFocus = 30_008,

    /// <summary>Whether the element can take the keyboard focus.</summary>
    IsKeyboardFocusable = 30_009,

    /// <summary>Whether the element is enabled.</summary>
    IsEnabled = 30_010,

    /// <summary>The element's automation identifier.</summary>
    AutomationId = 30_011,

    /// <summary>The element's class name.</summary>
    ClassName = 30_012,

    /// <summary>The element's help text.</summary>
    HelpText = 30_013,

    /// <summary>Whether the element is in the control view.</summary>
    IsControlElement = 30_016,

    /// <summary>Whether the element is in the content view.</summary>
    IsContentElement = 30_017,

    /// <summary>Whether the element is a password box.</summary>
    IsPassword = 30_019,

    /// <summary>The handle of the window that holds the element.</summary>
    NativeWindowHandle = 30_020,

    /// <summary>Whether the element is off screen.</summary>
    IsOffscreen = 30_022,

    /// <summary>The way the element lies.</summary>
    Orientation = 30_023,

    /// <summary>The name of the UI framework.</summary>
    FrameworkId = 30_024,

    /// <summary>Whether the element must be filled in.</summary>
    IsRequiredForForm = 30_025,

    /// <summary>The text of a value.</summary>
    ValueValue = 30_045,

    /// <summary>Whether the value cannot be changed.</summary>
    ValueIsReadOnly = 30_046,

    /// <summary>The state of an expandable element.</summary>
    ExpandCollapseState = 30_070,

    /// <summary>Whether a window blocks the rest of the application.</summary>
    WindowIsModal = 30_077,

    /// <summary>Whether an item is selected.</summary>
    SelectionItemIsSelected = 30_079,

    /// <summary>The container of a selectable item.</summary>
    SelectionItemSelectionContainer = 30_080,

    /// <summary>The state of a toggle.</summary>
    ToggleState = 30_086,

    /// <summary>The place of an item among its siblings, from 1.</summary>
    PositionInSet = 30_152,

    /// <summary>The number of siblings in the set.</summary>
    SizeOfSet = 30_153,

    /// <summary>The level of an item in a hierarchy.</summary>
    Level = 30_154,

    /// <summary>The kind of landmark.</summary>
    LandmarkType = 30_157,

    /// <summary>The name of the landmark kind in the user's language.</summary>
    LocalizedLandmarkType = 30_158,

    /// <summary>The longer explanation of the element.</summary>
    FullDescription = 30_159,

    /// <summary>The level of a heading.</summary>
    HeadingLevel = 30_173,

    /// <summary>Whether the element is a dialog.</summary>
    IsDialog = 30_174,
}
