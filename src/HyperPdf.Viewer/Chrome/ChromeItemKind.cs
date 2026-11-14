// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Viewer.Chrome;

/// <summary>How a control of the window is drawn and read out.</summary>
public enum ChromeItemKind
{
    /// <summary>Plain text.</summary>
    Label = 0,

    /// <summary>A button with its text in the middle.</summary>
    Button = 1,

    /// <summary>A document tab.</summary>
    Tab = 2,

    /// <summary>A row of a menu or a list, with its text on the left.</summary>
    Row = 3,

    /// <summary>A line between groups of rows.</summary>
    Separator = 4,

    /// <summary>A box the user types into.</summary>
    Field = 5,

    /// <summary>A filled background behind other controls.</summary>
    Panel = 6,

    /// <summary>A raised panel with a border: a menu or a dialog.</summary>
    Popup = 7,

    /// <summary>Bold text at the top of a dialog.</summary>
    Heading = 8,
}
