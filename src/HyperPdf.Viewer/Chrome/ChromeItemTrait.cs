// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Viewer.Chrome;

/// <summary>What a control is beyond how it is drawn, which a screen reader says along with its name: a switch, a list that opens, a ticked entry.</summary>
public enum ChromeItemTrait
{
    /// <summary>Nothing more than its kind says.</summary>
    None = 0,

    /// <summary>A setting that is on or off. It is on while the control is selected.</summary>
    Switch = 1,

    /// <summary>A list of values that is closed.</summary>
    Collapsed = 2,

    /// <summary>A list of values that is open.</summary>
    Expanded = 3,

    /// <summary>A menu entry that is ticked.</summary>
    Ticked = 4,

    /// <summary>A menu entry that opens another menu.</summary>
    OpensMore = 5,
}
