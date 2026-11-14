// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Layout;

namespace HyperPdf.Viewer.Chrome;

/// <summary>What the side panel shows between layouts: the bookmarks listed, how far the list is scrolled and where the panel is.</summary>
[DebuggerDisplay("SidebarState: {Rows.Count} rows from {First}")]
internal sealed class SidebarState
{
    /// <summary>Gets the bookmarks listed, with the open ones followed by their children.</summary>
    internal List<SidebarRow> Rows { get; } = [];

    /// <summary>Gets or sets the first row shown.</summary>
    internal int First { get; set; }

    /// <summary>Gets or sets a number that changes when the panel was scrolled or a bookmark was opened or closed.</summary>
    internal int Version { get; set; }

    /// <summary>Gets or sets where the panel is in the window, in device pixels. Empty while the panel is hidden.</summary>
    internal LayoutRect Bounds { get; set; }

    /// <summary>Gets or sets the rows that fit the panel.</summary>
    internal int VisibleRows { get; set; }
}
