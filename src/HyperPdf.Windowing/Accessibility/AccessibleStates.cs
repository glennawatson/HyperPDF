// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Windowing.Accessibility;

/// <summary>State flags of a node.</summary>
[Flags]
public enum AccessibleStates
{
    /// <summary>No state.</summary>
    None = 0,

    /// <summary>The node has keyboard focus.</summary>
    Focused = 1 << 0,

    /// <summary>The node is selected.</summary>
    Selected = 1 << 1,

    /// <summary>The node is checked.</summary>
    Checked = 1 << 2,

    /// <summary>The node cannot be used now.</summary>
    Disabled = 1 << 3,

    /// <summary>The node can be expanded or is expanded.</summary>
    Expanded = 1 << 4,
}
