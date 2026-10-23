// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Platform.Windows.Accessibility.Uia;

/// <summary>The UI Automation events the bridge raises.</summary>
internal enum UiaEventId
{
    /// <summary>The tree of elements changed.</summary>
    StructureChanged = 20_002,

    /// <summary>A property of an element changed.</summary>
    AutomationPropertyChanged = 20_004,

    /// <summary>The keyboard focus moved.</summary>
    AutomationFocusChanged = 20_005,

    /// <summary>An element was clicked.</summary>
    InvokeInvoked = 20_009,

    /// <summary>An item became the selected one.</summary>
    SelectionItemElementSelected = 20_012,

    /// <summary>The selection or the caret in some text moved.</summary>
    TextTextSelectionChanged = 20_014,

    /// <summary>Some text changed.</summary>
    TextTextChanged = 20_015,

    /// <summary>A window opened.</summary>
    WindowWindowOpened = 20_016,

    /// <summary>A window closed.</summary>
    WindowWindowClosed = 20_017,
}
