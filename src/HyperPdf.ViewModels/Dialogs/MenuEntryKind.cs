// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Dialogs;

/// <summary>The kinds of entry a context menu holds.</summary>
public enum MenuEntryKind
{
    /// <summary>An item that runs a command.</summary>
    Item = 0,

    /// <summary>A line between groups of items.</summary>
    Separator = 1,

    /// <summary>An item that opens a menu of its own.</summary>
    Submenu = 2,

    /// <summary>An item that is one of a group of which one is ticked.</summary>
    RadioItem = 3,
}
