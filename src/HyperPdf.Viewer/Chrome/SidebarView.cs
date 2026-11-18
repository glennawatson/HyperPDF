// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Layout;

namespace HyperPdf.Viewer.Chrome;

/// <summary>What the side panel lists and how far down the list it is.</summary>
/// <param name="Rows">The rows of the list the panel shows now.</param>
/// <param name="First">The first row shown.</param>
/// <param name="Bounds">Where the panel is, or the default when it is not shown.</param>
/// <param name="VisibleRows">The rows that fit the panel as it was last laid out.</param>
/// <param name="FollowedTab">The tab whose page the list last followed, or <see langword="null"/> when it has followed none.</param>
/// <param name="FollowedPage">The page the list last followed, so a page the reader scrolled away from by hand is not pulled back.</param>
[DebuggerDisplay("SidebarView: {Rows.Length} rows from {First}")]
internal sealed record SidebarView(SidebarRow[] Rows, int First, LayoutRect Bounds, int VisibleRows, object? FollowedTab, int FollowedPage)
{
    /// <summary>Gets the panel before it was laid out.</summary>
    internal static SidebarView Empty { get; } = new([], 0, default, 0, null, -1);
}
