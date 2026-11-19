// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Layout;
using HyperPdf.ViewModels.Sidebar;

namespace HyperPdf.Viewer.Chrome;

/// <summary>What the side panel lists and how far down the list it is.</summary>
/// <param name="Rows">The rows of the list the panel shows now.</param>
/// <param name="First">The first row shown.</param>
/// <param name="Bounds">Where the panel is, or the default when it is not shown.</param>
/// <param name="VisibleRows">The rows that fit the panel as it was last laid out.</param>
/// <param name="FollowedTab">The tab whose page the list last followed, or <see langword="null"/> when it has followed none.</param>
/// <param name="FollowedPage">The page the list last followed, so a page the reader scrolled away from by hand is not pulled back.</param>
/// <param name="ListedPages">The list of pages the rows were made from, or <see langword="null"/> when they list something else, so the same pages are not listed again.</param>
[DebuggerDisplay("SidebarView: {Rows.Length} rows from {First}")]
internal sealed record SidebarView(SidebarRow[] Rows, int First, LayoutRect Bounds, int VisibleRows, object? FollowedTab, int FollowedPage, object? ListedPages)
{
    /// <summary>Gets the panel before it was laid out.</summary>
    internal static SidebarView Empty { get; } = new([], 0, default, 0, null, -1, null);

    /// <summary>
    /// Gets the rows made for each list of pages the panel has shown, so coming back to a tab does not make a row for every page
    /// again; <see langword="null"/> when the rows are not kept. Rows are held only while the tab holds its list of pages.
    /// </summary>
    internal ConditionalWeakTable<ThumbnailItemViewModel[], SidebarRow[]>? PageRows { get; init; }
}
