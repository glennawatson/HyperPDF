// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.ViewModels.Sidebar;

namespace HyperPdf.Viewer.Chrome;

/// <summary>One bookmark as it is listed in the side panel.</summary>
/// <param name="Item">The bookmark.</param>
/// <param name="Depth">How many bookmarks it is nested under.</param>
[DebuggerDisplay("SidebarRow: depth {Depth} {Item.Title}")]
internal sealed record SidebarRow(OutlineItemViewModel Item, int Depth);
